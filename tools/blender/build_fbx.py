#!/usr/bin/env python3
"""Export every registered Blender scene to FBX for the Level Editor's model preview.

python3 tools/blender/build_fbx.py [--blender /path/to/blender] [--only Goblin/goblin.bdae ...]

Each Data/Scenes/models.json entry writes <output>.fbx beside its .blend folder, e.g.
Data/Scenes/Goblin/goblin.fbx. These files are committed: the Level Editor reads them
straight from the source tree, so rerun this after changing a registered .blend file.
"""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]


def fbx_path(scene_directory, entry):
    """Preview location for a models.json entry (its .bdae output with an .fbx suffix)."""
    result = (scene_directory/entry['output']).with_suffix('.fbx').resolve()
    if not result.is_relative_to(scene_directory.resolve()):
        raise ValueError(f'Export path leaves the scene directory: {entry["output"]}')
    return result


def build(manifest, blender='blender', only=()):
    manifest = Path(manifest).resolve()
    config = json.loads(manifest.read_text())
    if config.get('version') != 1 or not isinstance(config.get('assets'), list):
        raise ValueError('Expected a version 1 Blender asset manifest')
    executable = shutil.which(blender)
    if not executable:
        raise ValueError('Blender 5.2+ is required; set BLENDER_BIN to its executable')
    entries = [e for e in config['assets'] if not only or e['output'] in only]
    unknown = set(only)-{e['output'] for e in entries}
    if unknown:
        raise ValueError('Not registered in models.json: '+', '.join(sorted(unknown)))
    tool = Path(__file__).resolve().parent
    for entry in entries:
        target = fbx_path(manifest.parent, entry)
        print(f'Export FBX: {entry["blend"]} -> {target.relative_to(manifest.parent)}', flush=True)
        subprocess.run([executable, '--background', '--factory-startup', str(manifest.parent/entry['blend']),
                        '--python-exit-code', '1', '--python', str(tool/'export_fbx.py'),
                        '--', '--output', str(target)], cwd=ROOT, check=True)
    print(f'Exported {len(entries)} FBX preview(s)', flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--manifest', type=Path, default=ROOT/'Data/Scenes/models.json')
    parser.add_argument('--blender', default=os.environ.get('BLENDER_BIN', 'blender'))
    parser.add_argument('--only', nargs='*', default=[], metavar='OUTPUT',
                        help='models.json "output" values to export (default: all)')
    args = parser.parse_args()
    try:
        build(args.manifest, args.blender, args.only)
    except ValueError as error:
        sys.exit(str(error))


if __name__ == '__main__':
    main()
