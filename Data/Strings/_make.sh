#!/usr/bin/env bash

# Linux equivalent of Data/Strings/_make.bat
# Exports localized strings from the .XLS source via tools/string_parser.py, then
# stages the generated data and headers.
#
# Usage: _make.sh [clean|no_bar|no_pause]
#   clean    - clean artifacts and exit
#   (none)   - clean, build, and archive
#   no_bar   - build without archiving
#   no_pause - build and archive (no interactive pause)
set -euo pipefail
shopt -s nullglob

source "$(dirname -- "${BASH_SOURCE[0]}")/../_env.sh"

PACK_NAME="Strings"
dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
[[ "$OUTPUT_PATH" == /* ]] || OUTPUT_PATH="$PWD/$OUTPUT_PATH"
[[ "$HEADERS_OUT_PATH" == /* ]] || HEADERS_OUT_PATH="$PWD/$HEADERS_OUT_PATH"
cd "$dir"

STRING_FILE_NAME="fortress_defender_strings.xls"
arg="${1:-}"

# Clean data (mirrors _make.bat: clean when arg is "clean" or empty).
if [[ "$arg" == "clean" ]]; then
    OUTPUT_PATH="$OUTPUT_PATH" bash "$dir/_clean.sh"
    exit 0
fi
if [[ "$arg" == "" ]]; then
    OUTPUT_PATH="$OUTPUT_PATH" bash "$dir/_clean.sh"
fi

echo
echo "**************************************************************"
echo "   Make $PACK_NAME"
echo "**************************************************************"

# Export text for all languages from the .XLS file.
"$PYTHON_BIN" "$PROJECT_TOOLS_PATH/string_parser.py" "$STRING_FILE_NAME" bin 7

# Exported headers and generated string tables.
mkdir -p "$HEADERS_OUT_PATH"
mv -f bin/*.hpp "$HEADERS_OUT_PATH"/ 2>/dev/null || true
mv -f bin/*.gen . 2>/dev/null || true

# Archive (mirrors the "not no_bar" section of _make.bat).
if [[ "$arg" != "no_bar" ]]; then
    echo
    echo "**************************************************************"
    echo "   Archiving $PACK_NAME"
    echo "**************************************************************"

    mkdir -p "$OUTPUT_PATH/$PACK_NAME"
    mv -f bin/* "$OUTPUT_PATH/$PACK_NAME"/ 2>/dev/null || true
    rmdir bin 2>/dev/null || true

    # Install any generated headers that are new or changed.
    for f in "$OUTPUT_PATH"/*.h; do
        [[ -e "$f" ]] || continue
        base="$(basename "$f" .h)"
        if [[ ! -e "$HEADERS_OUT_PATH/$base.h" ]]; then
            mv -f "$f" "$HEADERS_OUT_PATH/"
        elif ! cmp -s "$f" "$HEADERS_OUT_PATH/$base.h"; then
            mv -f "$f" "$HEADERS_OUT_PATH/"
        else
            rm -f "$f"
        fi
    done
fi
