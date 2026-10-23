"""Procedural pixel-art Ghost (hooded slasher in a tattered black robe, white mask, hunting knife).

Run:  Tools/.venv/bin/python Tools/pixelart/ghost.py
Writes Assets/Resources/Characters/ghost/{atlas.png, portrait.png, character.json} and preview sheets in Tools/out/.
"""
import json
import math
import os
import random
from dataclasses import dataclass, field, replace

import numpy as np
from PIL import Image

from pixel import Canvas, Ramp, hex_rgb, ik2, lerp, lerp_pt, polar, save_rgba

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT_DIR = os.path.join(ROOT, "Assets", "Resources", "Characters", "ghost")
PREVIEW_DIR = os.path.join(ROOT, "Tools", "out")

FRAME_W, FRAME_H = 128, 112
PIVOT_X, PIVOT_Y = 64, 104  # image coordinates (top-left origin)
COLUMNS = 10

ROBE = Ramp(["#060608", "#0d0e13", "#15171f", "#20232e", "#2f3342", "#464c60"],
            [-0.45, -0.1, 0.22, 0.52, 0.8])
ROBE_BACK = Ramp(["#040405", "#09090d", "#101117", "#181a22", "#232633"], [-0.4, -0.05, 0.3, 0.62])
GLOVE = Ramp(["#09090c", "#141419", "#202028", "#33333f"], [-0.3, 0.15, 0.55])
BOOT = Ramp(["#08080a", "#121216", "#1e1e25", "#30303a"], [-0.3, 0.15, 0.6])
TROUSER = Ramp(["#0a0a0f", "#13141b", "#1d1f29"], [-0.2, 0.35])
MASK = Ramp(["#5f5a52", "#8f897d", "#bdb6a7", "#ddd8cb", "#f4f1e8", "#ffffff"],
            [-0.35, -0.02, 0.28, 0.55, 0.86])
HOOD_IN = Ramp(["#040406", "#08090d"], [0.4])
BLADE = Ramp(["#4f5862", "#87919d", "#bcc5cf", "#e9eef3", "#ffffff"], [-0.3, 0.1, 0.45, 0.8])
HANDLE = Ramp(["#1d130c", "#352419", "#523825"], [-0.1, 0.4])
BLOOD = Ramp(["#360005", "#5e0710", "#8c0f19", "#bb1f28", "#e0434a"], [-0.3, 0.05, 0.4, 0.75])
FEATURE = "#050506"
BLOOD_HI = "#ff6a6f"


@dataclass
class Pose:
    pelvis: tuple = (0.0, 41.0)
    lean: float = 12.0          # torso lean, + = forward
    head: float = 6.0           # head tilt relative to torso, + = chin down/forward
    ff: tuple = (10.0, 3.0)     # front ankle target
    bf: tuple = (-10.0, 3.0)    # back ankle target
    fh: tuple = (18.0, 54.0)    # front hand target (holds the knife)
    bh: tuple = (13.0, 47.0)    # back hand target
    fe: float = -1.0            # front elbow bend sign
    be: float = -1.0            # back elbow bend sign
    knife: float = 115.0        # knife angle (0 down, 90 forward, 180 up); None follows forearm
    grip_open: bool = False
    hem: float = 0.0            # robe sway, + trails backwards
    hood_tail: float = 0.0
    sleeve_flutter: float = 0.0
    phase: float = 0.0          # varies tattered hems between frames
    blood_neck: int = 0         # headless spurt strength


THIGH, SHIN = 21.0, 21.0
UPPER_ARM, FOREARM = 13.0, 12.5
SPINE = 22.0


def rot(v, deg):
    a = math.radians(deg)
    return (v[0] * math.cos(a) - v[1] * math.sin(a), v[0] * math.sin(a) + v[1] * math.cos(a))


def add(a, b, s=1.0):
    return (a[0] + b[0] * s, a[1] + b[1] * s)


class Rig:
    def __init__(self, pose: Pose):
        self.p = pose
        lean = math.radians(pose.lean)
        self.up = (math.sin(lean), math.cos(lean))
        self.fwd = (math.cos(lean), -math.sin(lean))
        self.pelvis = pose.pelvis
        self.chest = add(self.pelvis, self.up, SPINE)
        self.neck = add(self.chest, self.up, 3.0)
        head_ang = pose.lean + pose.head
        self.head_up = (math.sin(math.radians(head_ang)), math.cos(math.radians(head_ang)))
        self.head_fwd = (math.cos(math.radians(head_ang)), -math.sin(math.radians(head_ang)))
        self.head_c = add(self.neck, self.head_up, 7.5)
        self.head_angle = -head_ang  # ellipse angle in degrees (ccw)

        self.f_sh = add(add(self.chest, self.up, -2.5), self.fwd, 2.0)
        self.b_sh = add(add(self.chest, self.up, -1.5), self.fwd, -2.5)
        self.f_hip = add(self.pelvis, self.fwd, 2.5)
        self.b_hip = add(self.pelvis, self.fwd, -2.5)
        self.f_knee, self.f_ankle = ik2(self.f_hip, pose.ff, THIGH, SHIN, +1)
        self.b_knee, self.b_ankle = ik2(self.b_hip, pose.bf, THIGH, SHIN, +1)
        self.f_elbow, self.f_wrist = ik2(self.f_sh, pose.fh, UPPER_ARM, FOREARM, pose.fe)
        self.b_elbow, self.b_wrist = ik2(self.b_sh, pose.bh, UPPER_ARM, FOREARM, pose.be)

    @staticmethod
    def angle_of(a, b):
        """Polar angle (0 down, 90 forward) of the segment a->b."""
        return math.degrees(math.atan2(b[0] - a[0], -(b[1] - a[1])))


