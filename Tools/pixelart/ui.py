"""Pixel-art UI kit: bitmap fonts, HUD frames, select-screen pieces, hit sparks, blood decals, logo, fallback stage.

Run:  Tools/.venv/bin/python Tools/pixelart/ui.py
Writes into Assets/Resources/UI/ and Assets/Resources/Stages/.
"""
import json
import math
import os
import random

import numpy as np
from PIL import Image

from pixel import hex_rgb

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
UI_DIR = os.path.join(ROOT, "Assets", "Resources", "UI")
STAGE_DIR = os.path.join(ROOT, "Assets", "Resources", "Stages", "fallback")
PREVIEW_DIR = os.path.join(ROOT, "Tools", "out")

GLYPHS = {
    "A": [".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"],
    "B": ["####.", "#...#", "#...#", "####.", "#...#", "#...#", "####."],
    "C": [".###.", "#...#", "#....", "#....", "#....", "#...#", ".###."],
    "D": ["####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####."],
    "E": ["#####", "#....", "#....", "####.", "#....", "#....", "#####"],
    "F": ["#####", "#....", "#....", "####.", "#....", "#....", "#...."],
    "G": [".###.", "#...#", "#....", "#.###", "#...#", "#...#", ".####"],
    "H": ["#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"],
    "I": [".###.", "..#..", "..#..", "..#..", "..#..", "..#..", ".###."],
    "J": ["..###", "...#.", "...#.", "...#.", "#..#.", "#..#.", ".##.."],
    "K": ["#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#"],
    "L": ["#....", "#....", "#....", "#....", "#....", "#....", "#####"],
    "M": ["#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#"],
    "N": ["#...#", "#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#"],
    "O": [".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."],
    "P": ["####.", "#...#", "#...#", "####.", "#....", "#....", "#...."],
    "Q": [".###.", "#...#", "#...#", "#...#", "#.#.#", "#..#.", ".##.#"],
    "R": ["####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#"],
    "S": [".####", "#....", "#....", ".###.", "....#", "....#", "####."],
    "T": ["#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.."],
    "U": ["#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."],
    "V": ["#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#.."],
    "W": ["#...#", "#...#", "#...#", "#.#.#", "#.#.#", "#.#.#", ".#.#."],
    "X": ["#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#"],
    "Y": ["#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.."],
    "Z": ["#####", "....#", "...#.", "..#..", ".#...", "#....", "#####"],
    "0": [".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###."],
    "1": ["..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###."],
    "2": [".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####"],
    "3": ["####.", "....#", "....#", ".###.", "....#", "....#", "####."],
    "4": ["...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#."],
    "5": ["#####", "#....", "####.", "....#", "....#", "#...#", ".###."],
    "6": [".###.", "#....", "#....", "####.", "#...#", "#...#", ".###."],
    "7": ["#####", "....#", "...#.", "..#..", "..#..", "..#..", "..#.."],
    "8": [".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###."],
    "9": [".###.", "#...#", "#...#", ".####", "....#", "....#", ".###."],
    "!": ["..#..", "..#..", "..#..", "..#..", "..#..", ".....", "..#.."],
    "?": [".###.", "#...#", "....#", "...#.", "..#..", ".....", "..#.."],
    ".": [".....", ".....", ".....", ".....", ".....", ".....", "..#.."],
    ",": [".....", ".....", ".....", ".....", ".....", "..#..", ".#..."],
    ":": [".....", "..#..", ".....", ".....", ".....", "..#..", "....."],
    "-": [".....", ".....", ".....", ".###.", ".....", ".....", "....."],
    "'": ["..#..", "..#..", ".....", ".....", ".....", ".....", "....."],
    "/": ["....#", "....#", "...#.", "..#..", ".#...", "#....", "#...."],
    "+": [".....", "..#..", "..#..", "#####", "..#..", "..#..", "....."],
    "&": [".##..", "#..#.", "#.#..", ".#...", "#.#.#", "#..#.", ".##.#"],
    "%": ["##..#", "##..#", "...#.", "..#..", ".#...", "#..##", "#..##"],
    "(": ["...#.", "..#..", ".#...", ".#...", ".#...", "..#..", "...#."],
    ")": [".#...", "..#..", "...#.", "...#.", "...#.", "..#..", ".#..."],
    "<": ["...#.", "..#..", ".#...", "#....", ".#...", "..#..", "...#."],
    ">": [".#...", "..#..", "...#.", "....#", "...#.", "..#..", ".#..."],
    "*": [".....", "#.#.#", ".###.", "#####", ".###.", "#.#.#", "....."],
    " ": [".....", ".....", ".....", ".....", ".....", ".....", "....."],
}
CHARSET = "".join(GLYPHS.keys())

