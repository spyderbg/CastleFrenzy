# Refined scene models

All **33 mesh-containing scenes** are registered in [models.json](models.json).
Open a `*_smooth.blend` file, select its mesh, and press **Tab** to inspect the
actual denser geometry. Subdivision has been applied, so the added vertices are
editable. The original `bin/*.bdae` exports remain untouched. They are the source
for animation, material and event records, which the Blender export copies from them,
so keep them. The COLLADA `.dae` files they were converted from have been removed.
The `*.fbx` beside each scene are Level Editor previews exported from the `.blend` files by
`python3 tools/blender/build_fbx.py`; the game does not load them.

Save the registered Blender file, close the running game, then run from the project root:

```bash
./rebuild_Linux.sh --resources-only
# For the IDE's Debug assets:
./rebuild_Linux.sh --debug --resources-only
```

Organic bodies use two levels of rounded Catmull-Clark subdivision. Rigid weapons,
architecture, gates, and flat effects use subdivision that retains their intended
shape. UVs, secondary UVs, vertex colors/alpha and interpolated skin weights survive.
Gates retain corresponding dense morph targets, exposed as shape keys in Blender.

The original Goblin uses the full animation/texture exporter. The other scenes
use **mesh export mode**: editable geometry and skin weights are written back,
while original animation channels, clip times, events, material bindings, animated
UVs, morph controllers, visibility, cameras, splines and engine extensions remain
from the binary template. Blender rig Actions and gate shape keys preview motion;
changes to those Actions/materials are not exported in mesh mode. Edit source
textures in `Data/Scenes` to change them through the resource build.

`Castle/cameras.bdae` has no mesh and is retained as supplied. Hydra/Tornado path
splines are retained; they are not subdivided as surfaces.

## Mesh counts

Vertex totals below are the actual editable Blender meshes, including hidden
morph-target reference meshes. Game vertices can be higher after UV/normal splits.

