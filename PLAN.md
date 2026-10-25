# PUB Template Parameter Editor implementation plan

Revision 4, 25 October 2026. New project baseline: no application code, repository setup, CI execution, native DTA verification or Store submission is assumed complete. This package contains instructions and examples only. Mark milestones complete only after executing their checks. Update this plan with commands, results, decisions and missing inputs as work proceeds.

## S0 Create the solution and prove desktop startup

- Initialize the local repository and C# solution using docs/PROJECT_SETUP.md. Create App, Core, Infrastructure and test projects, with Core independent of WPF.
- Record a supported patched .NET 10 SDK and actual restore/build/test commands. Configure GPLv3 metadata/notices using real owner details when available.
- Build a minimal WPF window with embedded light/dark resources, native folder dialog, asInvoker execution and user-scoped settings. Confirm UI responsiveness and preference persistence.
- Configure and test the separate self-contained single-file win-x64 publish profile. Test actual Windows startup without separately installed .NET where available; record extraction, size, startup and policy observations honestly.
- Establish a draft MSIX packaging path and basic Windows GitHub CI configuration early. Record any real identity/account inputs still needed and validate placeholders appropriately.

Done when the scaffold builds, meaningful startup observations are recorded and the project has reproducible commands. If Windows execution or account details are unavailable, leave those gates open and continue independent implementation. An optional portable EXE block does not establish the outcome of the eventual Store-installed app.

## S1 Establish template format and read-only domain core

- Inspect representative native DTA bytes and record the target PUB version. Use the supplied XML findings as limited reference evidence; do not assume native DTA is plain XML or a particular archive format.
- Implement format detection, declaration reading, supported-field capabilities, exact-name matching and baseline fingerprints. Do not list expression references as declarations.
- Build immutable redistributable fixtures and independently specified expected results. Test valid/malformed input, empty and missing fields, supported encodings and duplicate declarations.
- Establish a source-preserving patch model using structural XML validation and exact lexical spans. Test C# string offsets versus encoded byte positions. Keep uncertain constructs read-only.

Done when supported samples yield verified names/defaults/access/descriptions, errors remain recoverable and no read operation writes templates. Missing native samples leave native compatibility open while synthetic/core work continues.

## S2 Build the read-only WPF explorer

- Implement folder selection, remembered folder/theme, left template list and active-template parameter table/detail panel.
- Add filename/read-scope selection and explicit Read parameters, progress and per-template errors.
- Add name/description search, All/Internal/External filtering and clear counts/states. Keep active focus separate from multi-selection.
- Use resizable panels, keyboard focus and accessible light/dark styles through built-in WPF controls.

Status 2026-10-25: implemented — folder selection with remembered folder and theme,
non-recursive `.dta` listing with per-entry stat errors, filename filter with
Select/Clear visible, read checkboxes separate from the focused template, explicit
async Read parameters with progress and per-template errors, name/description search
with All/Internal/External filter and live counts, read-only parameter table and
detail cards, resizable panels (`GridSplitter`), Ctrl+E search focus, accessible
light/dark/system styles matching the reference palette. Read hashes of the sample
templates are re-verified unchanged after listing/reading (see the file-fingerprint
assertions in the integration tests). Still open: human review of the rendered UI in
an interactive Windows desktop session (see the Progress record).

Done when reading/filter combinations and persistence work in the actual Windows GUI, counts match verified samples and original file hashes remain unchanged.

## S3 Add edits comparison and bulk staging

- Enable default value, access classification and supported description edits with explicit Apply edits. Keep names read-only and default strings exact.
- Add dirty indicators, no-op normalization and safe navigation/close/re-read handling for unapplied and staged edits.
- Compare the selected declaration across selected/all loaded templates, with exact-name matching, field/line differences, distinct missing/empty states and visible draft values.
- Add eligible bulk-target selection and explicit value-only Apply changes. Preserve target access/description and explain ineligible ambiguous declarations.

Done when only selected field/target proposals change in memory, filtering/navigation retains drafts and all original template files remain untouched.

