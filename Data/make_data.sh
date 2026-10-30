#!/usr/bin/env bash
# Linux equivalent of make_data.bat
# Generates the version header, builds every pack in DATA_BUILD_FLOW, then
# archives the result into resources.pack.
#
# Tool mapping (Windows -> Linux):
#   StringParser.jar / SoundConfigExporter.jar  -> java (OpenJDK)
#   AuroraMX (node)                             -> node / npm
#   zip_directory.py                            -> python3
#   ColladaConverter.exe                      -> legacy model staging here;
#       full texture/model conversion is provided
#       by the modern pipeline (rebuild_Linux.sh + tools/linux/build_resources.py).
set -euo pipefail
shopt -s nullglob

source "$(dirname -- "${BASH_SOURCE[0]}")/_env.sh"
source "$PROJECT_DATA_PATH/make_prepare.sh"

# Java tools live in the capital-T Tools/ directory (case differs from tools/).
TOOLS_CAP="$PROJECT_PATH/Tools"

banner() {
    echo
    echo "**************************************************************"
    echo "   $1"
    echo "**************************************************************"
}

# Move a generated header into HEADERS_OUT_PATH only if it is new or changed
# (mirrors the FC "no differences" check in the _make.bat scripts).
install_header() {
    local src="$1" base
    base="$(basename "$src" .h)"
    if [[ ! -e "$HEADERS_OUT_PATH/$base.h" ]]; then
        mv -f "$src" "$HEADERS_OUT_PATH/"
    elif ! cmp -s "$src" "$HEADERS_OUT_PATH/$base.h"; then
        mv -f "$src" "$HEADERS_OUT_PATH/"
    else
        rm -f "$src"
    fi
}

# ---------------------------------------------------------------------------
# Set version (mirrors make_data.bat).
# ---------------------------------------------------------------------------
{
    echo "#pragma once"
    echo "const wchar_t g_castle_frenzy_version_str[] = L\"${VERSION_MAJOR}.${VERSION_MINOR}.${VERSION_BUILD}\";"
} > "$HEADERS_OUT_PATH/version.h"

banner "Building Data"