| Scene / Blender project | Vertices: original → refined | Triangles: original → refined | Original clips | Refinement |
| --- | ---: | ---: | ---: | --- |
| [Castle/castle](Castle/castle_smooth.blend) | 1,592 → **16,855** | 1,127 → 27,048 | 10 | Rounded + shape-preserving |
| [Dark_elf/arrow](Dark_elf/arrow_smooth.blend) | 3 → **19** | 1 → 24 | 1 | Shape-preserving subdivision |
| [Dark_elf/elf](Dark_elf/elf_smooth.blend) | 364 → **4,636** | 367 → 8,808 | 17 | Rounded + shape-preserving |
| [Fireball/bowl_gold](Fireball/bowl_gold_smooth.blend) | 89 → **1,066** | 74 → 1,776 | 0 | Shape-preserving subdivision |
| [Fireball/bowl_ice](Fireball/bowl_ice_smooth.blend) | 89 → **1,066** | 74 → 1,776 | 0 | Shape-preserving subdivision |
| [Fireball/burned_ground](Fireball/burned_ground_smooth.blend) | 12 → **99** | 6 → 144 | 2 | Shape-preserving subdivision |
| [Fireball/fireball_light](Fireball/fireball_light_smooth.blend) | 4 → **33** | 2 → 48 | 1 | Shape-preserving subdivision |
| [Fireball/ice](Fireball/ice_smooth.blend) | 4 → **33** | 2 → 48 | 0 | Shape-preserving subdivision |
| [Gates/arch](Gates/arch_smooth.blend) | 78 → **678** | 42 → 1,008 | 0 | Shape-preserving subdivision |
| [Gates/gate_01](Gates/gate_01_smooth.blend) | 72 → **456** | 24 → 576 | 6 | Shape-preserving subdivision |
| [Gates/gate_02](Gates/gate_02_smooth.blend) | 180 → **1,140** | 60 → 1,440 | 6 | Shape-preserving subdivision |
| [Gates/gate_03](Gates/gate_03_smooth.blend) | 180 → **1,140** | 60 → 1,440 | 6 | Shape-preserving subdivision |
| [Gates/gate_04](Gates/gate_04_smooth.blend) | 180 → **1,140** | 60 → 1,440 | 6 | Shape-preserving subdivision |
| [Goblin/goblin](Goblin/goblin_smooth.blend) | 257 → **3,432** | 282 → 6,720 | 12 | Rounded subdivision |
| [Goblin/shadow](Goblin/shadow_smooth.blend) | 4 → **33** | 2 → 48 | 0 | Shape-preserving subdivision |
| [Goblin_bomb/bomb](Goblin_bomb/bomb_smooth.blend) | 66 → **791** | 63 → 1,512 | 4 | Rounded + shape-preserving |
| [Goblin_bomb/goblin_bomb](Goblin_bomb/goblin_bomb_smooth.blend) | 229 → **3,215** | 264 → 6,336 | 11 | Rounded + shape-preserving |
| [Griffin/broken_road](Griffin/broken_road_smooth.blend) | 12 → **99** | 6 → 144 | 2 | Shape-preserving subdivision |
| [Griffin/griffin](Griffin/griffin_smooth.blend) | 1,000 → **9,515** | 716 → 17,184 | 12 | Rounded + shape-preserving |
| [Griffin/griffin_wave](Griffin/griffin_wave_smooth.blend) | 80 → **936** | 64 → 1,536 | 1 | Shape-preserving subdivision |
| [Hydra/hydra](Hydra/hydra_smooth.blend) | 130 → **2,026** | 166 → 3,888 | 32 | Rounded + shape-preserving |
| [Hydra/hydra_pool](Hydra/hydra_pool_smooth.blend) | 194 → **1,817** | 114 → 2,736 | 1 | Shape-preserving subdivision |
| [MenuShop/shop_floor](MenuShop/shop_floor_smooth.blend) | 4 → **33** | 2 → 48 | 0 | Shape-preserving subdivision |
| [Shaman/shaman](Shaman/shaman_smooth.blend) | 249 → **3,139** | 258 → 6,192 | 15 | Rounded + shape-preserving |
| [Shaman/wave_blast](Shaman/wave_blast_smooth.blend) | 8 → **66** | 4 → 96 | 1 | Shape-preserving subdivision |
| [Skeleton/skeleton](Skeleton/skeleton_smooth.blend) | 284 → **3,479** | 261 → 6,264 | 11 | Rounded + shape-preserving |
| [Spells/boulder](Spells/boulder_smooth.blend) | 142 → **2,118** | 176 → 4,224 | 2 | Rounded + shape-preserving |
| [Spells/spell_shield](Spells/spell_shield_smooth.blend) | 12 → **165** | 12 → 288 | 3 | Shape-preserving subdivision |
| [Spells/strike_plane](Spells/strike_plane_smooth.blend) | 73 → **819** | 56 → 1,344 | 1 | Shape-preserving subdivision |
| [Tornado/tornado](Tornado/tornado_smooth.blend) | 285 → **2,797** | 184 → 4,416 | 3 | Shape-preserving subdivision |
| [Troll/troll](Troll/troll_smooth.blend) | 283 → **4,197** | 339 → 8,136 | 14 | Rounded + shape-preserving |
| [Wild_boar/boar](Wild_boar/boar_smooth.blend) | 171 → **2,638** | 212 → 5,088 | 12 | Rounded + shape-preserving |
| [Wild_boar/boar_rider](Wild_boar/boar_rider_smooth.blend) | 212 → **2,621** | 212 → 5,088 | 19 | Rounded + shape-preserving |

Total: **6,542 → 72,297 editable vertices** and **5,292 → 126,864 triangles**.
The scenes retain **211 original named clips**, including source single-frame clips.

## Checks and previews

The reader inventories every source file. Mesh exports compare every original
non-mesh payload byte and pointer destination, and recheck morph topology.
Character skinning is checked at original sample times. `BdaeCheck` exercises the
native scene construction, animation binding and clips; the PNG resource build
also checks each staged model and resolves/decodes its texture references.

- [All scene previews](../../build/verification/scenes/gallery.png)
- [Per-scene refinement report](../../build/verification/scenes/refinement-report.json)
- [Blender tooling instructions](../../tools/blender/README.md)
