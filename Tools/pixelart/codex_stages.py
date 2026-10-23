"""Turns Codex stage panoramas (Tools/art_src/stages/<id>/{far,mid}.png) into parallax stages, and gibs.png into
the shared gore-chunk sheet.

  Tools/.venv/bin/python Tools/pixelart/codex_stages.py
"""
import json
import os

import numpy as np
from PIL import Image

from anim_spec import STAGES
from codex_char import despeckle, downscale, is_background, outline, slice_strip

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = os.path.join(ROOT, "Tools", "art_src", "stages")
OUT = os.path.join(ROOT, "Assets", "Resources", "Stages")
UI = os.path.join(ROOT, "Assets", "Resources", "UI")

STAGE_W, STAGE_H = 640, 216
GROUND_Y = 28
MUSIC = {"camp": "fight", "utez": "fight_utez", "woodsboro": "fight_woodsboro"}


def palette_of(img, colors=72):
    return img.convert("RGB").quantize(colors=colors, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE)


def layer(path, keyed):
    img = Image.open(path).convert("RGB")
    rgb = np.array(img)
    rgba = np.zeros(rgb.shape[:2] + (4,), dtype=np.uint8)
    rgba[..., :3] = rgb
    rgba[..., 3] = np.where(is_background(rgb), 0, 255) if keyed else 255
    small = downscale(rgba, STAGE_W / rgba.shape[1])
    small = small[:STAGE_H] if small.shape[0] >= STAGE_H else np.pad(small, ((STAGE_H - small.shape[0], 0), (0, 0),
                                                                           (0, 0)))
    pal = palette_of(Image.fromarray(small[..., :3]))
    q = np.array(Image.fromarray(small[..., :3]).quantize(palette=pal, dither=Image.Dither.NONE).convert("RGB"))
    small[..., :3] = q
    if keyed:
        small = despeckle(small, 2)
    else:
        small[..., 3] = 255
    return small


def main():
    stage_ids = []
    for stage_id, (title, _) in STAGES.items():
        far_path = os.path.join(SRC, stage_id, "far.png")
        mid_path = os.path.join(SRC, stage_id, "mid.png")
        if not (os.path.exists(far_path) and os.path.exists(mid_path)):
            print(f"{stage_id}: missing art, skipped")
            continue
        out_dir = os.path.join(OUT, stage_id)
        os.makedirs(out_dir, exist_ok=True)
        far = layer(far_path, False)
        mid = layer(mid_path, True)
        # Darken the far layer a touch so fighters and the main layer read in front of it.
        far[..., :3] = (far[..., :3].astype(np.float32) * 0.82).astype(np.uint8)
        Image.fromarray(far).save(os.path.join(out_dir, "far.png"))
        Image.fromarray(mid).save(os.path.join(out_dir, "mid.png"))
        composite = Image.fromarray(far)
        composite.alpha_composite(Image.fromarray(mid))
        thumb = composite.crop((STAGE_W // 2 - 140, 40, STAGE_W // 2 + 140, STAGE_H)).resize((56, 36), Image.BOX)
        thumb_rgba = np.array(thumb.convert("RGBA"))
        thumb_rgba[0, :] = thumb_rgba[-1, :] = (120, 96, 80, 255)
        thumb_rgba[:, 0] = thumb_rgba[:, -1] = (120, 96, 80, 255)
        Image.fromarray(thumb_rgba).save(os.path.join(out_dir, "thumb.png"))
        with open(os.path.join(out_dir, "stage.json"), "w") as f:
            json.dump({"id": stage_id, "displayName": title, "groundY": GROUND_Y, "music": MUSIC.get(stage_id, "fight"),
                       "layers": [{"file": "far", "parallax": 0.25}, {"file": "mid", "parallax": 1.0}]}, f, indent=1)
        stage_ids.append(stage_id)
        print(f"{stage_id}: ok")

    gibs_path = os.path.join(SRC, "gibs.png")
    if os.path.exists(gibs_path):
        cells, _ = slice_strip(gibs_path, 8)
        sheet = np.zeros((24, 24 * 8, 4), dtype=np.uint8)
        for i, (cell, _, _) in enumerate(cells):
            ys, xs = np.nonzero(cell[..., 3] > 0)
            if len(xs) == 0:
                continue
            crop = cell[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
            small = outline(np.pad(despeckle(downscale(crop, 20 / max(crop.shape[:2]))), ((1, 1), (1, 1), (0, 0))))
            h, w = small.shape[:2]
            sheet[(24 - h) // 2:(24 - h) // 2 + h, i * 24 + (24 - w) // 2:i * 24 + (24 - w) // 2 + w] = small
        Image.fromarray(sheet).save(os.path.join(UI, "gibs.png"))
        print("gibs: ok")

    roster_path = os.path.join(ROOT, "Assets", "Resources", "Characters", "roster.json")
    with open(roster_path) as f:
        roster = json.load(f)
    if stage_ids:
        roster["stages"] = stage_ids
        roster["stage"] = stage_ids[0]
    with open(roster_path, "w") as f:
        json.dump(roster, f, indent=1)


if __name__ == "__main__":
    main()
