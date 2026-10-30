"""FBX preview driver tests; the Blender export itself runs only in the real pipeline."""
import contextlib
import io
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

import build_fbx as previews

ROOT = Path(__file__).resolve().parents[2]


class FbxPreviewTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.scenes = Path(self.temporary.name)
        self.manifest = self.scenes/'models.json'
        self.manifest.write_text(json.dumps(dict(version=1, assets=[
            dict(blend='Goblin/goblin_smooth.blend', template='bin/Goblin/goblin.bdae', output='Goblin/goblin.bdae'),
            dict(blend='Gates/gate_01_smooth.blend', template='bin/Gates/gate_01.bdae', output='Gates/gate_01.bdae')])))
        self.commands = []

    def tearDown(self):
        self.temporary.cleanup()

    def run_tool(self, args, **kwargs):
        self.commands.append(args)
        return subprocess.CompletedProcess(args, 0)

    def build(self, **kwargs):
        with patch('build_fbx.shutil.which', return_value='/usr/bin/blender'), \
                patch('build_fbx.subprocess.run', self.run_tool), contextlib.redirect_stdout(io.StringIO()):
            previews.build(self.manifest, **kwargs)

    def test_previews_sit_beside_the_bdae_output_with_an_fbx_suffix(self):
        entry = dict(output='Goblin/goblin.bdae')
        self.assertEqual(previews.fbx_path(self.scenes, entry), (self.scenes/'Goblin/goblin.fbx').resolve())

    def test_output_cannot_leave_the_scene_directory(self):
        with self.assertRaisesRegex(ValueError, 'leaves the scene directory'):
            previews.fbx_path(self.scenes, dict(output='../escape.bdae'))

    def test_every_registered_scene_is_exported_by_default(self):
        self.build()
        self.assertEqual([c[c.index('--output')+1] for c in self.commands],
                         [str((self.scenes/'Goblin/goblin.fbx').resolve()), str((self.scenes/'Gates/gate_01.fbx').resolve())])
        for command in self.commands:
            self.assertEqual(command[1:3], ['--background', '--factory-startup'])
            self.assertIn('--python-exit-code', command)
            self.assertEqual(Path(command[command.index('--python')+1]).name, 'export_fbx.py')

    def test_only_selects_scenes_and_rejects_unregistered_ones(self):
        self.build(only=['Gates/gate_01.bdae'])
        self.assertEqual(len(self.commands), 1)
        with self.assertRaisesRegex(ValueError, 'Not registered in models.json: Nothing/here.bdae'):
            self.build(only=['Nothing/here.bdae'])

    def test_blender_is_required(self):
        with patch('build_fbx.shutil.which', return_value=None), self.assertRaisesRegex(ValueError, 'Blender 5.2'):
            previews.build(self.manifest)

    def test_every_registered_scene_has_a_committed_preview(self):
        config = json.loads((ROOT/'Data/Scenes/models.json').read_text())
        for entry in config['assets']:
            path = previews.fbx_path(ROOT/'Data/Scenes', entry)
            with self.subTest(output=entry['output']):
                self.assertEqual(path.read_bytes()[:20], b'Kaydara FBX Binary  ')


if __name__ == '__main__':
    unittest.main()
