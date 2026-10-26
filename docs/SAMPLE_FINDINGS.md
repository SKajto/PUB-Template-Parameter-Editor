# Observed XML variable structure

Inspected 15 October 2026: template_old.xml, template_new.xml and template_latest.xml used as XML format reference samples. Analysis was performed on the actual file bytes. These are XML files; no native .dta container was inspected.

| Sample | Declared variables | Internal | External |
| --- | ---: | ---: | ---: |
| template_old.xml | 216 | 152 | 64 |
| template_new.xml | 218 | 152 | 66 |
| template_latest.xml | 218 | 152 | 66 |

The observed declaration path is `/template/dataset/variables/variable/feature[@tag='definition']`. Expression references elsewhere in the file are not declarations.

| UI field | Observed XML location relative to declaration feature |
| --- | --- |
| Name | `property[@name='name']/@value` |
| Description | `property[@name='description']/expression[@type='constant']/text()` |
| Internal or External | `property[@name='access']/@value` |
| Default value | `property[@name='default']/@value` |

All declarations in these samples also contain `type` and `central variable` properties. Preserve those properties. In particular, `type='user'` is not the same field as `access='internal'` or `access='external'`.

All observed descriptions use constant expressions, all defaults use a value attribute, and no duplicate declaration names were found. This evidence does not establish the shape of all PUB versions or templates. The editor must detect unsupported variants rather than silently normalize them.

Original template files are intentionally not redistributed in this package. Supply local copies in samples/ for implementation. The synthetic example in examples/ illustrates the observed properties and is not a complete PUB template.

## The native .dta samples stay on the maintainer's disk

The dated PUB templates that the evidence tests were written against (`examples/*.dta`) are the
owner's own template content. They are git-ignored and must never be committed or published:
redistributing them is a separate right that this project does not hold, and they are not the
"complete corresponding source" that GPL-3.0 requires - the source of the editor is.

Concretely:

- `.gitignore` carries `examples/*.dta`, CI and the test suite assert that no `.dta` is tracked.
- `tests/TestSampleSupport.cs` gives every test a choice: the real file when the disk has it, or a
  synthetic container of the same shape (template XML plus a schema folder with a non-XML
  `__RPE_MAPPINGS__` entry) when it does not. The evidence theories simply have no rows without the
  samples, which is recorded as "not executed", never as a pass.
- The measurements themselves - entry layout, declaration path, counts per file, BOM and encoding
  behaviour, which entries a save must leave byte-identical - are what travels with the source, in
  this file and in the patcher tests.

So a contributor who clones the repository runs the full suite and gets the same guarantees, while
the machine that holds the real templates additionally checks the parser and the patcher against
production bytes.
