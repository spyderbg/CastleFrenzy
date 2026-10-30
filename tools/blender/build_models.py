#!/usr/bin/env python3
"""Export registered Blender authoring files for the Linux game resource build."""
import argparse
import hashlib
import json
import os
import re
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile

ROOT = Path(__file__).resolve().parents[2]
INDEX = 'blender-models.json'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def below(root, relative):
    result = (root/relative).resolve()
    if not result.is_relative_to(root.resolve()) or result == root.resolve():
        raise ValueError(f'Export path leaves the output directory: {relative}')
    return result


def atomic_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary = tempfile.mkstemp(dir=path.parent, prefix=path.name+'.', suffix='.tmp')
    try:
        with os.fdopen(descriptor, 'w') as stream:
            json.dump(value, stream, indent=2)
            stream.write('\n')
        os.replace(temporary, path)
    finally:
        Path(temporary).unlink(missing_ok=True)


def build(manifest, output, blender='blender', texture_format='png', validator=None):
    if texture_format != 'png':
        raise ValueError('Game model textures must use PNG')
    manifest, output = Path(manifest).resolve(), Path(output).resolve()
    config = json.loads(manifest.read_text())
    if config.get('version') != 1 or not isinstance(config.get('assets'), list):
        raise ValueError('Expected a version 1 Blender asset manifest')
    output.mkdir(parents=True, exist_ok=True)
    index_path = output/INDEX
    previous = json.loads(index_path.read_text()) if index_path.exists() else {}
    executable = shutil.which(blender)
    if config['assets'] and not executable:
        raise ValueError('Blender 5.2+ is required for registered model sources; set BLENDER_BIN to its executable')
    version = subprocess.run([executable, '--version'], check=True, capture_output=True, text=True).stdout.splitlines()[0] if config['assets'] else ''
    match = re.search(r'Blender (\d+)\.(\d+)', version)
    if config['assets'] and (not match or tuple(map(int, match.groups())) < (5, 2)):
        raise ValueError('The model exporter requires Blender 5.2 or newer')
    tool = Path(__file__).resolve().parent
    code = {p.name: digest(p) for p in sorted(tool.glob('*.py')) if not p.name.startswith('test_')}
    assets, files = [], {}
    used_outputs = set()
    for entry in config['assets']:
        blend = (manifest.parent/entry['blend']).resolve()
        template = (manifest.parent/entry['template']).resolve()
        target = below(output, entry['output'])
        if target in used_outputs or target.suffix.lower() != '.bdae':
            raise ValueError('Each Blender asset must have a unique .bdae output')
        used_outputs.add(target)
        inputs = dict(blend=digest(blend), template=digest(template), tools=code,
                      texture_format=texture_format, blender=str(Path(executable).resolve()),
                      blender_mtime=Path(executable).stat().st_mtime_ns, blender_version=version, rig=entry.get('rig'))
        old = next((a for a in previous.get('assets', []) if a['output'] == entry['output']), {})
        cached = old.get('inputs') == inputs
        if cached:
            for relative, expected in old.get('files', {}).items():
                p = below(output, relative)
                if not p.is_file() or digest(p) != expected:
                    cached = False
            for dependency, expected in old.get('dependencies', {}).items():
                p = Path(dependency)
                if not p.is_file() or digest(p) != expected:
                    cached = False
        if not cached:
            args = [executable, '--background', '--factory-startup', str(blend), '--python-exit-code', '1',
                    '--python', str(tool/'export_bdae.py'), '--', '--template', str(template),
                    '--output', str(target), '--texture-format', texture_format]
            if entry.get('rig'):
                args.extend(['--rig', entry['rig']])
            print(f'Export Blender model: {entry["blend"]} -> {entry["output"]}', flush=True)
            subprocess.run(args, cwd=ROOT, check=True)
        else:
            print(f'Blender model unchanged: {entry["blend"]}', flush=True)
        report_path = target.with_suffix('.export.json')
        report = json.loads(report_path.read_text())
        if report['template_sha256'] != inputs['template'] or digest(target) != report['output_sha256']:
            raise ValueError('Blender export report does not match the binary/template')
        outputs = [target]
        if report.get('texture'):
            outputs.append(below(target.parent, report['texture']))
        generated = {str(p.relative_to(output)): digest(p) for p in outputs}
        dependencies = {p: digest(Path(p)) for p in report.get('dependencies', [])}
        if validator:
            command = [str(Path(validator).resolve()), str(target), '--texture-root', str(template.parent)]
            if report.get('mode') == 'scene mesh':
                command.append('--geometry-only')  # Original textures are staged by build_resources.py.
            subprocess.run(command, check=True, cwd=ROOT)
        assets.append(dict(blend=entry['blend'], output=entry['output'], inputs=inputs,
                           files=generated, dependencies=dependencies,
                           vertices=report['vertices'], triangles=report['triangles'],
                           clips=len(report['clips']), verification=report['verification']))
        for name, value in generated.items():
            if name in files and files[name] != value:
                raise ValueError(f'Two models export conflicting files: {name}')
            files[name] = value
    result = dict(version=1, exporter='Blender BDAE', manifest=str(manifest), assets=assets, files=files)
    atomic_json(index_path, result)
    print(f'Blender exported {len(assets)} model(s): {output}', flush=True)
    return result


def validate_staged(output, scene_directory, validator, texture_format='png'):
    index = json.loads((Path(output)/INDEX).read_text())
    for asset in index['assets']:
        target = below(Path(scene_directory), asset['output'])
        expected = index['files'][asset['output']]
        if digest(target) != expected:
            raise ValueError('Staged model does not match its verified export: '+asset['output'])
        command = [str(Path(validator).resolve()), str(target)]
        if texture_format != 'png':
            command.append('--geometry-only')  # Compressed GPU texture formats are not IImage inputs.
        subprocess.run(command, check=True, cwd=ROOT)
    print(f'Native validation passed for {len(index["assets"])} staged Blender scenes', flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--manifest', type=Path, default=ROOT/'Data/Scenes/models.json')
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--blender', default=os.environ.get('BLENDER_BIN', 'blender'))
    parser.add_argument('--texture-format', choices=['png'], default='png')
    parser.add_argument('--validator', type=Path)
    parser.add_argument('--validate-staged', type=Path, help='Check exported models and resolved PNG images in staged Scenes/')
    args = parser.parse_args()
    if args.validate_staged:
        if not args.validator: parser.error('--validate-staged requires --validator')
        validate_staged(args.output, args.validate_staged, args.validator, args.texture_format)
    else:
        build(args.manifest, args.output, args.blender, args.texture_format, args.validator)


if __name__ == '__main__':
    main()