OUTLINE = hex_rgb("#0a0508")
SHADOW = hex_rgb("#000000")


def glyph_mask(ch, scale):
    rows = GLYPHS[ch]
    m = np.array([[c == "#" for c in row] for row in rows], dtype=bool)
    return np.kron(m, np.ones((scale, scale), dtype=bool))


def dilate(mask, r=1):
    out = mask.copy()
    for dy in range(-r, r + 1):
        for dx in range(-r, r + 1):
            if dx == 0 and dy == 0:
                continue
            shifted = np.zeros_like(mask)
            ys = slice(max(dy, 0), mask.shape[0] + min(dy, 0))
            yd = slice(max(-dy, 0), mask.shape[0] + min(-dy, 0))
            xs = slice(max(dx, 0), mask.shape[1] + min(dx, 0))
            xd = slice(max(-dx, 0), mask.shape[1] + min(-dx, 0))
            shifted[yd, xd] = mask[ys, xs]
            out |= shifted
    return out


def shift(mask, dx, dy):
    out = np.zeros_like(mask)
    h, w = mask.shape
    out[max(dy, 0):h + min(dy, 0), max(dx, 0):w + min(dx, 0)] = mask[max(-dy, 0):h + min(-dy, 0),
                                                                     max(-dx, 0):w + min(-dx, 0)]
    return out


def build_font(name, scale, gradient, outline_px, shadow_px, advance_extra=1):
    gw, gh = 5 * scale, 7 * scale
    pad = outline_px + shadow_px
    cw, ch = gw + outline_px * 2 + shadow_px, gh + outline_px * 2 + shadow_px
    cols = 16
    rows = math.ceil(len(CHARSET) / cols)
    atlas = np.zeros((rows * ch, cols * cw, 4), dtype=np.uint8)
    grad = [hex_rgb(c) for c in gradient]
    for i, c in enumerate(CHARSET):
        g = np.zeros((ch, cw), dtype=bool)
        g[outline_px:outline_px + gh, outline_px:outline_px + gw] = glyph_mask(c, scale)
        outline = dilate(g, outline_px) & ~g
        shadow = shift(dilate(g, outline_px), shadow_px, shadow_px) & ~dilate(g, outline_px) if shadow_px else None
        cell = np.zeros((ch, cw, 4), dtype=np.uint8)
        if shadow is not None:
            cell[shadow] = (*SHADOW, 200)
        cell[outline] = (*OUTLINE, 255)
        ys, xs = np.nonzero(g)
        for y, x in zip(ys, xs):
            t = (y - outline_px) / max(gh - 1, 1)
            idx = min(int(t * len(grad)), len(grad) - 1)
            cell[y, x] = (*grad[idx], 255)
        # Top-left specular pixel on large fonts for an arcade shine.
        if scale >= 2:
            top = g & ~shift(g, 0, 1)
            cell[top & (np.arange(cw)[None, :] % 3 == 0)] = (255, 255, 240, 255)
        r, col = divmod(i, cols)
        atlas[r * ch:(r + 1) * ch, col * cw:(col + 1) * cw] = cell
    Image.fromarray(atlas).save(os.path.join(UI_DIR, f"font_{name}.png"))
    return {"name": name, "chars": CHARSET, "cellW": cw, "cellH": ch, "columns": cols,
            "advance": gw + scale * advance_extra, "glyphW": gw, "glyphH": gh, "lineHeight": gh + scale * 3, "originX": outline_px,
            "originY": outline_px + shadow_px}


# ---------------------------------------------------------------- drawing helpers