Status 2026-10-25: implemented (Core + WPF), automated gates green; interactive desktop
review still open. See the 2026-10-25 S3 progress record below.

## S4 Implement review and recoverable saving

- Add grouped per-file/parameter/field review with selection controls and delivery/revert counts. Back preserves all drafts. Changed proposals require renewed review.
- Implement output preflight/validation, SHA-256 external-change detection, backups, same-volume temporary replacement, verification and recovery journal.
- Handle cancel, locked/read-only files, conflicts and injected mid-batch failures. Keep accurate per-file outcomes and reconcile unresolved drafts before retry.
- After complete success clear delivered changes, revert excluded fields and return to the explorer with a concise confirmation. Prevent duplicate save execution.

Done when the specified review workflow and meaningful failure/interruption checks pass. Per-file replacement is not described as a globally atomic batch.

## S5 Verify native PUB compatibility

- Edit copies of native templates with the application: default value, access, description and multi-template value changes.
- Open generated templates in the target PUB version and generate representative documents. Verify intended effects and retained scripts, images and layout.
- Record PUB version, source fixtures, edits, outputs and actual observations. Provide precise manual verification steps if the environment is unavailable.

Done only after real PUB checks. XML validity, synthetic fixtures and build success alone cannot close native compatibility.

## S6 Complete GitHub licensing CI and packaging

- Complete LICENSE, actual copyright/source notices, bundled offline GPL/third-party notices, About source links and source-release instructions.
- Implement Windows PR CI for restore/build/test and package/manifest validation with no Store secrets or release write permission.
- Implement trusted release packaging and exact-version corresponding-source archives containing all required source/resource/submodule/build/package inputs. Link app/package version and commit to artifacts/hashes.
- Complete the MSIX packaging configuration with real Partner Center identity when available, required assets/notices, justified desktop capabilities and monotonic version mapping. Record selected SDK/tool versions.
- Prepare a separate protected Store-submission workflow and document needed account/service credentials. Build/source checks must not need maintainer secrets. Keep Store price an explicit owner choice.

Done when reproducible CI/package/source outputs and trust-boundary/placeholder checks pass. Unconfigured external account or missing real identity is recorded as a submission gate, not invented or treated as a failed core build.

## S7 Prepare first Microsoft Store submission

- Prepare Partner Center setup steps, product association, listing, screenshots, ratings, support/privacy facts and clear GPL/source directions.
- Verify current release-tool prerequisites. The documented update automation route assumes a live free product; select a supported first-submission/pricing path for the owner's decision.
- Review actual applicable Store terms and GPL release obligations. Do not add app restrictions that conflict with GPL rights.
- Produce the reviewable first submission package and listing materials. An authorized release task can perform account/agreements/submission; preparation is not certification or deployment.
- Track actual submission/certification results when that release action occurs. Enable protected automated updates only once its prerequisites are met.

Done for preparation when materials and prerequisites are documented and artifacts validated. Record live submission/certification as separate observed states rather than claiming them in advance.

## S8 Validate Store delivery and release readiness

- Acquire the actual Store-distributed app and verify standard-user first launch without installed .NET, offline editing and all required workflows.
- Test update behavior using a real earlier test/Store version created by this project's release process; verify settings persistence and preservation of external templates. Record uninstall/backup/journal retention behavior.
- Check actual Store-delivered signature, installed identity/version and target managed-device execution. CI artifact trust and optional portable EXE results remain separate.
- Execute docs/TEST_MATRIX.md, including real WPF usability and PUB compatibility. Keep unavailable checks explicitly unverified.
- Publish/retain exact-version corresponding source through the authorized release process. Maintain runtime security rebuilds, increasing Store package versions and release records.

Done when application, format, packaging, lifecycle, licensing and actual distribution checks required for the claimed release have passed. A build, uploaded artifact or completed certification alone is insufficient evidence.

## Progress record

Initial status: all milestones not started. For each completed increment record what works, executed checks and commands, artifact locations, decisions, next work and remaining external gates. Update the status as actual development proceeds; do not infer completion from this document.

