"""Cache invalidation tests; actual Blender export has a separate integration test."""
import contextlib
import io
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

import build_models as models


class ModelBuildTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        self.blender = self.root/'blender'
        self.blender.write_bytes(b'executable fixture')
        self.blend = self.root/'source.blend'
        self.blend.write_bytes(b'blend fixture')
        self.template = self.root/'source.bdae'
        self.template.write_bytes(b'template fixture')
        self.dependency = self.root/'paint.png'
        self.dependency.write_bytes(b'external texture fixture')
        self.manifest = self.root/'models.json'
        self.manifest.write_text(json.dumps(dict(version=1, assets=[dict(
            blend=self.blend.name, template=self.template.name, output='Goblin/model.bdae')])) )
        self.output = self.root/'generated'
        self.exports = 0
        self.validations = 0

    def tearDown(self):
        self.temporary.cleanup()

    def run_tool(self, args, **kwargs):
        if args[-1] == '--version':
            return subprocess.CompletedProcess(args, 0, stdout='Blender 5.2.2 LTS\n')
        if '--python' not in args:
            self.validations += 1
            return subprocess.CompletedProcess(args, 0)
        self.exports += 1
        target = Path(args[args.index('--output')+1])
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(b'exported model fixture')
        texture = target.with_suffix('.png')
        texture.write_bytes(self.dependency.read_bytes())
        target.with_suffix('.export.json').write_text(json.dumps(dict(
            template_sha256=models.digest(self.template), output_sha256=models.digest(target),
            texture=texture.name, dependencies=[str(self.dependency)],
            vertices=3, triangles=1, clips=['walk'], verification={'passed': True})))
        return subprocess.CompletedProcess(args, 0)

    def build(self):
        with patch.object(models.shutil, 'which', return_value=str(self.blender)), \
                patch.object(models.subprocess, 'run', side_effect=self.run_tool), \
                contextlib.redirect_stdout(io.StringIO()):
            return models.build(self.manifest, self.output, validator=self.root/'native-check')

    def test_cache_reuses_exports_but_always_runs_native_validator(self):
        first = self.build()
        second = self.build()
        self.assertEqual(first, second)
        self.assertEqual(self.exports, 1)
        self.assertEqual(self.validations, 2)

    def test_source_template_dependency_and_corrupt_output_invalidate_cache(self):
        self.build()
        for path in [self.blend, self.template, self.dependency,
                     self.output/'Goblin/model.png', self.output/'Goblin/model.bdae']:
            before = self.exports
            path.write_bytes(path.read_bytes()+b'changed')
            self.build()
            self.assertEqual(self.exports, before+1, str(path))

    def test_escaping_output_is_rejected_before_export(self):
        config = json.loads(self.manifest.read_text())
        config['assets'][0]['output'] = '../escape.bdae'
        self.manifest.write_text(json.dumps(config))
        with self.assertRaisesRegex(ValueError, 'leaves the output directory'):
            self.build()
        self.assertEqual(self.exports, 0)

    def test_game_build_rejects_tga_exports(self):
        with self.assertRaisesRegex(ValueError, 'must use PNG'):
            models.build(self.manifest, self.output, texture_format='tga')
        self.assertEqual(self.exports, 0)


if __name__ == '__main__':
    unittest.main()
