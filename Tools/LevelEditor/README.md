# Castle Frenzy Level Editor

A local TypeScript application with a Vue 3 frontend and Fastify backend, following `Tools/AuroraMX` and the `ts-hello-world` reference: separate `backend/`, `frontend/`, `shared/`, an embedded production frontend, browser-tab lifetime, and Node SEA executable packaging.

Launch from the project root:

```bash
./Tools/LevelEditor/level-editor.sh
```

Opens **http://127.0.0.1:5190**. The launcher uses `dist/level-editor` when packaged; otherwise it installs locked npm dependencies and builds with Node.js 22.12+ and npm. Launching again opens the existing instance for the same workspace, asset directory and page-close setting. **Save & exit** (or Escape outside a field) always saves and stops the backend, resumes the test game, and leaves that game running. A browser may keep a manually opened tab visible after exit; the server is closed.

Page-close shutdown follows `project_structure.md` and is controlled by `STOP_ON_PAGE_CLOSE`:

- Plain Node, `npm start`, development, and the launcher's Node fallback default to `'false'`: closing a tab leaves the backend running. Set `STOP_ON_PAGE_CLOSE=true` to enable shutdown in these modes; only `'true'` and `'false'` are accepted.
- Packaged `dist/level-editor` and `dist/level-editor.exe` always use `'true'`, baked into a separate backend bundle by `scripts/package.ts`. Runtime environment variables cannot override it. Packaging leaves the ordinary `build/server.cjs` configurable with its default of `'false'`.
- When enabled, each tab holds an authenticated SSE connection. Closing the **last** tab stops the backend and its test game after a two-second grace period. Refreshes and other open tabs keep it alive; saves and game commands finish before shutdown. Before the first tab connects, it waits indefinitely.

```bash
cd Tools/LevelEditor
STOP_ON_PAGE_CLOSE=true npm start -- --no-open
STOP_ON_PAGE_CLOSE=true npm run dev
```

```bash
./Tools/LevelEditor/level-editor.sh --no-open
./Tools/LevelEditor/level-editor.sh --workspace /path/to/project --port 5194
./Tools/LevelEditor/level-editor.sh --assets build/linux-debug/assets
```

`--assets` and `--game` take paths relative to the project. The asset selector also offers existing `Build/bin`, `build/linux-debug/assets`, and `build/linux-release/assets` directories. It is disabled while a test game uses those assets. Default source edits go directly to `Build/bin/Levels`, which resource rebuilds copy. Edits made in a generated asset directory must be copied back to `Build/bin/Levels` to survive a rebuild.

## Editing

- **Waves:** create/delete/reorder levels, create/cut/copy/paste/reorder days, edit day end time and every challenge, and select easy/medium/hard for endless segments. The final indexed level is always Endless, as in the game. New levels are inserted before it; reordering changes which level occupies the final slot.
- Create/delete/clone/reorder waves. Edit name, packed spawning, start, duration, repeat interval/count. Add/remove variants and enemy types, select units with inherited config/icon/visibility settings, edit min/max counts, and set intro camera on each variant.
- The timeline shows each repeat. Left-drag changes start, right-drag changes duration, middle-drag changes repeat spacing; wheel changes count. Zoom scrolls wider timelines. Numeric fields use the native editor ranges.
- **Weapons:** all five weapons (gate, hydra, fireball, iceball, griffin), starting levels 0–3, shop availability, spell bought, and none/boulder/lightning/ghost army/tornado. UI levels 0–3 map to XML values -1–2, matching the game.
- **Models:** all 21 inherited model definitions, scaled/textured 3D previews, named animation clips, step interpolation, looping, native gate morphs. Orbit with left drag, zoom with wheel, pan with right drag. Fit camera, animation pause, speed, scrub and wireframe are also available. Previews load the Blender-exported `Data/Scenes/<scene>/<model>.fbx` files with Three.js's FBXLoader; runtime `.bdae` files remain game resources. Each FBX take is one game clip, listed alphabetically, and carries its bone animation and gate morph weights. Material UV scrolling (water, tornado, shield effects) is not previewed. The FBX files are committed: after changing a registered `.blend` file, run `python3 tools/blender/build_fbx.py` (see tools/blender/README.md) so the browser and game models agree. The viewer does not modify meshes or model configuration, matching the built-in Models tab.
- **Save / Ctrl+S**, Undo/Redo (Ctrl+Z/Y outside text fields), Reload, Save & exit. E/R remain ordinary text in browser fields. Closing a tab with unsaved edits triggers the browser's discard prompt.

