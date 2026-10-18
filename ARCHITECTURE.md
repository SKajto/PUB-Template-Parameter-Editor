# PUB Template Parameter Editor architecture

## Decision

Recommend Electron with React and TypeScript. Use the Electron main process for authorized local filesystem operations, a narrow typed preload bridge for application commands, and React for the UI. There is no backend service or authentication layer. Electron embeds its runtime, which fits ordinary-user desktop distribution; its package and memory footprint are accepted tradeoffs.

The M0 scaffold uses Electron Forge 8.0.1 with its Webpack + TypeScript template, React 19.3.1, and a lockfile. Forge's current documentation labels its Vite plugin experimental, so Webpack was selected for this baseline. The builder host used Node.js 24.21.0 and npm 11.19.0; Electron 44.7.2 and TypeScript 6.0.3 were resolved during scaffolding. These are observed development versions, not a promise of indefinite support; recheck Electron's supported release lines when updating.

Python with Qt could also satisfy the broad requirements, but a modern web UI, unified TypeScript types, and bundled browser rendering favor Electron here. A standalone browser implementation would require additional decisions about filesystem permissions and remembered folder handles. A system-webview solution would require verifying installed webview dependencies. These are design judgments, not claims that those alternatives are impossible.

## Modules

| Module | Responsibility |
| --- | --- |
| Desktop shell | Window lifecycle, native folder picker, platform paths, theme and last-folder settings |
| Template adapter | Detect container/content, inspect declarations, expose source locations and field capabilities, produce minimal edits |
| Workspace | Loaded baseline bytes and hashes, declaration identities, drafts, dirty state, explicit selections |
| Comparison service | Exact-name matching and field comparisons using baselines plus staged edits |
| Change service | Apply local field edits, stage bulk values, normalize no-op changes, build selectable review items |
| Save service | Preflight, output validation, original backups, journal, replacements, verification and recovery |
| React interface | Explorer, parameter grid, detail editor, comparison and bulk dialogs, review screen and status |

Illustrative source organization: `src/main/`, `src/preload/`, `src/renderer/`, `src/core/`, `tests/unit/`, `tests/integration/`, `tests/e2e/`. Keep filesystem access out of the renderer and keep the editing core independent of React.

## Evidence from the available samples

The previously inspected XML exports establish XML variable mappings, not native `.dta` packaging. Native-format implementation details are summarized in the sanitized `docs/NATIVE_DTA_FINDINGS.md`; proprietary sample identifiers and measurements are intentionally not recorded.

The adapter identifies supported declarations structurally and keeps `name`, `default`, `access`, and `type` as separate properties. Description text is handled as text, including empty values. Unsupported field shapes remain read-only; other PUB versions and templates may differ.

The adapter treats the archive and its XML as separate preservation layers. A no-op whole-archive rewrite can change ZIP metadata, so it is not used for saving. `desktop/src/core/dta-zip-writer.ts` supports only a narrowly identified ZIP32 variant; it preserves untouched records, validates member checksums, and fails closed on other layouts. Tests use temporary copies. PUB acceptance and broader container compatibility remain unverified.

## Template preservation strategy

1. Detect format from bytes, not extension. The supported native adapter requires a ZIP container with a `template.xml` entry; validate central-directory consistency and required members before reading. Do not assume every `.dta` is this ZIP variant.
2. Parse supported XML using a structural parser with source position support and external entity resolution disabled. Reject malformed input and do not evaluate scripts. Record exact original bytes, encoding, BOM, and line endings.
3. Locate only declaration properties within the verified declaration scope. Use an XML-aware token/span representation to identify attribute values and description text; a regular-expression-only editor is insufficient.
4. Apply escaping appropriate to attribute or text context and replace the smallest verified source spans, from the end of the file backwards. Do not reserialize the entire template with a generic XML serializer.
5. Preserve raw representation when a logical value is unchanged. Decode entities for display and comparison; preserve distinctions between missing and empty fields. Validate XML character legality without coercing values.
6. Treat a complex expression, unknown encoding, ambiguous declaration, or unverified structural variant as read-only rather than rewriting it with guessed semantics. Support common UTF-8 samples first, then add evidenced formats deliberately.
7. Reparse output and assert that only selected logical fields changed. Assert byte identity outside authorized source spans. A no-op operation must not write a file.
8. For native DTA, introduce a container adapter only after its no-op and selected-member preservation tests pass. Preserve unrelated entries and metadata within demonstrable guarantees. XML byte preservation does not prove container preservation.

