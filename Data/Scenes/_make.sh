#!/usr/bin/env bash

# Bash equivalent of Data/Scenes/_make.bat.
# Usage: _make.sh [clean|no_bar|no_pause]
# No argument cleans first; no_bar skips copying into the output folder.
# bin/ holds the committed BDAE models. They are source data (engine animation,
# material and event records plus the unrefined meshes), so they are staged as
# they are and never removed. Refined meshes come from the .blend files:
# see ../../rebuild_Linux.sh and models.json.
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

if [[ ! -d bin ]]; then
    echo "Data/Scenes/bin is missing; restore it from version control." >&2
    exit 1
fi

if [[ -z "$arg" ]]; then
    OUTPUT_PATH="$OUTPUT_PATH" bash "$dir/_clean.sh"
fi

echo
echo "**************************************************************"
echo "   Make Scenes"
echo "**************************************************************"

if [[ "$arg" != no_bar ]]; then
    echo
    echo "**************************************************************"
    echo "   Archiving Scenes"
    echo "**************************************************************"
    mkdir -p "$OUTPUT_PATH/Scenes"
    cp -R bin/. "$OUTPUT_PATH/Scenes/"
    for scene in ./*/; do
        name="${scene#./}"
        name="${name%/}"
        [[ "$name" == bin ]] && continue
        mkdir -p "$OUTPUT_PATH/Scenes/$name"
        # Scene textures sit beside the .blend files; they override bin/.
        for f in "$scene"*.png "$scene"*.bmp; do
            cp -f -- "$f" "$OUTPUT_PATH/Scenes/$name/"
        done
    done
fi
