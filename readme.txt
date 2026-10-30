Ubuntu / Linux build (tested on Ubuntu 24.04 x86_64):

The Linux game, bundled Irrlicht fork and asset utilities require a 64-bit
compiler. Existing BRES assets retain their portable four-byte offsets; the
loader resolves them to full-width native pointers in a resource-owned handle
table. No 32-bit compiler, multilib packages or asset conversion is needed.

1. Install CMake 3.24+, GCC/G++, make, Python 3 and GDB, then prepare dependencies:

   bash tools/linux/bootstrap.sh

   This downloads native amd64 Ubuntu development/runtime packages into
   .deps/linux64/root without sudo, including Mesa software rendering.
   It uses the Ubuntu repositories. The former .deps/root 32-bit dependencies
   are ignored. Re-run CMake with --fresh after moving the project because
   local library paths are absolute.

   Alternatively, install dependencies system-wide and omit the bootstrap:

   sudo apt update
   sudo apt install build-essential cmake gdb libgl-dev libglu1-mesa-dev \
       libopenal-dev libx11-dev libxxf86vm-dev libgl1-mesa-dri libpulse0

2. From the project root:

   cmake --fresh --preset linux-debug
   cmake --build --preset linux-debug
   ./build/linux-debug/bin/FortressDefender

   For an optimized build, use the linux-release configure/build presets.
   Use --fresh for the first configure of an existing build folder to discard
   cached 32-bit compiler flags. rebuild_Linux.sh does this automatically.
   Launch from a graphical desktop with DISPLAY available (X11 or XWayland).
   The executable locates Build/bin automatically, independent of the current
   working directory. Override with --assets /path/to/assets if needed.
   The window starts at 960x640 (2x the original 480x320 layout). Menus,
   fonts, gameplay and mouse input scale together; no asset rebuild is needed.
   Use --scale 1 for 480x320, --scale 3 for 1440x960, or --scale 4 for 1920x1280.
   Save data goes to $XDG_DATA_HOME/castle-frenzy/ or, by default,
   ~/.local/share/castle-frenzy/.

3. Full Linux rebuild and resource format:

   ./rebuild_Linux.sh

   Runtime textures use PNG on Linux and Windows. Pillow is required for legacy
   TGA/BMP imports (sudo apt install python3-pil). Matching PNGs in Data/Sprites,
   Data/Fonts, Data/Scenes and Data/Effects take precedence. PNG atlases retain
   the dimensions used by their YAML module coordinates. PVR encoders, texture
   files and the runtime loader have been removed. Builds reject PVR assets.
   Set PYTHON_BIN=/usr/bin/python3 if the default Python lacks Pillow.

   Project-owned TGA images have been migrated losslessly to PNG. When a TGA
   differed from an existing runtime PNG, its source pixels are preserved under
   Data/TextureSources; that directory is not staged or packaged. Immutable
   legacy model/sprite templates and packed Blender source images retain their
   original image labels; the game and authoring tools resolve PNG variants.

   The script reads the version from config.bat, prepares resources, performs a
   clean Release build, and creates build/packages/CastleFrenzy_Linux_v*_*.zip.
   Use --debug for Debug, --incremental to skip cleaning, --no-package to skip
   the ZIP, or --resources-only to update assets without compiling the game.
   Each preset's generated assets live in build/linux-{release,debug}/assets;
   CMake configures that build's executable to find them automatically.
   Build/bin and the source images are preserved. Logs are in the preset's
   build directory as rebuild.log. Run the ZIP's run.sh after extracting it.

   Node.js 22.12+ and npm are required for the AuroraMX sprite exporter.
   The resource step runs Data/Sprites/Sprites.sprcmd through AuroraMX to
   copy all 29 .yaml files unchanged and generate frame/animation ID headers. Its npm
   dependencies are installed automatically on the first rebuild. Generated
   YAML files are staged in the preset's auroramx/bin directory, then copied
   into its assets directory. No .sprite or .bsprite files are packaged.
   Blender 5.2+ exports the .blend models registered in Data/Scenes/models.json.
   Start with Data/Scenes/Goblin/goblin_smooth.blend: save edits, then rebuild.
   All 33 mesh scenes now have *_smooth.blend projects. Data/Scenes/README.md
   lists them with geometry counts. Organic bodies are rounded; architecture,
   gates and effect planes keep their shapes with denser editable geometry.
   Other scenes use mesh export mode, retaining original animation/materials
   and engine metadata. The Goblin supports the full animation exporter.
   Set BLENDER_BIN to select a Blender executable. Unchanged model exports are
   cached; the native engine checks each export before it is staged. See
   tools/blender/README.md for the exporter add-on, CLI and editing constraints.
   The main resource build reuses camera, string-table and configuration exports.
   Regenerate the spreadsheet exports with the Python tools below when needed.
   Textures and WAV sound packs rebuild natively; unregistered .dae assets use
   their existing model exports.

   The standalone Data/Strings and Data/Sounds builds use Python:

   python3 -m pip install -r tools/requirements-exporters.txt
   python3 tools/string_parser.py Data/Strings/fortress_defender_strings.xls build/strings 7
   python3 tools/sound_config_exporter.py Data/Sounds/SoundConfiguration.xls build/sounds_config.xml

   The exporter writes the seven UTF-16LE language tables, font-character maps,
   both C++ ID headers and a deterministic All_Strings.gen. Data/Strings/_make.sh
   and _make.bat stage these outputs using the existing clean/no_bar modes.
   The sound exporter preserves global defaults and per-sound overrides.
   Data/Sounds/_make.sh and _make.bat use it before packaging WAV groups.
   PYTHON_BIN selects the Python executable. No Java installation is needed.
   The make.bat/make.sh entry points in Tools/StringParser and
   Tools/SoundConfigExporter launch the Python tools; no compilation is needed.
   Packages built with the rootless .deps/linux64 setup retain absolute dependency
   paths to this project. Use system-installed dependencies when building
   packages for other machines.

