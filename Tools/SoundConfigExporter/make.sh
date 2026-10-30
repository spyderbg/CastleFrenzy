#!/usr/bin/env bash
# Compatibility launcher; the Python exporter needs no compilation.
set -euo pipefail
tool_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
if (( $# == 0 )); then set -- --help; fi
exec "${PYTHON_BIN:-python3}" "$tool_dir/../../tools/sound_config_exporter.py" "$@"
