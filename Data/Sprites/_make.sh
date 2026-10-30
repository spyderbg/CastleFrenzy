#!/usr/bin/env bash

# Bash equivalent of Data/Sprites/_make.bat.
# Usage: _make.sh [clean|no_bar|no_pause]
# No argument cleans first; no_bar keeps staged files in bin/ without archiving.
# Requires Node.js 22.12+ and npm for the native AuroraMX batch exporter.
set -euo pipefail
shopt -s nullglob

source "$(dirname -- "${BASH_SOURCE[0]}")/../_env.sh"
dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# Resolve relative overrides before changing the working directory.
[[ "$OUTPUT_PATH" == /* ]] || OUTPUT_PATH="$PWD/$OUTPUT_PATH"
[[ "$HEADERS_OUT_PATH" == /* ]] || HEADERS_OUT_PATH="$PWD/$HEADERS_OUT_PATH"
cd "$dir"
arg="${1:-}"

if [[ "$arg" == clean ]]; then
    OUTPUT_PATH="$OUTPUT_PATH" bash "$dir/_clean.sh"
    exit 0
fi

for tool in node npm; do
    if ! command -v "$tool" >/dev/null 2>&1; then
        echo "Missing tool: $tool (AuroraMX requires Node.js 22.12+ and npm)." >&2
        exit 1
    fi
done

if [[ -z "$arg" ]]; then
    OUTPUT_PATH="$OUTPUT_PATH" bash "$dir/_clean.sh"
fi

echo
echo "**************************************************************"
echo "   Make Sprites"
echo "**************************************************************"

mkdir -p bin "$HEADERS_OUT_PATH"
find bin -type f -name '*.h' -delete
find . -type f -name '*.bak' -delete

sprcmd=Sprites.sprcmd
echo '// Copy YAML metadata unchanged and generate runtime ID headers.' > "$sprcmd"
for pack in ./*/; do
    [[ "$pack" == ./bin/ ]] && continue
    name="${pack#./}"
    name="${name%/}"
    mkdir -p "bin/$name"
    for f in "$pack"*.yaml; do
        base="${f##*/}"
        base="${base%.yaml}"
        {
            printf '\nLoad("%s")\n' "$f"
            printf 'ExportFramesID("bin/%s.h", "%s_F_%s_", APPEND|TOUPPER)\n' "$name" "$name" "$base"
            printf 'ExportAnimsID("bin/%s.h", "%s_A_%s_", APPEND|TOUPPER)\n' "$name" "$name" "$base"
            printf 'Copy("bin/%s/%s.yaml")\n' "$name" "$base"
        } >> "$sprcmd"
    done
    for f in "$pack"*.png; do
        cp -f -- "$f" "bin/$name/"
    done
done

# JavaScript tools live in the capital-T Tools/ directory.
auroramx="$PROJECT_PATH/Tools/AuroraMX"
if [[ ! -x "$auroramx/node_modules/.bin/esbuild" ]]; then
    npm ci --prefix "$auroramx" --no-audit --no-fund
fi
npm run build:cli --prefix "$auroramx"
node "$auroramx/build/cli.cjs" batch "$dir/$sprcmd"

# Preserve unchanged headers' timestamps and changed headers' existing modes.
install_header() {
    local src="$1" target mode=""
    target="$HEADERS_OUT_PATH/${src##*/}"
    if [[ -f "$target" ]] && cmp -s "$src" "$target"; then
        rm -f -- "$src"
        return
    fi
    if [[ -f "$target" ]]; then
        mode="$(stat -c '%a' "$target")"
    fi
    mv -f -- "$src" "$target"
    if [[ -n "$mode" ]]; then
        chmod "$mode" "$target"
    fi
}

for f in bin/*.h; do
    install_header "$f"
done

if [[ "$arg" != no_bar ]]; then
    echo
    echo "**************************************************************"
    echo "   Archiving Sprites"
    echo "**************************************************************"
    mkdir -p "$OUTPUT_PATH/Sprites"
    find "$OUTPUT_PATH/Sprites" -type f \( -name '*.bsprite' -o -name '*.sprite' \) -delete
    cp -R bin/. "$OUTPUT_PATH/Sprites/"
    rm -rf bin

    for f in "$OUTPUT_PATH"/*.h; do
        install_header "$f"
    done
fi