class Img:
    def __init__(self, w, h):
        self.a = np.zeros((h, w, 4), dtype=np.uint8)
        self.w, self.h = w, h

    def rect(self, x, y, w, h, color, alpha=255):
        rgb = hex_rgb(color) if isinstance(color, str) else color
        self.a[max(y, 0):y + h, max(x, 0):x + w] = (*rgb, alpha)

    def px(self, x, y, color, alpha=255):
        if 0 <= x < self.w and 0 <= y < self.h:
            self.a[y, x] = (*(hex_rgb(color) if isinstance(color, str) else color), alpha)

    def frame(self, x, y, w, h, color):
        self.rect(x, y, w, 1, color)
        self.rect(x, y + h - 1, w, 1, color)
        self.rect(x, y, 1, h, color)
        self.rect(x + w - 1, y, 1, h, color)

    def mask(self, m, color, alpha=255):
        self.a[m] = (*(hex_rgb(color) if isinstance(color, str) else color), alpha)

    def outline(self, color="#050306"):
        opaque = self.a[..., 3] > 0
        border = dilate(opaque, 1) & ~opaque
        self.a[border] = (*hex_rgb(color), 255)

    def save(self, name, folder=None):
        Image.fromarray(self.a).save(os.path.join(folder or UI_DIR, name))


def disc(w, h, cx, cy, r):
    ys, xs = np.mgrid[0:h, 0:w]
    return (xs + 0.5 - cx) ** 2 + (ys + 0.5 - cy) ** 2 <= r * r


SKULL = [
    "..#####..",
    ".#######.",
    "#########",
    "##..#..##",
    "##..#..##",
    "#########",
    ".###.###.",
    "..#.#.#..",
    "..#####..",
]


def skull_mask(scale=1):
    m = np.array([[c == "#" for c in row] for row in SKULL], dtype=bool)
    return np.kron(m, np.ones((scale, scale), dtype=bool))


def bar_frame():
    """Ornate health-bar frame for P1 (P2 is mirrored in code). Inner fill area: x 12..150, y 3..11."""
    img = Img(160, 15)
    img.frame(8, 1, 146, 13, "#5c4a3e")
    img.frame(9, 2, 144, 11, "#1a1216")
    img.rect(9, 1, 144, 1, "#8c7462")
    img.rect(8, 13, 146, 1, "#2b2019")
    # Rivets.
    for x in range(20, 150, 16):
        img.px(x, 1, "#c9a988")
        img.px(x, 13, "#3d2e25")
    # Skull cap on the outer end.
    sk = skull_mask(1)
    cap = np.zeros((15, 160), dtype=bool)
    cap[3:12, 0:9] = sk
    img.rect(0, 2, 10, 11, "#1a1216")
    img.mask(cap, "#e8dcc4")
    eyes = np.zeros_like(cap)
    eyes[6:8, 2:4] = True
    eyes[6:8, 5:7] = True
    img.mask(eyes & cap | eyes, "#b0101a")
    img.outline()
    img.save("bar_frame.png")


def bar_fill():
    img = Img(1, 9)
    shades = ["#ffffff", "#f2f2f2", "#dcdcdc", "#cfcfcf", "#c2c2c2", "#b3b3b3", "#a0a0a0", "#8a8a8a", "#6e6e6e"]
    for y, c in enumerate(shades):
        img.px(0, y, c)
    img.save("bar_fill.png")


def timer_box():
    img = Img(34, 26)
    img.rect(1, 1, 32, 24, "#140c10")
    img.frame(1, 1, 32, 24, "#6d5646")
    img.frame(2, 2, 30, 22, "#2b1f19")
    img.rect(2, 2, 30, 1, "#a1846c")
    for (x, y) in ((1, 1), (32, 1), (1, 24), (32, 24)):
        img.px(x, y, "#e0c49c")
    img.outline()
    img.save("timer_box.png")


def skulls():
    for name, fill, eye in (("skull_on", "#f1e6cc", "#c0121c"), ("skull_off", "#3a2e2a", "#1a1214")):
        img = Img(11, 11)
        m = np.zeros((11, 11), dtype=bool)
        m[1:10, 1:10] = skull_mask(1)
        img.mask(m, fill)
        e = np.zeros_like(m)
        e[4:6, 3:5] = True
        e[4:6, 6:8] = True
        img.mask(e, eye)
        img.outline()
        img.save(f"{name}.png")


