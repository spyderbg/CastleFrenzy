#!/usr/bin/env bash

# Bash equivalent of Data/Scenes/_make.bat.
# Usage: _make.sh [clean|no_bar|no_pause]
# No argument cleans first; no_bar keeps staged files in bin/ without archiving.
# DAE_CONVERTER may name a native executable (or a wrapper) accepting -d -i.
# The default is the bundled Windows converter, run through Wine.
set -euo pipefail
shopt -s nullglob

source "$(dirname -- "${BASH_SOURCE[0]}")/../_env.sh"
dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# Resolve a relative output override before changing the working directory.
[[ "$OUTPUT_PATH" == /* ]] || OUTPUT_PATH="$PWD/$OUTPUT_PATH"
cd "$dir"
arg="${1:-}"

if [[ "$arg" == clean ]]; then
    OUTPUT_PATH="$OUTPUT_PATH" bash "$dir/_clean.sh"
    exit 0
fi

# Check conversion dependencies before cleaning any previous build artifacts.
dae_files=()
for scene in ./*/; do
    [[ "$scene" == ./bin/ ]] && continue
    dae_files+=("$scene"*.dae)
done
converter_command=()
if (( ${#dae_files[@]} )); then
    converter="${DAE_CONVERTER:-$PROJECT_PATH/Lib/Irrlicht/tools/ColladaConverter/bin/Release/ColladaConverter_2008.exe}"
    if [[ "${converter,,}" == *.exe ]]; then
        if [[ ! -f "$converter" ]] || ! command -v wine >/dev/null 2>&1; then
            echo "DAE conversion requires Wine and $converter; alternatively set DAE_CONVERTER to a native converter or wrapper." >&2
            exit 1
        fi
        [[ "$converter" == /* ]] || converter="$PWD/$converter"
        converter_command=(wine "$converter")
    else
        if ! converter="$(command -v -- "$converter")"; then
            echo "DAE_CONVERTER must name an executable converter or wrapper accepting -d -i." >&2
            exit 1
        fi
        [[ "$converter" == /* ]] || converter="$PWD/$converter"
        converter_command=("$converter")
    fi
fi

if [[ -z "$arg" ]]; then
    OUTPUT_PATH="$OUTPUT_PATH" bash "$dir/_clean.sh"
fi

echo
echo "**************************************************************"
echo "   Make Scenes"
echo "**************************************************************"

mkdir -p bin
for scene in ./*/; do
    [[ "$scene" == ./bin/ ]] && continue
    name="${scene#./}"
    name="${name%/}"
    mkdir -p "bin/$name"
    echo "$name"
    for f in "$scene"*.dae; do
        echo "$f"
        # Work beside the model so relative texture references still resolve.
        (cd "$scene" && "${converter_command[@]}" -d -i "${f##*/}")
        if [[ ! -f "${f%.dae}.bdae" ]]; then
            echo "Converter did not produce ${f%.dae}.bdae" >&2
            exit 1
        fi
    done
    for f in "$scene"*.bdae; do
        mv -f -- "$f" "bin/$name/"
    done
    for f in "$scene"*.png "$scene"*.bmp; do
        cp -f -- "$f" "bin/$name/"
    done
done

if [[ "$arg" != no_bar ]]; then
    echo
    echo "**************************************************************"
    echo "   Archiving Scenes"
    echo "**************************************************************"
    mkdir -p "$OUTPUT_PATH/Scenes"
    cp -R bin/. "$OUTPUT_PATH/Scenes/"
    rm -rf bin
fi