# ---------------------------------------------------------------- drawing helpers

def tattered(canvas, a, b, r0, r1, ramp, teeth, depth, phase, part=None, fold=0.25, bias=0.0, clip=None):
    """Tapered cloth panel from a to b whose far end is cut into ragged teeth."""
    ax, ay = a
    bx, by = b
    dx, dy = bx - ax, by - ay
    length = max(math.hypot(dx, dy), 1e-3)
    ux, uy = dx / length, dy / length
    px_, py_ = -uy, ux
    rel_x, rel_y = canvas.wx - ax, canvas.wy - ay
    along = rel_x * ux + rel_y * uy
    across = rel_x * px_ + rel_y * py_
    t = along / length
    radius = r0 + (r1 - r0) * np.clip(t, 0, 1)
    tooth = np.abs(((across / max(r1, 1e-3)) * teeth + phase) % 2.0 - 1.0)
    cut = length - depth * tooth
    body = (along >= 0) & (along <= cut) & (np.abs(across) <= radius)
    cap = (rel_x * rel_x + rel_y * rel_y) <= r0 * r0
    mask = body | cap
    if clip is not None:
        mask &= clip
    n = np.clip(across / np.maximum(radius, 1e-3), -1, 1)
    nz = np.sqrt(np.clip(1 - n * n, 0, 1))
    shade = n * (px_ * -0.55 + py_ * 0.62) + nz * 0.56 + bias
    shade = shade + fold * np.sin(across * 1.15 + phase * 2.0) - 0.18 * np.clip(t - 0.75, 0, 1) * 4
    return canvas.paint(mask, shade, ramp, part)


def draw_knife(canvas, hand, angle):
    handle_end = polar(hand, 3.5, angle)
    back = polar(hand, -1.5, angle)
    canvas.capsule(back, handle_end, 1.2, 1.2, HANDLE)
    guard_a = polar(handle_end, 1.6, angle + 90)
    guard_b = polar(handle_end, 1.6, angle - 90)
    canvas.capsule(guard_a, guard_b, 0.7, 0.7, BLADE, shade_bias=-0.3)
    tip = polar(handle_end, 12.5, angle)
    blade = canvas.capsule(handle_end, tip, 1.75, 0.35, BLADE, shade_bias=0.2)
    # Bright edge line along the blade.
    edge_a = polar(polar(handle_end, 1.5, angle), 0.6, angle + 90)
    edge_b = polar(tip, 0.4, angle + 90)
    canvas.line(edge_a, edge_b, "#ffffff", part=blade, width=0.6)
    return tip


def draw_arm(canvas, rig, front, pose):
    sh = rig.f_sh if front else rig.b_sh
    el = rig.f_elbow if front else rig.b_elbow
    wr = rig.f_wrist if front else rig.b_wrist
    ramp = ROBE if front else ROBE_BACK
    bias = 0.14 if front else -0.15
    part = canvas.new_part()
    canvas.capsule(sh, el, 4.0, 3.7, ramp, part=part, shade_bias=bias)
    fore_dir = (wr[0] - el[0], wr[1] - el[1])
    length = math.hypot(*fore_dir)
    cuff = add(el, fore_dir, (length + 1.5) / max(length, 1e-3))
    tattered(canvas, el, cuff, 3.7, 5.4 + pose.sleeve_flutter, ramp, teeth=2.2, depth=2.8,
             phase=pose.phase + (0.0 if front else 0.7), part=part, fold=0.18, bias=bias)
    angle = Rig.angle_of(el, wr)
    hand = polar(wr, 2.2, angle)
    if front and pose.grip_open:
        canvas.ellipse(hand, 3.0, 2.2, -(angle - 90), GLOVE, shade_bias=bias)
        for k in (-1.4, 0.0, 1.4):
            finger_base = polar(hand, 2.2, angle)
            canvas.capsule(polar(finger_base, k, angle + 90), polar(polar(finger_base, k, angle + 90), 2.0, angle + 25),
                           0.7, 0.6, GLOVE, shade_bias=bias)
    else:
        canvas.ellipse(hand, 2.6, 2.4, 0, GLOVE, shade_bias=bias)
    tip = None
    if front and pose.knife is not None:
        tip = draw_knife(canvas, hand, pose.knife)
    return hand, tip


def draw_leg(canvas, rig, front, pose):
    hip = rig.f_hip if front else rig.b_hip
    knee = rig.f_knee if front else rig.b_knee
    ankle = rig.f_ankle if front else rig.b_ankle
    bias = 0.0 if front else -0.18
    canvas.capsule(knee, ankle, 3.2, 2.8, TROUSER, shade_bias=bias)
    shin_angle = Rig.angle_of(knee, ankle)
    foot_dir = shin_angle + 90.0
    toe = polar(ankle, 5.5, foot_dir)
    heel = polar(ankle, -1.5, foot_dir)
    canvas.capsule(heel, toe, 2.9, 2.4, BOOT, shade_bias=bias)
    sole_a = polar(heel, 2.4, foot_dir - 90)
    sole_b = polar(toe, 2.2, foot_dir - 90)
    canvas.line(sole_a, sole_b, "#2b2b33", width=0.8)
    # Robe panel that follows the thigh and drapes past the knee.
    shin_vec = (ankle[0] - knee[0], ankle[1] - knee[1])
    hem_end = add(knee, shin_vec, 0.78 + 0.04 * math.sin(pose.phase * 3 + (0 if front else 1.4)))
    ramp = ROBE if front else ROBE_BACK
    part = canvas.new_part()
    canvas.capsule(hip, knee, 6.0, 6.0, ramp, part=part, shade_bias=bias)
    tattered(canvas, knee, hem_end, 6.2, 8.2, ramp, teeth=2.6, depth=3.6,
             phase=pose.phase + (0.3 if front else 1.1), part=part, fold=0.3, bias=bias)
    return toe


