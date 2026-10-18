# Native DTA format notes (sanitized)

Proprietary sample identifiers, filenames, measurements, declaration counts, and template-specific values have been removed from this tracked note. Original sample files remain local and are not included in source control.

## Supported adapter scope

The editor supports an observed ZIP-based DTA variant with a root `template.xml`. The archive reader validates the container and member checksums; the XML parser identifies supported declarations structurally. Save patches only supported source spans and preserves unrelated archive records. Unsupported container or field shapes fail closed and remain read-only.

These notes describe the adapter's current scope, not a claim that all DTA files share one format. Other PUB versions and template variants may differ.

## Preservation limits

Whole-archive repacking can change ZIP metadata even when uncompressed member payloads remain equal. The application therefore uses the source-span XML patcher and a narrowly scoped ZIP writer rather than rewriting the complete archive. Automated checks operate on temporary copies; they do not target original samples.

Successful opening and generation in PUB, broader container compatibility, and the supported PUB version remain unverified. Keep unsupported shapes read-only until lossless behavior is demonstrated.

## Local verification fixtures

Sample-dependent tests may use generic local fixtures such as `examples/sample-01.dta` through `examples/sample-04.dta`. Keep proprietary fixtures out of Git, do not use production templates as write-test targets, and preserve the originals unchanged.