Only changed level XML files are rewritten. No-op saves preserve original bytes, BOM, comments and whitespace. Changed files use UTF-8 and escaped XML values. Backups and a manifest are stored at `.level-editor/backups/`. Saves validate all data and reject stale revisions; each file is replaced atomically and an ordinary write failure rolls back already-written files. A process/power interruption during a multi-file save can require restoring the recorded backup. Deleted/reindexed files are backed up before removal.

## Play and live controls

Build the game once after installing the editor bridge:

```bash
cmake --build --preset linux-debug
```

**Play day** saves and launches the native game with the selected assets/level/day. In Weapons, **Play level** starts day one with the selected loadout. The backend finds the Debug executable first, then Release, or accepts `--game path/to/FortressDefender`. No build runs implicitly. An old game binary produces a rebuild message. The normal game window handles gameplay and cutscenes.

Returning focus to the browser pauses the test game by default; turn off **Pause while editing** to keep it running. Pause/Resume, **DIE!**, F9 overlay and Stop operate on that test game. **Current day / live spawns** displays the game's actual randomized spawn list, alive/dead/next/future states, extra spawns and time cursor for the matching selected day. Endless gameplay generates segments using the native game's endless rules. The bridge is enabled only with `--editor-bridge`; regular game launches retain their E/R editor.

The test game uses a separate save/profile directory under `.level-editor/sessions/`, so testing does not overwrite normal player progress. Save & exit leaves this session directory for the continuing game; Stop removes it. For persistent level edits, edit XML in the selected asset directory. Do not edit the same data simultaneously in the browser and built-in game editor; stale-save checks protect browser writes, and Reload reads external changes.

The backend listens only on loopback, validates local hosts, rejects cross-site API requests and requires a session token for mutations. Texture/model paths resolve inside the chosen workspace.

## Development and packaging

```bash
cd Tools/LevelEditor
npm ci
npm run dev                 # API 5190, Vite 5191, opens browser
npm run build               # embedded frontend + build/server.cjs
npm test                    # all 105 days, models, XML/API and save checks
npx playwright install chromium
npm run test:e2e            # copied asset fixture; edits no shipping levels
LEVEL_EDITOR_GAME_TEST=1 npm test  # native bridge smoke test; requires DISPLAY
npm run package:linux       # dist/level-editor
npm run package:windows     # dist/level-editor.exe (Windows x64)
LEVEL_EDITOR_PACKAGE_TEST=1 npm test  # verify packaged lifetime on Linux after packaging
```

The runnable entry scripts (`scripts/dev.ts`, `scripts/build.ts`, `scripts/package.ts`) carry a `#!/usr/bin/env -S npx tsx` shebang and the executable bit, so from the `Tools/LevelEditor` directory they can also be run directly, for example `./scripts/dev.ts` or `./scripts/package.ts --target linux`. They rely on the `tsx` dev dependency, so run `npm ci` first. On Windows, use the `npm run` forms; the shebang is ignored there.

`npm run package` creates both targets on Linux, verifying the official Windows Node runtime checksum. Standalone executables include Node, frontend and backend, but use the chosen project's assets and game executable. Windows game bridge source is included; native game/browser verification is performed on Ubuntu. `LEVEL_EDITOR_WORKSPACE`, `PORT`, `LEVEL_EDITOR_NO_OPEN=1` override launch defaults. Development honors `STOP_ON_PAGE_CLOSE`, with the same `'false'` default as ordinary Node runs. Tests can use `PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH` for an installed browser.

See [feature coverage](docs/FEATURES.md) for the mapping to the original editor.