def select_cell(name, locked=False):
    img = Img(46, 46)
    img.rect(2, 2, 42, 42, "#0d080b")
    img.frame(1, 1, 44, 44, "#4a3a31")
    img.frame(2, 2, 42, 42, "#7c6450")
    img.rect(3, 3, 40, 1, "#a98a6e")
    if locked:
        rng = random.Random(3)
        for _ in range(140):
            img.px(rng.randrange(4, 42), rng.randrange(4, 42), "#1c1216")
        q = np.zeros((46, 46), dtype=bool)
        q[12:33, 16:31] = glyph_mask("?", 3)
        img.mask(dilate(q, 1) & ~q, "#050305")
        img.mask(q, "#5a1a20")
        # Chains across the slot.
        for i in range(4, 42, 3):
            img.px(i, 10 + (i % 6 == 0), "#6a6a70")
            img.px(i, 36 - (i % 6 == 0), "#6a6a70")
    img.outline()
    img.save(name)


def cursor(name, color, label):
    img = Img(52, 58)
    rgb = color
    for t in range(2):
        img.frame(t, t, 52 - 2 * t, 52 - 2 * t, rgb)
    # Corner teeth.
    for (x, y) in ((0, 0), (44, 0), (0, 44), (44, 44)):
        img.rect(x, y, 8, 8, rgb)
        img.rect(x + 2, y + 2, 4, 4, "#ffffff")
    tag = np.zeros((58, 52), dtype=bool)
    for i, ch in enumerate(label):
        tag[51:58, 14 + i * 7:19 + i * 7] = glyph_mask(ch, 1)
    img.rect(12, 50, 6 + 7 * len(label), 8, rgb)
    img.mask(tag, "#ffffff")
    img.outline()
    img.save(name)


def sparks():
    """Hit spark (yellow/white), block spark (cyan) and blood burst, 4 frames of 32x32 each."""
    sheets = {
        "fx_hit.png": ["#ffffff", "#fff3a0", "#ffb030", "#e04010"],
        "fx_block.png": ["#ffffff", "#c8f4ff", "#5ac8ff", "#2060c0"],
        "fx_blood.png": ["#ff7078", "#d01e2a", "#8a0a14", "#4a0006"],
    }
    for name, colors in sheets.items():
        img = Img(128, 32)
        rng = random.Random(len(name))
        rays = [(rng.uniform(0, math.tau), rng.uniform(0.7, 1.0)) for _ in range(9)]
        for f in range(4):
            cx, cy = f * 32 + 16, 16
            t = (f + 1) / 4
            radius = 4 + 11 * t
            for ang, length in rays:
                steps = int(radius * length)
                for s in range(int(steps * (0.5 * t)), steps):
                    x = int(round(cx + math.cos(ang) * s))
                    y = int(round(cy + math.sin(ang) * s))
                    idx = min(3, int((s / max(steps, 1)) * 2 + f * 0.7))
                    img.px(x, y, colors[idx])
            if f < 2:
                core = disc(128, 32, cx, cy, 5 - f * 2)
                img.mask(core, colors[0] if f == 0 else colors[1])
        img.save(name)


def misc():
    img = Img(1, 1)
    img.px(0, 0, "#ffffff")
    img.save("pixel.png")

    shadow = Img(44, 8)
    for y in range(8):
        for x in range(44):
            dx, dy = (x + 0.5 - 22) / 22, (y + 0.5 - 4) / 4
            if dx * dx + dy * dy <= 1:
                shadow.px(x, y, "#000000", 110 if dx * dx + dy * dy > 0.45 else 160)
    shadow.save("shadow.png")

    stains = Img(60, 8)
    rng = random.Random(11)
    for k in range(3):
        cx = k * 20 + 10
        for _ in range(30 + k * 10):
            x = int(cx + rng.gauss(0, 3.5 + k))
            y = int(4 + rng.gauss(0, 0.9))
            stains.px(x, y, rng.choice(["#5e0710", "#7a0a14", "#4a0006", "#8c0f19"]))
    stains.save("stains.png")

    panel = Img(24, 24)
    panel.rect(1, 1, 22, 22, "#0e090c")
    panel.frame(1, 1, 22, 22, "#5c4a3e")
    panel.frame(2, 2, 20, 20, "#21181a")
    panel.rect(2, 2, 20, 1, "#8c7462")
    for (x, y) in ((1, 1), (22, 1), (1, 22), (22, 22)):
        panel.px(x, y, "#e0c49c")
    panel.outline()
    panel.save("panel.png")

    arrow = Img(9, 9)
    m = np.zeros((9, 9), dtype=bool)
    for i in range(5):
        m[1 + i, 1:2 + i] = True
        m[7 - i, 1:2 + i] = True
    arrow.mask(m, "#ffd040")
    arrow.outline()
    arrow.save("arrow.png")