### 2026-10-25 — S0 complete, S1 complete, S2 in progress

Toolchain actually used: `dotnet --version` = **10.0.402** (`global.json` pins
`10.0.0` feature band `latestPatch`), Windows 11 x64, MSBuild from the .NET SDK,
MakeAppx from Windows SDKs 10.0.19041.0 / 10.0.22621.0 / 10.0.26100.0.

Executed commands and results:

- `dotnet build -c Debug` → 0 warnings, 0 errors.
- `dotnet test` → **64 passing, 0 failing** (Core 35, Integration 9, App/WPF 20).
- `dotnet publish src/PubTemplateEditor.App -c Release -p:PublishProfile=PortableWin_x64`
  → single-file `PubTemplateEditor.exe` (~125 MB), no loose `.pdb` (embedded debug
  info), self-contained win-x64.
- `powershell -File packaging/build-msix.ps1 -Version 0.1.0 -Mode Draft`
  → `artifacts/msix/PubTemplateEditor_0.1.0_win-x64.msix` (60.8 MB) plus unpacked
  verification layout and provenance JSON with the SHA-256. `-Mode Strict`
  fails on the `REPLACE_*` Partner Center identity placeholders (REL-04 gate).
- GUI launch check: started `PubTemplateEditor.exe`, process alive after 6 s.
  This machine has no interactive desktop session, so no screen capture and no
  click-through verification is possible here (recorded gate, not a failure).

S2 work now in the tree (read-only explorer, matching the reference layout in
`docs/REFERENCE_UI_FINDINGS.md`):

- `Infrastructure/TemplateFolderScanner.cs` — non-recursive `.dta` listing with
  sizes and per-entry stat errors (FILE-02/FILE-04).
- `Core/Search/ParameterSearchMatcher.cs` — anchored case-sensitive name search
  with `*`, case-insensitive description search, name-region suppression; 8 Core
  tests including literal metacharacters and multi-line values.
- `ViewModels/WorkspaceViewModel.cs`, `TemplateItem`, `ParameterRowItem` —
  folder restore, filename filter, read scope vs active focus (FILE-05),
  explicit async Read parameters with progress and per-file errors, parameter
  search/access filter with live counts, read-only detail cards with the
  XML-type note and unsupported-shape explanations, Light/Dark/System theme
  persistence, footer text and version→licences entry point.
- `MainWindow.xaml` rebuilt to the reference screen map; `Themes/Controls.xaml`
  gained eyebrow/card/notice/badge/segment/ListView-GridView styles; empty states
  are mutually exclusive.
- `tests/PubTemplateEditor.App.Tests/ExplorerTests.cs` and `ShellSnapshotTests.cs`
  (the latter renders the shell in the STA host, asserts panel geometry and writes
  `artifacts/screenshots/shell-light.png` / `shell-dark.png` for human review).

Decisions recorded: GPL-3.0-only wording fixed in `NOTICE`; upstream design
lineage added to `NOTICE` and `docs/REFERENCE_UI_FINDINGS.md` (independent
implementation, no code copied); `SystemThemePreference` reads the Windows
registry value with a Light fallback; folder listing is `.dta` only and
non-recursive.

Remaining gates before S2 can be called done:

1. Human visual review of `artifacts/screenshots/*.png` and of the running window
   on a real desktop session (keyboard/mouse, DPI scaling, screen reader names).
2. Persistence round-trip already covered for folder and theme; detail-panel width
   and column widths are not yet remembered (open product question, not required
   by REQUIREMENTS.md).
3. Compare / "Apply value to templates" controls intentionally absent until S3.

Next: S3 — enable default value, access and supported description editing with
explicit Apply edits, dirty indicators, no-op normalisation, unsaved-work
navigation, compare panel and bulk value staging.

### 2026-10-25 — S2 code complete (GUI review gate still open)

Added after the first S2 pass:

