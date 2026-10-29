#!/usr/bin/env bash

# Linux equivalent of Data/Sounds/_clean.bat
# Removes the Sounds build artifacts (bin/, generated audio packs).
set -euo pipefail

source "$(dirname -- "${BASH_SOURCE[0]}")/../_env.sh"

PACK_NAME="Sounds"
dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"

echo
echo "**************************************************************"
echo "   Cleaning $PACK_NAME"
echo "**************************************************************"

rm -rf "$dir/bin"
rm -f "$OUTPUT_PATH"/*.pack
