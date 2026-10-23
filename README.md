# Ghost vs Jason — Halloween Kombat

Juego de peleas retro en pixel art (estilo Mortal Kombat) hecho en Unity para la campaña de becados de videojuegos
de Halloween. Gore pixel art, desmembramiento, FINISH HIM / FATALITY, filtro CRT de maquinita y 6 escenarios.

## Jugar

- **En el navegador:** https://ander-tsx.github.io/horror-game/ (no hay que instalar nada).
- **Windows / Mac:** descarga el zip de la última versión en
  [Releases](https://github.com/Ander-tsx/horror-game/releases), descomprime y abre `GhostVsJason`.
  - En Mac, la primera vez: clic derecho sobre la app → *Abrir* (no está firmada).

## Controles

| Acción | Jugador 1 | Jugador 2 |
|---|---|---|
| Moverse / saltar / agacharse | `A` `D` / `W` / `S` | Flechas |
| Golpe | `F` | `K` |
| Golpe fuerte | `G` | `L` |
| Patada | `H` | `;` (Ñ en teclado español) |
| Agarre | `R` | `P` |
| Especial | Adelante + `G` | Adelante + `L` |
| Patada giratoria | Atrás + `H` | Atrás + `;` |
| Bloquear | Mantener atrás (abajo + atrás = bloqueo bajo) | igual |

- Combos: `F F F` (jab, cross, uppercut), `G G`, `H H`.
- Al bajar la vida se pierden extremidades: sin un brazo, sin brazos, en una pierna o arrastrándose sin piernas.
- **FINISH HIM:** fuerte = decapitación, patada = en trozos, golpe o agarre = explota en mil pedazos.
- `Esc` pausa, `F1` activa o desactiva el filtro CRT, `Enter` o `F` para confirmar en menús.

## Abrir el proyecto

1. Instala **Unity 6000.6.4f1** con Unity Hub (con soporte WebGL / Windows / Mac si quieres compilar).
2. `git clone https://github.com/Ander-tsx/horror-game.git` y ábrelo desde Unity Hub.
3. Abre `Assets/Scenes/Game.unity` y dale Play. En el objeto `Game`, `quickFight` salta los menús.

Compilar desde la terminal (con el editor cerrado):

```sh
"/Applications/Unity/Hub/Editor/6000.6.4f1/Unity.app/Contents/MacOS/Unity" -batchmode -quit -projectPath . \
  -executeMethod GameBuilds.BuildAll
```

## Arte y pipeline

Los personajes y escenarios se generaron con generación de imágenes de Codex sobre plantillas
(`Tools/art_src/templates`) y se procesaron con los scripts de `Tools/pixelart` (ver `CLAUDE.md`). Las imágenes
fuente originales (cientos de MB) no están en el repo: se descargan como `codex-source-art.zip` desde Releases y se
descomprimen en `Tools/art_src/` para volver a procesarlas.

## Créditos

- Sonidos de impacto e interfaz: [Kenney](https://kenney.nl) (CC0), ver `Assets/Audio/Licenses/`.
- Algunos efectos y la voz de "FATALITY": ElevenLabs. Música chiptune sintetizada en `Tools/audio/chiptune.py`.
- Ghost y Jason son personajes inspirados en franquicias de terror existentes; este es un proyecto de fans sin fines
  comerciales.