def big_text(text, scale, gradient, outline_px=2, shadow_px=3, spacing=1):
    gw, gh = 5 * scale, 7 * scale
    width = len(text) * (gw + spacing * scale) + outline_px * 2 + shadow_px
    height = gh + outline_px * 2 + shadow_px
    m = np.zeros((height, width), dtype=bool)
    for i, ch in enumerate(text):
        x = outline_px + i * (gw + spacing * scale)
        m[outline_px:outline_px + gh, x:x + gw] |= glyph_mask(ch, scale)
    return m, height, width, gradient


def logo():
    """Title logo: GHOST / VS / JASON with fire gradient, blood drips and a heavy outline."""
    rng = random.Random(7)
    lines = [("GHOST", 5), ("VS", 3), ("JASON", 5)]
    widths = [len(t) * 6 * s for t, s in lines]
    W = max(widths) + 24
    heights = [7 * s for _, s in lines]
    H = sum(heights) + 8 * len(lines) + 30
    fill = np.zeros((H, W), dtype=bool)
    color = np.zeros((H, W, 3), dtype=np.uint8)
    y = 6
    fire = [hex_rgb(c) for c in ["#fff6c0", "#ffd24a", "#ff9a1e", "#f0501a", "#c01818", "#7a0a12"]]
    blood = [hex_rgb(c) for c in ["#e8323c", "#b01420", "#7a0a14"]]
    for (text, s), h in zip(lines, heights):
        lw = len(text) * 6 * s - s
        x0 = (W - lw) // 2
        for i, ch in enumerate(text):
            gm = glyph_mask(ch, s)
            x = x0 + i * 6 * s
            fill[y:y + h, x:x + 5 * s] |= gm
        palette = blood if text == "VS" else fire
        for yy in range(y, y + h):
            t = (yy - y) / max(h - 1, 1)
            color[yy, :] = palette[min(int(t * len(palette)), len(palette) - 1)]
        y += h + 8
    # Blood drips from the bottom edge of letters.
    bottoms = fill & ~shift(fill, 0, -1)
    ys, xs = np.nonzero(bottoms)
    for yy, xx in zip(ys, xs):
        if rng.random() < 0.07:
            length = rng.randint(3, 12)
            for k in range(1, length):
                if yy + k < H:
                    fill[yy + k, xx] = True
                    color[yy + k, xx] = blood[1]
            if yy + length < H - 1:
                fill[yy + length:yy + length + 2, max(xx - 1, 0):xx + 1] = True
                color[yy + length:yy + length + 2, max(xx - 1, 0):xx + 1] = blood[2]
    img = Img(W, H)
    img.a[fill, :3] = color[fill]
    img.a[fill, 3] = 255
    outer = dilate(fill, 2) & ~fill
    img.a[outer] = (*hex_rgb("#0a0306"), 255)
    shadow = shift(dilate(fill, 2), 3, 3) & ~dilate(fill, 2)
    img.a[shadow] = (0, 0, 0, 170)
    top = fill & ~shift(fill, 0, 1)
    img.a[top] = (255, 255, 235, 255)
    img.save("logo.png")


