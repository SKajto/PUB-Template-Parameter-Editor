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
