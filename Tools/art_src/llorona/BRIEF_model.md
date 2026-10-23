# LLORONA: model sheet

You produce sprite sheets for a 2D retro arcade fighting game. Use your image generation tool for every image below.

## Character
La Llorona, the Mexican weeping ghost woman, an ORIGINAL character design: tall and gaunt, pale grey-blue skin, long soaking-wet black hair falling down her back and partly over one side of her face, sunken dark eyes with tears of blood running down her cheeks, mouth open in a mournful cry. She wears a long flowing white Mexican dress, huipil style, with colourful embroidered flowers (red, pink, yellow, blue, green) on the sleeves, chest and hem, soaked, torn and stained with blood at the hem, plus a tattered white lace veil on her head. Barefoot. Her hands end in long sharp black claws. Graceful, floating, eerie body language; the wet dress and hair flow with every move.

## Reference images (in this directory)
- none yet: this batch creates `model_sheet.png`.

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

### `model_sheet.png`
No template: one full-body neutral fighting stance, facing right, holding the claws, flat magenta (#FF00FF) background. This becomes the reference for every other image, so make it clean and detailed.

## Output
Skip any image whose output file already exists. For every new image, copy the ORIGINAL generated PNG to the exact
path given (create folders as needed). Copy the exact file path the image tool returned for THAT generation; never
pick "the newest file" in ~/.codex/generated_images, other sessions write there at the same time.
Copy it unchanged: no resizing, cropping or recolouring. If a generation fails or
ignores the template, retry it once, then continue with the next one. Do not write any code for the game.
When finished, print the list of files written.
