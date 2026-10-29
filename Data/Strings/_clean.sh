#!/usr/bin/env bash

# Linux equivalent of Data/Strings/_clean.bat
# Removes the Strings build artifacts (bin/, temp, output pack dir).
set -euo pipefail

source "$(dirname -- "${BASH_SOURCE[0]}")/../_env.sh"

PACK_NAME="Strings"
dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"

echo
echo "**************************************************************"
echo "   Cleaning $PACK_NAME"
echo "**************************************************************"

rm -rf "$dir/bin"

# TEMP_PATH is not defined in the Linux environment; clean it only if set.
if [[ -n "${TEMP_PATH:-}" && -d "$TEMP_PATH/$PACK_NAME" ]]; then
    rm -rf "$TEMP_PATH/$PACK_NAME"
fi

rm -rf "$OUTPUT_PATH/$PACK_NAME"
