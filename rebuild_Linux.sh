#!/usr/bin/env bash
# All runtime textures are PNG. PYTHON_BIN selects Python with Pillow installed.
readonly PYTHON_BIN="${PYTHON_BIN:-python3}"
readonly BLENDER_BIN="${BLENDER_BIN:-blender}"

set -Eeuo pipefail

project_dir=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
cd "$project_dir"
preset=linux-release
resources_only=false
package=true
clean=true

usage() {
    cat <<'EOF'
Usage: ./rebuild_Linux.sh [--debug] [--resources-only] [--no-package] [--incremental]

Generate the version header, prepare resources, rebuild with CMake, and create
a ZIP in build/packages. Release is the default; --debug selects linux-debug.
--resources-only prepares assets without rebuilding or packaging the game.
--incremental skips the clean step. --no-package skips the ZIP step.

Requires Node.js 22.12+, npm, and the Linux build dependencies (see readme.txt).
AuroraMX copies YAML sprites unchanged and generates their ID headers.
Blender 5.2+ exports the .blend sources registered in Data/Scenes/models.json.
Set BLENDER_BIN to use a particular Blender executable.
Textures are staged as PNG. Requires Python Pillow: sudo apt install python3-pil
Set PYTHON_BIN to select a particular Python executable.
EOF
}

for arg in "$@"; do
    case "$arg" in
        --debug) preset=linux-debug ;;
        --resources-only) resources_only=true ;;
        --no-package) package=false ;;
        --incremental) clean=false ;;
        -h|--help) usage; exit 0 ;;
        *) printf 'Unknown option: %s\n' "$arg" >&2; usage >&2; exit 2 ;;
    esac
done
for tool in cmake "$PYTHON_BIN" node npm; do
    command -v "$tool" >/dev/null || { printf 'Missing tool: %s\n' "$tool" >&2; exit 1; }
done
"$PYTHON_BIN" -c 'from PIL import Image' || {
    printf 'Texture imports require Pillow: sudo apt install python3-pil\n' >&2
    exit 1
}

build_dir="$project_dir/build/$preset"
asset_dir="$build_dir/assets"
mkdir -p "$build_dir"
log_file="$build_dir/rebuild.log"
exec > >(tee "$log_file") 2>&1
trap 'status=$?; printf "Rebuild finished at %s (exit %s)\n" "$(date -u --iso-8601=seconds)" "$status"' EXIT
printf 'Rebuild started at %s\n' "$(date -u --iso-8601=seconds)"

printf '\nGenerate project version file\n'
version=$("$PYTHON_BIN" "$project_dir/tools/linux/build_resources.py" --version)
printf 'Version: %s; textures: PNG\n' "$version"

cmake --fresh --preset "$preset" -DCASTLE_ASSET_DIR="$asset_dir"

printf '\nCopy YAML sprites and generate ID headers with AuroraMX\n'
auroramx_dir="$project_dir/Tools/AuroraMX"
if [[ ! -x "$auroramx_dir/node_modules/.bin/esbuild" ]]; then
    npm ci --prefix "$auroramx_dir" --no-audit --no-fund
fi
npm run build:cli --prefix "$auroramx_dir"
cmake -E rm -rf "$build_dir/auroramx/bin"
node "$auroramx_dir/build/cli.cjs" batch "$project_dir/Data/Sprites/Sprites.sprcmd" \
    --output "$build_dir/auroramx"

printf '\nRebuild project data\n'
printf '\nExport Blender models\n'
cmake --build --preset "$preset" --target BdaeCheck
"$PYTHON_BIN" "$project_dir/tools/blender/build_models.py" \
    --output "$build_dir/blender" --blender "$BLENDER_BIN" \
    --texture-format png --validator "$build_dir/bin/BdaeCheck"

"$PYTHON_BIN" "$project_dir/tools/linux/build_resources.py" \
    --output "$asset_dir" \
    --sprite-exports "$build_dir/auroramx/bin" --scene-exports "$build_dir/blender"
"$PYTHON_BIN" "$project_dir/tools/blender/build_models.py" --output "$build_dir/blender" \
    --validate-staged "$asset_dir/Scenes" --validator "$build_dir/bin/BdaeCheck" \
    --texture-format png
if $resources_only; then
    printf '\nResources ready: %s\n' "$asset_dir"
    exit 0
fi

printf '\nRebuild project source\n'
build_args=()
if $clean; then build_args=(--clean-first); fi
cmake --build --preset "$preset" "${build_args[@]}"

if $package; then
    printf '\nPack project version\n'
    "$PYTHON_BIN" "$project_dir/tools/linux/build_resources.py" \
        --output "$asset_dir" \
        --package "$build_dir/bin/FortressDefender"
fi
printf '\nReady to run: %s/bin/FortressDefender\nLog: %s\n' "$build_dir" "$log_file"
