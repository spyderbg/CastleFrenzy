# AuroraMX

A local browser sprite editor for Castle Frenzy, implemented in TypeScript with a Vue 3 frontend and Fastify backend. Its module/frame/animation lists, composition lists, preview, selection bank, menus and bottom editor tabs follow AuroraGT's SpriteEditor layout. The original AuroraGT help informed this layout; its executable bundle has been retired.

## Launch

From the game project root:

```bash
./Tools/AuroraMX/auroramx.sh
```

The launcher opens the browser at **http://127.0.0.1:5180**. It uses the standalone executable when present; otherwise it installs the locked npm dependencies and builds the application with Node.js 22.12+ and npm. Closing the last editor tab stops the backend after a two-second grace period; refreshing or keeping another tab open keeps it running. An active resource build finishes before shutdown. You can also use **File → Exit AuroraMX** or Ctrl+C in its terminal. Automatic shutdown is disabled during development. Opening it again reconnects to the existing instance for the same project.

```bash
./Tools/AuroraMX/auroramx.sh --workspace /path/to/project --port 5182
./Tools/AuroraMX/auroramx.sh --no-open
```

`npm run package` creates `dist/auroramx` (Linux) and `dist/auroramx.exe` (Windows x64), bundling the frontend, backend and Node runtime. Run this command on Linux to build both targets, or use `package:linux` / `package:windows` for one target. Windows packaging downloads the matching official Node runtime and verifies its SHA-256 checksum; it needs no Wine or Windows SDK. The standalone editor does not require Node/npm on the receiving machine. Game builds still require the project's development dependencies.

## Edit sprites

- **File → Open** browses the project and opens `.sprite` and `.yaml` files. The YAML format (top-level `sprite:` mapping with named fields, see `Data/Sprites/Ingame/dizzy_effect.yaml`) is chosen by extension on save and detected from content on open, and works everywhere a `.sprite` does (export, `Load` in `.sprcmd`). Convert with `node build/cli.cjs convert in.sprite out.yaml` (or the reverse). YAML comments are not preserved on save.  The current game's inputs are in `Data/Sprites/`, as listed in `Sprites.sprcmd`.
- **Module Editor:** insert/clone/delete/reorder modules and images; edit IDs, descriptions, coordinates, sizes and image paths. Enable **All modules** to see the atlas. Use **Draw module on atlas** to create a rectangle by dragging. Drag an existing module to move its atlas rectangle.
- **Frame Editor:** insert frames, then double-click modules in the selection bank to add placements. Select a placement to edit its target, offsets, flags or hyperframe reference. Drag placements in the preview. Up/Down changes layer order. Add/edit/delete collision rectangles numerically, or use **Draw collision rectangle**.
- **Animation Editor:** create animations and double-click frames in the bank to append steps. Edit time, offsets and transforms; play/pause, scrub, step, loop and preview onion skin. The default is the game's 25 ticks per second; zero-duration steps hold the animation.
- **Source Editor:** edit the sprite language directly. Apply source or save to parse it. Unknown top-level extension blocks are retained. Source edits, including comments, are saved as text.
- Wheel zooms; Alt+drag or middle-button drag pans. **Fit** centers and fits the current atlas/frame. Grid, origin, bounds, transparency and zoom controls affect the preview.
- Ctrl+S saves; Ctrl+Shift+S saves as; Ctrl+O opens; Ctrl+N creates; Ctrl+Z/Y undoes/redoes structured edits; Space plays/pauses animation. Native text undo remains available while editing a text field.

PNG, TGA (including indexed/RLE), and uncompressed/bitfield BMP previews are decoded locally. Image pixels are not edited or rewritten by the editor. Alpha masks and transparent colors affect the preview. Relative image paths are rebased when saving into another folder.

Unchanged files retain their original bytes and encoding. Changed saves use UTF-8, write atomically, retain a backup under `<workspace>/.auroramx/backups/`, and reject stale revisions if another program changed the file. Save As refuses to overwrite an existing file. The backend listens only on loopback; mutations require the browser session token.

## Export and build game data

The game uses YAML directly; save edits to the `.yaml` source and rebuild resources. **File → Export binary sprite** remains a legacy compatibility option for the current document (including unsaved structured edits). The output extension is `.bsprite`. The export profile is the game's `GP_Data` layout from `Data/Sprites/GP_export.fft`: v5 module/frame/animation metadata, with an external atlas.

