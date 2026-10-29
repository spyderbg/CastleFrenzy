#!/usr/bin/env bash
# Linux equivalent of archiving_data.bat
# Zips the runtime asset directory into resources.pack, excluding sprite metadata.
set -euo pipefail

source "$(dirname -- "${BASH_SOURCE[0]}")/_env.sh"

echo
echo "**************************************************************"
echo "   Archiving Data"
echo "**************************************************************"

archive_name="resources.pack"
cd "$OUTPUT_PATH"

rm -f "$archive_name"

python3 "$PROJECT_TOOLS_PATH/zip_directory.py" "$archive_name" "." \
    --exclude-pattern "*.sprite" \
    --exclude-pattern "*.bsprite"
