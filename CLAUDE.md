# Ghost vs Jason (horror-game)

Unity 6 retro 2D arcade fighter (Mortal Kombat style): Ghost vs Jason, 1P vs CPU or 2P on one keyboard, best of 3,
FINISH HIM / FATALITY with dismemberment. URP 2D renderer, new Input System. No uGUI layout, no TextMeshPro, no Animator:
everything is code + data.

## Editor / verification

- Editor: `/Applications/Unity/Hub/Editor/6000.6.4f1` (pinned in `ProjectSettings/ProjectVersion.txt`).
- Batch commands need the Editor **closed** (Unity refuses a second instance on the same project):
  ```sh
  U="/Applications/Unity/Hub/Editor/6000.6.4f1/Unity.app/Contents/MacOS/Unity"
  "$U" -batchmode -quit -projectPath "$PWD" -executeMethod GameSceneSetup.BuildBatch -logFile -   # compile + rebuild scene
  "$U" -batchmode -projectPath "$PWD" -executeMethod RetroCapture.Run -logFile -                  # smoke test + PNG captures
  ```
  `RetroCapture` renders every screen and a full CPU-vs-CPU match in edit mode, writing `Tools/out/captures/*.png`
  (raw 3x and `_crt`) and `log.txt` (phase transitions, winner, fatality). The very first capture of a run can show
  wrong textures (GPU upload timing in batch mode); it is captured twice to absorb that.
- `Assets/Scenes/Game.unity` holds only a `GameBoot` object. `GameBoot.quickFight` skips menus for combat iteration.

## Rendering model

- One world unit = one pixel. Game renders at 384x216 into a point-filtered RenderTexture (`RetroDisplay`), shown
  full screen through `Resources/Shaders/CRT.shader` (F1 toggles). Ground is y = 0, camera y = `GameConfig.CameraY`.
- All UI is SpriteRenderers under `display.uiRoot` (inside the low-res render, so it gets the CRT too).
  Sorting orders live in `Layers`. Text is `PixelText` with bitmap fonts from `Resources/UI/fonts.json`.
- Sprites are cut at runtime from atlases (`Art`, `CharacterDef`) with `Sprite.Create`, PPU 1. `PixelArtImporter`
  forces point filtering / no compression for everything under `Assets/Resources/`.
- Mirroring is `transform.localScale.x = facing`, never `SpriteRenderer.flipX` (custom `Retro/Sprite` shader).

## Code map (`Assets/Scripts/`)

- `GameBoot` → `Flow/GameFlow` (fixed 60 Hz ticks, fades, screen stack) → screens in `Flow/` (Title, Mode, Select,
  Versus, Fight, Results).
- `Combat/Match` — rounds, timer, hit/block/throw resolution, KO → FINISH HIM → FATALITY (heavy = decapitation via
  `headless` anim, others = explode into `gore` pieces). `Combat/Fighter` — state machine + physics in pixels.
  `Combat/Fx` — blood particles, persistent floor stains, gore pieces, fountains, sparks, shake.
- `Input/FighterInput` — `InputFrame`, keyboard bindings (P1 WASD+F/G/H/R, P2 arrows+K/L/;/P), `CpuInput` AI.

## Content pipeline (`Tools/pixelart/`, venv at `Tools/.venv`)

```sh
Tools/.venv/bin/python Tools/pixelart/ghost.py   # Ghost: fully procedural (rig + IK + shading), Rocky-made
Tools/.venv/bin/python Tools/pixelart/jason.py   # Jason: processes Codex art in Tools/art_src/jason/
Tools/.venv/bin/python Tools/pixelart/ui.py      # fonts, HUD, select cells, sparks, logo, fallback stage
```

- Each character is `Assets/Resources/Characters/<id>/{atlas.png, portrait.png, thumb.png, character.json}`.
  `character.json` holds sprites (cell + pivot), animations (sprite indices, ticks, hand anchors), moves (frame data
  in 60 fps ticks, hitbox in pivot space, facing right), gore pieces and hurtboxes. Add a character = add a folder and
  its id to `Characters/roster.json`; the select screen grows automatically.
- Required animation names: idle walk crouch jump punch slash kick crouch_attack air_attack block hurt knockdown
  grab_throw ko headless dizzy victory. Move ids: light heavy kick crouch_light air grab special (forward+heavy).
- Jason source art comes from `codex exec` image generation using `Tools/art_src/jason/PROMPT.md`
  (pass the prompt on stdin; `-i` swallows following arguments).
- Audio: `Resources/Audio/SFX/<key>[_n]` (variants picked at random), `Resources/Audio/Music/<key>`. Kenney CC0
  licenses in `Assets/Audio/Licenses/`. Some SFX are ElevenLabs generations (flow "Ghost vs Jason - Audio").

## Conventions

- Player-facing menu text is Spanish; arcade callouts stay English (ROUND, FIGHT!, K.O., FINISH HIM!, FATALITY).
- The bitmap fonts have no accents/Ñ (mapped to plain letters in `PixelFont.Glyph`).
- Legacy fan-art sheets in `Assets/Art/Characters/` are unused (credited to "xxultra2006xx"); keep them out of builds.