Use **File → Run sprite commands** to run `Data/Sprites/Sprites.sprcmd`. Its 29 loads copy 29 YAML sprites byte for byte (including comments and formatting) and generate the `Fonts.h`, `Ingame.h`, and `Menu.h` frame/animation ID headers. The default output is `build/auroramx-export/bin/`. The command-line equivalent is:

```bash
cd Tools/AuroraMX
npm ci
npm run build:cli
node build/cli.cjs batch ../../Data/Sprites/Sprites.sprcmd --output ../../build/auroramx-export
node build/cli.cjs validate ../../Data/Sprites/Menu/menu_bg.yaml
```

**Tools → Build game data** runs `rebuild_Linux.sh --resources-only` and shows the live build log. That script now builds/runs the AuroraMX CLI on `Sprites.sprcmd`, stages the unchanged YAML copies in the selected preset's assets directory, and updates generated headers when their IDs change. The resource build does not export binary sprites. Packages contain YAML metadata and atlases; stale `.sprite`/`.bsprite` files are removed during staging. For a complete game rebuild, from the project root:

```bash
./rebuild_Linux.sh
./rebuild_Linux.sh --debug --incremental --no-package
```

Resource builds use PNG textures exclusively. Keep the game's external atlas names/dimensions consistent with its texture bindings. The runtime reads atlas names, transparency keys and optional alpha masks from YAML. `Build/bin` is preserved. AuroraGT is no longer required or bundled.

### Compatibility

All 29 current project YAML sprites are supported, and generated frame/animation ID values remain unchanged. Legacy compiler tests retain immutable hashes of the original AuroraGT metadata; those binaries are no longer game resources.

The supported command set is `InitBSpriteFFT`, `Load`, `Copy`, `Save`, `ExportBSpriteFFT`, `ExportFramesID`, `ExportAnimsID`, and `ExportModulesID`, with the project's APPEND/TOUPPER options. `Copy` requires YAML input/output and preserves the original bytes, without serialization or binary compilation. The game batch uses `Load`, `Copy` and ID commands only. For legacy binary export, the selected FFT must match the bundled GP_Data profile; different layouts are rejected. This is not a generic FFT interpreter or a replacement for AuroraGT's GameEditor/MapEditor, bitmap painting, palette optimization, or tileset authoring. Primitive shape modules can be edited/previewed in `.sprite` documents, but the game's export profile supports image modules in a single external atlas. Out-of-range fields and invalid/cyclic references are rejected.

## Development

The organization follows the referenced `/media/spyder/_Projects/TypeScript/ts-hello-world` project: `backend/`, `frontend/`, `shared/`, development/build scripts, an embedded frontend, browser-tab lifetime, and Node SEA packaging. The frontend uses Vue 3 single-file components and the Composition API, matching the reference.

```text
AuroraMX/
├── backend/       # local API, files, image decoding, compiler, batch CLI
├── frontend/      # Vue single-file components, canvas preview, classic desktop styling
├── shared/        # sprite model, validation and bounds
├── config/        # application defaults
├── scripts/       # development, build and standalone packaging
├── tests/         # binary conformance, filesystem/API and browser workflows
├── docs/          # game format notes
└── build/         # generated frontend, server and CLI bundles (ignored)
```

```bash
npm ci
npm run dev          # backend 5180 + Vite 5181; opens the browser
npm run typecheck
npm test             # all 29 golden binary comparisons, IDs, formats, files/API
npm run build        # embeds frontend into build/server.cjs; builds CLI too
npm start
npx playwright install chromium
npm run test:e2e     # real editing, drawing, saving, binary and batch exports
npm run package     # standalone Linux + Windows x64 applications
npm run package:linux
npm run package:windows
```

`config/defaults.json` controls the default port, startup sprite, command file, output directory and preview tick rate. `AURORA_MX_WORKSPACE`, `PORT`, and `AURORA_MX_NO_OPEN=1` can override launch behavior. Development uses the standard 5180/5181 port pair. If the system has exhausted its inotify watchers, use `CHOKIDAR_USEPOLLING=1 npm run dev`. Browser tests may use an existing Chromium with `PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH`.

See [the format notes](docs/FORMAT.md) for the binary layout and compatibility checks.