def draw_body(canvas, rig, pose):
    part = canvas.new_part()
    # Skirt bridging both legs so the robe reads as one garment.
    waist_f = add(rig.pelvis, rig.fwd, 7.5)
    waist_b = add(rig.pelvis, rig.fwd, -8.0)
    down = (0.0, -1.0)
    f_hem = lerp_pt(add(rig.f_hip, down, 34.0), lerp_pt(rig.f_knee, rig.f_ankle, 0.75), 0.6)
    b_hem = lerp_pt(add(rig.b_hip, down, 34.0), lerp_pt(rig.b_knee, rig.b_ankle, 0.75), 0.6)
    b_hem = (b_hem[0] - pose.hem, b_hem[1] + abs(pose.hem) * 0.3)
    pts = [waist_b, waist_f, add(f_hem, rig.fwd, 3.0)]
    teeth = 7
    for i in range(1, teeth):
        t = i / teeth
        base = lerp_pt(add(f_hem, rig.fwd, 3.0), add(b_hem, rig.fwd, -3.0), t)
        jag = 3.2 if i % 2 else 0.0
        jag += 1.2 * math.sin(pose.phase * 2.3 + i)
        pts.append((base[0], base[1] + jag))
    pts.append(add(b_hem, rig.fwd, -3.0))
    mask = canvas.polygon_mask(pts)
    across = (canvas.wx - rig.pelvis[0]) * rig.fwd[0] + (canvas.wy - rig.pelvis[1]) * rig.fwd[1]
    shade = -0.15 + across * 0.035 + 0.28 * np.sin(across * 0.9 + pose.phase)
    canvas.paint(mask, shade, ROBE, part)
    # Torso.
    canvas.capsule(rig.pelvis, rig.chest, 7.8, 8.6, ROBE, part=part, shade_bias=0.05)
    # Shoulder mantle.
    canvas.capsule(rig.b_sh, rig.f_sh, 4.8, 4.8, ROBE, part=part, shade_bias=0.1)
    # Rope belt suggestion: a darker band.
    belt_a = add(add(rig.pelvis, rig.up, 5.0), rig.fwd, -7.5)
    belt_b = add(add(rig.pelvis, rig.up, 5.0), rig.fwd, 7.8)
    canvas.line(belt_a, belt_b, "#0b0c12", part=part, width=1.0)


def draw_head(canvas, rig, pose, scale=1.0):
    c = rig.head_c
    ang = rig.head_angle
    s = scale
    hood = canvas.new_part()
    # Hood drape down the back to the shoulders.
    back_top = add(add(c, rig.head_up, 5.0 * s), rig.head_fwd, -6.5 * s)
    peak = add(add(c, rig.head_up, 9.5 * s), rig.head_fwd, (-9.0 - pose.hood_tail) * s)
    drape_end = add(rig.b_sh, rig.fwd, -3.0 * s)
    canvas.polygon([back_top, peak, add(c, rig.head_fwd, -7.5 * s), drape_end,
                    add(rig.f_sh, rig.up, 1.0)], ROBE,
                   shade_fn=lambda x, y: -0.1 + 0.25 * np.sin((x + y) * 0.7), part=hood)
    canvas.ellipse(c, 7.4 * s, 8.8 * s, ang, ROBE, part=hood, shade_bias=0.12)
    # Dark hood interior around the mask.
    inner_c = add(c, rig.head_fwd, 2.6 * s)
    canvas.ellipse(inner_c, 5.4 * s, 7.6 * s, ang, HOOD_IN)
    # Mask: elongated, slightly wider at the brow.
    mask_c = add(add(c, rig.head_fwd, 3.3 * s), rig.head_up, -0.6 * s)
    mask = canvas.new_part()
    canvas.ellipse(mask_c, 4.3 * s, 7.0 * s, ang, MASK, part=mask, shade_bias=0.25)
    brow = add(mask_c, rig.head_up, 2.6 * s)
    canvas.ellipse(brow, 4.6 * s, 3.6 * s, ang, MASK, part=mask, shade_bias=0.3)

    def local(u, v):
        return add(add(mask_c, rig.head_fwd, u * s), rig.head_up, v * s)

    def feature(u, v, rx, ry, tilt):
        sel = canvas.ellipse_mask(local(u, v), rx * s, ry * s, ang + tilt) & (canvas.part == mask)
        canvas.paint_flat(sel, FEATURE, part=mask)

    feature(1.9, 1.7, 1.25, 2.15, 28)    # near eye, slanted
    feature(-1.3, 1.9, 0.95, 1.9, -24)   # far eye
    feature(0.6, -3.3, 1.15, 2.6, 6)     # screaming mouth
    # Cheek shading line under the near eye.
    canvas.line(local(2.6, -0.8), local(3.4, -2.4), "#bdb6a7", part=mask, width=0.6)
    return mask_c


