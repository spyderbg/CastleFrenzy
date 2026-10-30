#!/usr/bin/env python3
"""Copy authored sprite YAML unchanged into game assets; never export binaries."""
import argparse
from pathlib import Path
import re
import shutil
import sys
import tempfile

YAML_EXTENSIONS = {".yaml", ".yml"}
SPRITE_EXTENSIONS = YAML_EXTENSIONS | {".sprite", ".bsprite"}


def sprite_sources(source):
    files = {}
    for path in sorted(source.rglob("*")):
        relative = path.relative_to(source)
        if not path.is_file() or path.suffix.lower() not in YAML_EXTENSIONS or \
                any(part in {"bin", ".svn"} for part in relative.parts):
            continue
        if relative.with_suffix("") in {p.with_suffix("") for p in files}:
            raise ValueError(f"Duplicate YAML sprite: {relative}")
        files[relative] = path
    if not files:
        raise ValueError(f"No sprite YAML files found in {source}")
    return files


def copy_sprites(source, destination, exports=None):
    files = sprite_sources(source)
    # Verify a supplied batch contains current byte-for-byte copies, so metadata
    # and generated headers cannot silently come from different source versions.
    if exports is not None:
        for relative, original in files.items():
            copied = exports / relative
            if not copied.is_file() or copied.read_bytes() != original.read_bytes():
                raise ValueError(f"Missing or stale YAML copy: {copied}; rerun the AuroraMX batch")
        files = {relative: exports / relative for relative in files}
    for path in destination.rglob("*"):
        if path.is_file() and path.suffix.lower() in SPRITE_EXTENSIONS:
            path.unlink()
    for relative, path in files.items():
        target = destination / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(path, target)
    return len(files)


def copy_headers(exports, destination):
    macros = lambda text: dict(re.findall(r"^\s*#define\s+(\w+)\s+(\d+)", text, re.MULTILINE))
    for header in sorted(exports.glob("*.h")):
        target = destination / header.name
        if not target.exists() or macros(target.read_text()) != macros(header.read_text()):
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(header, target)


def validate_assets(assets):
    files = [p for p in assets.rglob("*") if p.is_file()]
    if any(p.suffix.lower() == ".pvr" for p in files):
        raise ValueError("Assets contain PVR textures; replace them with PNG before building or packaging")
    if any(p.suffix.lower() == ".tga" for p in files):
        raise ValueError("Assets contain TGA textures; replace them with PNG before packaging")
    legacy = [p for p in files if p.suffix.lower() in {".sprite", ".bsprite"}]
    if legacy or (assets / "resources.pack").exists():
        raise ValueError("Assets contain legacy sprite data; rebuild resources before packaging")
    if not any(p.suffix.lower() in YAML_EXTENSIONS for p in (assets / "Sprites").rglob("*")):
        raise ValueError("Assets contain no sprite YAML files; rebuild resources before packaging")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--exports", type=Path, required=True)
    parser.add_argument("--seed", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--headers", type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    output = args.output.resolve()
    if not output.is_relative_to(root / "build") or output == args.seed.resolve() or \
            args.seed.resolve().is_relative_to(output):
        raise ValueError("Output must be a separate generated directory inside build/")
    marker = ".yaml-resources"
    if output.exists() and any(output.iterdir()) and not (output / marker).is_file():
        raise ValueError(f"Refusing to replace unmarked resource directory: {output}")
    output.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="yaml-assets-", dir=output.parent) as temporary:
        stage = Path(temporary) / "assets"
        shutil.copytree(args.seed, stage, ignore=shutil.ignore_patterns(
            "*.sprite", "*.bsprite", "resources.pack", ".svn", "*.bak", "*.log", "*.LOG"))
        for name in ("user_profile.xml", "default.xml"):
            (stage / name).unlink(missing_ok=True)
        count = copy_sprites(args.source, stage / "Sprites", args.exports)
        validate_assets(stage)
        (stage / marker).write_text("Sprite metadata: YAML (copied unchanged)\n")
        if output.exists():
            shutil.rmtree(output)
        shutil.move(str(stage), output)
    copy_headers(args.exports, args.headers)
    print(f"Copied {count} YAML sprites into {output}")


if __name__ == "__main__":
    try:
        main()
    except (OSError, ValueError) as error:
        print(f"Sprite resource copy failed: {error}", file=sys.stderr)
        sys.exit(1)
