"""Small procedural pixel-art renderer: shaded primitives, palette ramps, sel-out and outline passes.

World coordinates are in pixels, y up, origin at the character pivot (feet centre).
"""
import math

import numpy as np
from PIL import Image

LIGHT = np.array([-0.55, 0.62, 0.56])
LIGHT = LIGHT / np.linalg.norm(LIGHT)


def hex_rgb(value):
    value = value.lstrip("#")
    return tuple(int(value[i:i + 2], 16) for i in (0, 2, 4))


class Ramp:
    """Dark-to-light colour ramp; shade values in [-1, 1] map onto it."""

    def __init__(self, colors, thresholds=None):
        self.colors = [hex_rgb(c) for c in colors]
        count = len(self.colors)
        if thresholds is None:
            thresholds = list(np.linspace(-0.35, 0.75, count - 1))
        self.thresholds = thresholds

    def index(self, shade):
        idx = 0
        for limit in self.thresholds:
            if shade >= limit:
                idx += 1
        return min(idx, len(self.colors) - 1)


BAYER4 = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) / 16.0 - 0.5


class Canvas:
    def __init__(self, width, height, pivot_x, pivot_y):
        self.w, self.h = width, height
        self.px, self.py = pivot_x, pivot_y
        self.part = np.full((height, width), -1, dtype=np.int32)
        self.shade = np.zeros((height, width), dtype=np.float32)
        self.ramp_id = np.full((height, width), -1, dtype=np.int32)
        self.fixed = np.zeros((height, width, 4), dtype=np.uint8)
        self.ramps = []
        self.ramp_lookup = {}
        self.part_count = 0
        ys, xs = np.mgrid[0:height, 0:width]
        # Pixel centres in world space.
        self.wx = xs + 0.5 - pivot_x
        self.wy = pivot_y - (ys + 0.5)

    def new_part(self):
        self.part_count += 1
        return self.part_count

    def _ramp_index(self, ramp):
        key = id(ramp)
        if key not in self.ramp_lookup:
            self.ramp_lookup[key] = len(self.ramps)
            self.ramps.append(ramp)
        return self.ramp_lookup[key]

    def paint(self, mask, shade, ramp, part=None, dither=0.0):
        if part is None:
            part = self.new_part()
        if dither:
            ys, xs = np.mgrid[0:self.h, 0:self.w]
            shade = shade + BAYER4[ys % 4, xs % 4] * dither
        self.part[mask] = part
        self.shade[mask] = shade[mask] if np.ndim(shade) else shade
        self.ramp_id[mask] = self._ramp_index(ramp)
        self.fixed[mask] = 0
        return part

    def paint_flat(self, mask, color, part=None):
        """Paint an exact colour that ignores shading (eyes, blood highlights)."""
        if part is None:
            part = self.new_part()
        rgb = hex_rgb(color) if isinstance(color, str) else color
        self.part[mask] = part
        self.ramp_id[mask] = -2
        self.fixed[mask] = (*rgb, 255)
        return part

    # ---------- shapes ----------
    def capsule(self, a, b, r0, r1, ramp, part=None, shade_bias=0.0, dither=0.0, flat_end=False):
        ax, ay = a
        bx, by = b
        dx, dy = bx - ax, by - ay
        length_sq = max(dx * dx + dy * dy, 1e-6)
        t = ((self.wx - ax) * dx + (self.wy - ay) * dy) / length_sq
        if flat_end:
            inside_t = (t >= 0) & (t <= 1)
        t_clamped = np.clip(t, 0.0, 1.0)
        cx = ax + dx * t_clamped
        cy = ay + dy * t_clamped
        ox, oy = self.wx - cx, self.wy - cy
        dist = np.sqrt(ox * ox + oy * oy)
        radius = r0 + (r1 - r0) * t_clamped
        mask = dist <= radius
        if flat_end:
            mask &= inside_t | (t < 0)
        nx = ox / np.maximum(radius, 1e-3)
        ny = oy / np.maximum(radius, 1e-3)
        nz = np.sqrt(np.clip(1 - nx * nx - ny * ny, 0, 1))
        shade = nx * LIGHT[0] + ny * LIGHT[1] + nz * LIGHT[2] + shade_bias
        return self.paint(mask, shade, ramp, part, dither)

    def ellipse(self, centre, rx, ry, angle_deg, ramp, part=None, shade_bias=0.0, dither=0.0):
        cx, cy = centre
        ang = math.radians(angle_deg)
        ca, sa = math.cos(ang), math.sin(ang)
        lx = (self.wx - cx) * ca + (self.wy - cy) * sa
        ly = -(self.wx - cx) * sa + (self.wy - cy) * ca
        ex, ey = lx / rx, ly / ry
        mask = ex * ex + ey * ey <= 1.0
        nz = np.sqrt(np.clip(1 - ex * ex - ey * ey, 0, 1))
        # Rotate the local normal back to world space.
        nx = ex * ca - ey * sa
        ny = ex * sa + ey * ca
        shade = nx * LIGHT[0] + ny * LIGHT[1] + nz * LIGHT[2] + shade_bias
        return self.paint(mask, shade, ramp, part, dither)

    def ellipse_mask(self, centre, rx, ry, angle_deg):
        cx, cy = centre
        ang = math.radians(angle_deg)
        ca, sa = math.cos(ang), math.sin(ang)
        lx = (self.wx - cx) * ca + (self.wy - cy) * sa
        ly = -(self.wx - cx) * sa + (self.wy - cy) * ca
        return (lx / rx) ** 2 + (ly / ry) ** 2 <= 1.0

    def polygon_mask(self, points):
        pts = np.asarray(points, dtype=np.float64)
        inside = np.zeros(self.wx.shape, dtype=bool)
        n = len(pts)
        for i in range(n):
            x0, y0 = pts[i]
            x1, y1 = pts[(i + 1) % n]
            cond = (y0 > self.wy) != (y1 > self.wy)
            with np.errstate(divide="ignore", invalid="ignore"):
                xint = (x1 - x0) * (self.wy - y0) / (y1 - y0) + x0
            inside ^= cond & (self.wx < xint)
        return inside

    def polygon(self, points, ramp, shade_fn=None, part=None, dither=0.0):
        mask = self.polygon_mask(points)
        shade = shade_fn(self.wx, self.wy) if shade_fn else np.zeros_like(self.wx)
        return self.paint(mask, shade, ramp, part, dither)

    def pixel(self, x, y, color, part=None):
        ix = int(math.floor(self.px + x))
        iy = int(math.floor(self.py - y))
        if 0 <= ix < self.w and 0 <= iy < self.h:
            mask = np.zeros((self.h, self.w), dtype=bool)
            mask[iy, ix] = True
            self.paint_flat(mask, color, part)

    def line(self, a, b, color, part=None, width=1.0):
        mask = self._segment_mask(a, b, width / 2.0)
        return self.paint_flat(mask, color, part)

    def _segment_mask(self, a, b, radius):
        ax, ay = a
        bx, by = b
        dx, dy = bx - ax, by - ay
        length_sq = max(dx * dx + dy * dy, 1e-6)
        t = np.clip(((self.wx - ax) * dx + (self.wy - ay) * dy) / length_sq, 0, 1)
        ox = self.wx - (ax + dx * t)
        oy = self.wy - (ay + dy * t)
        return ox * ox + oy * oy <= radius * radius + 0.25

    def erase(self, mask):
        self.part[mask] = -1
        self.ramp_id[mask] = -1
        self.fixed[mask] = 0

    # ---------- output ----------
    def render(self, outline="#030305", selout=True, rim=True):
        h, w = self.h, self.w
        rgba = np.zeros((h, w, 4), dtype=np.uint8)
        index = np.full((h, w), -1, dtype=np.int32)
        for rid, ramp in enumerate(self.ramps):
            sel = self.ramp_id == rid
            if not sel.any():
                continue
            shades = self.shade[sel]
            idx = np.array([ramp.index(s) for s in shades], dtype=np.int32)
            index[sel] = idx
            colors = np.array(ramp.colors, dtype=np.uint8)[idx]
            rgba[sel, :3] = colors
            rgba[sel, 3] = 255
        fixed = self.ramp_id == -2
        rgba[fixed] = self.fixed[fixed]

        opaque = self.part >= 0
        if rim:
            # Moonlight rim: brighten pixels whose upper-left neighbour is empty.
            empty = ~opaque
            edge = np.zeros_like(opaque)
            edge[:, 1:] |= empty[:, :-1]
            edge[1:, :] |= empty[:-1, :]
            edge[:, 0] |= True
            edge[0, :] |= True
            edge &= opaque
            for rid, ramp in enumerate(self.ramps):
                sel = edge & (self.ramp_id == rid)
                if not sel.any():
                    continue
                brighter = np.minimum(index[sel] + 2, len(ramp.colors) - 1)
                index[sel] = brighter
                rgba[sel, :3] = np.array(ramp.colors, dtype=np.uint8)[brighter]
        if selout:
            # Sel-out: darken the edge of a front part where it overlaps a different part behind it.
            part = self.part
            edge = np.zeros_like(opaque)
            for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                shifted = np.full_like(part, -1)
                ys = slice(max(dy, 0), h + min(dy, 0))
                yd = slice(max(-dy, 0), h + min(-dy, 0))
                xs = slice(max(dx, 0), w + min(dx, 0))
                xd = slice(max(-dx, 0), w + min(-dx, 0))
                shifted[yd, xd] = part[ys, xs]
                edge |= opaque & (shifted >= 0) & (shifted < part)
            for rid, ramp in enumerate(self.ramps):
                sel = edge & (self.ramp_id == rid)
                if not sel.any():
                    continue
                darker = np.maximum(index[sel] - 2, 0)
                rgba[sel, :3] = np.array(ramp.colors, dtype=np.uint8)[darker]

        if outline:
            out_rgb = hex_rgb(outline)
            grown = np.zeros_like(opaque)
            grown[1:, :] |= opaque[:-1, :]
            grown[:-1, :] |= opaque[1:, :]
            grown[:, 1:] |= opaque[:, :-1]
            grown[:, :-1] |= opaque[:, 1:]
            border = grown & ~opaque
            rgba[border] = (*out_rgb, 255)
        return rgba