def draw_neck_stump(canvas, rig, pose, rng):
    canvas.ellipse(rig.neck, 3.6, 2.2, -pose.lean, BLOOD, shade_bias=0.2)
    canvas.ellipse(add(rig.neck, rig.up, 0.6), 1.6, 0.9, -pose.lean, BLOOD, shade_bias=0.8)
    for i in range(pose.blood_neck):
        dist = 2 + i * 2.2 + rng.random() * 1.5
        spread = (rng.random() - 0.5) * 3.5
        pt = add(add(rig.neck, rig.up, dist), rig.fwd, spread - i * 0.35)
        size = max(0.8, 1.8 - i * 0.12)
        canvas.ellipse(pt, size, size * 1.3, -pose.lean, BLOOD, shade_bias=0.3)


def blood_cap(canvas, point, direction_deg, size=3.0):
    canvas.ellipse(point, size, size * 0.7, -(direction_deg - 90), BLOOD, shade_bias=0.25)
    canvas.ellipse(point, size * 0.45, size * 0.3, -(direction_deg - 90), BLOOD, shade_bias=0.9)


# ---------------------------------------------------------------- full character

def render_pose(pose: Pose, headless=False, only=None, seed=0):
    canvas = Canvas(FRAME_W, FRAME_H, PIVOT_X, PIVOT_Y)
    rig = Rig(pose)
    rng = random.Random(seed)
    info = {}
    want = (lambda name: True) if only is None else (lambda name: name in only)
    if want("back_arm"):
        info["back_hand"], _ = draw_arm(canvas, rig, False, pose)
    if want("back_leg"):
        info["back_toe"] = draw_leg(canvas, rig, False, pose)
    if want("front_leg"):
        info["front_toe"] = draw_leg(canvas, rig, True, pose)
    if want("body"):
        draw_body(canvas, rig, pose)
    if want("head") and not headless:
        info["mask"] = draw_head(canvas, rig, pose)
    if headless:
        draw_neck_stump(canvas, rig, pose, rng)
    if want("front_arm"):
        info["front_hand"], info["knife_tip"] = draw_arm(canvas, rig, True, pose)
    info["rig"] = rig
    return canvas, info


def render_gore_parts():
    """Dismembered pieces cut from the neutral pose, each with a bloody stump."""
    base = Pose(pelvis=(0, 41), lean=4, head=4, ff=(8, 3), bf=(-8, 3), fh=(14, 30), bh=(-4, 30), knife=150)
    rig = Rig(base)
    parts = []

    def piece(only, cut_points, name, headless=False):
        canvas, info = render_pose(base, headless=headless, only=only)
        for point, direction in cut_points:
            blood_cap(canvas, point, direction)
        opaque = canvas.part >= 0
        ys, xs = np.nonzero(opaque)
        centre = (canvas.wx[ys, xs].mean(), canvas.wy[ys, xs].mean())
        parts.append((name, canvas.render(), centre))

    piece({"head"}, [(rig.neck, 0)], "head")
    piece({"body"}, [(rig.neck, 180), (rig.f_sh, 90), (rig.b_sh, -90)], "torso", headless=False)
    piece({"front_arm"}, [(rig.f_sh, 180)], "arm_front")
    piece({"back_arm"}, [(rig.b_sh, 180)], "arm_back")
    piece({"front_leg"}, [(rig.f_hip, 180)], "leg_front")
    piece({"back_leg"}, [(rig.b_hip, 180)], "leg_back")
    return parts, rig


# ---------------------------------------------------------------- animation poses

BASE = Pose()


def P(**kw):
    return replace(BASE, **kw)


def idle_frames():
    frames = []
    for i in range(4):
        breath = [0.0, -0.6, -1.2, -0.6][i]
        frames.append(P(pelvis=(0, 41 + breath * 0.6), lean=13 - breath * 0.4, head=6 + breath,
                        fh=(18, 54 + breath), bh=(13, 46 + breath * 0.5), hem=breath * 0.6,
                        hood_tail=-breath * 0.6, phase=i * 0.25))
    return frames


def walk_frames():
    frames = []
    for i in range(6):
        t = i / 6.0 * 2 * math.pi
        stride = 10.0
        ffx = math.cos(t) * stride
        bfx = -math.cos(t) * stride
        ffy = 3 + max(0.0, math.sin(t)) * 5
        bfy = 3 + max(0.0, -math.sin(t)) * 5
        bob = abs(math.sin(t)) * 1.5
        frames.append(P(pelvis=(0, 40 + bob), lean=15, head=4, ff=(ffx + 1, ffy), bf=(bfx - 1, bfy),
                        fh=(18 + math.cos(t) * 1.5, 53 + bob), bh=(12 - math.cos(t) * 2, 45 + bob),
                        hem=math.cos(t) * 2.0, hood_tail=1.5, phase=i * 0.5))
    return frames


def crouch_frames():
    return [
        P(pelvis=(0, 32), lean=18, head=4, ff=(12, 3), bf=(-11, 3), fh=(20, 44), bh=(14, 38), phase=0.2),
        P(pelvis=(-1, 24), lean=24, head=0, ff=(13, 3), bf=(-13, 3), fh=(21, 37), bh=(15, 31), phase=0.4),
    ]


def jump_frames():
    return [
        P(pelvis=(0, 36), lean=20, head=6, ff=(8, 3), bf=(-9, 3), fh=(16, 46), bh=(10, 40), hem=-2, phase=0.1),
        P(pelvis=(0, 44), lean=10, head=4, ff=(13, 26), bf=(-3, 22), fh=(17, 60), bh=(10, 54), hem=3,
          hood_tail=3, sleeve_flutter=1.0, phase=0.6),
        P(pelvis=(0, 44), lean=6, head=8, ff=(9, 10), bf=(-9, 14), fh=(18, 66), bh=(-4, 62), hem=-3,
          hood_tail=-2, sleeve_flutter=1.5, phase=0.9),
    ]