# ---------------------------------------------------------------------------
# Strings (mirrors Data/Strings/_make.bat).
# ---------------------------------------------------------------------------
build_strings() {
    local dir="$PROJECT_DATA_PATH/Strings"
    banner "Make Strings"
    cd "$dir"
    mkdir -p bin
    "$JAVA_BIN" -jar "$TOOLS_CAP/StringParser.jar" fortress_defender_strings.xls bin 7
    # Exported headers and generated string tables.
    mv -f bin/*.hpp "$HEADERS_OUT_PATH"/ 2>/dev/null || true
    mv -f bin/*.gen . 2>/dev/null || true
    # Archive the remaining string data.
    mkdir -p "$OUTPUT_PATH/Strings"
    mv -f bin/* "$OUTPUT_PATH/Strings"/ 2>/dev/null || true
    rmdir bin 2>/dev/null || true
}

# ---------------------------------------------------------------------------
# Fonts (mirrors Data/Fonts/_make.bat).
# ---------------------------------------------------------------------------
build_fonts() {
    local dir="$PROJECT_DATA_PATH/Fonts"
    banner "Make Fonts"
    cd "$dir"
    mkdir -p bin "$PROJECT_DATA_PATH/Sprites/Fonts"
    # Stage font metadata and textures for the sprite pipeline.
    cp -f *.yaml "$PROJECT_DATA_PATH/Sprites/Fonts"/ 2>/dev/null || true
    cp -f *.png  "$PROJECT_DATA_PATH/Sprites/Fonts"/ 2>/dev/null || true
    cp -f *.tga  "$PROJECT_DATA_PATH/Sprites/Fonts"/ 2>/dev/null || true
    cp -f *.txt  bin/ 2>/dev/null || true
    # Authored PNG textures are staged unchanged.
    # Archive.
    mkdir -p "$OUTPUT_PATH/Fonts"
    cp -r bin/. "$OUTPUT_PATH/Fonts"/
    rm -rf bin
}

# ---------------------------------------------------------------------------
# Sprites (mirrors Data/Sprites/_make.bat).
# ---------------------------------------------------------------------------
build_sprites() {
    local dir="$PROJECT_DATA_PATH/Sprites"
    banner "Make Sprites"
    cd "$dir"
    mkdir -p bin
    rm -f bin/*.h

    # Generate the AuroraMX command file.
    local sprcmd="Sprites.sprcmd"
    echo "// Copy YAML metadata unchanged and generate runtime ID headers." > "$sprcmd"

    # Remove sprite-editor backup files.
    find . -name "*.bak" -delete 2>/dev/null || true

    local d f base
    for d in */; do
        d="${d%/}"
        [[ "$d" == "bin" ]] && continue
        mkdir -p "bin/$d"
        for f in "$d"/*.yaml; do
            [[ -e "$f" ]] || continue
            base="$(basename "$f" .yaml)"
            {
                echo
                echo "Load(\"$f\")"
                echo "ExportFramesID(\"bin/$d.h\", \"${d}_F_${base}_\", APPEND|TOUPPER)"
                echo "ExportAnimsID(\"bin/$d.h\", \"${d}_A_${base}_\", APPEND|TOUPPER)"
                echo "Copy(\"bin/$d/${base}.yaml\")"
            } >> "$sprcmd"
        done
        for f in "$d"/*.png; do [[ -e "$f" ]] && cp -f "$f" "bin/$d"/; done
        for f in "$d"/*.tga; do
            [[ -e "$f" ]] || continue
            [[ ! -e "${f%.tga}.png" ]] && cp -f "$f" "bin/$d"/
        done
    done

    # Run AuroraMX to copy YAML and generate the runtime ID headers.
    local auroramx="$TOOLS_CAP/AuroraMX"
    if [[ ! -x "$auroramx/node_modules/.bin/esbuild" ]]; then
        npm ci --prefix "$auroramx" --no-audit --no-fund
    fi
    npm run build:cli --prefix "$auroramx"
    node "$auroramx/build/cli.cjs" batch "$dir/$sprcmd"

    # Install generated ID headers.
    for f in bin/*.h; do
        [[ -e "$f" ]] || continue
        install_header "$f"
    done

    # Archive.
    mkdir -p "$OUTPUT_PATH/Sprites"
    find "$OUTPUT_PATH/Sprites" \( -name "*.bsprite" -o -name "*.sprite" \) -delete 2>/dev/null || true
    cp -r bin/. "$OUTPUT_PATH/Sprites"/
    rm -rf bin
}

# ---------------------------------------------------------------------------
# Scenes (mirrors Data/Scenes/_make.bat).
# ---------------------------------------------------------------------------
build_scenes() {
    local dir="$PROJECT_DATA_PATH/Scenes"
    banner "Make Scenes"
    cd "$dir"
    mkdir -p bin
    local d f
    for d in */; do
        d="${d%/}"
        [[ "$d" == "bin" ]] && continue
        mkdir -p "bin/$d"
        # ColladaConverter.exe (DAE -> BDAE) has no Linux binary; the modern
        # pipeline (rebuild_Linux.sh + Blender) performs that conversion.
        # Stage the already-converted BDAE models and textures.
        for f in "$d"/*.bdae; do [[ -e "$f" ]] && mv -f "$f" "bin/$d"/; done
        for f in "$d"/*.tga;  do [[ -e "$f" ]] && cp -f "$f" "bin/$d"/; done
        for f in "$d"/*.png;  do [[ -e "$f" ]] && cp -f "$f" "bin/$d"/; done
    done
    # Archive.
    mkdir -p "$OUTPUT_PATH/Scenes"
    cp -r bin/. "$OUTPUT_PATH/Scenes"/
    rm -rf bin
}

# ---------------------------------------------------------------------------
# Sounds (mirrors Data/Sounds/_make.bat).
# ---------------------------------------------------------------------------
build_sounds() {
    local dir="$PROJECT_DATA_PATH/Sounds"
    banner "Make Sounds"
    cd "$dir"

    # Export the sound configuration.
    "$JAVA_BIN" -jar "$TOOLS_CAP/SoundConfigExporter.jar" SoundConfiguration.xls sounds_config.xml
    mkdir -p "$OUTPUT_PATH/config"
    mv -f sounds_config.xml "$OUTPUT_PATH/config/"

    local d
    for d in */; do
        d="${d%/}"
        [[ "$d" == "bin" ]] && continue
        (
            cd "$dir/$d"
            mkdir -p bin
            rm -f "$OUTPUT_PATH/audio_$d.pack"
            find . -name "*.wav" -exec cp -f {} bin/ \;
            ( cd bin && python3 "$PROJECT_TOOLS_PATH/zip_directory.py" "$OUTPUT_PATH/audio_$d.pack" "." --stored )
            rm -rf bin
        )
    done

    # Generate the sound ID header. ListSoundFiles.exe has no Linux binary, so
    # replicate its output: Music then SFX, each sorted by filename, uppercased.
    python3 - "$OUTPUT_PATH" "$HEADERS_OUT_PATH" <<'PY'
import sys, zipfile
from pathlib import Path
out, headers = Path(sys.argv[1]), Path(sys.argv[2])
names = []
for group in ("Music", "SFX"):
    pack = out / f"audio_{group}.pack"
    if pack.is_file():
        with zipfile.ZipFile(pack) as z:
            names.extend(info.stem.upper() for info in sorted(z.infolist(), key=lambda i: i.filename.lower()))
header = "// Generated by Data/make_data.sh\n#pragma once\n\n"
header += "".join(f"#define {name:<50} {i}\n" for i, name in enumerate(names))
headers.mkdir(parents=True, exist_ok=True)
(headers / "sounds.h").write_text(header)
print(f"Generated sounds.h with {len(names)} sound IDs")
PY
}

# ---------------------------------------------------------------------------
# Effects (mirrors Data/Effects/_make.bat).
# ---------------------------------------------------------------------------
build_effects() {
    local dir="$PROJECT_DATA_PATH/Effects"
    banner "Make Effects"
    cd "$dir"
    mkdir -p "$OUTPUT_PATH/Effects"
    cp -f *.png "$OUTPUT_PATH/Effects"/ 2>/dev/null || true
    cp -f effects.xml "$OUTPUT_PATH/Effects"/ 2>/dev/null || true
}

# ---------------------------------------------------------------------------
# Build every pack in order.
# ---------------------------------------------------------------------------
for pack in "${DATA_BUILD_FLOW[@]}"; do
    case "$pack" in
        Strings) build_strings ;;
        Fonts)   build_fonts ;;
        Sprites) build_sprites ;;
        Scenes)  build_scenes ;;
        Sounds)  build_sounds ;;
        Effects) build_effects ;;
        *) echo "Unknown pack: $pack" >&2; exit 1 ;;
    esac
done

# Archiving Data (mirrors make_data.bat calling archiving_data.bat).
"$PROJECT_DATA_PATH/archiving_data.sh"

banner "Done"
