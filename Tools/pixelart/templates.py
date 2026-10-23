"""Layout templates handed to Codex image generation so every strip shares one scale and baseline.

Each template is a flat magenta (#FF00FF) canvas split into equal cells by thin green (#00FF00) separators,
with a green baseline (feet) and a green head line (top of head when standing) in every cell.
Run:  Tools/.venv/bin/python Tools/pixelart/templates.py
"""
import os

from PIL import Image, ImageDraw

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Tools", "art_src", "templates")

# Codex image generation returns 2172x724 for a 3:1 canvas, so templates use exactly that size.
CANVAS_W, CANVAS_H = 2172, 724
HEAD_Y, BASE_Y = 190, 620         # standing character spans 430 px
MAGENTA, GREEN = (255, 0, 255), (0, 255, 0)


def cell_bounds(frames, index):
    return round(index * CANVAS_W / frames), round((index + 1) * CANVAS_W / frames)


def template(frames):
    img = Image.new("RGB", (CANVAS_W, CANVAS_H), MAGENTA)
    draw = ImageDraw.Draw(img)
    for i in range(frames):
        x0, x1 = cell_bounds(frames, i)
        if i:
            draw.rectangle([x0 - 1, 0, x0 + 1, CANVAS_H], fill=GREEN)
        draw.rectangle([x0 + 12, BASE_Y, x1 - 12, BASE_Y + 3], fill=GREEN)
        for x in range(x0 + 12, x1 - 12, 16):
            draw.rectangle([x, HEAD_Y, x + 7, HEAD_Y + 2], fill=GREEN)
        cx = (x0 + x1) // 2
        draw.rectangle([cx - 1, BASE_Y + 8, cx + 1, BASE_Y + 30], fill=GREEN)
    return img


def main():
    os.makedirs(OUT, exist_ok=True)
    for n in range(4, 9):
        template(n).save(os.path.join(OUT, f"template_{n}.png"))
    print("templates in", OUT)


if __name__ == "__main__":
    main()
