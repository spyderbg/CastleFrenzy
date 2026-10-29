#!/usr/bin/env bash
# Linux equivalent of make_prepare.bat
# Defines the pack build flow and creates the output directories.
set -euo pipefail

source "$(dirname -- "${BASH_SOURCE[0]}")/_env.sh"

# Create output directories (mirrors make_prepare.bat).
mkdir -p "$HEADERS_OUT_PATH"
mkdir -p "$OUTPUT_PATH"
