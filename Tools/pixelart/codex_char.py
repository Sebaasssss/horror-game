"""Builds a game character (atlas + character.json) from Codex strips drawn on the layout templates.

  Tools/.venv/bin/python Tools/pixelart/codex_char.py jason ghost

Per strip: find the green separators / baseline / centre tick, key out magenta and green, scale by the fixed template
ratio (so every strip shares one scale), quantise to the character palette, add a 1px outline and place the frame
with its pivot on the feet. Hitboxes come from the front-most pixels of each attack's active frame.
"""
import json
import math
import os
import sys

import numpy as np
from PIL import Image

from anim_spec import CHARACTERS, FULL, SETS, ALIASES
from templates import BASE_Y, CANVAS_H, CANVAS_W, HEAD_Y, cell_bounds

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = os.path.join(ROOT, "Tools", "art_src")
OUT = os.path.join(ROOT, "Assets", "Resources", "Characters")
PREVIEW = os.path.join(ROOT, "Tools", "out")

TARGET_HEIGHT = {"jason": 94, "ghost": 90}
FRAME_W, FRAME_H = 128, 156
PIVOT_X = 64
COLUMNS = 24
PALETTE_SIZE = 56
OUTLINE = (8, 5, 6, 255)

# Frame index where each attack is fully extended (the active frame).
ACTIVE = {"punch_1": 2, "punch_2": 2, "uppercut": 3, "slash": 3, "slash_side": 2, "kick": 2, "roundhouse": 3,
          "sweep": 2, "crouch_attack": 2, "air_attack": 2, "air_kick": 2, "special": 3, "headbutt": 2,
          "slash_low": 2, "lunge": 2}

TICKS = {"idle": 8, "walk_forward": 5, "walk_back": 6, "hop_forward": 6, "hop_back": 7, "crawl_forward": 8,
         "crawl_back": 9, "crouch": 3, "dizzy": 10, "victory": 10, "intro": 14}
LOOPS = {"idle", "walk_forward", "walk_back", "hop_forward", "hop_back", "crawl_forward", "crawl_back", "dizzy"}
HOLD_LAST = {"ko": [8, 8, 10, 12, 90], "headless": [10, 10, 12, 14, 90], "bisected": [10, 12, 12, 14, 90]}

GORE_PARTS = ["head", "torso", "arm_back", "arm_front", "leg_back", "leg_front", "guts", "spine"]
GORE_OFFSETS = [(8, 82), (2, 58), (-10, 62), (12, 60), (-6, 24), (7, 24), (0, 40), (0, 46)]
STUMPS = {"full": (0, 0), "one_arm": (-6, 70), "no_arms": (0, 70), "one_leg": (-5, 36), "no_legs": (-16, 14)}

VARIANT_FLAGS = {
    "full": dict(speedScale=1.0, jumpScale=1.0, damageScale=1.0, canBlock=True, canGrab=True, canJump=True,
                 canCrouch=True),
    "one_arm": dict(speedScale=0.95, jumpScale=0.95, damageScale=0.9, canBlock=True, canGrab=False, canJump=True,
                    canCrouch=True),
    "no_arms": dict(speedScale=0.95, jumpScale=0.9, damageScale=0.8, canBlock=False, canGrab=False, canJump=True,
                    canCrouch=True),
    "one_leg": dict(speedScale=0.6, jumpScale=0.7, damageScale=0.9, canBlock=True, canGrab=True, canJump=True,
                    canCrouch=True),
    "no_legs": dict(speedScale=0.45, jumpScale=0.0, damageScale=0.85, canBlock=False, canGrab=False, canJump=False,
                    canCrouch=False),
}


# ---------------------------------------------------------------- image helpers