- Reference review documentation `docs/REFERENCE_UI_FINDINGS.md`: upstream project,
  reviewed revision (`SKajto/PUB-Template-Parameter-Editor` @
  `7aa497d61f37fd4f7430eba266c5f6b18bf2ef42`, GPL-3.0), the screen map, the mirrored
  behaviour rules and the recorded differences. Provenance is also stated in
  `NOTICE`; no upstream source is copied or vendored (`.reference/` is gitignored).
- `NOTICE` wording corrected to the **GPL-3.0-only** grant (it previously carried the
  "either version" -or-later sentence while the SPDX choice is `GPL-3.0-only`).
- Theme palette aligned with the reviewed reference values (`Themes/Light.xaml`,
  `Themes/Dark.xaml`): accent `#315FCA`, border `#D8E0EB`, text `#364150`,
  muted `#8792A2`, window `#F3F5F8`, panel `#FFFFFF`, plus accent-soft, selection,
  row-hover and error/success/warning background+foreground pairs. Dark values are
  derived (upstream ships light only). No colour literals remain outside the theme
  dictionaries.
- `Infrastructure/ProductInfo.cs` is the single source for product name, version,
  licence id, copyright, runtime/OS description and the corresponding-source note;
  `WorkspaceViewModel` and the new **About tab** in `LicensesWindow` read it, so the
  footer button, About view and licence text cannot disagree. The About view is
  reachable offline (footer button, Ctrl+L).
- `TemplateFolderScanner` takes an optional stat seam so a failing entry can be
  tested; `tests/PubTemplateEditor.Integration.Tests/FolderScannerTests.cs` covers
  `.dta`-only, non-recursive, deterministic order, per-entry listing errors and a
  missing folder.
- New App tests: select/clear-visible changes only the read scope, focus never
  re-checks a checkbox, an empty scope is refused and a successful read clears the
  error state, and the rendered detail controls show the exact declaration values.
- `ShellSnapshotTests` now also renders the About window and asserts that the
  rendered shell text carries the view-model values (product name, folder name,
  file count badge, scope line, focused template, summary, footer counts,
  version button), which catches mistyped binding paths. `ThemeTests` gained
  "every StaticResource key is defined" and "no hard-coded colours in shell XAML".

Executed commands and results:

- `dotnet build -c Debug` → 0 warnings, 0 errors.
- `dotnet test` → **72 passing, 0 failing** (Core 35, Integration 12, App/WPF 25).
- `dotnet publish src/PubTemplateEditor.App -c Release -p:PublishProfile=PortableWin_x64`
  → `src/PubTemplateEditor.App/bin/Release/net10.0-windows/win-x64/publish/PubTemplateEditor.exe`
  (131,020,339 bytes, single file, self-contained).
- Started that executable: process alive after 8 s, then stopped
  (`artifacts` launch probe, no interactive desktop session on this machine).
- `powershell -File packaging/build-msix.ps1 -Version 0.2.0 -Mode Draft`
  → `artifacts/packaging/out/PubTemplateEditor_0.2.0_win-x64.msix`, SHA-256
  `A8BD3D9A…CFBB71`, unpacked layout contains `notices/`; `-Mode Strict` still
  rejects the unresolved identity values (REL-04).

Regression found and fixed while doing this: the `LinkButtonStyle` block had been
dropped from `Themes/Controls.xaml` by an edit, which crashed shell construction
(and aborted the WPF test host). The new "every StaticResource key is defined"
test plus the rendered-text assertions are the guards for that class of mistake.

Still open for S2:

1. Human visual review of `artifacts/screenshots/shell-light.png`,
   `shell-dark.png` and `about-light.png`, plus keyboard/mouse/DPI/screen-reader
   checks in an interactive Windows session. Software-rendered snapshots and the
   process-alive check do not prove interactive GUI behaviour.
2. Panel widths and column widths are not persisted (not required by
   REQUIREMENTS.md; recorded as an open product question).
3. Compare and "Apply value to templates" controls intentionally arrive with S3.


### 2026-10-25 — S3 editing, compare and bulk staging

Core (`src/PubTemplateEditor.Core/Editing/`):

- `TemplateFieldRules` — names and unsupported declarations are never staged; only
  `DefaultValue`, `Access` and a constant-`Description` are; access must be exactly one of
  the native `internal` / `external` values, default strings are never trimmed or coerced.
