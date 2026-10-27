# What a save must never touch

Implementation constraints for the template format and the save path, for whoever changes that
layer - human or AI agent. Each rule names the test that holds it; if you break one, a test breaks.

## Reading

- **Format comes from the bytes, not the extension.** A `.dta` is a ZIP container holding
  `template.xml`; a plain XML template is plain XML. `TemplateFormatDetector`,
  `DeclarationReadingTests`, `NativeEvidenceTests`.
- **XML is data, never code.** Parsing uses `XmlReader` with DTDs prohibited and no external
  resolver; malformed input is rejected, and names, descriptions and values are treated as text
  everywhere they are displayed. `XmlDeclarationParser`, `DeclarationReadingTests`.
- **The exact source text is recorded**: original bytes, encoding, BOM, line endings, and the precise
  character span of every property value the editor may change. Missing and empty are different
  states. `SourceText`, `ValueSpan`, `DeclarationReadingTests`.

## Writing

- **Patch the smallest verified span, from the end of the file backwards; never re-serialise the
  document.** A generic XML serializer would rewrite quoting, whitespace, entity forms and
  everything else the file did not ask to have changed. `TemplateXmlPatcher`,
  `TemplatePatcherTests`, `NativePatchEvidenceTests`.
- **A container's other entries keep their bytes.** Only the template entry is replaced; schema and
  mapping entries are compared by CRC in the evidence tests. The ZIP framing itself may be
  recompressed, and that is recorded as a known difference rather than claimed as identical output.
  `DtaContainer`, `NativePatchEvidenceTests`, `SaveFlowTests`.
- **Anything not understood stays read-only with a reason** - an unobserved value shape, a complex
  expression, a duplicate declaration name. Never rewritten with guessed semantics.
  `TemplateFieldRules`, field capability assertions in `DeclarationReadingTests` and
  `EditingTests`.
- **A no-op does not write.** Proposing the stored value again clears the draft instead of staging an
  empty change. `DraftStoreTests`, `EditingTests`.

## The save path

Drafts are staged in memory; **Review Save is the only write path**. Per file: re-check the content
fingerprint against what was read, verify the produced bytes by re-parsing, write a timestamped
backup, journal the batch in the user's app data, replace that one file, report per file - written,
rejected or not attempted. No batch-wide atomicity is claimed, because there is none.
`TemplateSaveService`, `TemplateSaveServiceTests`, `SaveFlowTests`.

## Bulk editing

Matches the exact declared name only. A duplicate name in a target blocks that target instead of
picking the first occurrence; a parameter that is absent stays absent; a field the target cannot hold
is refused with the reason rather than silently skipped. Default values are opaque strings, never
interpreted as numbers, booleans or code. `BulkPlan`, `BulkPlanTests`.

## Still open

The native PUB round trip - opening a file this editor wrote in PUB itself - has not been verified:
PUB is not available on the development machine, and the real templates are not published here.
