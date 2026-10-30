#!/usr/bin/env bash

# Linux equivalent of Data/Effects/_make.bat
# Copies the effect textures and configuration into the output directory.
#
# Usage: _make.sh [clean]
#   clean    - clean artifacts and exit
#   (none)   - clean, then build
set -euo pipefail
shopt -s nullglob

source "$(dirname -- "${BASH_SOURCE[0]}")/../_env.sh"

PACK_NAME="Effects"
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

mkdir -p "$OUTPUT_PATH/$PACK_NAME"
cp -f *.png "$OUTPUT_PATH/$PACK_NAME"/ 2>/dev/null || true
cp -f effects.xml "$OUTPUT_PATH/$PACK_NAME"/ 2>/dev/null || true
