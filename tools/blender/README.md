# Editing Castle Frenzy models in Blender

Blender projects use **`.blend`**. This repository includes a native importer,
exporter, optional export-menu add-on, and Linux resource-build integration.
They have been exercised with **Blender 5.2.2 LTS** and the game's C++ loader.

## All other scene models

All 33 scenes containing meshes now have a registered `*_smooth.blend` project.
See [Data/Scenes/README.md](../../Data/Scenes/README.md) for the complete list,
mesh counts, preview gallery and editing workflow. Organic bodies use applied
rounded subdivision; architecture, morphing gates, rigid parts and flat effects
gain geometry while retaining their shapes. Cameras and path splines are preserved.

The Goblin retains the full exporter described below. Other scene projects use
**mesh export mode**: geometry, UVs, colors and skin weights can change, while
original animation, clips/events, materials, morph controllers and engine metadata
remain in the template. Their rig Actions and gate shape keys provide previews;
Action/material edits are not exported by this mode. The add-on and build choose
the correct mode from the project automatically.

```bash
blender Data/Scenes/Troll/troll_smooth.blend
./rebuild_Linux.sh --resources-only
```

`scene_tools.py --batch` creates missing scene projects and refuses to overwrite
artist files. `bdae_scene.py` verifies that original non-mesh records are unchanged
after relocation. `preview_scenes.py` renders the projects with Blender's CPU renderer.

## Preview models in the Level Editor

The Level Editor's Models tab loads FBX files, not BDAE. Each registered scene has a
committed `Data/Scenes/<scene>/<model>.fbx` beside its `.blend` folder. After changing a
registered `.blend`, regenerate them (Blender 5.2+, set `BLENDER_BIN` if it is not `blender`):

```bash
python3 tools/blender/build_fbx.py                       # every scene in models.json
python3 tools/blender/build_fbx.py --only Goblin/goblin.bdae
```

`export_fbx.py` runs inside Blender and changes only that throwaway session. It skips the
long `__ALL_SOURCE_ANIMATION` timeline (each per-clip Action becomes one FBX take, named after
the game clip), bakes the driver-controlled gate shape keys into the takes, links each image
texture straight to Base Color, drops hidden morph-target meshes and degenerate planes, and
renames meshes that share a name with a bone. Scale is 1 Blender unit = 1 game unit, Y up.
Material UV scrolling cannot be stored in FBX, so the preview does not show it. Blender's snap
package exits silently if its output is redirected to a regular file; pipe it (`| tee log`).

## Edit the Goblin and rebuild

From the project root:

```bash
blender Data/Scenes/Goblin/goblin_smooth.blend
# Save your edits in Blender, then:
./rebuild_Linux.sh --resources-only
./build/linux-release/bin/FortressDefender
```

Open **`goblin_smooth.blend`** for the current improved model. It contains
**3,432 actual editable vertices, 3,360 faces and 6,720 triangles**. Two levels of
Catmull-Clark subdivision are applied to the mesh, and body creases are softened
to round the shoulders, limbs and boots. Weapon edges remain sharp. Press Tab
with `goblin` selected to inspect the dense mesh; only the Armature modifier remains.
The exporter writes 3,754 vertices after UV/normal splits.

All original **12 animation clips**, 25 rig bones, 23 weighted joints, UVs, packed
diffuse texture, and `hit`/`blood` events are retained. The refinement script checks
that animation curves and event metadata are identical before and after editing.

The earlier `goblin_edit.blend` remains available: it has 167 editable cage vertices
and a level-one subdivision modifier (882 evaluated vertices / 1,680 triangles).
It is no longer the registered build source. If that file is already open in
Blender, open `goblin_smooth.blend` to see the updated mesh.

`Data/Scenes/Goblin/goblin.blend` is the original conversion, with 257 vertices
and 282 triangles. All three projects contain the original 25-bone armature, 23 weighted
joints, packed diffuse image, and animation Actions. Original `.bdae`
files are unchanged. Both earlier Blender projects remain available.