def punch_frames():
    return [
        P(pelvis=(-1, 40), lean=8, head=6, ff=(12, 3), bf=(-11, 3), fh=(8, 54), bh=(13, 48), knife=120,
          fe=-1, phase=0.1),
        P(pelvis=(3, 40), lean=22, head=6, ff=(14, 3), bf=(-10, 3), fh=(38, 56), bh=(8, 48), knife=92,
          hem=2, phase=0.3),
        P(pelvis=(1, 40), lean=16, head=6, ff=(12, 3), bf=(-10, 3), fh=(24, 54), bh=(12, 47), knife=105,
          phase=0.5),
    ]


def slash_frames():
    return [
        P(pelvis=(-2, 41), lean=-4, head=-6, ff=(12, 3), bf=(-12, 3), fh=(-2, 82), bh=(14, 50), knife=200,
          fe=-1, hem=-2, phase=0.0),
        P(pelvis=(-1, 42), lean=-8, head=-10, ff=(12, 3), bf=(-12, 3), fh=(6, 90), bh=(16, 52), knife=175,
          fe=-1, hood_tail=2, phase=0.2),
        P(pelvis=(3, 40), lean=18, head=10, ff=(15, 3), bf=(-11, 3), fh=(32, 70), bh=(8, 46), knife=118,
          fe=-1, hem=3, sleeve_flutter=1.5, phase=0.4),
        P(pelvis=(5, 38), lean=32, head=14, ff=(16, 3), bf=(-11, 3), fh=(34, 40), bh=(4, 42), knife=62,
          fe=-1, hem=4, sleeve_flutter=2.0, phase=0.6),
        P(pelvis=(2, 40), lean=20, head=8, ff=(13, 3), bf=(-11, 3), fh=(24, 46), bh=(10, 45), knife=90,
          phase=0.8),
    ]


def kick_frames():
    return [
        P(pelvis=(-1, 42), lean=4, head=4, ff=(9, 18), bf=(-9, 3), fh=(16, 58), bh=(6, 52), knife=130,
          phase=0.1),
        P(pelvis=(-2, 43), lean=-12, head=8, ff=(38, 36), bf=(-10, 3), fh=(10, 62), bh=(-2, 56), knife=140,
          hem=-3, phase=0.3),
        P(pelvis=(-1, 42), lean=0, head=6, ff=(14, 20), bf=(-10, 3), fh=(14, 58), bh=(6, 52), knife=130,
          phase=0.5),
        P(pelvis=(0, 41), lean=10, head=6, ff=(11, 3), bf=(-10, 3), fh=(17, 55), bh=(12, 47), knife=118,
          phase=0.7),
    ]


def crouch_attack_frames():
    return [
        P(pelvis=(-1, 24), lean=26, head=0, ff=(13, 3), bf=(-13, 3), fh=(10, 34), bh=(15, 31), knife=150,
          phase=0.2),
        P(pelvis=(2, 23), lean=34, head=4, ff=(16, 3), bf=(-12, 3), fh=(40, 10), bh=(12, 28), knife=80,
          fe=+1, sleeve_flutter=1.5, phase=0.5),
        P(pelvis=(0, 24), lean=28, head=2, ff=(14, 3), bf=(-13, 3), fh=(26, 22), bh=(14, 30), knife=105,
          phase=0.8),
    ]


def air_attack_frames():
    return [
        P(pelvis=(0, 44), lean=-6, head=-4, ff=(12, 24), bf=(-4, 20), fh=(4, 76), bh=(12, 60), knife=190,
          hood_tail=3, sleeve_flutter=1.0, phase=0.3),
        P(pelvis=(2, 44), lean=26, head=12, ff=(12, 22), bf=(-6, 26), fh=(30, 34), bh=(4, 52), knife=50,
          fe=-1, hood_tail=4, sleeve_flutter=2.0, hem=3, phase=0.7),
    ]


def block_frames():
    return [
        P(pelvis=(-2, 40), lean=0, head=10, ff=(11, 3), bf=(-12, 3), fh=(14, 70), bh=(12, 62), fe=-1, be=-1,
          knife=170, phase=0.1),
        P(pelvis=(-2, 24), lean=14, head=10, ff=(12, 3), bf=(-13, 3), fh=(16, 52), bh=(13, 45), knife=160,
          phase=0.3),
    ]


def hurt_frames():
    return [
        P(pelvis=(-3, 41), lean=-16, head=-24, ff=(9, 3), bf=(-13, 3), fh=(14, 64), bh=(-6, 62), knife=150,
          hood_tail=-3, sleeve_flutter=1.5, hem=-3, phase=0.2),
        P(pelvis=(-3, 38), lean=36, head=22, ff=(9, 3), bf=(-13, 3), fh=(14, 34), bh=(6, 32), knife=80,
          hem=-2, phase=0.5),
        P(pelvis=(-3, 23), lean=-4, head=-18, ff=(11, 3), bf=(-14, 3), fh=(12, 44), bh=(-4, 44), knife=150,
          phase=0.8),
    ]


