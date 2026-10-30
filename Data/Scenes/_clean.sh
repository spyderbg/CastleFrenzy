#!/usr/bin/env bash

# Bash equivalent of Data/Scenes/_clean.bat.
set -euo pipefail

source "$(dirname -- "${BASH_SOURCE[0]}")/../_env.sh"
dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"

echo
echo "**************************************************************"
echo "   Cleaning Scenes"
echo "**************************************************************"

find "$dir" -type f -name '*.log' -delete
rm -rf -- "$dir/bin" "$OUTPUT_PATH/Scenes"
