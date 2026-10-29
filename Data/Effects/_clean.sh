#!/usr/bin/env bash

# Linux equivalent of Data/Effects/_clean.bat
# Removes the Effects build output directory.
set -euo pipefail

source "$(dirname -- "${BASH_SOURCE[0]}")/../_env.sh"

PACK_NAME="Effects"

echo
echo "**************************************************************"
echo "   Cleaning $PACK_NAME"
echo "**************************************************************"

rm -rf "$OUTPUT_PATH/$PACK_NAME"