- `DraftStore` — keys by file + declaration id + field (`ChangeKey`), keeps the parsed
  baseline for conflict detection and reverts only on a completely successful save, and
  normalizes a proposal back to the stored value into removal (`ClearedAsNoOp`), so counts,
  review and saving see real deltas only. Nothing here touches disk.
- `ParameterComparator` — exact name matching, active row always present, distinct
  `Self` / `Identical` / `Different` / `Missing` / `NotLoaded` / `Ineligible` states, plus
  per-field `FieldDiff`s and `LineDiff` line pairs; reads draft-aware so the comparison
  shows what would actually be saved.
- `BulkPlan` — classifies every template in scope as `Eligible`, `NotLoaded`, `Missing`,
  `Ambiguous`, `ReadOnlyShape` or `Conflict` with an explanation; `Plan()` throws for any
  field other than default value; `StageValues()` refuses non-eligible targets even if a UI
  hands them over, and skips stale targets whose declaration no longer exists.

App (`WorkspaceViewModel.Editing.cs` partial, `ReviewRows.cs`, `UnsavedWorkWindow`,
`ParameterRowItem` draft-aware values, `TemplateItem` staged badges):

- Details form edits the observed fields only (name/type/internal-name locked), stages on
  "Apply edits", marks each changed field with a `draft` badge, shows staged values in the
  table, and keeps `stored:` text under the current draft. Compare and bulk open as panels
  over the parameter list; both have a Cancel and state why nothing can be done.
- `ConfirmNavigation` guards folder change, template open, re-read, filter scope change,
  parameter switch and close. The modal prompt offers apply / discard form / discard staged
  only when those choices describe real pending work, and "stay where I am" is always
  available and is the default when no prompt is installed.

Executed commands and results:

- `dotnet build -c Debug` → 0 warnings, 0 errors.
- `dotnet test` → **105 passing, 0 failing** (Core 59, Integration 12, App/WPF 34).
- New evidence tests: `tests/PubTemplateEditor.Core.Tests/EditingTests.cs` (24 S3 tests) and
  `tests/PubTemplateEditor.App.Tests/EditingInteractionTests.cs` (8 tests). The Core fixtures
  parse XML held in memory only, so a staging bug cannot reach a template file; the UI tests
  write synthetic `.dta` copies under `%TEMP%` and assert the file bytes are byte-identical
  after applying, bulk-staging and discarding.
- `dotnet test tests/PubTemplateEditor.App.Tests` regenerated
  `artifacts/screenshots/editing-staged-light.png`, `editing-compare-light.png`,
  `editing-bulk-light.png` and `prompt-unsaved-light.png`.

Test naming is deliberately independent. The upstream reference suite
(`param-editor-ui/src/lib/__tests__/{draft-store,parameter-comparison,bulk-plan}.test.ts`,
1538 lines, read at the reviewed revision) was used to enumerate *behaviours to cover* —
no-op clearing, active-row anchoring, case/whitespace sensitivity of name matching, access
and description preserved by bulk, eligible-only staging — and its behaviour tests are
Apache-free GPLv3 TypeScript that cannot compile here, so the assertions are rewritten from
the specification in xunit/C# style with different names and fixtures.

Open gates for S3:

1. Human visual/interactive review of the editing UI in a real Windows session (typing,
   tabbing through the Access combo, screen magnification, keyboard-only flow). The
   software-rendered snapshots and process-alive checks do not cover that.
2. The reference app also offers a "Select all / clear" control for bulk targets and a
   virtualised comparison list; both are deferred until template counts make them necessary.
3. Undo/redo across staged drafts is not implemented; per-parameter and global discard are
   the documented recovery paths, matching REQUIREMENTS.md.

## Deferred scope

Linux UI, additional architectures, parameter creation/deletion/renaming, presets, recursive folders, in-app PUB generation, browser hosting and custom installer/updater services are outside this initial Windows release. Keep the domain core independent without building a second UI.
