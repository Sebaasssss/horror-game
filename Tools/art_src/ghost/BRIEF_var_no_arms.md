# GHOST: var_no_arms

You produce sprite sheets for a 2D retro arcade fighting game. Use your image generation tool for every image below.

## Character
Ghostface-style slasher: tall slim figure in a long flowing black hooded robe with ragged tattered sleeves and a jagged tattered hem, white elongated ghost mask with black drooping eye holes and a long open screaming mouth, black gloves, black boots. Large hunting knife with a silver blade in his RIGHT hand. Agile, twitchy, theatrical body language; the robe flows with every move.

**Mutilated state for this whole batch:** BOTH arms have been cut off at the shoulders: two ragged bloody stumps gushing blood, no weapon. He fights with headbutts and kicks.

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

### `no_arms/model.png`
FIRST create this reference: a single full-body neutral fighting stance of the character in this mutilated state, facing right, on a flat magenta (#FF00FF) background, edited from `model_sheet.png` so the design matches exactly. Use it as the reference for every strip below.

### `no_arms/idle.png`
Template: `templates/template_6.png` (6 frames). Reference: `no_arms/model.png`.
Swaying menacing stance without arms, stumps gushing blood: breathing loop like a fighting stance.

### `no_arms/walk_forward.png`
Template: `templates/template_8.png` (8 frames). Reference: `no_arms/model.png`.
Walking forward without arms, walk-in-place, stumps dripping: full 8-phase walk cycle.

### `no_arms/walk_back.png`
Template: `templates/template_8.png` (8 frames). Reference: `no_arms/model.png`.
Walking backwards while FACING RIGHT, walk-in-place, stumps dripping: 8-phase cycle.

### `no_arms/crouch.png`
Template: `templates/template_4.png` (4 frames). Reference: `no_arms/model.png`.
1 knees starting to bend, 2 half crouch, 3 full low crouch guard with hunting knife ready, 4 same full crouch (slight breathing variation).

### `no_arms/jump_up.png`
Template: `templates/template_4.png` (4 frames). Reference: `no_arms/model.png`.
Vertical jump, body kept at the same height in the cell (the game moves it): 1 deep crouch preparing, 2 launching with legs extended, 3 knees tucked at the peak, 4 descending with legs reaching down.

### `no_arms/headbutt.png`
Template: `templates/template_5.png` (5 frames). Reference: `no_arms/model.png`.
Brutal headbutt: 1 rearing the head back, 2 lunging forward, 3 head fully forward (impact), 4 recoil, 5 recover.

### `no_arms/kick.png`
Template: `templates/template_5.png` (5 frames). Reference: `no_arms/model.png`.
Front stomp kick: 1 knee up, 2 leg extending, 3 leg fully extended horizontally at stomach height with the sole forward, 4 retracting the knee, 5 foot down.

### `no_arms/sweep.png`
Template: `templates/template_5.png` (5 frames). Reference: `no_arms/model.png`.
Crouching leg sweep at ankle height: 1 drop low on one knee, 2 leg extending along the floor, 3 leg fully extended along the floor in front, 4 sweeping back, 5 crouch recover.

### `no_arms/hurt.png`
Template: `templates/template_6.png` (6 frames). Reference: `no_arms/model.png`.
1 head snapped back from a face hit, 2 recoiling further, 3 doubled over from a gut hit, 4 doubled over deeper, 5 crouching hit flinch, 6 crouching hit flinch deeper. Blood flies from the hits.

### `no_arms/knockdown.png`
Template: `templates/template_5.png` (5 frames). Reference: `no_arms/model.png`.
Knocked down, the body stays inside the cell: 1 blasted off the feet flying backward to the LEFT with the body tilted back, 2 airborne nearly horizontal, 3 falling back first, 4 lying flat on the back on the baseline, 5 lying flat (slight variation).

### `no_arms/getup.png`
Template: `templates/template_4.png` (4 frames). Reference: `no_arms/model.png`.
Getting up without arms: 1 lying, 2 rolling to the knees, 3 rising, 4 standing.

### `no_arms/dizzy.png`
Template: `templates/template_4.png` (4 frames). Reference: `no_arms/model.png`.
Stunned and wobbling loop (the FINISH HIM state): arms limp, head lolling, knees weak, swaying.

### `no_arms/ko.png`
Template: `templates/template_5.png` (5 frames). Reference: `no_arms/model.png`.
1 staggering backward clutching the chest, 2 knees buckling, 3 dropping to the knees, 4 falling face forward, 5 lying face down motionless in a pool of blood on the baseline.

## Output
Skip any image whose output file already exists. For every new image, copy the ORIGINAL generated PNG to the exact
path given (create folders as needed). Copy the exact file path the image tool returned for THAT generation; never
pick "the newest file" in ~/.codex/generated_images, other sessions write there at the same time.
Copy it unchanged: no resizing, cropping or recolouring. If a generation fails or
ignores the template, retry it once, then continue with the next one. Do not write any code for the game.
When finished, print the list of files written.
