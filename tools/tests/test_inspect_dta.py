from __future__ import annotations

import hashlib
import sys
import tempfile
import unittest
import warnings
from pathlib import Path
from zipfile import ZIP_DEFLATED, ZipFile

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from inspect_dta import DtaInspectionError, inspect_dta


XML = b'''<?xml version="1.0" encoding="UTF-8"?>
<template><dataset><variables><variable>
<feature tag="definition">
<property name="name" value="sample"/>
<property name="description"><expression type="constant"></expression></property>
<property name="type" value="user"/>
<property name="access" value="external"/>
<property name="default" value=""/>
<property name="central variable" value=""/>
</feature></variable></variables></dataset></template>'''


class InspectDtaTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp_dir = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp_dir.cleanup)
        self.path = Path(self.temp_dir.name) / "sample.dta"

    def write_archive(self, *, include_template: bool = True) -> None:
        with ZipFile(self.path, "w", ZIP_DEFLATED) as archive:
            if include_template:
                archive.writestr("template.xml", XML)
            archive.writestr("schemas/example.xsd", b"schema bytes")

    def test_reports_observed_fields_without_modifying_archive(self) -> None:
        self.write_archive()
        before = hashlib.sha256(self.path.read_bytes()).digest()

        report = inspect_dta(self.path)

        self.assertEqual(report["declarations"], 1)
        self.assertEqual(report["access"], {"external": 1})
        self.assertEqual(report["xml_type"], {"user": 1})
        self.assertEqual(report["default_value_attributes"], 1)
        self.assertEqual(report["empty_descriptions"], 1)
        self.assertEqual(report["names_unique"], True)
        self.assertEqual(hashlib.sha256(self.path.read_bytes()).digest(), before)

    def test_rejects_non_zip_input(self) -> None:
        self.path.write_bytes(b"not a ZIP container")

        with self.assertRaisesRegex(DtaInspectionError, "not a valid ZIP"):
            inspect_dta(self.path)

    def test_rejects_missing_template_member(self) -> None:
        self.write_archive(include_template=False)

        with self.assertRaisesRegex(DtaInspectionError, "missing root template.xml"):
            inspect_dta(self.path)

    def test_rejects_duplicate_zip_member_names(self) -> None:
        with warnings.catch_warnings():
            warnings.simplefilter("ignore", UserWarning)
            with ZipFile(self.path, "w", ZIP_DEFLATED) as archive:
                archive.writestr("template.xml", XML)
                archive.writestr("template.xml", XML)

        with self.assertRaisesRegex(DtaInspectionError, "duplicate ZIP member"):
            inspect_dta(self.path)


if __name__ == "__main__":
    unittest.main()
