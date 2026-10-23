# JASON: var_one_arm

You produce sprite sheets for a 2D retro arcade fighting game. Use your image generation tool for every image below.

## Character
Jason Voorhees-style slasher: very tall, broad and hulking. Off-white hockey mask with red chevron marks and breathing holes, bald scarred head behind the mask straps, worn torn dark olive-green work jacket over a dark shirt, dark grey-brown trousers, heavy brown boots, black gloves. Long rusty bloodstained machete in his RIGHT hand. Slow, heavy, unstoppable body language.

**Mutilated state for this whole batch:** His LEFT arm (the free arm, the one without the weapon) has been cut off at the shoulder: a ragged bloody stump with exposed flesh and bone, dripping blood. He still holds the weapon in the right hand.

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

### `one_arm/model.png`
FIRST create this reference: a single full-body neutral fighting stance of the character in this mutilated state, facing right, on a flat magenta (#FF00FF) background, edited from `model_sheet.png` so the design matches exactly. Use it as the reference for every strip below.

### `one_arm/idle.png`
Template: `templates/template_6.png` (6 frames). Reference: `one_arm/model.png`.
Fighting stance breathing loop, walk-in-place spot: 1 neutral guard, 2 shoulders rise slightly, 3 top of the breath, 4 shoulders settle, 5 slight forward lean, 6 back to neutral (loops into 1).

### `one_arm/walk_forward.png`
Template: `templates/template_8.png` (8 frames). Reference: `one_arm/model.png`.
Walking FORWARD toward the right, walk-in-place (do not drift inside the cell): 1 right foot forward heel contact, 2 weight down on the front leg, 3 left leg passing, 4 body up on the toes, 5 left foot forward heel contact, 6 weight down, 7 right leg passing, 8 body up. Torso bobs slightly, the free arm swings opposite, machete kept ready in front.

### `one_arm/walk_back.png`
Template: `templates/template_8.png` (8 frames). Reference: `one_arm/model.png`.
Walking BACKWARDS: still FACING RIGHT toward the opponent but stepping back to the left, walk-in-place, guarded posture with machete held in front. Same 8 phases as a walk cycle in reverse: rear foot reaches back, weight shifts back, front foot drags back, recover. Loop.

### `one_arm/crouch.png`
Template: `templates/template_4.png` (4 frames). Reference: `one_arm/model.png`.
1 knees starting to bend, 2 half crouch, 3 full low crouch guard with machete ready, 4 same full crouch (slight breathing variation).

### `one_arm/jump_up.png`
Template: `templates/template_4.png` (4 frames). Reference: `one_arm/model.png`.
Vertical jump, body kept at the same height in the cell (the game moves it): 1 deep crouch preparing, 2 launching with legs extended, 3 knees tucked at the peak, 4 descending with legs reaching down.

### `one_arm/punch_1.png`
Template: `templates/template_4.png` (4 frames). Reference: `one_arm/model.png`.
Quick one-handed machete jab: 1 chamber, 2 thrusting, 3 fully extended at head height, 4 retract.

### `one_arm/slash.png`
Template: `templates/template_6.png` (6 frames). Reference: `one_arm/model.png`.
Heavy overhead machete chop: 1 raising the machete behind the head, 2 machete fully back over the shoulder, 3 swinging down in front at head height, 4 impact low in front at waist height, 5 follow through near the floor, 6 recover to stance.

### `one_arm/kick.png`
Template: `templates/template_5.png` (5 frames). Reference: `one_arm/model.png`.
Front stomp kick: 1 knee up, 2 leg extending, 3 leg fully extended horizontally at stomach height with the sole forward, 4 retracting the knee, 5 foot down.

### `one_arm/block.png`
Template: `templates/template_4.png` (4 frames). Reference: `one_arm/model.png`.
1 standing guard with the machete raised in front of the face, 2 holding, 3 crouching guard, 4 holding.

### `one_arm/hurt.png`
Template: `templates/template_6.png` (6 frames). Reference: `one_arm/model.png`.
1 head snapped back from a face hit, 2 recoiling further, 3 doubled over from a gut hit, 4 doubled over deeper, 5 crouching hit flinch, 6 crouching hit flinch deeper. Blood flies from the hits.

### `one_arm/knockdown.png`
Template: `templates/template_5.png` (5 frames). Reference: `one_arm/model.png`.
Knocked down, the body stays inside the cell: 1 blasted off the feet flying backward to the LEFT with the body tilted back, 2 airborne nearly horizontal, 3 falling back first, 4 lying flat on the back on the baseline, 5 lying flat (slight variation).

### `one_arm/getup.png`
Template: `templates/template_4.png` (4 frames). Reference: `one_arm/model.png`.
1 lying propped on one elbow, 2 rolling onto one knee, 3 rising, 4 back in the fighting stance.

### `one_arm/dizzy.png`
Template: `templates/template_4.png` (4 frames). Reference: `one_arm/model.png`.
Stunned and wobbling loop (the FINISH HIM state): arms limp, head lolling, knees weak, swaying.

### `one_arm/ko.png`
Template: `templates/template_5.png` (5 frames). Reference: `one_arm/model.png`.
1 staggering backward clutching the chest, 2 knees buckling, 3 dropping to the knees, 4 falling face forward, 5 lying face down motionless in a pool of blood on the baseline.

## Output
Skip any image whose output file already exists. For every new image, copy the ORIGINAL generated PNG to the exact
path given (create folders as needed). Copy the exact file path the image tool returned for THAT generation; never
pick "the newest file" in ~/.codex/generated_images, other sessions write there at the same time.
Copy it unchanged: no resizing, cropping or recolouring. If a generation fails or
ignores the template, retry it once, then continue with the next one. Do not write any code for the game.
When finished, print the list of files written.
