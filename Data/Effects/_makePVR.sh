#!/usr/bin/env bash

# Linux equivalent of Data/Effects/_makePVR.bat
# Converts the .tga effect textures to .pvr (PVRTC4) in place.
#
# PVRTexTool.exe is Windows-only and has no Linux binary. If a PVR encoder
# (pvrtexcli, from Imagination's PVR Tools) is on the PATH it is used;
# otherwise the already-committed .pvr files are left as-is (PVR generation
# is provided by the modern pipeline, rebuild_Linux.sh).
set -euo pipefail
shopt -s nullglob

source "$(dirname -- "${BASH_SOURCE[0]}")/../_env.sh"

dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
cd "$dir"

# Locate a PVR encoder. PVRTexTool.exe is Windows-only; pvrtexcli is the
# Linux equivalent.
PVR_TOOL=""
if command -v pvrtexcli >/dev/null 2>&1; then
    PVR_TOOL="pvrtexcli"
fi

if [[ -z "$PVR_TOOL" ]]; then
    echo
    echo "**************************************************************"
    echo "   PVRTexTool has no Linux binary; skipping PVR generation."
    echo "   The committed .pvr files are used as-is (see rebuild_Linux.sh)."
    echo "**************************************************************"
    exit 0
fi

for f in *.tga; do
    [[ -e "$f" ]] || continue
    base="${f%.tga}"
    "$PVR_TOOL" -i "$f" -q1 -r0 -x128 -y128 -m -nt -fOGLPVRTC4 -pvrtciterations1 -p -o "$base" -yflip0
done
