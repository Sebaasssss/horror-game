"""Writes Codex image-generation briefs (one per batch) from anim_spec.py and runs them in parallel.

  Tools/.venv/bin/python Tools/pixelart/codex_jobs.py write          # only write the briefs
  Tools/.venv/bin/python Tools/pixelart/codex_jobs.py run [-j 6]     # write + run missing batches
Each batch runs `codex exec` in Tools/art_src/<char>/ (or Tools/art_src/stages/) and writes PNGs next to it.
"""
import os
import shutil
import subprocess
import sys
import time
from concurrent.futures import ThreadPoolExecutor

from anim_spec import CHARACTERS, FULL, GORE, SETS, STAGES, VARIANTS

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = os.path.join(ROOT, "Tools", "art_src")
TEMPLATES = os.path.join(SRC, "templates")

LAYOUT = """## Layout rules (critical: the game slices every image automatically)
- EDIT the given template image and keep its canvas exactly: 2172x724, flat magenta (#FF00FF) background,
  green guide lines untouched.
- The template has N equal cells separated by green vertical lines. Draw exactly one frame per cell, left to right
  in chronological order. Never cross the separators.
- Solid green line = the floor: feet stand on it. Dashed green line = the top of the head when standing upright.
  Small green tick under the floor = horizontal centre: keep the hips above it. The character animates IN PLACE
  (the game moves him); attacks extend forward from that spot.
- The character FACES RIGHT in every frame.
- Same character, same size, same palette in every frame and in every image. Consecutive frames differ only by a
  small, smooth change of pose so the animation is fluid. Loops connect the last frame back to the first.
- No text, labels, numbers, cast shadows, floor texture or scenery.
"""

STYLE = """## Style
Detailed 16-bit arcade fighting-game pixel art (Mortal Kombat II / Street Fighter Alpha era sprites): crisp hard
pixels, no blur, no anti-aliasing, limited palette, 1-pixel dark outline, light from the upper left. Gory horror
tone: blood is bright red, wet and abundant.
"""

OUTPUT = """## Output
Skip any image whose output file already exists. For every new image, copy the ORIGINAL generated PNG to the exact
path given (create folders as needed). Copy the exact file path the image tool returned for THAT generation; never
pick "the newest file" in ~/.codex/generated_images, other sessions write there at the same time.
Copy it unchanged: no resizing, cropping or recolouring. If a generation fails or
ignores the template, retry it once, then continue with the next one. Do not write any code for the game.
When finished, print the list of files written.
"""


def fill(text, info):
    return text.replace("{W}", info["weapon"]).replace("{SPECIAL}", info["special"])


def strip_section(path, frames, direction, info, reference):
    return (f"### `{path}`\nTemplate: `templates/template_{frames}.png` ({frames} frames). "
            f"Reference: `{reference}`.\n{fill(direction, info)}\n")


def character_batches(char_id):
    info = CHARACTERS[char_id]
    batches = {}
    full = list(FULL.items())
    half = len(full) // 2
    for name, chunk in (("full_a", full[:half]), ("full_b", full[half:])):
        items = [strip_section(f"full/{anim}.png", n, d, info, "model_sheet.png") for anim, (n, d) in chunk]
        if name == "full_b":
            for anim, (n, d) in GORE.items():
                items.append(strip_section(f"full/{anim}.png", n, d, info, "model_sheet.png"))
            items.append("### `portrait.png`\nNo template: a square 1:1 bust portrait for the character select "
                         "screen, three-quarter view facing right, dramatic red rim light, blood spatter, flat magenta "
                         "(#FF00FF) background. Reference: `model_sheet.png`.\n")
        batches[name] = ("", items)
    for variant, strips in SETS.items():
        if variant == "full":
            continue
        model = f"{variant}/model.png"
        items = [f"### `{model}`\nFIRST create this reference: a single full-body neutral fighting stance of the "
                 f"character in this mutilated state, facing right, on a flat magenta (#FF00FF) background, edited "
                 f"from `model_sheet.png` so the design matches exactly. Use it as the reference for every strip "
                 f"below.\n"]
        items += [strip_section(f"{variant}/{anim}.png", n, d, info, model) for anim, (n, d) in strips.items()]
        batches["var_" + variant] = (VARIANTS[variant], items)
    return info, batches


def brief(title, look, variant_text, items, reference_note):
    parts = [f"# {title}\n", "You produce sprite sheets for a 2D retro arcade fighting game. Use your image generation "
             "tool for every image below.\n", "## Character\n" + look + "\n"]
    if variant_text:
        parts.append("**Mutilated state for this whole batch:** " + variant_text + "\n")
    parts.append("## Reference images (in this directory)\n" + reference_note + "\n")
    parts += [LAYOUT, STYLE, "## Images (in this order)\n", "\n".join(items), OUTPUT]
    return "\n".join(parts)