def knockdown_frames():
    return [
        P(pelvis=(-4, 44), lean=-50, head=-30, ff=(14, 26), bf=(6, 18), fh=(10, 70), bh=(-14, 68), fe=+1,
          knife=200, hood_tail=-4, sleeve_flutter=2, hem=-4, phase=0.1),
        P(pelvis=(-6, 26), lean=-74, head=-20, ff=(20, 26), bf=(14, 16), fh=(-8, 52), bh=(-24, 44), fe=+1,
          knife=230, hem=-4, sleeve_flutter=2, phase=0.4),
        P(pelvis=(-2, 7), lean=-90, head=-8, ff=(20, 4), bf=(18, 7), fh=(-14, 14), bh=(-30, 4), fe=+1, be=+1,
          knife=260, hem=-2, phase=0.7),
        P(pelvis=(-2, 22), lean=20, head=6, ff=(12, 3), bf=(-14, 3), fh=(16, 12), bh=(8, 28), knife=95,
          phase=0.9),
    ]


def grab_frames():
    return [
        P(pelvis=(2, 40), lean=22, head=8, ff=(14, 3), bf=(-10, 3), fh=(36, 58), bh=(24, 52), knife=None,
          grip_open=True, phase=0.1),
        P(pelvis=(2, 40), lean=18, head=8, ff=(14, 3), bf=(-10, 3), fh=(30, 60), bh=(22, 54), knife=None,
          phase=0.3),
        P(pelvis=(-2, 42), lean=-12, head=-10, ff=(12, 3), bf=(-12, 3), fh=(6, 88), bh=(0, 84), fe=+1, be=+1,
          knife=None, hood_tail=2, phase=0.5),
        P(pelvis=(4, 39), lean=30, head=12, ff=(16, 3), bf=(-10, 3), fh=(38, 64), bh=(28, 58), knife=None,
          grip_open=True, sleeve_flutter=2.0, hem=4, phase=0.7),
        P(pelvis=(1, 41), lean=14, head=6, ff=(12, 3), bf=(-10, 3), fh=(18, 52), bh=(12, 46), knife=None,
          phase=0.9),
    ]


def ko_frames(headless=False):
    spurt = 6 if headless else 0
    return [
        P(pelvis=(-2, 40), lean=-10, head=24, ff=(9, 3), bf=(-12, 3), fh=(10, 30), bh=(-6, 32), knife=40,
          blood_neck=spurt, phase=0.1),
        P(pelvis=(0, 22), lean=6, head=34, ff=(-6, 3), bf=(-14, 3), fh=(10, 12), bh=(-2, 14), knife=60,
          blood_neck=spurt + 2, phase=0.4),
        P(pelvis=(2, 14), lean=62, head=30, ff=(-10, 3), bf=(-16, 4), fh=(28, 4), bh=(20, 6), fe=+1, be=+1,
          knife=90, blood_neck=spurt, phase=0.6),
        P(pelvis=(4, 7), lean=90, head=10, ff=(-16, 4), bf=(-18, 6), fh=(30, 3), bh=(16, 3), fe=+1, be=+1,
          knife=95, blood_neck=max(0, spurt - 4), phase=0.8),
    ]


def dizzy_frames():
    return [
        P(pelvis=(-1, 39), lean=-6, head=26, ff=(8, 3), bf=(-10, 3), fh=(8, 28), bh=(-4, 30), knife=20,
          phase=0.2),
        P(pelvis=(1, 39), lean=8, head=-14, ff=(9, 3), bf=(-9, 3), fh=(12, 27), bh=(0, 29), knife=20,
          hem=2, phase=0.6),
    ]


def victory_frames():
    return [
        P(pelvis=(0, 41), lean=2, head=0, ff=(10, 3), bf=(-10, 3), fh=(16, 70), bh=(8, 44), knife=150, phase=0.1),
        P(pelvis=(0, 42), lean=-6, head=-14, ff=(10, 3), bf=(-10, 3), fh=(6, 96), bh=(10, 44), fe=+1,
          knife=180, hood_tail=2, phase=0.4),
        P(pelvis=(0, 42), lean=-8, head=-22, ff=(10, 3), bf=(-10, 3), fh=(8, 97), bh=(12, 46), fe=+1,
          knife=176, hood_tail=3, phase=0.7),
    ]


ANIMATIONS = [
    # name, frames, ticks per frame, loop
    ("idle", idle_frames, [10, 10, 10, 10], True),
    ("walk", walk_frames, [6] * 6, True),
    ("crouch", crouch_frames, [3, 6], False),
    ("jump", jump_frames, [4, 8, 8], False),
    ("punch", punch_frames, None, False),
    ("slash", slash_frames, None, False),
    ("kick", kick_frames, None, False),
    ("crouch_attack", crouch_attack_frames, None, False),
    ("air_attack", air_attack_frames, None, False),
    ("block", block_frames, [6, 6], False),
    ("hurt", hurt_frames, [6, 6, 6], False),
    ("knockdown", knockdown_frames, [6, 8, 30, 12], False),
    ("grab_throw", grab_frames, None, False),
    ("ko", ko_frames, [10, 12, 10, 60], False),
    ("headless", lambda: ko_frames(True), [10, 12, 10, 60], False),
    ("dizzy", dizzy_frames, [14, 14], True),
    ("victory", victory_frames, [8, 10, 14], False),
]


def hitbox_around(point, w, h, back=0.0):
    x, y = point
    return {"x": round(x - w / 2 - back), "y": round(y - h / 2), "w": round(w), "h": round(h)}


