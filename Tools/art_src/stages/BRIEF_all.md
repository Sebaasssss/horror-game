# Stages and gore chunks

You produce background art for a 2D retro arcade fighting game. Use your image generation tool for every image below.

## Style
Detailed 16-bit arcade fighting-game stage pixel art (Mortal Kombat II / Samurai Shodown era): crisp hard pixels, no blur, limited moody palette, night-time horror atmosphere with fog and moonlight, readable silhouettes. Side view, horizon around the middle.

## Images (in this order)

### `camp/far.png`
No template, wide 3:1 panorama (2172x724). FAR layer of the stage 'CAMP BLOOD': Camp Crystal Lake at night: black pine forest, a misty lake with the full moon reflected, wooden summer-camp cabins with one lit window, a wooden dock with a bloody canoe, a hanging camp sign, fog, fireflies, a bloody axe stuck in a stump. Only the sky and the distant background (horizon at about 55% height), fully painted, no characters.

### `camp/mid.png`
No template, wide 3:1 panorama (2172x724), same scene, same palette and perspective as `camp/far.png` (pass it as reference). MAIN layer: the foreground architecture and props of 'CAMP BLOOD' seen from the side at human eye level, like a 2D fighting game stage. The bottom 13% of the image is a flat walkable ground strip where the fighters stand. Everything above the scenery silhouette (the sky) is flat magenta (#FF00FF) so it can be cut out. Keep the centre of the image open (fighters stand there). No characters.

### `utez/far.png`
No template, wide 3:1 panorama (2172x724). FAR layer of the stage 'UTEZ': The entrance gate of a Mexican technological university (Universidad Tecnologica Emiliano Zapata, UTEZ) turned into a horror scene at night: tall terracotta-red cylindrical concrete pillars, a long cream concrete beam across the top with the sign 'UNIVERSIDAD TECNOLOGICA EMILIANO ZAPATA', red metal bar gates half open and bent, a round red guard booth with the 'UTEZ' logo and one flickering lamp, palm trees and wilted bougainvillea, blood smeared on the pillars, a broken street light, fog, full moon, abandoned and ominous. Only the sky and the distant background (horizon at about 55% height), fully painted, no characters.

### `utez/mid.png`
No template, wide 3:1 panorama (2172x724), same scene, same palette and perspective as `utez/far.png` (pass it as reference). MAIN layer: the foreground architecture and props of 'UTEZ' seen from the side at human eye level, like a 2D fighting game stage. The bottom 13% of the image is a flat walkable ground strip where the fighters stand. Everything above the scenery silhouette (the sky) is flat magenta (#FF00FF) so it can be cut out. Keep the centre of the image open (fighters stand there). No characters.

### `woodsboro/far.png`
No template, wide 3:1 panorama (2172x724). FAR layer of the stage 'WOODSBORO': A quiet American suburban street at night on Halloween: a two-storey wooden house with a porch and lit windows, a phone ringing inside, jack-o-lanterns on the steps, police tape, a swing hanging from an old oak tree, picket fence, parked car with headlights on, fallen leaves, fog and a full moon. Only the sky and the distant background (horizon at about 55% height), fully painted, no characters.

### `woodsboro/mid.png`
No template, wide 3:1 panorama (2172x724), same scene, same palette and perspective as `woodsboro/far.png` (pass it as reference). MAIN layer: the foreground architecture and props of 'WOODSBORO' seen from the side at human eye level, like a 2D fighting game stage. The bottom 13% of the image is a flat walkable ground strip where the fighters stand. Everything above the scenery silhouette (the sky) is flat magenta (#FF00FF) so it can be cut out. Keep the centre of the image open (fighters stand there). No characters.

### `gibs.png`
Template: `templates/template_8.png` (8 cells). Small gore chunks, one centred in each cell, sized like a fist: 1 eyeball with optic nerve, 2 broken bone, 3 rib fragment, 4 chunk of raw flesh, 5 heart, 6 teeth with jaw piece, 7 brain chunk, 8 severed finger. Bloody and glistening.

## Output
Skip any image whose output file already exists. For every new image, copy the ORIGINAL generated PNG to the exact
path given (create folders as needed). Copy the exact file path the image tool returned for THAT generation; never
pick "the newest file" in ~/.codex/generated_images, other sessions write there at the same time.
Copy it unchanged: no resizing, cropping or recolouring. If a generation fails or
ignores the template, retry it once, then continue with the next one. Do not write any code for the game.
When finished, print the list of files written.
