#!/usr/bin/env python3
"""Read-only inspection of native PUB DTA ZIP samples; not a production adapter."""

from __future__ import annotations

import argparse
import json
import sys
from collections import Counter
from pathlib import Path
from zipfile import BadZipFile, ZipFile
import xml.etree.ElementTree as ET


class DtaInspectionError(ValueError):
    """Raised when a file does not match the observed native sample structure."""


def inspect_dta(path: Path) -> dict[str, object]:
    """Inspect a ZIP-backed DTA sample without extracting or modifying it."""
    try:
        with ZipFile(path) as archive:
            infos = archive.infolist()
            member_names = [info.filename for info in infos]
            duplicates = sorted(
                name for name, count in Counter(member_names).items() if count > 1
            )
            if duplicates:
                raise DtaInspectionError(f"duplicate ZIP member names: {duplicates}")
            if "template.xml" not in member_names:
                raise DtaInspectionError("missing root template.xml member")
            bad_member = archive.testzip()
            if bad_member is not None:
                raise DtaInspectionError(f"ZIP CRC check failed for {bad_member}")
            template_bytes = archive.read("template.xml")
            compression_methods = Counter(info.compress_type for info in infos)
    except BadZipFile as error:
        raise DtaInspectionError("not a valid ZIP-backed DTA file") from error

    try:
        root = ET.fromstring(template_bytes)
    except ET.ParseError as error:
        raise DtaInspectionError(f"template.xml is not valid XML: {error}") from error
    if root.tag != "template":
        raise DtaInspectionError(f"unexpected XML root element: {root.tag!r}")

    variable_nodes = root.findall("./dataset/variables/variable")
    declarations = [
        feature
        for variable in variable_nodes
        for feature in variable.findall("feature[@tag='definition']")
    ]
    if not declarations:
        raise DtaInspectionError("no declarations found at the observed dataset path")

    names: list[str | None] = []
    property_shapes: Counter[tuple[str | None, ...]] = Counter()
    accesses: Counter[str | None] = Counter()
    xml_types: Counter[str | None] = Counter()
    description_types: Counter[str | None] = Counter()
    empty_descriptions = 0
    defaults_with_value = 0
    missing_default_value = 0

    for declaration in declarations:
        properties = declaration.findall("property")
        property_shapes[tuple(sorted(prop.get("name") for prop in properties))] += 1
        by_name = {prop.get("name"): prop for prop in properties}

        name = by_name.get("name")
        names.append(name.get("value") if name is not None else None)
        access = by_name.get("access")
        accesses[access.get("value") if access is not None else None] += 1
        xml_type = by_name.get("type")
        xml_types[xml_type.get("value") if xml_type is not None else None] += 1

        default = by_name.get("default")
        if default is not None and "value" in default.attrib:
            defaults_with_value += 1
        else:
            missing_default_value += 1

        description = by_name.get("description")
        expression = description.find("expression") if description is not None else None
        expression_type = expression.get("type") if expression is not None else None
        description_types[expression_type] += 1
        if expression is None or not expression.text:
            empty_descriptions += 1

    duplicate_names = sorted(
        name
        for name, count in Counter(names).items()
        if name is not None and count > 1
    )
    return {
        "file": path.name,
        "file_bytes": path.stat().st_size,
        "zip_members": len(infos),
        "zip_methods": dict(sorted(compression_methods.items())),
        "template_bytes": len(template_bytes),
        "template_utf8_bom": template_bytes.startswith(b"\xef\xbb\xbf"),
        "template_lf_count": template_bytes.count(b"\n"),
        "template_crlf_count": template_bytes.count(b"\r\n"),
        "variable_nodes": len(variable_nodes),
        "declarations": len(declarations),
        "names_unique": not duplicate_names and all(name is not None for name in names),
        "duplicate_names": duplicate_names,
        "access": dict(sorted(accesses.items(), key=lambda item: str(item[0]))),
        "xml_type": dict(sorted(xml_types.items(), key=lambda item: str(item[0]))),
        "default_value_attributes": defaults_with_value,
        "missing_default_value": missing_default_value,
        "description_expression_types": dict(
            sorted(description_types.items(), key=lambda item: str(item[0]))
        ),
        "empty_descriptions": empty_descriptions,
        "property_shapes": {
            ", ".join(str(name) for name in shape): count
            for shape, count in sorted(property_shapes.items())
        },
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("files", nargs="+", type=Path, help="DTA files to inspect")
    args = parser.parse_args()

    reports = []
    failed = False
    for path in args.files:
        try:
            reports.append(inspect_dta(path))
        except (OSError, DtaInspectionError) as error:
            failed = True
            print(f"{path}: {error}", file=sys.stderr)
    print(json.dumps(reports, indent=2, ensure_ascii=False))
    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
