# LLORONA: var_no_legs

You produce sprite sheets for a 2D retro arcade fighting game. Use your image generation tool for every image below.

## Character
La Llorona, the Mexican weeping ghost woman, an ORIGINAL character design: tall and gaunt, pale grey-blue skin, long soaking-wet black hair falling down her back and partly over one side of her face, sunken dark eyes with tears of blood running down her cheeks, mouth open in a mournful cry. She wears a long flowing white Mexican dress, huipil style, with colourful embroidered flowers (red, pink, yellow, blue, green) on the sleeves, chest and hem, soaked, torn and stained with blood at the hem, plus a tattered white lace veil on her head. Barefoot. Her hands end in long sharp black claws. Graceful, floating, eerie body language; the wet dress and hair flow with every move.

**Mutilated state for this whole batch:** BOTH legs have been cut off at the hips: only the torso remains, dragging bloody guts behind, crawling on the floor with the arms and still gripping the weapon. The torso rests ON the green baseline.

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

### `no_legs/model.png`
FIRST create this reference: a single full-body neutral fighting stance of the character in this mutilated state, facing right, on a flat magenta (#FF00FF) background, edited from `model_sheet.png` so the design matches exactly. Use it as the reference for every strip below.

### `no_legs/idle.png`
Template: `templates/template_4.png` (4 frames). Reference: `no_legs/model.png`.
Torso propped up on both arms on the floor, breathing, guts trailing, claws in the right hand.

### `no_legs/crawl_forward.png`
Template: `templates/template_6.png` (6 frames). Reference: `no_legs/model.png`.
Crawling FORWARD on the arms dragging the torso and guts, crawl-in-place: alternate arm reaches, pulling the body, 6-phase loop.

### `no_legs/crawl_back.png`
Template: `templates/template_6.png` (6 frames). Reference: `no_legs/model.png`.
Crawling BACKWARDS while facing right, crawl-in-place, 6-phase loop.

### `no_legs/slash_low.png`
Template: `templates/template_5.png` (5 frames). Reference: `no_legs/model.png`.
Low claws swipe at ankle height from the floor: 1 wind up, 2 swing, 3 fully extended, 4 follow through, 5 recover.

### `no_legs/lunge.png`
Template: `templates/template_4.png` (4 frames). Reference: `no_legs/model.png`.
Lunging forward from the floor with the claws thrust: 1 coil, 2 launch, 3 fully extended, 4 land.

### `no_legs/hurt.png`
Template: `templates/template_4.png` (4 frames). Reference: `no_legs/model.png`.
Torso on the floor recoiling from hits: 1 head snaps back, 2 recoil, 3 flinch, 4 recover.

### `no_legs/dizzy.png`
Template: `templates/template_4.png` (4 frames). Reference: `no_legs/model.png`.
Torso on the floor wobbling, about to collapse, loop.

### `no_legs/ko.png`
Template: `templates/template_4.png` (4 frames). Reference: `no_legs/model.png`.
Torso collapsing face down in a pool of blood: 1 sagging, 2 falling, 3 down, 4 motionless.

## Output
Skip any image whose output file already exists. For every new image, copy the ORIGINAL generated PNG to the exact
path given (create folders as needed). Copy the exact file path the image tool returned for THAT generation; never
pick "the newest file" in ~/.codex/generated_images, other sessions write there at the same time.
Copy it unchanged: no resizing, cropping or recolouring. If a generation fails or
ignores the template, retry it once, then continue with the next one. Do not write any code for the game.
When finished, print the list of files written.