4. VS Code:

   Open this project root and install the recommended Microsoft C/C++ and
   CMake Tools extensions. Select "Castle Frenzy: Ubuntu Debug" in Run and
   Debug, then press F5. Configuration and compilation run automatically.
   Ctrl+Shift+B builds; the "Run Castle Frenzy (Ubuntu)" task runs without GDB.

Controls: left mouse button/drag emulates touch; right mouse button emulates
another touch. Escape exits. F2 halves game speed; F3 doubles game speed.
After loading, E or R opens the built-in editor (waves, weapons and models).
The same features are available in a standalone browser editor:

   ./Tools/LevelEditor/level-editor.sh

It follows AuroraMX's Vue frontend/Fastify backend application structure and
opens http://127.0.0.1:5190. It edits Build/bin/Levels by default, with backups,
and offers wave timelines, weapons/spells, animated model previews, and native
Play/pause/DIE/live-spawn controls. Rebuild the game once for Play support:
cmake --build --preset linux-debug. See Tools/LevelEditor/README.md.

The game stops updating while the editor is open. Select a level/day in the
waves tab, edit its waves, and use Play to test it. Save writes changes; Exit
or Escape saves and returns to the game. Editor controls use the window's
full resolution. E/R remain normal text input while editing a field.
Saving rewrites only files whose level data changed. Opening/closing the
editor, browsing, or saving without edits preserves the original XML bytes.
F9 toggles the wave timeline overlay during gameplay.
Edits are saved to Levels/ inside the active asset directory: Build/bin by
default, or build/linux-{debug,release}/assets for rebuild_Linux.sh output.
For generated assets, copy edited XML files back to Build/bin/Levels to retain
them across asset rebuilds.
For a bounded smoke run: ./build/linux-debug/bin/FortressDefender --frames 600

The desktop CMake build supports Linux and Windows x64. The old iPhone
project remains archived under Projects/IPhone.

