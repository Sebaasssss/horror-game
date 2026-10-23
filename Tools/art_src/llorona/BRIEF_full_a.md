# LLORONA: full_a

You produce sprite sheets for a 2D retro arcade fighting game. Use your image generation tool for every image below.

## Character
La Llorona, the Mexican weeping ghost woman, an ORIGINAL character design: tall and gaunt, pale grey-blue skin, long soaking-wet black hair falling down her back and partly over one side of her face, sunken dark eyes with tears of blood running down her cheeks, mouth open in a mournful cry. She wears a long flowing white Mexican dress, huipil style, with colourful embroidered flowers (red, pink, yellow, blue, green) on the sleeves, chest and hem, soaked, torn and stained with blood at the hem, plus a tattered white lace veil on her head. Barefoot. Her hands end in long sharp black claws. Graceful, floating, eerie body language; the wet dress and hair flow with every move.

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

### `full/idle.png`
Template: `templates/template_6.png` (6 frames). Reference: `model_sheet.png`.
Fighting stance breathing loop, walk-in-place spot: 1 neutral guard, 2 shoulders rise slightly, 3 top of the breath, 4 shoulders settle, 5 slight forward lean, 6 back to neutral (loops into 1).

### `full/walk_forward.png`
Template: `templates/template_8.png` (8 frames). Reference: `model_sheet.png`.
Walking FORWARD toward the right, walk-in-place (do not drift inside the cell): 1 right foot forward heel contact, 2 weight down on the front leg, 3 left leg passing, 4 body up on the toes, 5 left foot forward heel contact, 6 weight down, 7 right leg passing, 8 body up. Torso bobs slightly, the free arm swings opposite, claws kept ready in front.

### `full/walk_back.png`
Template: `templates/template_8.png` (8 frames). Reference: `model_sheet.png`.
Walking BACKWARDS: still FACING RIGHT toward the opponent but stepping back to the left, walk-in-place, guarded posture with claws held in front. Same 8 phases as a walk cycle in reverse: rear foot reaches back, weight shifts back, front foot drags back, recover. Loop.

### `full/crouch.png`
Template: `templates/template_4.png` (4 frames). Reference: `model_sheet.png`.
1 knees starting to bend, 2 half crouch, 3 full low crouch guard with claws ready, 4 same full crouch (slight breathing variation).

### `full/jump_up.png`
Template: `templates/template_4.png` (4 frames). Reference: `model_sheet.png`.
Vertical jump, body kept at the same height in the cell (the game moves it): 1 deep crouch preparing, 2 launching with legs extended, 3 knees tucked at the peak, 4 descending with legs reaching down.

### `full/jump_forward.png`
Template: `templates/template_6.png` (6 frames). Reference: `model_sheet.png`.
Forward somersault jump, body centred in the cell: 1 crouch takeoff leaning forward, 2 leaving the ground, 3 tucked ball rotating forward a quarter turn, 4 fully upside down tucked, 5 rotating out of the flip, 6 legs extended ready to land.

### `full/punch_1.png`
Template: `templates/template_4.png` (4 frames). Reference: `model_sheet.png`.
Fast jab with the LEFT free fist (first hit of a combo): 1 guard chamber, 2 fist half way, 3 arm fully extended at head height, 4 retracting.

### `full/punch_2.png`
Template: `templates/template_4.png` (4 frames). Reference: `model_sheet.png`.
Follow-up straight cross with the right fist still gripping the claws handle (second combo hit): 1 hips twisting, 2 extending, 3 full extension at chest height, 4 retracting.

### `full/uppercut.png`
Template: `templates/template_5.png` (5 frames). Reference: `model_sheet.png`.
Rising uppercut launcher (third combo hit): 1 dip low, 2 fist low near the hip, 3 driving upward, 4 fist high above the head with the body stretched up on the toes, 5 recover.

### `full/slash.png`
Template: `templates/template_6.png` (6 frames). Reference: `model_sheet.png`.
Heavy overhead claws chop: 1 raising the claws behind the head, 2 claws fully back over the shoulder, 3 swinging down in front at head height, 4 impact low in front at waist height, 5 follow through near the floor, 6 recover to stance.

### `full/slash_side.png`
Template: `templates/template_5.png` (5 frames). Reference: `model_sheet.png`.
Wide horizontal claws slash at chest height: 1 claws pulled back behind the body, 2 starting the swing, 3 claws fully extended straight in front (arm straight), 4 follow-through across the body, 5 recover.

### `full/kick.png`
Template: `templates/template_5.png` (5 frames). Reference: `model_sheet.png`.
Front stomp kick: 1 knee up, 2 leg extending, 3 leg fully extended horizontally at stomach height with the sole forward, 4 retracting the knee, 5 foot down.

### `full/roundhouse.png`
Template: `templates/template_6.png` (6 frames). Reference: `model_sheet.png`.
Spinning roundhouse kick at head height: 1 pivot on the front foot, 2 turning the back toward the viewer, 3 leg swinging around, 4 leg fully extended at head height, 5 leg coming down, 6 recover.

### `full/sweep.png`
Template: `templates/template_5.png` (5 frames). Reference: `model_sheet.png`.
Crouching leg sweep at ankle height: 1 drop low on one knee, 2 leg extending along the floor, 3 leg fully extended along the floor in front, 4 sweeping back, 5 crouch recover.

### `full/crouch_attack.png`
Template: `templates/template_4.png` (4 frames). Reference: `model_sheet.png`.
Crouching claws thrust at shin height: 1 crouch, 2 thrusting, 3 claws fully extended low in front, 4 recover to crouch.

## Output
Skip any image whose output file already exists. For every new image, copy the ORIGINAL generated PNG to the exact
path given (create folders as needed). Copy the exact file path the image tool returned for THAT generation; never
pick "the newest file" in ~/.codex/generated_images, other sessions write there at the same time.
Copy it unchanged: no resizing, cropping or recolouring. If a generation fails or
ignores the template, retry it once, then continue with the next one. Do not write any code for the game.
When finished, print the list of files written.
