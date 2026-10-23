# JASON: full_b

You produce sprite sheets for a 2D retro arcade fighting game. Use your image generation tool for every image below.

## Character
Jason Voorhees-style slasher: very tall, broad and hulking. Off-white hockey mask with red chevron marks and breathing holes, bald scarred head behind the mask straps, worn torn dark olive-green work jacket over a dark shirt, dark grey-brown trousers, heavy brown boots, black gloves. Long rusty bloodstained machete in his RIGHT hand. Slow, heavy, unstoppable body language.

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

### `full/air_attack.png`
Template: `templates/template_4.png` (4 frames). Reference: `model_sheet.png`.
Airborne downward machete strike, legs tucked: 1 machete raised mid-air, 2 swinging down diagonally, 3 machete fully extended downward in front, 4 held.

### `full/air_kick.png`
Template: `templates/template_4.png` (4 frames). Reference: `model_sheet.png`.
Flying jump kick: 1 tucked, 2 extending, 3 leg extended diagonally downward forward, 4 held.

### `full/special.png`
Template: `templates/template_6.png` (6 frames). Reference: `model_sheet.png`.
MACHETE RUSH: lowered-shoulder charge that ends in a lunging machete slash: 1 crouch leaning forward, 2 dashing low, 3 dashing with the body almost horizontal, 4 attack fully extended, 5 overextended follow-through, 6 recover.

### `full/block.png`
Template: `templates/template_4.png` (4 frames). Reference: `model_sheet.png`.
1 standing guard raising the forearms with the machete crossed in front of the face, 2 standing guard holding, 3 crouching guard, 4 crouching guard holding.

### `full/hurt.png`
Template: `templates/template_6.png` (6 frames). Reference: `model_sheet.png`.
1 head snapped back from a face hit, 2 recoiling further, 3 doubled over from a gut hit, 4 doubled over deeper, 5 crouching hit flinch, 6 crouching hit flinch deeper. Blood flies from the hits.

### `full/knockdown.png`
Template: `templates/template_5.png` (5 frames). Reference: `model_sheet.png`.
Knocked down, the body stays inside the cell: 1 blasted off the feet flying backward to the LEFT with the body tilted back, 2 airborne nearly horizontal, 3 falling back first, 4 lying flat on the back on the baseline, 5 lying flat (slight variation).

### `full/getup.png`
Template: `templates/template_4.png` (4 frames). Reference: `model_sheet.png`.
1 lying propped on one elbow, 2 rolling onto one knee, 3 rising, 4 back in the fighting stance.

### `full/grab_throw.png`
Template: `templates/template_6.png` (6 frames). Reference: `model_sheet.png`.
Throw. Do NOT draw the opponent: 1 reaching forward with the free hand, 2 clutching with the fist closed at chest height in front (holding someone by the throat), 3 lifting high overhead, 4 holding overhead, 5 hurling forward and down, 6 recover.

### `full/thrown.png`
Template: `templates/template_4.png` (4 frames). Reference: `model_sheet.png`.
Being thrown by an invisible opponent: 1 grabbed by the neck with the feet dangling, 2 lifted overhead horizontally, 3 flying upside down, 4 slammed into the ground on the baseline.

### `full/dizzy.png`
Template: `templates/template_4.png` (4 frames). Reference: `model_sheet.png`.
Stunned and wobbling loop (the FINISH HIM state): arms limp, head lolling, knees weak, swaying.

### `full/ko.png`
Template: `templates/template_5.png` (5 frames). Reference: `model_sheet.png`.
1 staggering backward clutching the chest, 2 knees buckling, 3 dropping to the knees, 4 falling face forward, 5 lying face down motionless in a pool of blood on the baseline.

### `full/victory.png`
Template: `templates/template_6.png` (6 frames). Reference: `model_sheet.png`.
Triumph sequence: slowly raising the machete overhead, menacing pose, blood dripping from the machete.

### `full/intro.png`
Template: `templates/template_4.png` (4 frames). Reference: `model_sheet.png`.
Round intro taunt: tilting the head, pointing the machete at the opponent, menacing.

### `full/headless.png`
Template: `templates/template_5.png` (5 frames). Reference: `model_sheet.png`.
DECAPITATED body: NO HEAD, a ragged bloody neck stump spurting a tall fountain of blood: 1 standing rigid, 2 staggering, 3 knees buckling, 4 collapsing, 5 lying in a pool of blood.

### `full/bisected.png`
Template: `templates/template_5.png` (5 frames). Reference: `model_sheet.png`.
Body CUT IN HALF at the waist: 1 upper torso sliding off the legs with guts spilling, 2 upper half falling, 3 legs still standing gushing blood, 4 legs collapsing, 5 both halves on the ground in a pool of blood (draw both halves in the cell).

### `full/gore_parts.png`
Template: `templates/template_8.png` (8 frames). Reference: `model_sheet.png`.
Separate dismembered body parts of this character, one per cell, centred, no full body: 1 severed head with mask and bloody neck, 2 ripped upper torso with exposed ribs, 3 severed LEFT arm, 4 severed RIGHT arm still gripping the machete, 5 severed LEFT leg, 6 severed RIGHT leg, 7 pile of intestines, 8 bloody spine with pelvis. Every cut is ragged and bloody with exposed bone.

### `portrait.png`
No template: a square 1:1 bust portrait for the character select screen, three-quarter view facing right, dramatic red rim light, blood spatter, flat magenta (#FF00FF) background. Reference: `model_sheet.png`.

## Output
Skip any image whose output file already exists. For every new image, copy the ORIGINAL generated PNG to the exact
path given (create folders as needed). Copy the exact file path the image tool returned for THAT generation; never
pick "the newest file" in ~/.codex/generated_images, other sessions write there at the same time.
Copy it unchanged: no resizing, cropping or recolouring. If a generation fails or
ignores the template, retry it once, then continue with the next one. Do not write any code for the game.
When finished, print the list of files written.