AuroraMX sprite editor:

   The C++ runtime loads AuroraMX .yaml/.yml metadata directly.
   ASprite::Load("Ingame/dizzy_effect") resolves the matching YAML file.
   Explicit .yaml/.yml filenames also work. Missing or invalid YAML reports
   failure. The game does not load .sprite or .bsprite files.
   Resource builds copy YAML byte for byte, preserving comments and formatting,
   and generate the ID headers from the same YAML sources. Build/bin/Sprites
   also contains YAML, so a direct CMake build needs no sprite conversion.

   Use Data/Sprites/Ingame/dizzy_effect.yaml as the schema example. IDs are
   hexadecimal strings; document order determines runtime module/frame/animation
   indices, as with binary exports. Parts reference module IDs (or frame IDs
   with HYPER_FM); animation steps reference frame IDs. Flags are name lists:
   FLIP_X, FLIP_Y, ROT_90, and HYPER_FM for frame parts. Frame rects use
   x1/y1/x2/y2. Bounds are computed automatically, including hyperframes.
   The existing GP_Data runtime supports MD_IMAGE modules in one image atlas
   per sprite. Other module types and multiple used atlases report an error.
   Image paths are relative to YAML; PNG/TGA/BMP variants are resolved like
   existing resources. transparent is an RGB color key; optional alphaFile uses
   the mask's red channel for alpha, matching AuroraMX previews.

   CMake builds a pinned static yaml-cpp 0.8.0 dependency on first configure.
   Its MIT license is in Lib/yaml-cpp.LICENSE and included in game packages.

   ./Tools/AuroraMX/auroramx.sh

   This opens the TypeScript/Vue 3 editor in a browser at localhost:5180.
   It follows AuroraGT's module/frame/animation layout and supports source
   editing, undo/redo, PNG/TGA/BMP atlas previews, collision rectangles,
   animation playback, YAML authoring, and batch .sprcmd execution.
   Tools > Build game data runs the Linux resource build with AuroraMX.
   See Tools/AuroraMX/README.md for launch, development and format details.



Windows x64 build (Visual Studio 2022):

1. Install Visual Studio 2022 with Desktop development with C++, the Windows
   SDK and CMake 3.24+. Configure from the project root:

   cmake --preset windows-x64
   cmake --build --preset windows-release
   ctest --preset windows-release
   build\windows-x64\bin\Release\FortressDefender.exe

   Use windows-debug for Debug. The first configure downloads the pinned
   OpenAL Soft source; it is compiled for x64 together with the game.
   The matching OpenAL32.dll is copied beside the executable. Desktop OpenGL
   uses Windows system libraries; the old x86 emulator DLLs are not used.
   Existing Build/bin assets are ready to use, with no Java or asset rebuild.

2. Alternatively, open Projects/Win32/FortressDefender.sln in Visual Studio
   2022 and select Debug|x64 or Release|x64. The solution delegates to the same
   CMake build, including Irrlicht and SoundLib built from source. The Win32
   directory and rebuild_Win32.bat filenames are kept for compatibility;
   only 64-bit compilation is supported. Old .vcproj files are historical.

3. Full source rebuild, tests and ZIP packaging:

   rebuild_Win32.bat

   Requires Node.js 22.12+, npm and Python 3.10+. The script copies YAML and
   regenerates sprite ID headers into build/windows-x64/assets before compiling.
   Use --debug, --incremental or --no-package as needed. The ZIP is written to
   build/packages/CastleFrenzy_Windows_x64.zip. Extract it and run
   bin/FortressDefender.exe. CMake packages only the native executable, its
   matching audio DLL, assets and license; legacy Build/*.dll files are excluded.
   The Visual C++ 2015-2022 Redistributable (x64) is required on other machines.
   Saves go under %LOCALAPPDATA%/CastleFrenzy/profiles. --assets overrides the
   resource directory; --frames limits a smoke run. Escape exits the game.

AuroraGT and bundled 7Zip have been retired. AuroraMX exports sprites; Python
and CMake create ZIP/resource archives. The legacy Data/Sprites/_make.bat uses
AuroraMX and requires Node.js 22.12+ and npm. Sprite format references live in
Tools/AuroraMX/docs/legacy-bsprite-format.txt and legacy-sprcmd.txt.


iPhone build:

1. Set 'JAVA_PATH' path in 'setenv.bat' file.
2. Set version number in 'config.bat' file.
3. In command line call 'make data_rebuild'.
4. From folder 'Projects/IPhone' open Mac project and rebuild it.