`desktop/src/core/xml-source-patcher.ts` uses Saxes to validate and interpret declarations, then a structural lexical scanner to locate original attribute/text spans. It does not reserialize XML. The reader exposes field capabilities and keeps nested markup, comments, and CDATA descriptions read-only. The staged renderer sends only reviewed field changes through a typed preload API; only the main process patches XML and replaces the native DTA member. Unchanged source outside authorized spans and unrelated ZIP member records are retained within the tested container subset.

Native DTA editing is a release gate, not an optional later feature. XML reading can support development but must not be represented as finished DTA support. Minified XML must receive a readable semantic field diff in the UI rather than a multi-megabyte single-line diff.

## Working data and identity

A TemplateRecord contains a canonical local path, supported format/capabilities, baseline bytes, baseline content fingerprint, declarations, and read status. A declaration has a stable identity within that template and an exact declared name. A field capability indicates editable or read-only and why. A DraftChange is keyed by template identity, declaration identity, and field, and carries baseline and proposed logical values. XML property names are not the public UI type system.

A ReviewItem snapshots one draft change plus included/excluded status. A review snapshot is invalidated for proposals changed since review. Excluding a change has no effect on disk or the editing draft until a successful save completes. Back retains proposals. After success, excluded fields return to baseline while included fields acquire the saved baseline.

Bulk matching uses exact names only within supported declaration scopes. Duplicate names block that target rather than selecting the first occurrence. A parameter absent from a template remains absent. Defaults are opaque strings, including values that appear boolean, numeric, or executable.

## Save lifecycle

Editing creates drafts. Main Save creates review. Review Back returns to editing. Review Save freezes included proposals, checks their files against baseline fingerprints, builds outputs and originals for all targets, validates them, records a journal, and replaces files individually. Complete verification returns to the explorer. Failure triggers recovery and an accurate per-file report.

Place temporary replacement files beside their originals where practical so a replacement does not cross filesystems. Create backups before changing any original; preserve recoverable originals until an explicit retention policy is implemented. Journal batches in user application data and discover incomplete batches at startup. Do not implement an automatic cleanup policy that removes the only recoverable copy.

Recheck fingerprints immediately before replacement and use exclusive access where supported. Detect additional races where feasible and document remaining filesystem guarantees. Preserve permissions as appropriate for the target OS. Tests must simulate write failure, locked files, external changes, and interruption between files. Never claim crash-proof or batch-wide atomicity without evidence.

The current `save-service.ts` implements preflight, same-directory temporary outputs, app-data backups, a journal, per-file replacement, verification, and rollback for failures detected while the process remains alive. It has not yet scanned/reconciled journals after a process crash; crash recovery remains open. The renderer exposes Save only from Review and retains drafts on Back or reported failure.

## Packaging proof

M0 configures Electron Forge's ZIP maker for Windows and [ADDRESS] extracted archives, matching the ordinary-user/no-admin requirement without assuming an installer. A Windows x64 ZIP was built and launched from a user-writable extraction folder on Windows 11 with the process not elevated. Its runtime and bundled assets opened successfully. A Linux x64 ZIP cross-build completed on Windows, but the archive recorded no Unix executable mode bits; it is not a usable or validated Linux release. Build and test Linux on a Linux host, verify executable permissions and sandbox/system-library behavior, and record the supported distribution before making a release claim. Do not use `--no-sandbox` as the supported [ADDRESS] distribution strategy.

Development dependencies are acceptable on the development machine; the end-user package must bundle the programming runtime and assets. No claim of a dependency-free Linux binary is made. The current full npm audit reports 11 development-tree findings; production dependencies report zero. Resolve the development findings before release.

## Interface direction

Use a resizable left template explorer, a central searchable parameter table, and a right detail editor. The central/right divider resizes both adjacent panels; the details panel defaults to 360 CSS pixels and has keyboard arrow support. Put the folder picker, Read parameters, Compare, bulk action, theme selector, and Save in a consistent toolbar. Use restrained colors and spacing, legible text, keyboard operation, and icons accompanied by labels for important actions. Show dirty state through both symbols/text and color. Persist only requested preferences; panel sizes are session-only.

Use a dedicated full-width review view with per-template grouping and old/new field values. Its primary controls are Back and Save. Include/exclude controls affect delivery, not immediate persistence. On complete success return to the explorer in the same folder and show a concise result confirmation.
