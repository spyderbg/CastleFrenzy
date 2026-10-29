#!/usr/bin/env bash
# Linux equivalent of clean.bat
# Cleans the build artifacts of every pack in DATA_BUILD_FLOW.
# Mirrors the per-pack _clean.bat scripts (bin/, logs, output dir, extras).
set -euo pipefail

source "$(dirname -- "${BASH_SOURCE[0]}")/_env.sh"
source "$PROJECT_DATA_PATH/make_prepare.sh"

echo
echo "**************************************************************"
echo "   Cleaning Data"
echo "**************************************************************"

for pack in "${DATA_BUILD_FLOW[@]}"; do
    dir="$PROJECT_DATA_PATH/$pack"
    echo
    echo "Cleaning $pack"

    # Common artifacts (present in every _clean.bat).
    rm -rf "$dir/bin"
    rm -f "$dir"/*.log
    rm -rf "$OUTPUT_PATH/$pack"

    # Pack-specific extras.
    case "$pack" in
        Fonts)
            # Fonts/_clean.bat also removes the copied sprite metadata.
            rm -rf "$PROJECT_DATA_PATH/Sprites/Fonts"
            ;;
        Sprites)
            # Sprites/_clean.bat also removes the generated AuroraMX command file.
            rm -f "$dir/Sprites.sprcmd"
            ;;
        Sounds)
            # Sounds/_clean.bat also removes the generated audio packs.
            rm -f "$OUTPUT_PATH"/*.pack
            ;;
    esac
done