def write_all():
    jobs = []
    for char_id in CHARACTERS:
        info, batches = character_batches(char_id)
        workdir = os.path.join(SRC, char_id)
        os.makedirs(workdir, exist_ok=True)
        shutil.copytree(TEMPLATES, os.path.join(workdir, "templates"), dirs_exist_ok=True)
        model_path = os.path.join(workdir, "model_sheet.png")
        if not os.path.exists(model_path):
            model_brief = brief(f"{info['name']}: model sheet", info["look"], "", [
                "### `model_sheet.png`\nNo template: one full-body neutral fighting stance, facing right, holding the "
                f"{info['weapon']}, flat magenta (#FF00FF) background. This becomes the reference for every other "
                "image, so make it clean and detailed.\n"], "- none yet: this batch creates `model_sheet.png`.")
            jobs.append((char_id, "model", workdir, model_brief, None, ["model_sheet.png"]))
        for name, (variant_text, items) in batches.items():
            note = ("- `model_sheet.png`: the character reference; match its design, colours, proportions and detail "
                    "exactly and always pass it (or the variant reference) to the image tool.\n"
                    "- `templates/template_N.png`: layout templates, pass the one matching the frame count as the "
                    "image to edit.")
            text = brief(f"{info['name']}: {name}", info["look"], variant_text, items, note)
            outputs = [line.split("`")[1] for line in items if line.startswith("### `")]
            jobs.append((char_id, name, workdir, text, "model_sheet.png", outputs))

    stage_dir = os.path.join(SRC, "stages")
    os.makedirs(stage_dir, exist_ok=True)
    shutil.copytree(TEMPLATES, os.path.join(stage_dir, "templates"), dirs_exist_ok=True)
    items = []
    for stage_id, (title, desc) in STAGES.items():
        items.append(f"### `{stage_id}/far.png`\nNo template, wide 3:1 panorama (2172x724). FAR layer of the stage "
                     f"'{title}': {desc} Only the sky and the distant background (horizon at about 55% height), fully "
                     f"painted, no characters.\n")
        items.append(f"### `{stage_id}/mid.png`\nNo template, wide 3:1 panorama (2172x724), same scene, same palette "
                     f"and perspective as `{stage_id}/far.png` (pass it as reference). MAIN layer: the foreground "
                     f"architecture and props of '{title}' seen from the side at human eye level, like a 2D fighting "
                     f"game stage. The bottom 13% of the image is a flat walkable ground strip where the fighters "
                     f"stand. Everything above the scenery silhouette (the sky) is flat magenta (#FF00FF) so it can be "
                     f"cut out. Keep the centre of the image open (fighters stand there). No characters.\n")
    items.append("### `gibs.png`\nTemplate: `templates/template_8.png` (8 cells). Small gore chunks, one centred in "
                 "each cell, sized like a fist: 1 eyeball with optic nerve, 2 broken bone, 3 rib fragment, 4 chunk of "
                 "raw flesh, 5 heart, 6 teeth with jaw piece, 7 brain chunk, 8 severed finger. Bloody and glistening.\n")
    stage_brief = "\n".join([
        "# Stages and gore chunks\n", "You produce background art for a 2D retro arcade fighting game. Use your image "
        "generation tool for every image below.\n",
        "## Style\nDetailed 16-bit arcade fighting-game stage pixel art (Mortal Kombat II / Samurai Shodown era): "
        "crisp hard pixels, no blur, limited moody palette, night-time horror atmosphere with fog and moonlight, "
        "readable silhouettes. Side view, horizon around the middle.\n",
        "## Images (in this order)\n", "\n".join(items), OUTPUT])
    outputs = [line.split("`")[1] for line in items]
    jobs.append(("stages", "all", stage_dir, stage_brief, None, outputs))

    for char_id, name, workdir, text, _, _ in jobs:
        with open(os.path.join(workdir, f"BRIEF_{name}.md"), "w") as f:
            f.write(text)
    return jobs


def run_job(job):
    char_id, name, workdir, text, needs, outputs = job
    missing = [o for o in outputs if not os.path.exists(os.path.join(workdir, o))]
    if not missing:
        return f"{char_id}/{name}: already complete"
    if needs:
        while not os.path.exists(os.path.join(workdir, needs)):
            time.sleep(15)
    log = open(os.path.join(workdir, f"codex_{name}.log"), "w")
    started = time.time()
    subprocess.run(["codex", "exec", "--skip-git-repo-check", "-s", "workspace-write", "-C", workdir, "-"],
                   input=text.encode(), stdout=log, stderr=subprocess.STDOUT)
    done = [o for o in outputs if os.path.exists(os.path.join(workdir, o))]
    return f"{char_id}/{name}: {len(done)}/{len(outputs)} files in {int(time.time() - started)}s"


def main():
    jobs = write_all()
    if len(sys.argv) > 1 and sys.argv[1] == "run":
        workers = int(sys.argv[3]) if len(sys.argv) > 3 and sys.argv[2] == "-j" else 6
        # Model sheets first so dependent batches can start as soon as possible.
        jobs.sort(key=lambda j: 0 if j[1] == "model" else 1)
        with ThreadPoolExecutor(max_workers=workers) as pool:
            for line in pool.map(run_job, jobs):
                print(line, flush=True)
    else:
        for job in jobs:
            print(f"{job[0]}/{job[1]}: {len(job[5])} images")


if __name__ == "__main__":
    main()
