# PUB template preservation and save model

These rules define source preservation, drafts, review and recovery for the new implementation. The dated XML findings are reference evidence only; verify actual native template bytes during development.

## Evidence from the available samples

The three inspected files are XML exports, not native `.dta` samples. Their declarations occur under `/template/dataset/variables/variable/feature[@tag='definition']`. A variable's `name` property stores its name in `@value`; `access` stores `internal` or `external`; `default` stores the default in `@value`; `description` contains an expression with `type='constant'`. The separate `type` property is `user` in these samples and must not be confused with access classification.

There are 216 declarations in template_old.xml and 218 in each other sample. All observed descriptions are constant expressions, all observed defaults use a value attribute, and no duplicate declaration names were found. Other files may differ. See docs/SAMPLE_FINDINGS.md.

## Template preservation strategy

1. Detect format from bytes, not extension. Establish native DTA structure using real files in S1. Do not assume DTA is plain XML or an archive.
2. Parse supported XML using a structural parser with source position support and external entity resolution disabled. Reject malformed input and do not evaluate scripts. Record exact original bytes, encoding, BOM, and line endings.
3. Locate only declaration properties within the verified declaration scope. Use an XML-aware token/span representation to identify attribute values and description text; a regular-expression-only editor is insufficient.
4. Apply escaping appropriate to attribute or text context and replace the smallest verified source spans, from the end of the file backwards. Do not reserialize the entire template with a generic XML serializer.
5. Preserve raw representation when a logical value is unchanged. Decode entities for display and comparison; preserve distinctions between missing and empty fields. Validate XML character legality without coercing values.
6. Treat a complex expression, unknown encoding, ambiguous declaration, or unverified structural variant as read-only rather than rewriting it with guessed semantics. Support common UTF-8 samples first, then add evidenced formats deliberately.
7. Reparse output and assert that only selected logical fields changed. Assert byte identity outside authorized source spans. A no-op operation must not write a file.
8. If native DTA is a container, introduce a container adapter. Preserve unrelated entries and metadata within demonstrable guarantees. Test container preservation explicitly; XML byte preservation does not prove container preservation.

Native DTA editing is a release gate, not an optional later feature. XML reading can support development but must not be represented as finished DTA support. Minified XML must receive a readable semantic field diff in the UI rather than a multi-megabyte single-line diff.

## Working data and identity

A TemplateRecord contains a canonical local path, supported format/capabilities, baseline bytes, baseline content fingerprint, declarations, and read status. A declaration has a stable identity within that template and an exact declared name. A field capability indicates editable or read-only and why. A DraftChange is keyed by template identity, declaration identity, and field, and carries baseline and proposed logical values. XML property names are not the public UI type system.

A ReviewItem snapshots one draft change plus included/excluded status. A review snapshot is invalidated for proposals changed since review. Excluding a change has no effect on disk or the editing draft until a successful save completes. Back retains proposals. After success, excluded fields return to baseline while included fields acquire the saved baseline.

Bulk matching uses exact names only within supported declaration scopes. Duplicate names block that target rather than selecting the first occurrence. A parameter absent from a template remains absent. Defaults are opaque strings, including values that appear boolean, numeric, or executable.

## Save lifecycle

Editing creates drafts. Main Save creates review. Review Back returns to editing. Review Save freezes included proposals, checks their files against baseline fingerprints, builds outputs and originals for all targets, validates them, records a journal, and replaces files individually. Complete verification returns to the explorer. Failure triggers recovery and an accurate per-file report.

Place temporary replacement files beside their originals where practical so a replacement does not cross filesystems. Create backups before changing any original; preserve recoverable originals until an explicit retention policy is implemented. Journal batches in user application data and discover incomplete batches at startup. Do not implement an automatic cleanup policy that removes the only recoverable copy.

Recheck fingerprints immediately before replacement and use exclusive access where supported. Detect additional races where feasible and document remaining filesystem guarantees. Preserve permissions as appropriate for the target OS. Tests must simulate write failure, locked files, external changes, and interruption between files. Never claim crash-proof or batch-wide atomicity without evidence.