def vs_badge():
    m, h, w, _ = big_text("VS", 6, None)
    img = Img(w + 6, h + 6)
    fill = np.zeros((h + 6, w + 6), dtype=bool)
    fill[3:3 + h, 3:3 + w] = m
    grad = [hex_rgb(c) for c in ["#ffffff", "#ffe070", "#ff8020", "#d01818", "#600008"]]
    ys, xs = np.nonzero(fill)
    top, bottom = ys.min(), ys.max()
    for yy, xx in zip(ys, xs):
        t = (yy - top) / max(bottom - top, 1)
        img.a[yy, xx] = (*grad[min(int(t * len(grad)), len(grad) - 1)], 255)
    img.a[dilate(fill, 2) & ~fill] = (*hex_rgb("#0a0306"), 255)
    img.save("vs.png")


def fallback_stage():
    """Moonlit lake camp at night, 640x216, three parallax layers."""
    os.makedirs(STAGE_DIR, exist_ok=True)
    W, H = 640, 216
    rng = random.Random(42)
    bayer = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) / 16.0
    sky_cols = [hex_rgb(c) for c in ["#05040c", "#0b0a1c", "#15122c", "#221a3a", "#35204a", "#4a2650"]]
    sky = np.zeros((H, W, 4), dtype=np.uint8)
    for y in range(H):
        t = (y / H) ** 1.2 * (len(sky_cols) - 1)
        for x in range(W):
            base = int(t)
            frac = t - base
            idx = base + (1 if frac > bayer[y % 4, x % 4] else 0)
            sky[y, x] = (*sky_cols[min(idx, len(sky_cols) - 1)], 255)
    for _ in range(220):
        x, y = rng.randrange(W), rng.randrange(int(H * 0.6))
        b = rng.choice([110, 150, 200, 255])
        sky[y, x] = (b, b, min(255, b + 20), 255)
    moon_c = (470, 48)
    ys, xs = np.mgrid[0:H, 0:W]
    d = np.sqrt((xs - moon_c[0]) ** 2 + (ys - moon_c[1]) ** 2)
    halo = (d < 46) & (d >= 26)
    sky[halo & ((xs + ys) % 2 == 0)] = (*hex_rgb("#3a2c5a"), 255)
    moon = d < 26
    sky[moon] = (*hex_rgb("#f2ecd6"), 255)
    shade = moon & (np.sqrt((xs - moon_c[0] - 8) ** 2 + (ys - moon_c[1] + 5) ** 2) > 24)
    sky[shade] = (*hex_rgb("#c9c0a4"), 255)
    for _ in range(9):
        cx, cy, r = moon_c[0] + rng.randint(-16, 16), moon_c[1] + rng.randint(-16, 16), rng.randint(2, 5)
        crater = moon & (np.sqrt((xs - cx) ** 2 + (ys - cy) ** 2) < r)
        sky[crater] = (*hex_rgb("#d8d0b6"), 255)
    Image.fromarray(sky).save(os.path.join(STAGE_DIR, "layer0.png"))

    # Far tree line + lake.
    far = np.zeros((H, W, 4), dtype=np.uint8)
    lake_top = 150
    for x in range(W):
        h = 30 + int(10 * math.sin(x * 0.05) + 6 * math.sin(x * 0.13 + 1)) + rng.randint(0, 4)
        if x % 7 == 0:
            h += rng.randint(4, 14)
        far[lake_top - h:lake_top, x] = (*hex_rgb("#0b0a16"), 255)
    for y in range(lake_top, H):
        for x in range(W):
            far[y, x] = (*hex_rgb("#0d1224" if (y + x // 3) % 5 else "#141b34"), 255)
    # Moon reflection on the lake.
    for y in range(lake_top + 2, H, 2):
        half = max(2, 12 - (y - lake_top) // 6) + rng.randint(-2, 2)
        far[y, moon_c[0] - 140 - half:moon_c[0] - 140 + half] = (*hex_rgb("#8a86a8"), 255)
    Image.fromarray(far).save(os.path.join(STAGE_DIR, "layer1.png"))

    # Near layer: cabin, dead trees, fence, ground (ground line at y = 188).
    near = np.zeros((H, 960, 4), dtype=np.uint8)
    ground = 188
    near[ground:, :] = (*hex_rgb("#14100e"), 255)
    for x in range(960):
        for y in range(ground, H):
            if rng.random() < 0.08:
                near[y, x] = (*hex_rgb(rng.choice(["#211a16", "#0c0908", "#2a1f18"])), 255)
    near[ground, :] = (*hex_rgb("#2f2620"), 255)
    near[ground + 1, :] = (*hex_rgb("#211a16"), 255)

    def tree(x0, height, lean):
        for i in range(height):
            w = max(1, 5 - i // 18)
            x = int(x0 + lean * i * i / height / 4)
            near[ground - i, x - w // 2:x + w // 2 + 1] = (*hex_rgb("#08060a"), 255)
            if i > height * 0.35 and rng.random() < 0.08:
                direction = rng.choice([-1, 1])
                length = rng.randint(8, 22)
                for k in range(length):
                    bx = x + direction * k
                    by = ground - i - k // 2 - (k * k) // 40
                    if 0 <= bx < 960 and 0 <= by < H:
                        near[by, bx] = (*hex_rgb("#08060a"), 255)

    for x0 in (30, 150, 300, 640, 760, 905):
        tree(x0, rng.randint(110, 170), rng.uniform(-6, 6))
    # Cabin with a lit window.
    cx0, cy0 = 420, ground - 70
    near[cy0:ground, cx0:cx0 + 120] = (*hex_rgb("#110c0a"), 255)
    for y in range(cy0, ground, 6):
        near[y, cx0:cx0 + 120] = (*hex_rgb("#1d1512"), 255)
    for i in range(40):
        near[cy0 - i, cx0 + i:cx0 + 120 - i] = (*hex_rgb("#0b0807"), 255)
    near[cy0 + 18:cy0 + 38, cx0 + 24:cx0 + 44] = (*hex_rgb("#e8a040"), 255)
    near[cy0 + 27, cx0 + 24:cx0 + 44] = (*hex_rgb("#3a2010"), 255)
    near[cy0 + 18:cy0 + 38, cx0 + 33] = (*hex_rgb("#3a2010"), 255)
    near[cy0 + 30:ground, cx0 + 74:cx0 + 94] = (*hex_rgb("#070505"), 255)
    # Fence.
    for x in range(0, 960, 14):
        near[ground - 22:ground, x:x + 3] = (*hex_rgb("#1a1310"), 255)
        near[ground - 23, x + 1] = (*hex_rgb("#2a1f1a"), 255)
    near[ground - 16, :] = (*hex_rgb("#1a1310"), 255)
    near[ground - 8, :] = (*hex_rgb("#1a1310"), 255)
    near[:ground - 140] = 0
    Image.fromarray(near).save(os.path.join(STAGE_DIR, "layer2.png"))
    with open(os.path.join(STAGE_DIR, "stage.json"), "w") as f:
        json.dump({"id": "fallback", "displayName": "CAMP BLOOD", "groundY": H - ground,
                   "layers": [{"file": "layer0", "parallax": 0.1}, {"file": "layer1", "parallax": 0.35},
                              {"file": "layer2", "parallax": 1.0}]}, f, indent=1)


def main():
    os.makedirs(UI_DIR, exist_ok=True)
    fonts = [
        build_font("small", 1, ["#ffffff"], 1, 1),
        build_font("medium", 2, ["#ffffff", "#fff0b0", "#ffd060", "#ffa030"], 1, 1),
        build_font("large", 3, ["#fff8d0", "#ffe060", "#ffaa20", "#f06010", "#c01818", "#800810"], 2, 2),
        build_font("blood", 3, ["#ff8a90", "#e8323c", "#c01824", "#8a0a14", "#5a0008"], 2, 2),
    ]
    with open(os.path.join(UI_DIR, "fonts.json"), "w") as f:
        json.dump({"fonts": fonts}, f, indent=1)
    bar_frame()
    bar_fill()
    timer_box()
    skulls()
    select_cell("cell.png")
    select_cell("cell_locked.png", locked=True)
    cursor("cursor_p1.png", hex_rgb("#3c8cff"), "1P")
    cursor("cursor_p2.png", hex_rgb("#ff3c3c"), "2P")
    cursor("cursor_cpu.png", hex_rgb("#ff3c3c"), "CPU")
    sparks()
    misc()
    logo()
    vs_badge()
    fallback_stage()
    print("ui kit written")


if __name__ == "__main__":
    main()
