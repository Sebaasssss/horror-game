# JASON: var_one_leg

You produce sprite sheets for a 2D retro arcade fighting game. Use your image generation tool for every image below.

## Character
Jason Voorhees-style slasher: very tall, broad and hulking. Off-white hockey mask with red chevron marks and breathing holes, bald scarred head behind the mask straps, worn torn dark olive-green work jacket over a dark shirt, dark grey-brown trousers, heavy brown boots, black gloves. Long rusty bloodstained machete in his RIGHT hand. Slow, heavy, unstoppable body language.

**Mutilated state for this whole batch:** His LEFT leg has been cut off at mid-thigh: a ragged bloody stump dripping blood. He balances and hops on his right leg. He still holds the weapon.

## Reference images (in this directory)
- `model_sheet.png`: the character reference; match its design, colours, proportions and detail exactly and always pass it (or the variant reference) to the image tool.
- `templates/template_N.png`: layout templates, pass the one matching the frame count as the image to edit.

## Layout rules (critical: the game slices every image automatically)
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

## Style
Detailed 16-bit arcade fighting-game pixel art (Mortal Kombat II / Street Fighter Alpha era sprites): crisp hard
pixels, no blur, no anti-aliasing, limited palette, 1-pixel dark outline, light from the upper left. Gory horror
tone: blood is bright red, wet and abundant.

## Images (in this order)

### `one_leg/model.png`
FIRST create this reference: a single full-body neutral fighting stance of the character in this mutilated state, facing right, on a flat magenta (#FF00FF) background, edited from `model_sheet.png` so the design matches exactly. Use it as the reference for every strip below.

### `one_leg/idle.png`
Template: `templates/template_6.png` (6 frames). Reference: `one_leg/model.png`.
Balancing on one leg loop, small hops in place, stump dripping blood, machete ready.

### `one_leg/hop_forward.png`
Template: `templates/template_6.png` (6 frames). Reference: `one_leg/model.png`.
Hopping FORWARD on the right leg, hop-in-place: 1 crouch, 2 push off, 3 airborne, 4 peak, 5 landing, 6 absorb. The stump swings and drips blood.

### `one_leg/hop_back.png`
Template: `templates/template_6.png` (6 frames). Reference: `one_leg/model.png`.
Hopping BACKWARDS on the right leg while facing right, hop-in-place, same 6 phases.

### `one_leg/crouch.png`
Template: `templates/template_4.png` (4 frames). Reference: `one_leg/model.png`.
Kneeling down on the right knee, stump on the floor: 1 bending, 2 half, 3 kneeling guard, 4 hold.

### `one_leg/jump_up.png`
Template: `templates/template_4.png` (4 frames). Reference: `one_leg/model.png`.
One-legged jump: 1 crouch on one leg, 2 launching, 3 airborne, 4 descending.

### `one_leg/punch_1.png`
Template: `templates/template_4.png` (4 frames). Reference: `one_leg/model.png`.
Fast jab with the LEFT free fist (first hit of a combo): 1 guard chamber, 2 fist half way, 3 arm fully extended at head height, 4 retracting.

### `one_leg/slash.png`
Template: `templates/template_6.png` (6 frames). Reference: `one_leg/model.png`.
Heavy overhead machete chop: 1 raising the machete behind the head, 2 machete fully back over the shoulder, 3 swinging down in front at head height, 4 impact low in front at waist height, 5 follow through near the floor, 6 recover to stance.

### `one_leg/hurt.png`
Template: `templates/template_6.png` (6 frames). Reference: `one_leg/model.png`.
1 head snapped back from a face hit, 2 recoiling further, 3 doubled over from a gut hit, 4 doubled over deeper, 5 crouching hit flinch, 6 crouching hit flinch deeper. Blood flies from the hits.

### `one_leg/knockdown.png`
Template: `templates/template_5.png` (5 frames). Reference: `one_leg/model.png`.
Knocked down, the body stays inside the cell: 1 blasted off the feet flying backward to the LEFT with the body tilted back, 2 airborne nearly horizontal, 3 falling back first, 4 lying flat on the back on the baseline, 5 lying flat (slight variation).

### `one_leg/getup.png`
Template: `templates/template_4.png` (4 frames). Reference: `one_leg/model.png`.
Getting up on one leg: 1 lying, 2 on one knee, 3 rising leaning on the machete, 4 balancing.

### `one_leg/dizzy.png`
Template: `templates/template_4.png` (4 frames). Reference: `one_leg/model.png`.
Stunned and wobbling loop (the FINISH HIM state): arms limp, head lolling, knees weak, swaying.

### `one_leg/ko.png`
Template: `templates/template_5.png` (5 frames). Reference: `one_leg/model.png`.
1 staggering backward clutching the chest, 2 knees buckling, 3 dropping to the knees, 4 falling face forward, 5 lying face down motionless in a pool of blood on the baseline.

## Output
Skip any image whose output file already exists. For every new image, copy the ORIGINAL generated PNG to the exact
path given (create folders as needed). Copy the exact file path the image tool returned for THAT generation; never
pick "the newest file" in ~/.codex/generated_images, other sessions write there at the same time.
Copy it unchanged: no resizing, cropping or recolouring. If a generation fails or
ignores the template, retry it once, then continue with the next one. Do not write any code for the game.
When finished, print the list of files written.
