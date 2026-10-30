#!/usr/bin/env bash

# Bash equivalent of _makePVR.bat: convert local TGA textures to PVRTC4.
# PVR_TOOL may name a native executable or wrapper accepting the legacy flags.
# The bundled Windows PVRTexTool is run through Wine by default.
set -euo pipefail
shopt -s nullglob

source "$(dirname -- "${BASH_SOURCE[0]}")/../../_env.sh"
dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
cd "$dir"
textures=(*.tga)
(( ${#textures[@]} )) || exit 0

tool="${PVR_TOOL:-$PROJECT_PATH/Tools/PVRTexTool/PVRTexTool.exe}"
if [[ "${tool,,}" == *.exe ]]; then
    if [[ ! -f "$tool" ]] || ! command -v wine >/dev/null 2>&1; then
        echo "PVR conversion requires Wine and $tool; alternatively set PVR_TOOL to a native converter or wrapper accepting the legacy PVRTexTool flags." >&2
        exit 1
    fi
    tool_command=(wine "$tool")
else
    if ! tool="$(command -v -- "$tool")"; then
        echo "PVR_TOOL must name an executable converter or wrapper accepting the legacy PVRTexTool flags." >&2
        exit 1
    fi
    tool_command=("$tool")
fi

for f in "${textures[@]}"; do
    "${tool_command[@]}" "-i$f" -q1 -m -nt -fOGLPVRTC4 -pvrtciterations8 -p "-o${f%.tga}" -yflip0
done
