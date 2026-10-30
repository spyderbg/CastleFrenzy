"""Run: python tools/test_sound_config_exporter.py (requirements-exporters.txt)."""
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
import xml.etree.ElementTree as ET

import sound_config_exporter as exporter

ROOT = Path(__file__).resolve().parents[1]
WORKBOOK = ROOT / "Data/Sounds/SoundConfiguration.xls"


def elements(data):
    # The game format deliberately has sibling elements rather than one root.
    return ET.fromstring(b"<root>" + data.split(b"?>", 1)[1] + b"</root>")


class SoundConfigTests(unittest.TestCase):
    def test_project_workbook_matches_shipped_xml_byte_for_byte(self):
        with tempfile.TemporaryDirectory() as temporary:
            target = Path(temporary) / "sounds.xml"
            self.assertEqual(exporter.export_sound_config(WORKBOOK, target), 77)
            self.assertEqual(target.read_bytes(), (ROOT / "Build/bin/config/sounds_config.xml").read_bytes())
            self.assertEqual(len(elements(target.read_bytes())), 78)

    def test_global_defaults_and_empty_per_sound_values_inherit(self):
        document = elements(exporter.render_config([["sfx_test", "", "", "", "", "", ""]]))
        self.assertEqual(document[0].attrib, {"id": "Global_SoundConfig"})
        self.assertEqual({a.attrib['name']: a.attrib['value'] for a in document[0]},
                         {"Obligatory": "0", "Overlap": "2", "Priority": "3", "Volume": "1"})
        self.assertEqual(document[1].attrib['extends'], "Global_SoundConfig")
        self.assertEqual([(a.tag, a.attrib) for a in document[1]],
                         [("int", {"name": "Obligatory", "value": "0"})])

    def test_explicit_overrides_and_escaped_unicode_names(self):
        document = elements(exporter.render_config([["sfx_caf\u00e9&\"", "", "", "Yes", "1", "5", "0.25"]]))
        sound = document[1]
        self.assertEqual(sound.attrib['id'], 'sfx_caf\u00e9&".wav')
        self.assertEqual({a.attrib['name']: a.attrib['value'] for a in sound},
                         {"Obligatory": "1", "Overlap": "1", "Priority": "5", "Volume": "0.25"})

    def test_duplicate_and_invalid_values_are_rejected(self):
        row = ["sfx_test", "", "", "Yes", "2", "3", "1"]
        with self.assertRaisesRegex(exporter.SoundConfigExportError, "Duplicate sound ID"):
            exporter.render_config([row, row])
        for column, value in [(4, "two"), (5, "1.5"), (6, "nan"), (6, "inf")]:
            bad = row.copy(); bad[column] = value
            with self.subTest(value=value), self.assertRaises(exporter.SoundConfigExportError):
                exporter.render_config([bad])
        with self.assertRaisesRegex(exporter.SoundConfigExportError, "seven columns"):
            exporter.render_config([["sfx_short"]])

    def test_workbook_selection_and_numeric_cells(self):
        import xlrd
        class Book:
            released = False
            def sheet_names(self): return [exporter.SHEET_NAME]
            def sheet_by_name(self, name):
                class Sheet:
                    ncols, nrows = 7, 2
                    def cell(self, row, column):
                        if row == 0: return xlrd.sheet.Cell(xlrd.XL_CELL_TEXT, "heading")
                        values = ["sfx_test", "", "", "Yes", 2.0, 3.0, 0.125]
                        return xlrd.sheet.Cell(xlrd.XL_CELL_TEXT if column < 4 else xlrd.XL_CELL_NUMBER, values[column])
                return Sheet()
            def release_resources(self): self.released = True
        book = Book()
        with patch.object(xlrd, "open_workbook", return_value=book):
            self.assertEqual(exporter.read_sound_rows(Path("fixture.xls")),
                             [["sfx_test", "", "", "Yes", "2", "3", "0.125"]])
        self.assertTrue(book.released)
        book = Book()
        with patch.object(book, "sheet_names", return_value=["Wrong sheet"]), \
             patch.object(xlrd, "open_workbook", return_value=book), \
             self.assertRaisesRegex(exporter.SoundConfigExportError, "Missing"):
            exporter.read_sound_rows(Path("fixture.xls"))
        self.assertTrue(book.released)

    def test_repeat_export_preserves_timestamp_and_existing_mode(self):
        with tempfile.TemporaryDirectory() as temporary:
            target = Path(temporary) / "sounds.xml"
            exporter.export_sound_config(WORKBOOK, target)
            previous = target.stat().st_mtime_ns
            exporter.export_sound_config(WORKBOOK, target)
            self.assertEqual(target.stat().st_mtime_ns, previous)
            target.chmod(0o640)
            target.write_bytes(b"outdated export")
            exporter.export_sound_config(WORKBOOK, target)
            self.assertEqual(target.stat().st_mode & 0o777, 0o640)

    def test_invalid_workbook_preserves_previous_output(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            target = root / "sounds.xml"
            target.write_bytes(b"existing configuration")
            invalid = root / "invalid.xls"
            invalid.write_bytes(b"not an Excel workbook")
            with self.assertRaises(exporter.SoundConfigExportError):
                exporter.export_sound_config(invalid, target)
            self.assertEqual(target.read_bytes(), b"existing configuration")

    def test_cli_handles_spaces_and_returns_nonzero_on_failure(self):
        with tempfile.TemporaryDirectory(prefix="sound exporter ") as temporary:
            target = Path(temporary) / "output with spaces.xml"
            result = subprocess.run([sys.executable, str(ROOT / "tools/sound_config_exporter.py"),
                                     str(WORKBOOK), str(target)], capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertIn("77 sound configurations", result.stdout)
            result = subprocess.run([sys.executable, str(ROOT / "tools/sound_config_exporter.py"),
                                     str(Path(temporary) / "missing.xls"), str(target)], capture_output=True, text=True)
            self.assertEqual(result.returncode, 1)
            self.assertIn("Sound export failed:", result.stderr)


if __name__ == "__main__":
    unittest.main()
