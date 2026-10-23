"""Turns Codex-generated Jason source art (Tools/art_src/jason/*.png) into game sprites.

Pipeline per strip: alpha mask -> split into frames on empty columns -> normalise scale using the
bright hockey-mask area -> box downscale -> shared palette quantisation -> 1px outline -> feet pivot.
Hitboxes are measured from the front-most pixels of each active frame.

Run:  Tools/.venv/bin/python Tools/pixelart/jason.py
"""
import json
import math
import os

import numpy as np
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = os.path.join(ROOT, "Tools", "art_src", "jason")
OUT_DIR = os.path.join(ROOT, "Assets", "Resources", "Characters", "jason")
PREVIEW_DIR = os.path.join(ROOT, "Tools", "out")

FRAME_W, FRAME_H = 160, 136
PIVOT_X, PIVOT_BOTTOM = 80, 8
COLUMNS = 10
TARGET_HEIGHT = 92  # standing height in game pixels
PALETTE_SIZE = 40
OUTLINE = (10, 7, 6, 255)

# name, expected frames, anchor mode ("head" keeps the mask column steady, "box" centres the silhouette)
STRIPS = [
    ("idle", 4, "head"), ("walk", 6, "head"), ("crouch", 2, "head"), ("jump", 3, "box"),
    ("punch", 3, "head"), ("slash", 5, "head"), ("kick", 4, "head"), ("crouch_attack", 3, "head"),
    ("air_attack", 2, "box"), ("block", 2, "head"), ("hurt", 3, "head"), ("knockdown", 4, "box"),
    ("grab_throw", 5, "head"), ("ko", 4, "box"), ("headless", 4, "box"), ("dizzy", 2, "head"),
    ("victory", 3, "head"),
]

ANIM_TICKS = {
    "idle": ([11] * 4, True), "walk": ([7] * 6, True), "crouch": ([3, 6], False), "jump": ([4, 8, 8], False),
    "block": ([6, 6], False), "hurt": ([6, 6, 6], False), "knockdown": ([6, 8, 30, 12], False),
    "ko": ([10, 12, 10, 60], False), "headless": ([10, 12, 10, 60], False), "dizzy": ([16, 16], True),
    "victory": ([10, 12, 16], False),
}


def load(name):
    return np.array(Image.open(os.path.join(SRC, name + ".png")).convert("RGBA"))


def components(mask):
    """4-connected component labels of a boolean grid (BFS, fine for downsampled masks)."""
    from collections import deque
    h, w = mask.shape
    labels = np.full((h, w), -1, dtype=np.int32)
    count = 0
    for y0, x0 in zip(*np.nonzero(mask)):
        if labels[y0, x0] >= 0:
            continue
        queue = deque([(y0, x0)])
        labels[y0, x0] = count
        while queue:
            y, x = queue.popleft()
            for ny, nx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
                if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and labels[ny, nx] < 0:
                    labels[ny, nx] = count
                    queue.append((ny, nx))
        count += 1
    return labels, count