def build_moves(tips):
    """Frame data in 60 fps ticks. Hitboxes come from the knife tip / foot of each active frame."""
    return [
        {"id": "light", "input": "light", "state": "stand", "anim": "punch",
         "startupFrames": [0], "activeFrames": [1], "recoveryFrames": [2],
         "startup": 4, "active": 3, "recovery": 9, "damage": 45, "hitstun": 15, "blockstun": 10,
         "level": "mid", "pushback": 5.0, "knockdown": False, "lunge": 1.0,
         "hitbox": hitbox_around(tips["punch"][1], 22, 14, back=6), "sfxWhiff": "whoosh_light",
         "sfxHit": "stab_light", "blood": 6, "shake": 0.0, "hitstop": 6},
        {"id": "heavy", "input": "heavy", "state": "stand", "anim": "slash",
         "startupFrames": [0, 1], "activeFrames": [2, 3], "recoveryFrames": [4],
         "startup": 11, "active": 5, "recovery": 16, "damage": 110, "hitstun": 22, "blockstun": 14,
         "level": "overhead", "pushback": 7.0, "knockdown": False, "lunge": 1.5,
         "hitbox": {"x": 10, "y": 30, "w": 34, "h": 50}, "sfxWhiff": "whoosh_heavy",
         "sfxHit": "stab_heavy", "blood": 14, "shake": 0.6, "hitstop": 10},
        {"id": "kick", "input": "kick", "state": "stand", "anim": "kick",
         "startupFrames": [0], "activeFrames": [1], "recoveryFrames": [2, 3],
         "startup": 7, "active": 4, "recovery": 12, "damage": 70, "hitstun": 18, "blockstun": 12,
         "level": "mid", "pushback": 11.0, "knockdown": False, "lunge": 0.0,
         "hitbox": hitbox_around(tips["kick"][1], 18, 16, back=4), "sfxWhiff": "whoosh_light",
         "sfxHit": "hit_kick", "blood": 4, "shake": 0.25, "hitstop": 8},
        {"id": "crouch_light", "input": "any", "state": "crouch", "anim": "crouch_attack",
         "startupFrames": [0], "activeFrames": [1], "recoveryFrames": [2],
         "startup": 6, "active": 4, "recovery": 14, "damage": 60, "hitstun": 16, "blockstun": 10,
         "level": "low", "pushback": 6.0, "knockdown": False, "lunge": 0.5,
         "hitbox": hitbox_around(tips["crouch_attack"][1], 24, 12, back=8), "sfxWhiff": "whoosh_light",
         "sfxHit": "stab_light", "blood": 7, "shake": 0.0, "hitstop": 7},
        {"id": "air", "input": "any", "state": "air", "anim": "air_attack",
         "startupFrames": [0], "activeFrames": [1], "recoveryFrames": [],
         "startup": 5, "active": 18, "recovery": 0, "damage": 70, "hitstun": 18, "blockstun": 12,
         "level": "overhead", "pushback": 6.0, "knockdown": False, "lunge": 0.0,
         "hitbox": hitbox_around(tips["air_attack"][1], 22, 22, back=4), "sfxWhiff": "whoosh_light",
         "sfxHit": "stab_heavy", "blood": 9, "shake": 0.2, "hitstop": 8},
        {"id": "grab", "input": "grab", "state": "stand", "anim": "grab_throw",
         "startupFrames": [0], "activeFrames": [0], "recoveryFrames": [4],
         "startup": 5, "active": 3, "recovery": 20, "damage": 130, "hitstun": 0, "blockstun": 0,
         "level": "throw", "pushback": 0.0, "knockdown": True, "lunge": 0.0,
         "hitbox": {"x": 6, "y": 20, "w": 30, "h": 50}, "sfxWhiff": "whoosh_light",
         "sfxHit": "grab", "blood": 0, "shake": 0.0, "hitstop": 0},
        {"id": "special", "input": "forward+heavy", "state": "stand", "anim": "punch",
         "startupFrames": [0], "activeFrames": [1], "recoveryFrames": [2],
         "startup": 9, "active": 8, "recovery": 18, "damage": 85, "hitstun": 20, "blockstun": 14,
         "level": "mid", "pushback": 8.0, "knockdown": True, "lunge": 7.0,
         "hitbox": hitbox_around(tips["punch"][1], 26, 18, back=8), "sfxWhiff": "whoosh_heavy",
         "sfxHit": "stab_heavy", "blood": 12, "shake": 0.5, "hitstop": 10,
         "name": "LUNGE STAB"},
    ]


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    os.makedirs(PREVIEW_DIR, exist_ok=True)
    cells = []
    sprites = []
    animations = []
    tips = {}
    for name, factory, ticks, loop in ANIMATIONS:
        poses = factory()
        indices, anchor_x, anchor_y, tip_list = [], [], [], []
        for i, pose in enumerate(poses):
            headless = name == "headless"
            canvas, info = render_pose(pose, headless=headless, seed=i + len(cells))
            cells.append(canvas.render())
            sprite_index = len(sprites)
            sprites.append({"name": f"{name}_{i}", "cell": len(cells) - 1, "pivotX": PIVOT_X,
                            "pivotY": FRAME_H - PIVOT_Y})
            indices.append(sprite_index)
            hand = info.get("front_hand", (0, 0))
            anchor_x.append(round(hand[0], 1))
            anchor_y.append(round(hand[1], 1))
            if name == "kick":
                tip_list.append(info["front_toe"])
            elif info.get("knife_tip") is not None:
                tip_list.append(info["knife_tip"])
            else:
                tip_list.append(hand)
        tips[name] = tip_list
        animations.append({"name": name, "sprites": indices, "ticks": ticks or [], "loop": loop,
                           "anchorX": anchor_x, "anchorY": anchor_y})

    gore_parts, rig = render_gore_parts()
    gore = []
    for part_name, rgba, centre in gore_parts:
        cells.append(rgba)
        sprite_index = len(sprites)
        sprites.append({"name": f"gore_{part_name}", "cell": len(cells) - 1,
                        "pivotX": round(PIVOT_X + centre[0], 1), "pivotY": round(FRAME_H - PIVOT_Y + centre[1], 1)})
        gore.append({"part": part_name, "sprite": sprite_index, "x": round(centre[0], 1), "y": round(centre[1], 1)})

    rows = math.ceil(len(cells) / COLUMNS)
    atlas = np.zeros((rows * FRAME_H, COLUMNS * FRAME_W, 4), dtype=np.uint8)
    for i, rgba in enumerate(cells):
        r, c = divmod(i, COLUMNS)
        atlas[r * FRAME_H:(r + 1) * FRAME_H, c * FRAME_W:(c + 1) * FRAME_W] = rgba
    save_rgba(atlas, os.path.join(OUT_DIR, "atlas.png"))
    save_rgba(atlas, os.path.join(PREVIEW_DIR, "ghost_atlas_x3.png"), scale=3)

    portrait = render_portrait()
    save_rgba(portrait, os.path.join(OUT_DIR, "portrait.png"))
    save_rgba(portrait, os.path.join(PREVIEW_DIR, "ghost_portrait_x4.png"), scale=4)
    thumb = Image.fromarray(portrait).resize((40, 40), Image.BOX)
    thumb = np.array(thumb)
    thumb[..., 3] = np.where(thumb[..., 3] > 110, 255, 0)
    save_rgba(thumb, os.path.join(OUT_DIR, "thumb.png"))

    data = {
        "id": "ghost",
        "displayName": "GHOST",
        "subtitle": "THE MASKED STALKER",
        "author": "Rocky (procedural)",
        "frameW": FRAME_W, "frameH": FRAME_H, "columns": COLUMNS,
        "sprites": sprites,
        "animations": animations,
        "moves": build_moves(tips),
        "gore": gore,
        "stats": {"health": 950, "walkSpeed": 1.55, "backSpeed": 1.25, "jumpVelocity": 6.6,
                  "jumpForward": 2.2, "gravity": 0.36, "pushWidth": 22, "weight": 0.9},
        "hurtStand": {"x": -11, "y": 0, "w": 24, "h": 82},
        "hurtCrouch": {"x": -12, "y": 0, "w": 28, "h": 54},
        "hurtAir": {"x": -11, "y": 8, "w": 24, "h": 70},
        "bloodColor": "#8c0f19",
        "accentColor": "#7fa8ff",
        "neckX": round(rig.neck[0], 1), "neckY": round(rig.neck[1], 1),
    }
    with open(os.path.join(OUT_DIR, "character.json"), "w") as f:
        json.dump(data, f, indent=1)
    print(f"ghost: {len(cells)} cells, atlas {atlas.shape[1]}x{atlas.shape[0]}")


