#!/usr/bin/env bash

# Linux equivalent of Data/Fonts/_clean.bat
# Removes the Fonts build artifacts (logs, bin/, staged sprite dir, output pack dir).
set -euo pipefail

source "$(dirname -- "${BASH_SOURCE[0]}")/../_env.sh"

PACK_NAME="Fonts"
dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"

echo
echo "**************************************************************"
echo "   Cleaning $PACK_NAME"
echo "**************************************************************"

# Remove build logs (mirrors `del /s /q *.log`).
find "$dir" -name "*.log" -delete 2>/dev/null || true

rm -rf "$dir/bin"
rm -rf "$PROJECT_DATA_PATH/Sprites/$PACK_NAME"
rm -rf "$OUTPUT_PATH/$PACK_NAME"
