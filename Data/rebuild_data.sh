#!/usr/bin/env bash
# Linux equivalent of rebuild_data.bat
# Cleans then rebuilds all Data.
set -euo pipefail

data_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"

"$data_dir/clean.sh"
"$data_dir/make_data.sh"