def save_rgba(rgba, path, scale=1):
    img = Image.fromarray(rgba)
    if scale != 1:
        img = img.resize((img.width * scale, img.height * scale), Image.NEAREST)
    img.save(path)


def ik2(root, target, len_a, len_b, bend):
    """Two-bone IK. bend=+1 bends the middle joint counter-clockwise from the root->target line."""
    rx, ry = root
    tx, ty = target
    dx, dy = tx - rx, ty - ry
    dist = math.hypot(dx, dy)
    dist = min(max(dist, abs(len_a - len_b) + 0.01), len_a + len_b - 0.01)
    base = math.atan2(dy, dx)
    cos_a = (len_a * len_a + dist * dist - len_b * len_b) / (2 * len_a * dist)
    angle = base + bend * math.acos(max(-1.0, min(1.0, cos_a)))
    mid = (rx + len_a * math.cos(angle), ry + len_a * math.sin(angle))
    end_dir = math.atan2(ty - mid[1], tx - mid[0])
    end = (mid[0] + len_b * math.cos(end_dir), mid[1] + len_b * math.sin(end_dir))
    return mid, end


def polar(origin, length, angle_deg):
    """Angle 0 points straight down, 90 forward (+x), 180 up."""
    ang = math.radians(angle_deg)
    return (origin[0] + length * math.sin(ang), origin[1] - length * math.cos(ang))


def lerp(a, b, t):
    return a + (b - a) * t


def lerp_pt(a, b, t):
    return (lerp(a[0], b[0], t), lerp(a[1], b[1], t))