def split_frames(rgba, count):
    """Split a strip into `count` figures using 2D connected components on a 4x downsampled mask."""
    alpha = rgba[..., 3] > 96
    step = 4
    h, w = alpha.shape
    small = alpha[:h - h % step, :w - w % step].reshape(h // step, step, w // step, step).any(axis=(1, 3))
    labels, n = components(small)
    for _ in range(count):
        areas = np.bincount(labels[labels >= 0], minlength=n)
        order = np.argsort(-areas)
        big = [int(i) for i in order[:count] if areas[i] > areas[order[0]] * 0.08]
        if len(big) >= count or areas[order[0]] < 1.6 * np.median(areas[order[:count]]):
            break
        # Two figures touching: cut the largest component at its thinnest column.
        target = int(order[0])
        ys, xs = np.nonzero(labels == target)
        x0, x1 = xs.min(), xs.max()
        density = np.bincount(xs - x0, minlength=x1 - x0 + 1)
        lo, hi = int((x1 - x0) * 0.3), int((x1 - x0) * 0.7)
        cut = x0 + lo + int(np.argmin(density[lo:hi]))
        right = (labels == target) & (np.arange(labels.shape[1])[None, :] > cut)
        labels[right] = n
        n += 1
    areas = np.bincount(labels[labels >= 0], minlength=n)
    order = np.argsort(-areas)
    big = [int(i) for i in order[:count] if areas[i] > areas[order[0]] * 0.08]
    if len(big) == count:
        centroids = {}
        for i in range(n):
            ys, xs = np.nonzero(labels == i)
            centroids[i] = (xs.mean(), ys.mean())
        owner = np.full(n, -1)
        for i in range(n):
            if i in big:
                owner[i] = i
            else:
                cx, cy = centroids[i]
                owner[i] = min(big, key=lambda b: (centroids[b][0] - cx) ** 2 + (centroids[b][1] - cy) ** 2)
        frames = []
        for b in sorted(big, key=lambda b: centroids[b][0]):
            region = np.isin(labels, np.nonzero(owner == b)[0])
            full = np.kron(region, np.ones((step, step), dtype=bool))
            full = np.pad(full, ((0, h - full.shape[0]), (0, w - full.shape[1])))
            sel = full & alpha
            ys, xs = np.nonzero(sel)
            crop = rgba[ys.min():ys.max() + 1, xs.min():xs.max() + 1].copy()
            crop[~sel[ys.min():ys.max() + 1, xs.min():xs.max() + 1]] = 0
            frames.append(crop)
        return frames
    return split_frames_columns(rgba, count)


def split_frames_columns(rgba, count):
    alpha = rgba[..., 3] > 96
    cols = alpha.any(axis=0)
    runs = []
    start = None
    for x, filled in enumerate(cols):
        if filled and start is None:
            start = x
        elif not filled and start is not None:
            runs.append([start, x])
            start = None
    if start is not None:
        runs.append([start, len(cols)])
    # Drop specks (stray blood drops) that are tiny and far from everything.
    runs = [r for r in runs if (r[1] - r[0]) > 3 or alpha[:, r[0]:r[1]].sum() > 40]
    while len(runs) > count:
        gaps = [runs[i + 1][0] - runs[i][1] for i in range(len(runs) - 1)]
        # Merge the narrowest run into its closest neighbour.
        widths = [r[1] - r[0] for r in runs]
        smallest = int(np.argmin(widths))
        if smallest == 0:
            j = 0
        elif smallest == len(runs) - 1:
            j = smallest - 1
        else:
            j = smallest - 1 if gaps[smallest - 1] < gaps[smallest] else smallest
        runs[j] = [runs[j][0], runs[j + 1][1]]
        del runs[j + 1]
    if len(runs) < count:
        raise RuntimeError(f"found {len(runs)} frames, expected {count}")
    frames = []
    for x0, x1 in runs:
        sub = alpha[:, x0:x1]
        ys = np.nonzero(sub.any(axis=1))[0]
        frames.append(rgba[ys[0]:ys[-1] + 1, x0:x1])
    return frames


def mask_pixels(rgba):
    """Bright, low-saturation pixels: the hockey mask."""
    rgb = rgba[..., :3].astype(np.int32)
    lo, hi = rgb.min(axis=2), rgb.max(axis=2)
    return (rgba[..., 3] > 128) & (lo > 168) & (hi - lo < 52)


def downscale(rgba, scale):
    h, w = rgba.shape[:2]
    nw, nh = max(1, int(round(w * scale))), max(1, int(round(h * scale)))
    rgb = rgba[..., :3].astype(np.float32)
    a = rgba[..., 3:4].astype(np.float32) / 255.0
    premul = np.concatenate([rgb * a, a * 255.0], axis=2)
    img = Image.fromarray(np.clip(premul, 0, 255).astype(np.uint8))
    small = np.array(img.resize((nw, nh), Image.BOX)).astype(np.float32)
    alpha = small[..., 3:4] / 255.0
    color = np.where(alpha > 0.01, small[..., :3] / np.maximum(alpha, 1e-3), 0)
    out = np.zeros((nh, nw, 4), dtype=np.uint8)
    out[..., :3] = np.clip(color, 0, 255).astype(np.uint8)
    out[..., 3] = np.where(alpha[..., 0] > 0.45, 255, 0)
    return out


def quantize(rgba, palette_img):
    rgb = Image.fromarray(rgba[..., :3])
    q = np.array(rgb.quantize(palette=palette_img, dither=Image.Dither.NONE).convert("RGB"))
    out = rgba.copy()
    out[..., :3] = q
    out[rgba[..., 3] == 0] = 0
    return out


def add_outline(rgba):
    opaque = rgba[..., 3] > 0
    grown = opaque.copy()
    grown[1:] |= opaque[:-1]
    grown[:-1] |= opaque[1:]
    grown[:, 1:] |= opaque[:, :-1]
    grown[:, :-1] |= opaque[:, 1:]
    out = rgba.copy()
    out[grown & ~opaque] = OUTLINE
    return out


def build_palette(model):
    opaque = model[..., 3] > 128
    pixels = model[opaque][:, :3]
    side = int(math.ceil(math.sqrt(len(pixels))))
    pad = np.zeros((side * side, 3), dtype=np.uint8)
    pad[:len(pixels)] = pixels
    pad[len(pixels):] = pixels[0]
    img = Image.fromarray(pad.reshape(side, side, 3))
    return img.quantize(colors=PALETTE_SIZE, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE)


def place(sprite, anchor_x):
    """Put a downscaled sprite in a frame cell: bottom row on the baseline, anchor_x on the pivot."""
    cell = np.zeros((FRAME_H, FRAME_W, 4), dtype=np.uint8)
    h, w = sprite.shape[:2]
    top = FRAME_H - PIVOT_BOTTOM - h
    left = int(round(PIVOT_X - anchor_x))
    for y in range(h):
        cy = top + y
        if not 0 <= cy < FRAME_H:
            continue
        x0, x1 = max(left, 0), min(left + w, FRAME_W)
        if x1 <= x0:
            continue
        row = sprite[y, x0 - left:x1 - left]
        dst = cell[cy, x0:x1]
        m = row[..., 3] > 0
        dst[m] = row[m]
    return cell


def front_box(cell, depth=26, max_h=52):
    """Hitbox around the front-most pixels of a frame, in pivot space (y up)."""
    opaque = cell[..., 3] > 0
    xs = np.nonzero(opaque.any(axis=0))[0]
    max_x = xs[-1]
    region = opaque[:, max(max_x - depth, 0):max_x + 1]
    ys = np.nonzero(region.any(axis=1))[0]
    y_top, y_bottom = ys[0], ys[-1]
    if y_bottom - y_top > max_h:
        mid = (y_top + y_bottom) // 2
        y_top, y_bottom = mid - max_h // 2, mid + max_h // 2
    return {"x": int(max_x - depth - PIVOT_X), "y": int(FRAME_H - PIVOT_BOTTOM - y_bottom - PIVOT_BOTTOM),
            "w": int(depth + 3), "h": int(y_bottom - y_top + 1)}


def front_point(cell):
    opaque = cell[..., 3] > 0
    xs = np.nonzero(opaque.any(axis=0))[0]
    max_x = xs[-1]
    ys = np.nonzero(opaque[:, max_x])[0]
    y = int(ys.mean())
    return float(max_x - PIVOT_X - 3), float(FRAME_H - PIVOT_BOTTOM - y)


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    os.makedirs(PREVIEW_DIR, exist_ok=True)
    model = load("model_sheet")
    palette = build_palette(model)
    model_frame = split_frames(model, 1)[0]
    model_scale = TARGET_HEIGHT / model_frame.shape[0]
    target_mask_area = mask_pixels(model_frame).sum() * model_scale * model_scale
    target_area = (model_frame[..., 3] > 96).sum() * model_scale * model_scale

    # Head offset from the feet centre in the neutral stance, used to anchor "head" strips.
    idle_small = downscale(model_frame, model_scale)
    mk = mask_pixels(idle_small)
    feet = idle_small[-6:, :, 3] > 0
    feet_cx = np.nonzero(feet.any(axis=0))[0].mean()
    head_offset = np.nonzero(mk.any(axis=0))[0].mean() - feet_cx if mk.any() else 0.0

    cells, sprites, animations, report = [], [], [], []
    scales = {}
    for name, count, anchor_mode in STRIPS:
        frames = split_frames(load(name), count)
        areas = [(f[..., 3] > 96).sum() for f in frames]
        scale = math.sqrt(target_area / float(np.median(areas)))
        if name == "headless":
            scale *= 0.96
        scales[name] = scale
        indices = []
        anchor_x_list, anchor_y_list = [], []
        for i, frame in enumerate(frames):
            small = quantize(downscale(frame, scale), palette)
            small = add_outline(np.pad(small, ((1, 1), (1, 1), (0, 0))))
            mk = mask_pixels(small)
            if anchor_mode == "head" and mk.any():
                anchor = np.nonzero(mk.any(axis=0))[0].mean() - head_offset
            else:
                xs = np.nonzero((small[..., 3] > 0).any(axis=0))[0]
                anchor = (xs[0] + xs[-1]) / 2.0
            cell = place(small, anchor)
            cells.append(cell)
            indices.append(len(sprites))
            sprites.append({"name": f"{name}_{i}", "cell": len(cells) - 1, "pivotX": PIVOT_X, "pivotY": PIVOT_BOTTOM})
            ax, ay = front_point(cell)
            anchor_x_list.append(round(ax, 1))
            anchor_y_list.append(round(ay, 1))
            if small.shape[1] > FRAME_W or small.shape[0] > FRAME_H - PIVOT_BOTTOM:
                report.append(f"{name}_{i} clipped ({small.shape[1]}x{small.shape[0]})")
        ticks, loop = ANIM_TICKS.get(name, ([], False))
        animations.append({"name": name, "sprites": indices, "ticks": ticks, "loop": loop,
                           "anchorX": anchor_x_list, "anchorY": anchor_y_list})

    def cell_of(anim, frame):
        a = next(x for x in animations if x["name"] == anim)
        return cells[sprites[a["sprites"][frame]]["cell"]]

    # Gore pieces: head, torso, left arm, right arm (machete), left leg, right leg.
    gore_frames = split_frames(load("gore_parts"), 6)
    parts_area = sum((f[..., 3] > 96).sum() for f in gore_frames)
    gore_scale = math.sqrt(target_area / parts_area) * 1.15
    gore_names = ["head", "torso", "arm_back", "arm_front", "leg_back", "leg_front"]
    gore_offsets = [(8, 80), (2, 56), (-9, 60), (12, 58), (-6, 22), (7, 22)]
    gore = []
    for part, frame, (ox, oy) in zip(gore_names, gore_frames, gore_offsets):
        small = add_outline(np.pad(quantize(downscale(frame, gore_scale), palette), ((1, 1), (1, 1), (0, 0))))
        h, w = small.shape[:2]
        cell = np.zeros((FRAME_H, FRAME_W, 4), dtype=np.uint8)
        top, left = (FRAME_H - h) // 2, (FRAME_W - w) // 2
        cell[top:top + h, left:left + w] = small
        cells.append(cell)
        sprites.append({"name": f"gore_{part}", "cell": len(cells) - 1, "pivotX": FRAME_W / 2, "pivotY": FRAME_H / 2})
        gore.append({"part": part, "sprite": len(sprites) - 1, "x": ox, "y": oy})

    rows = math.ceil(len(cells) / COLUMNS)
    atlas = np.zeros((rows * FRAME_H, COLUMNS * FRAME_W, 4), dtype=np.uint8)
    for i, cell in enumerate(cells):
        r, c = divmod(i, COLUMNS)
        atlas[r * FRAME_H:(r + 1) * FRAME_H, c * FRAME_W:(c + 1) * FRAME_W] = cell
    Image.fromarray(atlas).save(os.path.join(OUT_DIR, "atlas.png"))
    preview = Image.new("RGBA", (atlas.shape[1], atlas.shape[0]), (40, 30, 45, 255))
    preview.alpha_composite(Image.fromarray(atlas))
    preview.resize((atlas.shape[1] * 2, atlas.shape[0] * 2), Image.NEAREST).save(
        os.path.join(PREVIEW_DIR, "jason_atlas_x2.png"))

    portrait_src = load("portrait")
    portrait = portrait_card(portrait_src, palette, 96)
    Image.fromarray(portrait).save(os.path.join(OUT_DIR, "portrait.png"))
    Image.fromarray(portrait_card(portrait_src, palette, 40)).save(os.path.join(OUT_DIR, "thumb.png"))

    moves = [
        {"id": "light", "input": "light", "state": "stand", "anim": "punch",
         "startupFrames": [0], "activeFrames": [1], "recoveryFrames": [2],
         "startup": 6, "active": 3, "recovery": 10, "damage": 55, "hitstun": 16, "blockstun": 10,
         "level": "mid", "pushback": 6.0, "knockdown": False, "lunge": 0.8,
         "hitbox": front_box(cell_of("punch", 1), 22, 20), "sfxWhiff": "whoosh_light", "sfxHit": "hit_light",
         "blood": 4, "shake": 0.15, "hitstop": 7},
        {"id": "heavy", "input": "heavy", "state": "stand", "anim": "slash",
         "startupFrames": [0, 1], "activeFrames": [2, 3], "recoveryFrames": [4],
         "startup": 15, "active": 5, "recovery": 20, "damage": 150, "hitstun": 24, "blockstun": 16,
         "level": "overhead", "pushback": 9.0, "knockdown": False, "lunge": 1.2,
         "hitbox": front_box(cell_of("slash", 2), 40, 60), "sfxWhiff": "whoosh_heavy", "sfxHit": "slash_heavy",
         "blood": 18, "shake": 0.8, "hitstop": 12},
        {"id": "kick", "input": "kick", "state": "stand", "anim": "kick",
         "startupFrames": [0], "activeFrames": [1], "recoveryFrames": [2, 3],
         "startup": 9, "active": 4, "recovery": 14, "damage": 85, "hitstun": 18, "blockstun": 12,
         "level": "mid", "pushback": 14.0, "knockdown": False, "lunge": 0.0,
         "hitbox": front_box(cell_of("kick", 1), 22, 22), "sfxWhiff": "whoosh_heavy", "sfxHit": "hit_kick",
         "blood": 5, "shake": 0.4, "hitstop": 9},
        {"id": "crouch_light", "input": "any", "state": "crouch", "anim": "crouch_attack",
         "startupFrames": [0], "activeFrames": [1], "recoveryFrames": [2],
         "startup": 9, "active": 4, "recovery": 18, "damage": 70, "hitstun": 18, "blockstun": 12,
         "level": "low", "pushback": 6.0, "knockdown": True, "lunge": 0.4,
         "hitbox": front_box(cell_of("crouch_attack", 1), 36, 18), "sfxWhiff": "whoosh_heavy",
         "sfxHit": "slash_light", "blood": 8, "shake": 0.3, "hitstop": 8},
        {"id": "air", "input": "any", "state": "air", "anim": "air_attack",
         "startupFrames": [0], "activeFrames": [1], "recoveryFrames": [],
         "startup": 6, "active": 18, "recovery": 0, "damage": 90, "hitstun": 18, "blockstun": 12,
         "level": "overhead", "pushback": 6.0, "knockdown": False, "lunge": 0.0,
         "hitbox": front_box(cell_of("air_attack", 1), 34, 40), "sfxWhiff": "whoosh_heavy",
         "sfxHit": "slash_heavy", "blood": 10, "shake": 0.3, "hitstop": 9},
        {"id": "grab", "input": "grab", "state": "stand", "anim": "grab_throw",
         "startupFrames": [0], "activeFrames": [0], "recoveryFrames": [4],
         "startup": 6, "active": 3, "recovery": 22, "damage": 170, "hitstun": 0, "blockstun": 0,
         "level": "throw", "pushback": 0.0, "knockdown": True, "lunge": 0.0,
         "hitbox": {"x": 8, "y": 20, "w": 32, "h": 56}, "sfxWhiff": "whoosh_light", "sfxHit": "grab",
         "blood": 0, "shake": 0.0, "hitstop": 0},
        {"id": "special", "input": "forward+heavy", "state": "stand", "anim": "slash",
         "startupFrames": [0, 1], "activeFrames": [2, 3], "recoveryFrames": [4],
         "startup": 14, "active": 7, "recovery": 22, "damage": 120, "hitstun": 22, "blockstun": 16,
         "level": "mid", "pushback": 10.0, "knockdown": True, "lunge": 5.0,
         "hitbox": front_box(cell_of("slash", 2), 40, 60), "sfxWhiff": "whoosh_heavy", "sfxHit": "slash_heavy",
         "blood": 16, "shake": 0.7, "hitstop": 12, "name": "MACHETE RUSH"},
    ]

    data = {
        "id": "jason", "displayName": "JASON", "subtitle": "THE CAMP BUTCHER", "author": "Codex (image gen)",
        "frameW": FRAME_W, "frameH": FRAME_H, "columns": COLUMNS,
        "sprites": sprites, "animations": animations, "moves": moves, "gore": gore,
        "stats": {"health": 1100, "walkSpeed": 1.15, "backSpeed": 0.95, "jumpVelocity": 6.1, "jumpForward": 1.8,
                  "gravity": 0.36, "pushWidth": 28, "weight": 1.2},
        "hurtStand": {"x": -15, "y": 0, "w": 30, "h": 90},
        "hurtCrouch": {"x": -16, "y": 0, "w": 34, "h": 62},
        "hurtAir": {"x": -14, "y": 8, "w": 28, "h": 76},
        "bloodColor": "#8a0b12", "accentColor": "#9fc26a",
        "neckX": 6.0, "neckY": 76.0,
    }
    with open(os.path.join(OUT_DIR, "character.json"), "w") as f:
        json.dump(data, f, indent=1)
    print(f"jason: {len(cells)} cells, atlas {atlas.shape[1]}x{atlas.shape[0]}")
    for line in report:
        print("  warning:", line)


def portrait_card(src, palette, size):
    opaque = src[..., 3] > 96
    ys, xs = np.nonzero(opaque)
    crop = src[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    h, w = crop.shape[:2]
    scale = size / max(h, w)
    small = quantize(downscale(crop, scale), palette)
    card = np.zeros((size, size, 4), dtype=np.uint8)
    sh, sw = small.shape[:2]
    top, left = size - sh, (size - sw) // 2
    card[top:top + sh, left:left + sw] = small
    return add_outline(card)


if __name__ == "__main__":
    main()