def is_background(rgb):
    r, g, b = rgb[..., 0].astype(int), rgb[..., 1].astype(int), rgb[..., 2].astype(int)
    magenta = (r > 140) & (b > 140) & (g < 120) & (np.abs(r - b) < 100)
    fringe = (r > 110) & (b > 110) & ((r + b) // 2 - g > 95)
    green = (g > 170) & (r < 120) & (b < 120)
    return magenta | fringe | green


def despill(rgb):
    """Remove the magenta cast that anti-aliasing against the key colour leaves on edges."""
    out = rgb.astype(np.int32)
    r, g, b = out[..., 0], out[..., 1], out[..., 2]
    excess = np.clip(np.minimum(r, b) - g, 0, None)
    out[..., 0] = r - excess
    out[..., 2] = b - excess
    return out.astype(np.uint8)


def green_mask(rgb):
    r, g, b = rgb[..., 0].astype(int), rgb[..., 1].astype(int), rgb[..., 2].astype(int)
    return (g > 170) & (r < 120) & (b < 120)


def downscale(rgba, scale):
    h, w = rgba.shape[:2]
    nw, nh = max(1, int(round(w * scale))), max(1, int(round(h * scale)))
    rgb = rgba[..., :3].astype(np.float32)
    a = rgba[..., 3:4].astype(np.float32) / 255.0
    premul = np.concatenate([rgb * a, a * 255.0], axis=2)
    small = np.array(Image.fromarray(np.clip(premul, 0, 255).astype(np.uint8)).resize((nw, nh), Image.BOX))
    small = small.astype(np.float32)
    alpha = small[..., 3:4] / 255.0
    color = np.where(alpha > 0.01, small[..., :3] / np.maximum(alpha, 1e-3), 0)
    out = np.zeros((nh, nw, 4), dtype=np.uint8)
    out[..., :3] = np.clip(color, 0, 255).astype(np.uint8)
    out[..., 3] = np.where(alpha[..., 0] > 0.5, 255, 0)
    return out


def quantize(rgba, palette):
    q = np.array(Image.fromarray(rgba[..., :3]).quantize(palette=palette, dither=Image.Dither.NONE).convert("RGB"))
    out = rgba.copy()
    out[..., :3] = q
    out[rgba[..., 3] == 0] = 0
    return out


def outline(rgba):
    opaque = rgba[..., 3] > 0
    grown = opaque.copy()
    grown[1:] |= opaque[:-1]
    grown[:-1] |= opaque[1:]
    grown[:, 1:] |= opaque[:, :-1]
    grown[:, :-1] |= opaque[:, 1:]
    out = rgba.copy()
    out[grown & ~opaque] = OUTLINE
    return out


def despeckle(rgba, minimum=3):
    """Drop isolated opaque pixels left over from keying."""
    opaque = rgba[..., 3] > 0
    count = np.zeros(opaque.shape, dtype=np.int32)
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            if dx or dy:
                count += np.roll(np.roll(opaque, dy, 0), dx, 1)
    out = rgba.copy()
    out[opaque & (count < minimum)] = 0
    return out


def build_palette(images):
    pixels = []
    for rgba in images:
        sel = rgba[..., 3] > 0
        pixels.append(rgba[sel][:, :3])
    pixels = np.concatenate(pixels)
    if len(pixels) > 400000:
        pixels = pixels[np.random.default_rng(1).choice(len(pixels), 400000, replace=False)]
    side = int(math.ceil(math.sqrt(len(pixels))))
    pad = np.zeros((side * side, 3), dtype=np.uint8)
    pad[:len(pixels)] = pixels
    pad[len(pixels):] = pixels[0]
    return Image.fromarray(pad.reshape(side, side, 3)).quantize(colors=PALETTE_SIZE, method=Image.Quantize.MEDIANCUT,
                                                                dither=Image.Dither.NONE)


# ---------------------------------------------------------------- slicing

def find_near(profile, expected, radius):
    lo, hi = max(0, expected - radius), min(len(profile), expected + radius + 1)
    window = profile[lo:hi]
    if window.max() <= 0:
        return expected
    return lo + int(np.argmax(window))


def slice_strip(path, frames):
    """Returns [(cell_rgba_keyed, pivot_x_in_cell, baseline_y)] in source pixels."""
    img = np.array(Image.open(path).convert("RGB"))
    h, w = img.shape[:2]
    sx, sy = w / CANVAS_W, h / CANVAS_H
    green = green_mask(img)
    col_profile = green.sum(axis=0)
    bounds = []
    for i in range(frames):
        x0, x1 = cell_bounds(frames, i)
        x0, x1 = int(x0 * sx), int(x1 * sx)
        if i:
            x0 = find_near(col_profile, x0, int(20 * sx))
        if i < frames - 1:
            x1 = find_near(col_profile, x1, int(20 * sx))
        bounds.append((x0, x1))
    cells = []
    keyed_all = ~is_background(img)
    for x0, x1 in bounds:
        sub_green = green[:, x0:x1]
        row_profile = sub_green.sum(axis=1)
        baseline = find_near(row_profile, int(BASE_Y * sy), int(30 * sy))
        tick_zone = sub_green[baseline + int(6 * sy):baseline + int(34 * sy)]
        cols = np.nonzero(tick_zone.sum(axis=0) > 3)[0]
        pivot = float(cols.mean()) if len(cols) else (x1 - x0) / 2.0
        if abs(pivot - (x1 - x0) / 2.0) > 40 * sx:
            pivot = (x1 - x0) / 2.0
        rgba = np.zeros((h, x1 - x0, 4), dtype=np.uint8)
        rgba[..., :3] = despill(img[:, x0:x1])
        rgba[..., 3] = np.where(keyed_all[:, x0:x1], 255, 0)
        # Trim a few pixels at the cell edges where separator lines bleed.
        rgba[:, :int(4 * sx), 3] = 0
        rgba[:, -int(4 * sx):, 3] = 0
        cells.append((rgba, pivot, baseline))
    return cells, sy


# Strips drawn standing upright: their body height is a reliable scale reference.
UPRIGHT = {"idle", "walk_forward", "walk_back", "hop_forward", "hop_back", "dizzy", "intro"}


# Strips whose FIRST frame is the standing guard (attacks start from stance).
FRAME0_UPRIGHT = {"punch_1", "punch_2", "slash", "slash_side", "kick", "roundhouse", "block", "hurt", "grab_throw",
                  "victory", "ko", "headbutt"}


def mask_height(cell):
    """Height of the largest bright low-saturation blob (the mask), measured on a 3x downsampled grid."""
    from collections import deque
    c = cell[::3, ::3]
    rgb = c[..., :3].astype(int)
    lo, hi = rgb.min(axis=2), rgb.max(axis=2)
    m = (c[..., 3] > 0) & (lo > 130) & (hi - lo < 75)
    h, w = m.shape
    seen = np.zeros_like(m)
    best = 0
    for y0, x0 in zip(*np.nonzero(m)):
        if seen[y0, x0]:
            continue
        queue, ys = deque([(y0, x0)]), []
        seen[y0, x0] = True
        while queue:
            y, x = queue.popleft()
            ys.append(y)
            for ny, nx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
                if 0 <= ny < h and 0 <= nx < w and m[ny, nx] and not seen[ny, nx]:
                    seen[ny, nx] = True
                    queue.append((ny, nx))
        if len(ys) > 12:
            best = max(best, (max(ys) - min(ys) + 1) * 3)
    return best


NO_MASK = {"headless", "bisected", "gore_parts", "thrown", "knockdown", "getup"}


def strip_mask_height(cells):
    heights = [mask_height(c) for c, _, _ in cells]
    heights = [h for h in heights if h > 0]
    return float(np.median(heights)) if len(heights) >= max(1, len(cells) // 2) else 0.0


def body_height(cell, baseline, min_row=14):
    """Baseline to the first row at least min_row pixels wide (ignores blood drops and thin blade tips)."""
    rows = np.nonzero((cell[..., 3] > 0).sum(axis=1) >= min_row)[0]
    return float(baseline - rows[0]) if len(rows) else 0.0


def strip_height(cells):
    heights = [body_height(c, b) for c, _, b in cells]
    heights = [h for h in heights if h > 0]
    return float(np.median(heights)) if heights else 0.0


def to_frame(cell, pivot, baseline, scale, palette, base_scale=None):
    """scale sizes the drawing; base_scale (the template scale) fixes where the pivot sits in the frame."""
    base_scale = base_scale or scale
    small = downscale(cell, scale)
    small = despeckle(quantize(small, palette))
    small = outline(np.pad(small, ((1, 1), (1, 1), (0, 0))))
    frame = np.zeros((FRAME_H, FRAME_W, 4), dtype=np.uint8)
    pivot_y = FRAME_H - pivot_bottom(base_scale)
    ox = int(round(PIVOT_X - (pivot * scale + 1)))
    oy = int(round(pivot_y - (baseline * scale + 1)))
    h, w = small.shape[:2]
    for y in range(h):
        fy = oy + y
        if not 0 <= fy < FRAME_H:
            continue
        x0, x1 = max(ox, 0), min(ox + w, FRAME_W)
        if x1 <= x0:
            continue
        row = small[y, x0 - ox:x1 - ox]
        m = row[..., 3] > 0
        frame[fy, x0:x1][m] = row[m]
    return frame


def pivot_bottom(scale):
    return int(round((CANVAS_H - BASE_Y) * scale))


# ---------------------------------------------------------------- measurements (pivot space, y up)

def opaque_bbox(frame):
    ys, xs = np.nonzero(frame[..., 3] > 0)
    if len(xs) == 0:
        return None
    return xs.min(), xs.max(), ys.min(), ys.max()


def to_pivot(x, y, scale):
    return x - PIVOT_X, (FRAME_H - pivot_bottom(scale)) - y


def front_box(frame, scale, depth=26, max_h=50):
    opaque = frame[..., 3] > 0
    xs = np.nonzero(opaque.any(axis=0))[0]
    if len(xs) == 0:
        return {"x": 10, "y": 30, "w": 20, "h": 30}
    max_x = xs[-1]
    region = opaque[:, max(max_x - depth, 0):max_x + 1]
    ys = np.nonzero(region.any(axis=1))[0]
    top, bottom = ys[0], ys[-1]
    if bottom - top > max_h:
        mid = (top + bottom) // 2
        top, bottom = mid - max_h // 2, mid + max_h // 2
    left, base = to_pivot(max_x - depth, bottom, scale)
    return {"x": int(max(left, -4)), "y": int(max(base, 0)), "w": int(depth + 3), "h": int(bottom - top + 1)}


def hand_point(frame, scale):
    opaque = frame[..., 3] > 0
    xs = np.nonzero(opaque.any(axis=0))[0]
    if len(xs) == 0:
        return 30.0, 60.0
    max_x = xs[-1]
    ys = np.nonzero(opaque[:, max_x])[0]
    x, y = to_pivot(max_x - 3, ys.mean(), scale)
    return float(x), float(y)


def hurt_rect(frame, scale, shrink=0.62):
    box = opaque_bbox(frame)
    if box is None:
        return {"x": -12, "y": 0, "w": 24, "h": 80}
    x0, x1, y0, y1 = box
    _, top = to_pivot(0, y0, scale)
    width = max(18, int((x1 - x0) * shrink))
    width = min(width, 34)
    return {"x": -width // 2, "y": 0, "w": width, "h": int(max(20, top))}


# ---------------------------------------------------------------- moves

def phases(anim, frames):
    a = min(ACTIVE.get(anim, frames // 2), frames - 1)
    return list(range(0, a)) or [0], [a], list(range(a + 1, frames))


def move(mid, inp, state, anim, frames, startup, active, recovery, damage, **extra):
    s, a, r = phases(anim, frames)
    entry = {"id": mid, "input": inp, "state": state, "anim": anim, "startupFrames": s, "activeFrames": a,
             "recoveryFrames": r, "startup": startup, "active": active, "recovery": recovery, "damage": damage,
             "hitstun": extra.pop("hitstun", 16 + damage // 10), "blockstun": extra.pop("blockstun", 10 + damage // 20),
             "level": extra.pop("level", "mid"), "pushback": extra.pop("pushback", 6.0),
             "knockdown": extra.pop("knockdown", False), "launcher": extra.pop("launcher", False),
             "lunge": extra.pop("lunge", 0.0), "sfxWhiff": extra.pop("whiff", "whoosh_light"),
             "sfxHit": extra.pop("hit", "hit_light"), "blood": extra.pop("blood", 6),
             "shake": extra.pop("shake", 0.1), "hitstop": extra.pop("hitstop", 7),
             "dismember": extra.pop("dismember", 0.0), "chains": extra.pop("chains", []),
             "name": extra.pop("name", "")}
    assert not extra, extra
    return entry


def chain(*pairs):
    return [{"input": i, "move": m} for i, m in pairs]


def base_moves(char_id, anim_frames, blade_hit):
    """Full-body move list. anim_frames maps animation name -> frame count (after aliases)."""
    slow = 2 if char_id == "jason" else 0
    dmg = 1.15 if char_id == "jason" else 1.0
    f = anim_frames
    d = lambda v: int(round(v * dmg))
    return [
        move("light", "light", "stand", "punch_1", f["punch_1"], 5 + slow // 2, 3, 8, d(40),
             chains=chain(("light", "light2"), ("kick", "kick"), ("heavy", "heavy")), hit="hit_light", blood=4),
        move("light2", "light", "stand", "punch_2", f["punch_2"], 5, 3, 10, d(45),
             chains=chain(("light", "uppercut"), ("heavy", "slash_side"), ("kick", "roundhouse")),
             hit="hit_light", blood=5),
        move("uppercut", "light", "stand", "uppercut", f["uppercut"], 7, 4, 16, d(80), launcher=True,
             dismember=0.15, hit="hit_kick", whiff="whoosh_heavy", shake=0.5, hitstop=10, blood=10),
        move("heavy", "heavy", "stand", "slash", f["slash"], 12 + slow, 5, 18, d(120), level="overhead",
             chains=chain(("heavy", "slash_side")), dismember=0.1, hit=blade_hit + "_heavy",
             whiff="whoosh_heavy", shake=0.7, hitstop=11, blood=16, pushback=8.0),
        move("slash_side", "heavy", "stand", "slash_side", f["slash_side"], 9 + slow, 4, 16, d(100),
             dismember=0.22, hit=blade_hit + "_heavy", whiff="whoosh_heavy", shake=0.6, hitstop=10, blood=14),
        move("kick", "kick", "stand", "kick", f["kick"], 8 + slow, 4, 13, d(70), pushback=12.0,
             chains=chain(("kick", "roundhouse")), hit="hit_kick", whiff="whoosh_light", shake=0.3, blood=4),
        move("roundhouse", "back+kick", "stand", "roundhouse", f["roundhouse"], 11 + slow, 4, 18, d(95),
             knockdown=True, dismember=0.12, hit="hit_kick", whiff="whoosh_heavy", shake=0.6, hitstop=10, blood=8),
        move("crouch_light", "light", "crouch", "crouch_attack", f["crouch_attack"], 6, 3, 12, d(45), level="low",
             chains=chain(("kick", "sweep")), hit=blade_hit + "_light", blood=6),
        move("sweep", "kick", "crouch", "sweep", f["sweep"], 9 + slow, 4, 18, d(65), level="low", knockdown=True,
             hit="hit_kick", whiff="whoosh_light", shake=0.3),
        move("uppercut_c", "heavy", "crouch", "uppercut", f["uppercut"], 8, 4, 18, d(85), launcher=True,
             dismember=0.1, hit="hit_kick", whiff="whoosh_heavy", shake=0.5, hitstop=10, blood=10),
        move("air", "light", "air", "air_attack", f["air_attack"], 5, 16, 0, d(75), level="overhead",
             hit=blade_hit + "_heavy", whiff="whoosh_heavy", blood=10, shake=0.3),
        move("air_kick", "kick", "air", "air_kick", f["air_kick"], 5, 16, 0, d(65), level="overhead",
             hit="hit_kick", blood=4, shake=0.3),
        move("grab", "grab", "stand", "grab_throw", f["grab_throw"], 6, 3, 22, d(140), level="throw",
             knockdown=True, hit="grab", blood=0, hitstop=0),
        move("special", "forward+heavy", "stand", "special", f["special"], 12 + slow, 8, 20, d(110),
             lunge=6.0 if char_id == "ghost" else 4.5, knockdown=True, dismember=0.15, hit=blade_hit + "_heavy",
             whiff="whoosh_heavy", shake=0.7, hitstop=12, blood=16,
             name="LUNGE STAB" if char_id == "ghost" else "MACHETE RUSH"),
    ]


def variant_moves(char_id, variant, anims, blade_hit):
    counts = {name: len(a["sprites"]) for name, a in anims.items()}
    resolved = lambda name: name if name in counts else None
    moves = base_moves(char_id, {k: counts.get(k, 4) for k in list(FULL) + ["headbutt", "slash_low", "lunge"]},
                       blade_hit)
    flags = VARIANT_FLAGS[variant]
    out = []
    for m in moves:
        if m["id"] == "grab" and not flags["canGrab"]:
            continue
        if m["state"] == "crouch" and not flags["canCrouch"]:
            continue
        if m["state"] == "air" and not flags["canJump"]:
            continue
        if not resolved(m["anim"]):
            continue
        m = dict(m)
        m["startupFrames"], m["activeFrames"], m["recoveryFrames"] = phases(m["anim"], counts[m["anim"]])
        out.append(m)
    if variant == "no_legs":
        for m in out:
            m["level"] = "low" if m["anim"] == "slash_low" else "mid"
    return out


# ---------------------------------------------------------------- build

def main(char_ids):
    os.makedirs(PREVIEW, exist_ok=True)
    for char_id in char_ids:
        build(char_id)


def build(char_id):
    info = CHARACTERS[char_id]
    src = os.path.join(SRC, char_id)
    out_dir = os.path.join(OUT, char_id)
    os.makedirs(out_dir, exist_ok=True)
    blade_hit = "stab" if char_id == "ghost" else "slash"

    # Fixed template scale: standing height between the guide lines becomes TARGET_HEIGHT game pixels.
    strips = {}
    for variant, spec in SETS.items():
        for anim, (frames, _) in spec.items():
            path = os.path.join(src, variant, anim + ".png")
            if os.path.exists(path):
                strips[(variant, anim)] = (path, frames)
    if ("full", "idle") not in strips:
        raise SystemExit(f"{char_id}: full/idle.png missing")

    sliced = {}
    for key, (path, frames) in strips.items():
        try:
            sliced[key] = slice_strip(path, frames)
        except Exception as error:  # noqa: BLE001 - report and keep building
            print(f"  skip {key}: {error}")
    _, sy = sliced[("full", "idle")]
    scale = TARGET_HEIGHT[char_id] / ((BASE_Y - HEAD_Y) * sy)
    palette = build_palette([c for c, _, _ in sliced[("full", "idle")][0]] +
                            [c for c, _, _ in sliced.get(("full", "slash"), ([], 0))[0]])

    cells, sprites = [], []

    def add_frame(frame, name, pivot=(PIVOT_X, None)):
        cells.append(frame)
        sprites.append({"name": name, "cell": len(cells) - 1, "pivotX": pivot[0],
                        "pivotY": pivot[1] if pivot[1] is not None else pivot_bottom(scale)})
        return len(sprites) - 1

    # Codex does not always respect the head guide: normalise upright strips to full/idle and let every other
    # strip of a body state inherit that state's idle correction.
    reference = strip_height(sliced[("full", "idle")][0])
    mask_reference = strip_mask_height(sliced[("full", "idle")][0])
    corrections = {}
    for variant in SETS:
        idle = sliced.get((variant, "idle"))
        base = 1.0
        if idle and variant in ("full", "one_arm", "one_leg", "no_arms"):
            height = strip_height(idle[0])
            base = float(np.clip(reference / height, 0.75, 1.25)) if height else 1.0
        for key, (cell_list, _) in sliced.items():
            if key[0] != variant:
                continue
            corr = base
            if variant == "no_legs":
                pass
            elif key[1] in UPRIGHT:
                height = strip_height(cell_list)
                corr = float(np.clip(reference / height, 0.75, 1.25)) if height else base
            elif key[1] in FRAME0_UPRIGHT:
                cell, _, baseline = cell_list[0]
                height = body_height(cell, baseline)
                if height and 0.8 <= reference / height <= 1.25:
                    corr = reference / height
            elif key[1] not in NO_MASK and mask_reference:
                height = strip_mask_height(cell_list)
                if height:
                    corr = float(np.clip(mask_reference / height, 0.85, 1.18))
            corrections[key] = corr
    odd = {f"{k[0]}/{k[1]}": round(v, 2) for k, v in corrections.items() if abs(v - 1) > 0.04}
    if odd:
        print("  scale corrections:", odd)

    variants = []
    for variant in SETS:
        anims = {}
        frames_of = {}
        for (v, anim), (cell_list, _) in sliced.items():
            if v != variant:
                continue
            indices, ax, ay, pix = [], [], [], []
            strip_scale = scale * corrections[(v, anim)]
            for i, (cell, pivot, baseline) in enumerate(cell_list):
                frame = to_frame(cell, pivot, baseline, strip_scale, palette, scale)
                indices.append(add_frame(frame, f"{variant}_{anim}_{i}"))
                hx, hy = hand_point(frame, scale)
                ax.append(round(hx, 1))
                ay.append(round(hy, 1))
                pix.append(frame)
            frames_of[anim] = pix
            n = len(indices)
            ticks = HOLD_LAST.get(anim, [TICKS.get(anim, 6)] * n)[:n]
            anims[anim] = {"name": anim, "sprites": indices, "ticks": ticks, "loop": anim in LOOPS,
                           "anchorX": ax, "anchorY": ay}
        if "idle" not in anims:
            continue
        # Fill the names the game expects through the variant's aliases, then fall back to the full body.
        for name, target in ALIASES.get(variant, {}).items():
            if name not in anims and target in anims:
                anims[name] = dict(anims[target], name=name)
        full_anims = variants[0]["_anims"] if variants else {}
        for name, entry in full_anims.items():
            if name not in anims and variant != "full" and name in ("thrown", "headless", "bisected", "victory",
                                                                     "intro", "grab_throw"):
                anims[name] = dict(entry)
        stand = hurt_rect(frames_of["idle"][0], scale)
        crouch_src = frames_of.get("crouch", frames_of["idle"])
        crouch = hurt_rect(crouch_src[min(2, len(crouch_src) - 1)], scale)
        air_src = frames_of.get("jump_up", frames_of["idle"])
        air = hurt_rect(air_src[min(2, len(air_src) - 1)], scale)
        air["y"] = 8
        flags = VARIANT_FLAGS[variant]
        stump = STUMPS[variant]
        k = TARGET_HEIGHT[char_id] / 92.0
        data = {"name": variant, "animations": list(anims.values()),
                "moves": variant_moves(char_id, variant, anims, blade_hit),
                "hurtStand": stand, "hurtCrouch": crouch, "hurtAir": air,
                "stumpX": round(stump[0] * k, 1), "stumpY": round(stump[1] * k, 1), **flags}
        # Hitboxes from the active frame of each move.
        for m in data["moves"]:
            if m["level"] == "throw":
                m["hitbox"] = {"x": 6, "y": 20, "w": 34, "h": 56}
                continue
            pix = frames_of.get(m["anim"]) or frames_of.get(ALIASES.get(variant, {}).get(m["anim"], ""), None)
            if pix is None and variants:
                pix = variants[0]["_frames"].get(m["anim"])
            active = m["activeFrames"][0]
            m["hitbox"] = front_box(pix[min(active, len(pix) - 1)], scale) if pix else {"x": 8, "y": 30, "w": 26,
                                                                                          "h": 30}
        data["_anims"] = anims
        data["_frames"] = frames_of
        variants.append(data)
        print(f"  {char_id}/{variant}: {len(anims)} animations, {len(data['moves'])} moves")

    # Gore parts (centred pivots) from full/gore_parts.png.
    gore = []
    gore_path = os.path.join(src, "full", "gore_parts.png")
    if os.path.exists(gore_path):
        cells_g, _ = slice_strip(gore_path, 8)
        k = TARGET_HEIGHT[char_id] / 92.0
        # Gore parts are drawn larger than the body; size them so the severed head is head-sized (~17 px).
        head = cells_g[0][0]
        ys, xs = np.nonzero(head[..., 3] > 0)
        head_px = max(ys.max() - ys.min(), xs.max() - xs.min()) if len(xs) else 1
        gore_scale = min(scale, 17.0 * k / head_px)
        for part, (cell, _, _), (ox, oy) in zip(GORE_PARTS, cells_g, GORE_OFFSETS):
            small = outline(np.pad(despeckle(quantize(downscale(cell, gore_scale), palette)), ((1, 1), (1, 1), (0, 0))))
            box = opaque_bbox(small)
            if box is None:
                continue
            x0, x1, y0, y1 = box
            piece = small[y0:y1 + 1, x0:x1 + 1]
            frame = np.zeros((FRAME_H, FRAME_W, 4), dtype=np.uint8)
            ph, pw = piece.shape[:2]
            top, left = (FRAME_H - ph) // 2, (FRAME_W - pw) // 2
            frame[top:top + ph, left:left + pw] = piece[:FRAME_H - top, :FRAME_W - left]
            index = add_frame(frame, "gore_" + part, (FRAME_W / 2, FRAME_H / 2))
            gore.append({"part": part, "sprite": index, "x": round(ox * k, 1), "y": round(oy * k, 1)})

    rows = math.ceil(len(cells) / COLUMNS)
    atlas = np.zeros((rows * FRAME_H, COLUMNS * FRAME_W, 4), dtype=np.uint8)
    for i, frame in enumerate(cells):
        r, c = divmod(i, COLUMNS)
        atlas[r * FRAME_H:(r + 1) * FRAME_H, c * FRAME_W:(c + 1) * FRAME_W] = frame
    if atlas.shape[0] > 4096 or atlas.shape[1] > 4096:
        print(f"  warning: atlas {atlas.shape[1]}x{atlas.shape[0]} exceeds 4096")
    Image.fromarray(atlas).save(os.path.join(out_dir, "atlas.png"))
    preview = Image.new("RGBA", (atlas.shape[1], atlas.shape[0]), (40, 30, 45, 255))
    preview.alpha_composite(Image.fromarray(atlas))
    preview.save(os.path.join(PREVIEW, f"{char_id}_atlas.png"))

    portrait_path = os.path.join(src, "portrait.png")
    if os.path.exists(portrait_path):
        src_img = np.array(Image.open(portrait_path).convert("RGB"))
        rgba = np.zeros(src_img.shape[:2] + (4,), dtype=np.uint8)
        rgba[..., :3] = src_img
        rgba[..., 3] = np.where(is_background(src_img), 0, 255)
        for size, name in ((96, "portrait"), (40, "thumb")):
            Image.fromarray(portrait_card(rgba, palette, size)).save(os.path.join(out_dir, name + ".png"))

    full = variants[0]
    for v in variants:
        v.pop("_anims")
        v.pop("_frames")
    data = {
        "id": char_id, "displayName": info["name"], "subtitle": info["subtitle"], "author": "Codex (image gen)",
        "frameW": FRAME_W, "frameH": FRAME_H, "columns": COLUMNS, "sprites": sprites,
        "animations": full["animations"], "moves": full["moves"], "gore": gore, "variants": variants,
        "stats": {"health": 1100 if char_id == "jason" else 1000,
                  "walkSpeed": 1.2 if char_id == "jason" else 1.55, "backSpeed": 1.0 if char_id == "jason" else 1.3,
                  "jumpVelocity": 6.3 if char_id == "jason" else 6.7, "jumpForward": 1.9 if char_id == "jason" else 2.3,
                  "gravity": 0.36, "pushWidth": 28 if char_id == "jason" else 24, "weight": 1.0},
        "hurtStand": full["hurtStand"], "hurtCrouch": full["hurtCrouch"], "hurtAir": full["hurtAir"],
        "bloodColor": "#9a0c14", "accentColor": "#9fc26a" if char_id == "jason" else "#9fb6ff",
        "neckX": round(4 * TARGET_HEIGHT[char_id] / 92.0, 1), "neckY": round(78 * TARGET_HEIGHT[char_id] / 92.0, 1),
    }
    with open(os.path.join(out_dir, "character.json"), "w") as f:
        json.dump(data, f, separators=(",", ":"))
    print(f"{char_id}: {len(cells)} frames, atlas {atlas.shape[1]}x{atlas.shape[0]}, scale {scale:.3f}")


def portrait_card(rgba, palette, size):
    ys, xs = np.nonzero(rgba[..., 3] > 0)
    crop = rgba[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    scale = size / max(crop.shape[:2])
    small = quantize(downscale(crop, scale), palette)
    card = np.zeros((size, size, 4), dtype=np.uint8)
    sh, sw = small.shape[:2]
    card[size - sh:, (size - sw) // 2:(size - sw) // 2 + sw] = small
    return outline(card)


if __name__ == "__main__":
    main(sys.argv[1:] or list(CHARACTERS))