The resource build reads `Data/Scenes/models.json`. Paths are relative to that file:

```json
{
  "version": 1,
  "assets": [{
    "blend": "Goblin/goblin_smooth.blend",
    "template": "bin/Goblin/goblin.bdae",
    "output": "Goblin/goblin.bdae",
    "rig": "Goblin_Rig"
  }]
}
```

Save the registered `.blend` to publish your edits into the next local build.
Exports are written below `build/linux-release/blender/` (or `linux-debug`), checked
by the native `BdaeCheck` executable, then staged under that build's `assets/Scenes/`.
Other models continue to use their existing exports. Changed source, template,
exporter, external images, or linked libraries invalidate the model cache, as do
missing or changed export files. Unchanged exports are reused and checked again.
`BLENDER_BIN=/path/to/blender ./rebuild_Linux.sh --resources-only` selects a binary.

The resource build and manual exports write PNG diffuse images. Project-owned
TGA files have been migrated to PNG; PVR encoding and runtime textures are no
longer supported. Original authoring variants that differ from active runtime
PNGs are preserved under `Data/TextureSources`, outside the staged game packs.

## Work with animations and events

1. Select **Goblin_Rig** and open **Dope Sheet → Action Editor**.
2. Choose an Action, for example `gob_vihren_walk_03`. Each named clip begins at
   frame 1; the original FPS is 30. Set playback bounds to the Action's range.
3. Edit keyframes, poses, or constraints, then save. All 12 required named Actions
   export automatically. Preserve their names because game code selects them.
4. For a new Action, enable **Game Export → Include as additional clip** with the
   add-on, or set the Action custom property `bdae_export` to `True`. New clips are
   exported, but game code must select them before they affect gameplay.

The exporter bakes poses at scene FPS and retains source key times within clips.
Changing an Action's frame range changes its exported duration; later clips move
to keep the timeline ordered. Use linear bone animation and the default Armature
skinning mode, with Preserve Volume disabled, to match this engine.

Action markers carry engine events. Move a marker to change its time, delete it
to remove the event, or add one with the desired event name. Unmoved source markers
keep their exact original millisecond times from Action metadata, even when their
display frames are rounded. The default events are `hit` at 666 ms and `blood` at
3000 ms on the original combined timeline.

`Goblin__ALL_SOURCE_ANIMATION` preserves the complete original timeline and supplies
the gaps between named clips. Edit named Actions for gameplay clips. The original
NLA layout is muted so the selected Action can play independently; export samples
one Action at a time and restores the active Action and frame afterward.

## Export manually or use the Blender menu

The CLI needs no add-on and uses Blender's Python:

```bash
blender --background --factory-startup Data/Scenes/Goblin/goblin_smooth.blend \
  --python-exit-code 1 --python tools/blender/export_bdae.py -- \
  --template Data/Scenes/bin/Goblin/goblin.bdae \
  --output build/manual-models/Goblin/goblin.bdae --texture-format png
```

This produces `.bdae`, a diffuse image, and `.export.json` with provenance and
verification results. Export to a generated directory; the original template is
required and cannot be overwritten. If the Snap launcher has trouble starting,
the binary installed on this machine is `/snap/blender/current/blender`.

To enable **File → Export → Castle Frenzy (.bdae)**:

```bash
python3 tools/blender/package_addon.py
```

In Blender, open **Edit → Preferences → Add-ons → Install from Disk**, select
`build/packages/castle_bdae_tools.zip`, and enable **Castle Frenzy BDAE Export**.
The 3D View sidebar gains a **Game Export** tab. The template and output paths
default from the imported rig. Installation is optional; the automatic resource
build calls the same exporter without changing your Blender preferences.

## Supported editing profile

The tools currently support the Goblin's single-skin, single-diffuse-texture
character profile in this project's little-endian 32-bit `0,0,0,325` BRES layout.
They serialize new geometry, normals, UVs, weights, inverse binds, skeleton nodes,
animation tracks, clips and events. Opaque material and engine extensions remain
from the original template. This is not a universal BDAE or PBR exporter.

