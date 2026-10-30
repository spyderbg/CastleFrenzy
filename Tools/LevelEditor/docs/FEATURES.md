# Native E/R editor coverage

Reference: `Src/Editor/GS_Editor.cpp`, `EditorUI.cpp`, `Src/Game/LevelInfo.cpp`, `Playfield.cpp`, and `config/{units,models}.xml`.

| Native feature | Browser implementation |
| --- | --- |
| Levels: new/delete/up/down; last level is endless | Levels sidebar; same positional endless convention |
| Days: new/cut/up/down/copy/paste | Days/segments sidebar, deep-copy clipboard |
| Day end time, all 19 challenges | Day settings, exact runtime challenge strings |
| Endless easy/medium/hard segments and grouped labels | Difficulty selector and difficulty-relative numbering |
| Wave new/up/down/delete/clone | Wave toolbar; deep clone includes variants/enemies |
| Name (15 chars), packed, time start/duration, interval, repeats | Wave inspector; native ranges validated on save |
| Left/right/middle drag time values, wheel repeat count | Wave timeline; pointer capture and zoom |
| Variant add/delete and intro camera | Variant sections |
| Enemy type add/delete, count bounds and unit icons | Enemy rows; inherited HideInEditor and IconRect |
| Current-day realized wave timeline | Actual native spawn states and extra spawns over the bridge |
| Gate, hydra, fireball, iceball, griffin starting levels/shop | Weapons tab; same XML indices |
| Spell bought and shop spell selection | Spell section |
| Model list, load scale, texture overrides | Blender FBX preview with inherited model configuration |
| Animation list, loop, step interpolation | Named clip selector, loop/interpolation controls |
| Camera orbit/wheel zoom and ground grid | OrbitControls, fit camera, grid, plus pan |
| Play from Waves / Weapons | Save then native selected-day / first-day launch |
| DIE! bypasses invulnerability | Native damageGate command with restored Invulnerable flag |
| Save, Exit/Escape saves and returns to game | Save, Save & exit; running game resumes and detaches |
| Game paused while editing | Automatic pause on browser focus (optional), Pause/Resume controls; E/R built-in editor still available in game |

The browser adds history, backups, stale-save rejection and validation. It previews Blender-exported FBX files using Three.js rather than rendering runtime BDAE in Irrlicht, so engine-specific materials, UV scrolling and export differences can affect appearance. Live gameplay always runs in the native engine. The native model tab's unused `m_showDummies` member has no exposed control and adds no editing behavior.
