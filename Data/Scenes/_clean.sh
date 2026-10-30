#!/usr/bin/env bash

# Bash equivalent of Data/Scenes/_clean.bat.
set -euo pipefail

source "$(dirname -- "${BASH_SOURCE[0]}")/../_env.sh"
dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"

echo
echo "**************************************************************"
echo "   Cleaning Scenes"
echo "**************************************************************"

# bin/ is committed source data (BDAE models), so only logs and output are removed.
find "$dir" -type f -name '*.log' -delete
rm -rf -- "$OUTPUT_PATH/Scenes"