- Edit topology, UVs, skin weights, rest bones and poses, or replace the mesh.
  Additional meshes must use the same rig and diffuse image. New bones are allowed.
- Retain the original bones and their `bdae_id`/`bdae_sid` custom properties; these
  preserve the node identities expected by the game. Blender display names may change.
- Put modeling modifiers **before a single final Armature modifier**. All exported
  vertices need bone weights. The exporter normalizes weights and verifies that
  baked engine skinning matches Blender's evaluated deformation.
- Use one image connected directly to Principled BSDF Base Color. Texture-paint
  edits are exported, including packed images. Procedural materials, multiple
  diffuse images, PBR maps, morph animation and vertex-color painting are outside
  this profile. Exported vertex colors are white, matching the original Goblin.
- Keep exported geometry within **32,768 vertices**, including UV/normal splits,
  and at most **255 weighted joints**. Avoid shear and Preserve Volume skinning.
  Unsupported cases raise errors rather than silently dropping required content.

Y-up/Z-up conversion, the Irrlicht quaternion convention, bind matrices and the
ColladaMax UV origin are handled by the scripts. Animation is baked: Blender's
continuous interpolation between samples can differ slightly from engine SLERP.
Increasing scene FPS adds samples when necessary.

## Import the original BDAE again

```bash
blender --background --factory-startup --python-exit-code 1 \
  --python tools/blender/import_bdae.py -- \
  --input Build/bin/Scenes/Goblin/goblin.bdae \
  --output build/imports/Goblin/goblin.blend
```

Choose an unused filename. The importer refuses to overwrite a project and writes
an adjacent `.import.json`. It creates editable geometry and a real rig, persistent
named Action assets, exact event metadata, and a packed diffuse image.

The project no longer contains COLLADA `.dae` files. The custom BDAE importer reads
the game's `.bdae` files directly, so Blender 5's removal of native COLLADA support
does not matter here.

## Verification

The original import was reopened and checked at 958 Action sample times across
all 25 bones and 257 vertices; maximum skin-position error was about 0.00000575
units. Export checks compare evaluated Blender deformation at every baked time
against the serialized skin and compare reopened binary node transforms against
the sampled poses. The native C++ check exercises real BRES relocation, image
decoding, scene construction, animation binding and every clip.

```bash
python3 tools/blender/test_bdae_reader.py
python3 tools/blender/test_bdae_writer.py
python3 tools/blender/test_bdae_scene.py
python3 tools/blender/test_build_models.py

blender --background Data/Scenes/Goblin/goblin_smooth.blend --python-exit-code 1 \
  --python tools/blender/test_export_roundtrip.py

build/linux-release/bin/BdaeCheck \
  build/verification/blender-export/edited-test/goblin.bdae
```

The Blender integration test exports the dense editable topology, a newly weighted bone,
an additional animated clip, a changed head pose, and a painted texture pixel.
It writes only below `build/verification/`. `verify_bdae.py` can also reopen an
imported project and render all 12 clip midpoint poses with `--previews DIRECTORY`.

The first modifier-based refinement was also checked in the running Linux game.
Its earlier preview evidence remains available:

- `build/verification/blender-export/animation-overview.png`: all 12 clip poses after export and reimport.
- `build/verification/blender-export/game-goblin-tutorial.png`: the first refinement rendered by the game.

The latest dense-mesh comparison renders use identical cameras, lighting and poses:

- `build/verification/goblin-smoothing/mesh-comparison.png`: original, previous and current clay/wireframe views.
- `build/verification/goblin-smoothing/textured-comparison.png`: the same three meshes with the original diffuse image.

To reproduce the dense mesh without overwriting an artist project:

```bash
blender --background --factory-startup Data/Scenes/Goblin/goblin_edit.blend \
  --python-exit-code 1 --python tools/blender/refine_goblin.py -- \
  --output build/imports/Goblin/goblin_smooth.blend
```

The adjacent `goblin_smooth.mesh.json` records mesh counts and the preserved
animation signature. The script refuses to overwrite an existing `.blend`.
