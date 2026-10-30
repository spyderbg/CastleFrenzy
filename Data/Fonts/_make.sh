#!/usr/bin/env bash

# Linux equivalent of Data/Fonts/_make.bat
# Stages the font metadata and textures into the sprite pipeline, then
# archives the generated data.
#
# Usage: _make.sh [clean|no_bar]
#   clean    - clean artifacts and exit
#   (none)   - clean, build, and archive
#   no_bar   - build without archiving
set -euo pipefail
shopt -s nullglob

source "$(dirname -- "${BASH_SOURCE[0]}")/../_env.sh"

PACK_NAME="Fonts"
dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
cd "$dir"

arg="${1:-}"

# Clean data (mirrors _make.bat: clean when arg is "clean" or empty).
if [[ "$arg" == "clean" ]]; then
    "$dir/_clean.sh"
    exit 0
fi
if [[ "$arg" == "" ]]; then
    "$dir/_clean.sh"
fi

echo
echo "**************************************************************"
echo "   Make $PACK_NAME"
echo "**************************************************************"

# Stage font metadata and textures for the sprite pipeline.
mkdir -p bin "$PROJECT_DATA_PATH/Sprites/$PACK_NAME"
cp -f *.yaml "$PROJECT_DATA_PATH/Sprites/$PACK_NAME"/ 2>/dev/null || true
cp -f *.png  "$PROJECT_DATA_PATH/Sprites/$PACK_NAME"/ 2>/dev/null || true
cp -f *.tga  "$PROJECT_DATA_PATH/Sprites/$PACK_NAME"/ 2>/dev/null || true
cp -f *.txt  bin/ 2>/dev/null || true

# Authored PNG textures are staged unchanged.

# Archive (mirrors the "not no_bar" section of _make.bat).
if [[ "$arg" != "no_bar" ]]; then
    echo
    echo "**************************************************************"
    echo "   Archiving $PACK_NAME"
    echo "**************************************************************"

    mkdir -p "$OUTPUT_PATH/$PACK_NAME"
    cp -r bin/. "$OUTPUT_PATH/$PACK_NAME"/
    rm -rf bin
fi
