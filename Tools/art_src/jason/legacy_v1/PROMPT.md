# Jason — Codex image generation brief

You are producing source art for a 2D retro arcade fighting game (Mortal Kombat / Street Fighter style, side view).
Use your image generation tool for every image below. Work in this directory. Do not write any code for the game.

## Character

Jason Voorhees-style slasher: very tall, broad, hulking. Off-white hockey mask with red chevron marks and breathing holes,
bald scarred head behind the mask straps, worn dark olive-green work jacket, dark grey-brown trousers, heavy brown boots,
black gloves, a long rusty machete in his RIGHT hand. Menacing, slow, heavy.

## Style rules (apply to EVERY image)

- Detailed 16-bit arcade pixel art, crisp hard pixels, no anti-aliasing, no blur, limited palette (about 24 colors),
  1-pixel dark outline, strong light from the upper left.
- Side view, character FACING RIGHT, like sprites in a classic 2D fighting game.
- Fully transparent background (no floor, no shadow, no scenery, no text, no labels, no frame numbers, no grid lines).
- Same character design, colors, proportions and scale in every image. After the first image, ALWAYS pass `model_sheet.png`
  as the reference image so every later image matches it exactly.
- Animation strips: ONE horizontal row, frames evenly spaced in equal-width cells, the character the same height in
  every frame, feet on the same baseline (except airborne frames), generous empty space between frames so they never touch.

## Images to generate (in this order)

| File | Content |
|---|---|
| `model_sheet.png` | Single full-body neutral fighting stance, facing right, holding machete. This is the reference for everything else. |
| `idle.png` | 4 frames: breathing fighting stance loop (subtle shoulder rise/fall). |
| `walk.png` | 6 frames: heavy menacing walk cycle forward. |
| `crouch.png` | 2 frames: going down, fully crouched guard. |
| `jump.png` | 3 frames: takeoff, airborne tucked knees, falling/landing. |
| `punch.png` | 3 frames: fast left-hand jab: chamber, full extension, return. |
| `slash.png` | 5 frames: heavy machete overhead chop: wind-up behind head, top, blade coming down, full follow-through low in front, recover. |
| `kick.png` | 4 frames: front stomp kick: knee up, leg fully extended at stomach height, retract, recover. |
| `crouch_attack.png` | 3 frames: crouching low machete sweep at ankle height. |
| `air_attack.png` | 2 frames: jumping downward machete slash (airborne). |
| `block.png` | 2 frames: standing guard with forearms raised in front of mask; crouching guard. |
| `hurt.png` | 3 frames: head snapped back from a hit; body doubled over from a gut hit; crouching hit. |
| `knockdown.png` | 4 frames: flying backwards off his feet, falling, lying flat on his back, getting up on one knee. |
| `grab_throw.png` | 5 frames: reaching forward with left hand, gripping (hand closed at chest height in front), lifting the (invisible) opponent overhead, hurling forward, recover. Do NOT draw the opponent. |
| `ko.png` | 4 frames: staggering, dropping to knees, collapsing forward, lying face down motionless. |
| `headless.png` | 4 frames: the same body DECAPITATED (no head, bloody neck stump spurting blood): staggering, dropping to knees, collapsing, lying. |
| `dizzy.png` | 2 frames: wobbling stunned, arms hanging, about to fall (the "FINISH HIM" state). |
| `victory.png` | 3 frames: raising the machete in triumph. |
| `gore_parts.png` | Separate dismembered pieces with bloody cuts, spaced apart: masked head, torso, left arm, right arm holding machete, left leg, right leg. |
| `portrait.png` | Square bust portrait for a character select screen, three-quarter view facing right, dramatic red rim light, dark transparent background. |

## Output

For each image: copy the ORIGINAL generated PNG from `~/.codex/generated_images/...` into this directory with the exact
file name in the table. Copy it unchanged: do NOT resize, crop, recolor or convert it. When all are done, print the list
of files you wrote. If a generation fails, retry it once and continue with the rest.