def render_portrait():
    """Large bust for the select screen, drawn with the same head routine at 3x scale."""
    size = 96
    canvas = Canvas(size, size, 48, 96)
    scale = 3.0

    class BustRig:
        pass

    rig = BustRig()
    lean, tilt = 6.0, 6.0
    ang = lean + tilt
    rig.up = (math.sin(math.radians(lean)), math.cos(math.radians(lean)))
    rig.fwd = (math.cos(math.radians(lean)), -math.sin(math.radians(lean)))
    rig.head_up = (math.sin(math.radians(ang)), math.cos(math.radians(ang)))
    rig.head_fwd = (math.cos(math.radians(ang)), -math.sin(math.radians(ang)))
    rig.head_angle = -ang
    rig.head_c = (-4.0, 58.0)
    rig.b_sh = (-26.0, 20.0)
    rig.f_sh = (18.0, 18.0)
    canvas.capsule((-34, 4), (30, 2), 20, 20, ROBE_BACK)
    canvas.capsule((-24, 14), (22, 12), 14, 14, ROBE, shade_bias=0.05)
    canvas.capsule((0, -10), (0, 24), 24, 22, ROBE)
    draw_head(canvas, rig, Pose(hood_tail=2), scale=scale)
    # Knife raised beside the mask.
    canvas.ellipse((30, 30), 6, 5.5, 0, GLOVE)
    draw_knife_scaled(canvas, (30, 34), 172, 2.4)
    rgba = canvas.render()
    # Red rim light along the back edge, arcade-select style.
    opaque = rgba[..., 3] > 0
    rim = opaque & ~np.roll(opaque, 2, axis=1)
    rgba[rim] = (*hex_rgb("#c0202a"), 255)
    return rgba


def draw_knife_scaled(canvas, hand, angle, s):
    handle_end = polar(hand, 3.5 * s, angle)
    canvas.capsule(polar(hand, -1.5 * s, angle), handle_end, 1.2 * s, 1.2 * s, HANDLE)
    canvas.capsule(polar(handle_end, 1.6 * s, angle + 90), polar(handle_end, 1.6 * s, angle - 90), 0.7 * s, 0.7 * s,
                   BLADE, shade_bias=-0.3)
    tip = polar(handle_end, 12.0 * s, angle)
    blade = canvas.capsule(handle_end, tip, 1.75 * s, 0.4 * s, BLADE, shade_bias=0.2)
    canvas.line(polar(polar(handle_end, 1.5 * s, angle), 0.8 * s, angle + 90), polar(tip, 0.5 * s, angle + 90),
                "#ffffff", part=blade, width=1.0)


if __name__ == "__main__":
    main()
