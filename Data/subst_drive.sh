#!/usr/bin/env bash
# Linux equivalent of subst_drive.bat
# Windows maps the Data folder to drive P: with `subst`. The Linux equivalent is
# a symbolic link. By default a link named "P" is created next to the project;
# pass a target path to place it elsewhere:
#     ./subst_drive.sh /some/path/P
set -euo pipefail

data_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
link_path="${1:-$(cd -- "$data_dir/.." && pwd)/P}"

# Remove any existing link (mirrors `subst /D P:`).
if [[ -L "$link_path" ]]; then
    rm -f "$link_path"
fi

# Create the link to the Data folder (mirrors `subst P: <data>`).
ln -s "$data_dir" "$link_path"

echo "Mapped link:"
ls -l "$link_path"
