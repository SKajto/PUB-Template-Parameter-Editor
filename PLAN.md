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

Status: in progress. The review surface, the source-preserving patcher and the per-file write path are
implemented and tested; the interactive desktop gate and native PUB round-trip evidence stay open.

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

### 2026-10-25 — S4 save review surface (write step intentionally absent)

App (`ViewModels/WorkspaceViewModel.Review.cs` partial, `StagedReviewRow` in `ReviewRows.cs`,
`MainWindow.xaml` overlay, `DraftStore.Remove` in Core):

- The review lists every staged change of the session — one row per change key, ordered by parameter
  name — with columns for the per-row tick, Parameter, Template, Field, Now (session baseline),
  After save (proposal) and Writes. It opens over the workspace with `Ctrl+S`, the top-bar
  "Review all staged changes..." button, or the details action, and it never writes: opening,
  selecting and discarding were each asserted against `File.ReadAllBytes` of the source templates.
- Selection is remembered per change key (`_reviewDeselected`) rather than "select all when nothing
  is ticked", so staging or discarding while the review is open rebuilds the table without silently
  re-ticking rows the user had cleared; a new review starts fully ticked.
- "Discard selected" removes proposals from the session — the only safe meaning of "do not save this
  field" before a write step exists — is disabled when nothing is ticked, and closes the panel when
  the last row disappears instead of showing an empty table.
- Save stays disabled with a visible reason (`CanSaveReview == false` plus `SaveUnavailableReason`,
  echoed in the panel footer, which also states that the baseline shown is the value as read in this
  session and is re-checked before anything is written). A disabled button that explains itself beats
  a silent no-op.
- Top bar now carries the live staged counter (`N staged in M template(s)`), the review button and
  "Discard all" (moved out of the footer where it was easy to miss). The unsaved-work prompt was
  rebuilt around the same distinction: typed-but-not-staged vs staged counts, a "Discard all" choice
  that only appears when that would really throw work away, and an Apply choice that is disabled —
  with its own explanation — while a field is blocked or the session cannot write.
- `DraftStore.Remove(ChangeKey)` drops exactly one staged change and reports false on a second
  removal, so the later save step can clean up what it has already written without a bookkeeping call
  aborting a partially applied batch.

Icons and control templates: the icon set is hand-drawn `StreamGeometry` path data on a 24×24 grid
(folder, reload, list, save, undo, close, compare, brush, sun/moon/display, warning, search) in the
theme resource dictionaries — no icon font and no third-party icon asset, consistent with the
zero-dependency rule. Full templates were added for CheckBox, RadioButton, ComboBox, TabItem,
ScrollBar and ToolTip because the default Aero chrome tints those with its own colours, which was the
source of most contrast failures.

Contrast as a measurable requirement (`ContrastAuditTests`): walks the visual tree of the shell in
base, bulk, compare and save-review states, both prompt variants and the licence window in both
themes, resolves the effective text and background brushes behind every `TextBlock`, and reports
pairs below the WCAG 3.0 minimum (4.5:1 body, 3:1 large/UI). It writes
`artifacts/screenshots/contrast-light.txt` / `contrast-dark.txt`; current state is 0 offenders per
theme (body text 8.06:1 and 12.64:1 light, 10.03:1 and 6.34:1 dark). Two structural causes were found
and are now pinned by tests: the implicit `TextBlock` style used to set `Foreground`, and a style
setter wins over an inherited value — so it pinned labels inside accent-filled buttons to the theme
text colour (now the window root sets `TextElement.Foreground` once, specificity 0, and
`ImplicitTextBlockStyleDoesNotSetForeground` / `EveryWindowRootInheritsTheThemeTextColour` guard it);
and `ShellXamlCarriesNoHardCodedColours` now rejects named colours (`White`, `Black`, …) as well as
hex literals, with the brand mark switched to `OnAccentBrush`.

Executed commands and results:

- `dotnet build -c Release` → 0 warnings, 0 errors.
- `dotnet test` → **115 passing, 0 failing** (Core 60, Integration 12, App/WPF 43).
- New tests: `tests/PubTemplateEditor.App.Tests/ReviewTests.cs` (5),
  `DraftStoreTests.RemoveDropsExactlyOneStagedChange` in `EditingTests.cs` (Core 60),
  `ContrastAuditTests` (2, now including the review panel), and the review case in
  `ShellSnapshotTests.EditingPanelsRenderAndWriteSnapshots` which writes
  `artifacts/screenshots/editing-review-light.png` and asserts the summary text and row count.
- `dotnet publish src/PubTemplateEditor.App -c Release -p:PublishProfile=PortableWin_x64`
  and `powershell -File packaging/build-msix.ps1 -Version 0.4.0 -Mode Draft`
  → `artifacts/packaging/out/PubTemplateEditor_0.4.0_win-x64.msix` (unsigned draft, placeholders).

A binding-path guard tried first watched WPF binding errors with a `TraceListener` across the first
layout pass; that crashed the WPF test host, and a crashed suite is worse than the bug it hunts. The
replacement is static: every `{Binding}` path inside the review table's columns must resolve on
`StagedReviewRow`, and the expected column set must be present. It paid for itself immediately — the
panel bound `IsNoOp`, a member of `DraftChange` that the row object does not expose, and such a typo
blanks a cell instead of throwing.

Also fixed while regenerating snapshots: `ShellSnapshotTests.EditingPanelsRenderAndWriteSnapshots`
closed the shell while three changes were staged. With no prompt seam installed the view model
correctly refuses to lose work, so the window never closed, the test hit its 4-minute timeout and the
abandoned window leaked into the next test. It now installs a `NavigationGuard` seam, asserts the
prompt was actually raised with `NavigationReason.CloseWorkspace`, and closes cleanly; a stale
`testhost.exe` left behind by the earlier run also had to be killed before the suite could be
re-run. The editing test file's helper used the wrong `NavigationDecision`/`NavigationReason` enum
members, which sent the prompt down a path that never dismisses it — another 4-minute timeout.

Open gates:

1. S3's interactive desktop review still stands, and now covers the review panel, the redesigned
   prompt and the icon theme switcher.
2. The write step: source-span patcher with exact lexical verification, output preflight, SHA-256
   fingerprint check against the bytes currently on disk, user-scoped backups and recovery journal,
   same-volume temporary file with verified per-file replacement, and writing only the ticked rows.
   Enabling `SaveReviewCommand` happens last, after round-trip, conflict, mid-batch-failure and
   byte-exactness tests on copies of the native samples pass.


### 2026-10-25 — S4 write-back: patcher, save service and the review save action

Executed commands and results
- `dotnet test` -> Core 74, Integration 33, App/WPF 50 = 157 tests, 0 failed, 0 build warnings (SDK 10.0.402).
- `dotnet publish src/PubTemplateEditor.App -c Release -p:PublishProfile=PortableWin_x64` ->
  `src/PubTemplateEditor.App/bin/Release/net10.0-windows/win-x64/publish/PubTemplateEditor.exe`, 131,174,963 bytes,
  no loose .pdb (DebugType=embedded).
- Launch check of that exe: process alive after 6 s, `MainWindowHandle` non-zero,
  title `[PUB Template Parameter Editor]`, then stopped.
- `powershell -File packaging/build-msix.ps1 -Version 0.5.0 -Mode Draft` ->
  `artifacts/packaging/out/PubTemplateEditor_0.5.0_win-x64.msix`,
  SHA-256 `C9A0E74EE4419334D894BFD8A1F00408F0BF23DC0F7C1D5CC82A0CCEA1E1D0B7` (unsigned draft, REPLACE_* identity).

What was built
- `Core/Format/TemplateXmlPatcher.cs` (no I/O): rewrites only the character span that the parse recorded
  for an edited value, applying edits from the end backwards. Escaping is per context (attribute vs text
  node); CR/LF/tab in a value are refused because XML attribute normalization would destroy them.
  Refusals: stale baseline under the span, change staged for another file, parameter names, missing or
  read-only fields, overlapping spans. The patcher re-parses its own output and compares every field
  against the plan; a patch that fails verification is never offered for writing.
- `Infrastructure/TemplateSaveService.cs`: preflight without writing, SHA-256 fingerprint of the current
  file compared with the fingerprint taken at read time, in-memory assembly of the whole replacement
  file (container rebuild for .dta so the other entries keep name/order/timestamp/comment/bytes),
  verification of that in-memory file, same-folder temporary file written with WriteThrough and hashed,
  user-scoped backup under `%LOCALAPPDATA%\PubTemplateEditorackups` hashed against the original,
  JSON-lines recovery journal (`prepared` / `backup-ready` / `confirmed`, or `aborted` /
  `mismatch-after-replace`) with both hashes, then `File.Replace`, then a read-back of the target
  checking every written field. Per-file outcomes; a batch is not atomic and is never described that way.
- `TemplateReaderService.LoadFromBytes` so the save path can verify what it wrote without replacing the
  record it worked from.
- Review save action (`WorkspaceViewModel.Review.cs` + review footer in MainWindow.xaml): only ticked,
  non no-op rows are sent; the command is an async command guarded by `IsSaving` so a second save cannot
  start while one runs; each file reports its own outcome under the table (`SaveOutcomeSummary`,
  `LastSaveOutcome`); a staged change leaves the session only when its own file was confirmed, everything
  else stays staged with the review open; written templates are re-read so the next save is not mistaken
  for an outside edit. Footer states plainly what saving does, or why it is not offered.

Two real read-path bugs found by the patcher's own verification, both fixed
- A `template.xml` whose decoded text begins with a byte order mark did not parse at all
  ("Data at the root level is invalid"). `SourceText` now exposes `ParseText`/`LeadCharCount`: the mark
  stays in `Text` so character offsets keep covering the whole document, the reader gets the mark-free
  text and every line/position it reports is shifted back.
- A paired empty constant description (`<expression type="constant"></expression>`, 1857 occurrences in
  the native samples) had its span anchored on the end tag's line info, which points just past `</`;
  writing there put the new text inside the tag and produced a broken document. The span now anchors on
  the zero-length gap directly before the end tag. Before this fix the patcher refused the write, so no
  corrupt file could be produced, but the field was effectively unwritable.

Deliberately not supported, with the reason shown in the UI instead of a guess
- `<expression type="constant"/>` (self-closing empty description) stays read-only: writing into it needs
  a tag-shape change, not a value replacement, and no native sample shows that shape.

Tests added
- Core: 13 patcher tests + `Remove` idempotence + the empty-constant span test. Byte-exact splice
  equality is the main assertion: the patched text must equal the original text with the new value
  spliced into the recorded span, character for character. Encoding round trips are asserted byte-exact
  for UTF-8, UTF-8+BOM, UTF-16 LE and BE.
- Integration: 3 native evidence tests that patch all five shipped .dta samples in memory (including all
  234 writable default values of one template and their empty constant descriptions), read the patched
  bytes back, compare every field of every declaration, and assert the sample file still hashes to what
  it hashed to before. 9 save tests on temporary copies only: entry-level byte preservation for the
  other container entries, plain-XML in-place write, conflict refusal before any write, foreign-file
  change, name never written, journal and backup contents, per-file batch reporting, preflight writes
  nothing, a read-only file producing a reported failure with the original intact, and an injected
  mid-batch write failure showing the earlier file already saved.
- App/WPF: 6 save-flow tests through the shell (write + reload, outside edit refused with the draft kept
  and the review open, only ticked rows written while the rest stay staged, disabled/Explains-itself
  states, disabled while a save runs, two templates reported separately, and a save of a copy of the real
  `example.dta` container: the other container entries come back byte for byte, and a second save of the
  same file goes through cleanly instead of looking like an outside edit. The in-flight guard is asserted
  inside the same dispatcher turn, so it cannot race with a fast write. One obsolete test that asserted
  "writing is not part of this build" was replaced by the real enabled/refused behaviour.

Open gates for S4
- Interactive desktop review of the whole edit-to-save flow by a human (typing, tabbing, keyboard-only
  flow, magnification, screen reader) on a real Windows session. Automated tests construct windows and
  commands, they do not prove the feel or accessibility of the flow.
- Native PUB round-trip evidence: a template saved by this app opened in PUB. Requires PUB on this
  machine; not available, so native compatibility remains unproven and the container framing difference
  (rewritten by System.IO.Compression, documented in `DtaContainer`) is the known risk to check there.
- Cancellation in the middle of a batch is implemented (per-file `CancellationToken` plus the command's
  own token) but not covered by a test; recorded as unverified rather than assumed. An injected mid-batch
  write failure is now covered: with the second file read-only, the first file is confirmed saved and the
  second reports a failure with its own bytes unchanged, no `~*.tmp` leftover and an `aborted` journal line.
- A read-only target is reported as `Failed` (not silently `Rejected`), and the backup file name carries a
  short unique suffix so saving the same template twice within one second cannot collide with its own
  earlier backup.

### 2026-10-26 - Shell button layer and where the parameter tools live

Owner feedback: the per-parameter "Review all staged changes" button only makes sense for more than one
change, Compare and "Apply value to templates" belong to the panel that lists the parameters (as in the
reference layout), and command buttons should not be plain text.

Changes
- Removed the per-parameter review button. The review entry exists once, in the top bar beside the
  staged-count badge, plus Ctrl+S. `ButtonChromeTests.TheSaveReviewHasASingleSessionWideEntryPoint`
  walks the realized shell and fails if any other button is wired to `OpenReviewCommand`.
- Added `ParameterActionsPanel` to the parameter panel: Compare and Apply value to templates now sit
  between the active-template/search/access toolbar and the notices/table, matching the reference
  `parameter-actions` row. The parameter panel gained a fifth row; the compare, bulk and review overlays
  span it. Form actions stay with the details form.
- Command buttons became Fluent-style (chosen over Material/Tailwind for a Windows desktop app, built
  only from stock WPF templates): 16 px glyph at the left of a real label, min 31 px height, 6 px radius,
  subtle hover fill, separate pressed fill, accent focus ring, accent variant with its own hover/pressed
  shades. `LinkButtonStyle` became a Fluent subtle button with an optional glyph.
- New plumbing: `Iconics.Icon` attached property for the glyph, `Iconics.HoverBrush` / `PressedBrush`
  because a template trigger cannot be overridden by a BasedOn style, and
  `PresentObjectToVisibilityConverter` to hide the glyph host when a button has no icon.
  `AccentHoverColor`/`AccentPressedColor` added to both theme dictionaries.
- New WPF tests (3): every named command button carries a non-empty geometry, its glyph host is visible
  and measures a legible box after an explicit layout pass, and its text label is still real text;
  Compare/Bulk are descendants of the parameter panel and not of the details card; single review entry.

Executed commands and results
- `dotnet build -c Debug` -> success, 0 warnings.
- `dotnet test` -> Core 74, Integration 33, App/WPF 53 = 160 tests, 0 failed.
- Contrast audit after the change: `contrast-light.txt` and `contrast-dark.txt` both `offenders=0`.
- `src/PubTemplateEditor.App/bin/Debug/net10.0-windows/PubTemplateEditor.exe` starts and stays alive,
  window title `[PUB Template Parameter Editor]`.
- Screenshots regenerated by `ShellSnapshotTests` (shell-light/dark, editing-*, prompt-unsaved-light,
  about-light). Human eyes still have to judge them; the tests prove structure, not appearance.

### 2026-10-26 - Save review became a readable diff instead of a clipped table

Owner feedback: in the review-and-save view the cell text was cut off, so the changes could not actually
be read; the request was to present it like the Compare view, showing the staged value against the
current template value with the changed text highlighted.

Changes
- The review table (`ListView` + `GridView` with fixed-width, ellipsised columns) is replaced by a
  scrollable list of cards (`ReviewGroupsList`) grouped per template file, mirroring the reference
  `save-review-list`. New `ReviewFileGroup` (file name, path, row list, "N changes" badge) is exposed as
  `WorkspaceViewModel.ReviewRows`-parallel `ReviewGroups`; rows are sorted file first, then parameter,
  then field, so a group is contiguous.
- Each card shows the whole value twice, wrapped and never trimmed: "In the template" (baseline) and
  "After the save" (proposed, in the accent colour), plus the parameter name, the field badge, the
  write/read-only badge, an explicit no-op line, and a badge plus tooltip when a value keeps whitespace
  at its edges (which a table cell hides completely).
- New Core type `TextDelta` (`src/PubTemplateEditor.Core/Editing/TextDelta.cs`): ordinal
  common-prefix/suffix character diff with `Prefix/Removed/Added/Suffix` and `…`-shortened `*Shown`
  variants (32 characters of context) plus `IsCompleteReplacement`. The card renders a "change in place"
  strip - muted context, strikethrough removed stretch, underlined added stretch - or, when nothing of
  the old value survives, an explicit "the whole value is replaced" note instead of an unreadable
  full-length strikethrough block.
- New converter `NonEmptyStringToVisibilityConverter` so empty context/diff pieces collapse.
- Tests: 9 new Core `TextDeltaTests` (mid-string edit, append, case and whitespace are real differences,
  complete replacement, equal values, missing/empty, both truncation paths, shared tail). 3 new WPF
  review tests: a ~320 character value is present in full with `TextWrapping.Wrap`, `TextTrimming.None`
  and a wrapped (multi-line) box; the exact changed words are rendered with decorations next to shortened
  context; staged changes group per file (2 groups, per-group counts, same row instances, per-change
  ticking still independent). The old GridView binding test became
  `EveryReviewCardBindingResolvesOnTheTypeThatOwnsIt`, which resolves every path in the card template
  against `ReviewFileGroup` or `StagedReviewRow` and fails on a path belonging to neither.

Executed commands and results
- `dotnet test` -> Core 83, Integration 33, App/WPF 56 = 172 tests, 0 failed, build 0 warnings.
- Contrast audit unchanged after the new brushes and badges: `contrast-light.txt`, `contrast-dark.txt`
  both `offenders=0`.
- `dotnet build -c Debug` then `src/PubTemplateEditor.App/bin/Debug/net10.0-windows/PubTemplateEditor.exe`
  starts and stays alive, title `[PUB Template Parameter Editor]`.
- Still open: human eyes on the regenerated screenshots (`editing-review-light/dark.png`), the interactive
  desktop pass over editing -> review -> save, and the native PUB round trip. Nothing here changes what
  is written; the patcher and save service are untouched by this presentation work.

### 2026-10-26 - Whole-row template selection, resizable columns, one folder entry point, staged-change keys

Owner feedback: selecting a template must mark the complete row and must not be an option button; the
parameter table columns must be resizable; the folder name was shown twice (top-right next to
"Choose folder" and again in the templates panel) - keep the panel one and move "Choose folder" into the
panel that lists the .dta files. Then the polish round (shortcuts, undo, recent folders, drop-to-open).

Changes
- Template list became a `ListBox` (`TemplateList`) with `TemplateRowStyle`: the whole row is the
  selection target, marked by a filled `SelectionBrush` row plus a 3 px accent bar and semi-bold name;
  the read check box on the same row stays an independent decision (FILE-05). No option button is left
  in that list. Keyboard arrow selection now works, and it goes through the same guard as clicking.
- `WorkspaceViewModel.ActiveFile` gained a public setter that routes through `SelectActiveFile`, i.e.
  through the unsaved-work guard. When navigation is refused the view model raises `PropertyChanged`
  for `ActiveFile` so the list snaps its visual selection back to the template that is really shown.
  Folder loading uses `SetActiveFileDirectly`, which deliberately bypasses the guard.
- The restyled `GridViewColumnHeader` template had dropped WPF's `PART_HeaderGripper`, which is exactly
  what makes `GridView` columns draggable - that is why the columns felt fixed. The handle is back
  (10 px hit area over the separator line, accent line on hover/drag). `Cursor` had to be declared as
  `{x:Static input:Cursors.SizeWE}`: the plain `Value="SizeWE"` form parsed to `Cursors.None`.
  `ScrollViewer.HorizontalScrollBarVisibility` on the table became `Auto`, so widening columns past the
  panel scrolls instead of clipping.
- Column widths are stored per position in `settings.json` (`EditorSettings.ParameterColumnWidths`),
  applied when the shell is constructed and saved on close; a set containing NaN/Infinity or a value
  outside 40..8000 px is rejected rather than half applied.
- Top bar: the folder name and the "Choose folder" button were removed (the top bar keeps identity,
  staged-change controls and the theme switch). The panel that lists the templates now holds the folder
  name, the count, "Choose folder", and a `Recent folders` combo.
- Recent folders: `EditorSettings.RecentFolders`, `WorkspaceViewModel.RecentFolders` (most recent first,
  capped at 8, de-duplicated case-insensitively), recorded when a folder lists successfully, restored on
  start, reopened through `OpenRecentFolderCommand`. A folder that disappeared stays in the list and
  reports "could not be opened" rather than silently dropping remembered user state.
- Drag and drop: the window accepts a dropped folder, or a dropped `.dta` file and opens the folder that
  contains it (`TryOpenDroppedPath`); anything else is refused with a message. A hover hint names the
  folder that would be opened.
- Keys and undo: `Ctrl+Enter` stages the open form, `Ctrl+Z` drops the most recently staged change,
  `Esc` closes the topmost overlay (review, then bulk, then compare); the footer lists these keys.
  `DraftStore` now stamps staging order (`MostRecent`, `RemoveMostRecent`) because the store is keyed by
  file + declaration + field and has no order. Inside a value box `Ctrl+Z` stays with the text editor,
  which is where a mistyped character has to be undone. Also removed a duplicated `InitReviewCommands()`
  call in the constructor.
- New tests: Core 3 (undo order across files/fields, re-staging a field makes it newest, discards clean
  the undo history). App 12 - whole-row marking (container selected, accent bar visible, mark spans the
  row width, no radio in the list, other rows unmarked), guard snap-back through the list, drag handle on
  every column plus width-driven layout, widths restored after close/reopen, unusable width sets ignored,
  folder named once and chooser inside the template panel, recent folder order/dedupe/persistence,
  recent list cap and a disappeared folder, drop rules, undo through the shell (review rows plus file
  bytes unchanged), Esc one step at a time, and the advertised keys match the actual bindings.
  `EveryStaticResourceKeyUsedByTheShellIsDefined` now also enforces definition-before-use inside one
  dictionary (proved by reintroducing the fault: it fails with the exact offset message).

Executed commands and results
- `dotnet test` -> Core 86, Integration 33, App/WPF 68 = 187 tests, 0 failed; `dotnet build -c Debug`
  reports 0 warnings and 0 errors.
- Contrast audit after the new row style, combo, drop hint and footer hints: `contrast-light.txt` and
  `contrast-dark.txt` both `offenders=0`.
- `src/PubTemplateEditor.App/bin/Debug/net10.0-windows/PubTemplateEditor.exe` starts and stays alive,
  title `[PUB Template Parameter Editor]`.
- Unverified on purpose: a real mouse drag of a column handle and a real Explorer drag-and-drop are not
  automated - resizing is proven through the handle plus width-driven layout, dropping through
  `TryOpenDroppedPath`. Screenshots in `artifacts/screenshots/` were regenerated; the whole-row marking,
  the chooser placement and the recent-folders combo still need human eyes.

### 2026-10-26 - Tools on one line, hover popups, and the misleading "nothing to write" sentence

Owner feedback: search field and the access dropdown belong on the same line as Compare / Apply value to
templates (them left, buttons right); hovering a parameter should pop name, description and current
value; hovering a template file should pop the file name with its parameter count and staged-change
count; and the review footer claimed "Every staged change already holds the value that is in the file, so
there is nothing to write" even though the session held real changes - is saving supported at all?

Diagnosis of the review message (saving itself was never the gap)
- The write path exists and is tested: source-preserving patcher (`60f34b1`), `TemplateSaveService`
  with preflight/fingerprint/backup/journal/verify-after-write (`687d35a`), and the review Save button
  wired to it (`b365128`). Ten integration tests plus seven shell save-flow tests cover writing,
  per-file batch behaviour, conflicts and recovery.
- What was wrong was the footer sentence. `CanSaveReview` and `SaveUnavailableReason` were never raised
  after staging (only inside a running save), and `ReviewRows` was rebuilt only while the review panel
  was open. The text therefore kept the value it had when the panel was first built - with an empty row
  list, and the old branch order read an empty list as "every staged change already matches". The save
  button itself often still worked because command requery was invalidated.
- Fix: one `SaveGate()` evaluation backs both the flag and the sentence (so button and text cannot
  disagree), staged rows are rebuilt on every draft change whether or not the panel is open, and
  `RaiseReviewSaveState()` fires on open, close, rebuild, tick, select all/none, discard and save.
  Empty list now says "Nothing is staged. Edit a value, apply it, and the change appears here."; the
  "already holds the value" sentence appears only when every row really is a no-op; "Tick at least one
  change" only when rows exist and none are ticked.
- `SaveFlowTests.TheSaveActionStaysDisabledWhileNothingUsefulIsTicked` had encoded the old wrong text as
  an expectation; it now asserts the honest sentence and forbids the old one.

Layout and popups
- Parameter panel line 1 is now `ParameterToolsRow`: search box (star column, min 150) on the left, the
  access filter combo (132 px) next to it, and `ParameterActionsPanel` (Compare, Apply value to
  templates) at the right end of the same line. The active template keeps its own heading line above.
- Row popups: `ParameterRowItem.RowTooltip` (name, current value, description, access, staged fields,
  read-only reasons, file) and `TemplateItem.RowTooltip` (file name, parameter count, staged-change
  count, size/status, full path). An unread file says "not read yet" instead of "0 parameter(s)", and the
  active-template heading repeats the same file summary. Both lists set the popup through their
  `ItemContainerStyle`; WPF has no `ToolTipService.Style`, so the container style carries a per-container
  `ToolTip` styled by the keyed `RowToolTipStyle` (MaxWidth 520, wrapping content template).

Tests
- New App tests: hover popups for parameter rows, staged-state wording in the popup, template row counts
  and path, unread file, popup style wraps and is read from the merged application dictionary, tools line
  geometry (search left of filter left of buttons, all on one line, above the table), and three review
  state tests (empty list wording, save offer follows staging with `PropertyChanged` raised for both
  properties, un-ticking everything takes the offer away and says why).
- `ShellSnapshotTests` layout proof rewritten to the one-line tools row; it compares vertical centres,
  because an input box and a button are not the same height.

Executed commands and results
- `dotnet test` -> Core 86, Integration 33, App/WPF 77 = 196 tests, 0 failed; `dotnet build -c Debug`
  0 warnings, 0 errors.
- `dotnet publish src/PubTemplateEditor.App -c Release -p:PublishProfile=PortableWin_x64` ->
  `src/PubTemplateEditor.App/bin/Release/net10.0-windows/win-x64/publish/PubTemplateEditor.exe`,
  131,204,147 bytes, rebuilt because the previously published exe dated from 2026-10-25 23:37.
- That published exe starts and stays alive, title `[PUB Template Parameter Editor]`.
- Contrast audit unchanged: `contrast-light.txt`, `contrast-dark.txt` both `offenders=0`.
- Still unverified by machine: an actual mouse hover (popups are asserted at the binding, style and
  geometry level, not by moving the pointer), the native PUB round trip, and the Store MSIX identity.
  A manual save through the fresh exe remains the owner's check; backups and the recovery journal are
  written under `%LOCALAPPDATA%\PubTemplateEditor`.

### 2026-10-26 - One control height, card popups, themed window frame, and two save-path faults

Owner feedback: the access dropdown is a third shorter than the buttons in its row; the search box is
 taller than the rest; hover popups should be formatted (cards), not plain text; the main window frame
 should follow the light/dark setting; and saving still did not appear to work - a popup appeared on
"Save to templates", choosing "Apply the form changes and continue" closed it and nothing happened, and
the popup never came back.

Uniform control height
- `Button`, `TextBox` and `ComboBox` in `Themes/Controls.xaml` now share `MinHeight 33` (measured: the
  row was search 37 / combo 31 / buttons 31; now all four render at 33), TextBox vertical padding went
  4 -> 3 so the text has room without the box growing. `HoverPopupTests` asserts the four controls on the
  tools line differ by at most 1 px, which is what "in line" means beyond matching centres.

Card popups instead of a text blob
- The implicit `ToolTip` style became the card frame (rounded 6, 1 px edge, drop shadow, padding 10/8,
  MaxWidth 560). Two `DataTemplate`s keyed by their data type (`ParameterRowItem`, `TemplateItem`) are
  the card contents: title line with a staged badge, rule, labelled rows (Current value / Description /
  Access, or Parameters / Staged / Size) in a two-column grid, rule, status/path footer. New view-model
  text: `ParameterRowItem.StagedNote`, `.LocationText`, `TemplateItem.ParameterCountText`,
  `.StagedChangesText`.
- Rows hand over the row object itself (`Setter Property="ToolTip" Value="{Binding}"`); the card is
  chosen by type when the popup opens. `AutomationProperties.HelpText` carries the same facts as one
  plain sentence, so a screen reader is not walked through a card layout. `RowTooltip` stays as that
  sentence.
- What WPF does not allow, recorded: there is no `ToolTipService.Style` in WPF (that is WinUI), and a
  binding inside a `ToolTip` element placed in a `Setter.Value` is not kept - the `Content` came back
  null while the `ContentTemplate` survived. Type-keyed templates (`DataTemplateKey`) avoid both.
- `ContrastAuditTests` now renders both cards against real rows in both themes (they are not in the
  shell's visual tree until a pointer hovers a row): `offenders=0` for Light and Dark.

Themed window frame
- New `Infrastructure/WindowFrameTheme.cs` asks the desktop window manager for the frame through
  `dwmapi.dll` (`DWMWA_USE_IMMERSIVE_DARK_MODE` 20, with the 19 fallback, and the Windows 11 caption,
  text and border colour attributes). Built-in API only, no package; an unsupported attribute simply
  returns an error and the window keeps the system frame.
- `ThemeManager` now records `LastApplied` (a "follow Windows" choice resolved) and raises `Applied`, so
  the caption follows a theme switch while the window is open. `MainWindow`, `LicensesWindow` and
  `UnsavedWorkWindow` call `WindowFrameTheme.Attach(this)`, which paints on `SourceInitialized` and
  unsubscribes on `Closed`.

Two real faults found while chasing "saving does not work"
1. Unapplied typing was lost. After a successful write the saved templates are re-read, which rebuilds
   the parameter rows and refills the details form - so anything typed but not applied vanished. The
   reload now captures the form's unapplied values first and restores them afterwards
   (`CaptureUnappliedFormEdits` / `RestoreUnappliedFormEdits`), covered by
   `SaveFlowTests.SavingWhileTheFormHoldsUnappliedTyping`.
2. "Apply the form changes and continue" could abort an action in silence. `ConfirmNavigation` ran
   `ApplyEditorForm()` and returned `!HasUnappliedFormEdits`; when the typing could not be staged, the
   action simply did not happen and only a notice elsewhere explained it. `ApplyEditorForm` now returns
   whether the form ended up fully staged, the failure branch states what is held back
   (`UnappliedFieldReport`, e.g. "default value of 'alpha': <reason>"), and the details panel shows that
   line in the error colour. Test: `TheFormNamesTheValueItIsHoldingBack`.
- Saving itself was already implemented and is covered (patcher, save service with fingerprint/backup/
  journal/verify-after-write, per-file batch behaviour). What was missing was the visible result: the
  review panel now keeps a result card (green for written, red for rejected/partly written) with the
  per-file sentences and the backup folder path, it reports attempts that never reached a file
  ("Nothing was written" + why), and it stays open after an attempt instead of closing and hiding the
  proof.

Executed commands and results
- `dotnet test` -> Core 86, Integration 33, App/WPF 80 = 199 tests, 0 failed; `dotnet build -c Debug`
  0 warnings, 0 errors.
- `dotnet publish src/PubTemplateEditor.App -c Release -p:PublishProfile=PortableWin_x64` ->
  `src/PubTemplateEditor.App/bin/Release/net10.0-windows/win-x64/publish/PubTemplateEditor.exe`,
  131,216,435 bytes, rebuilt at 16:33, starts and stays alive, title `[PUB Template Parameter Editor]`.
- Contrast audit including the two card popups: `contrast-light.txt`, `contrast-dark.txt` `offenders=0`.
- Unverified by machine, and it needs your eyes: whether the caption/border actually look right in dark
  mode on this desktop (the frame is painted by Windows, the test host never shows a window), whether a
  hovered card appears where you expect it, and the save flow on your real templates. If a save still
  does nothing, the result card in the review panel now names the file and the reason - report that
  sentence and it can be traced exactly.

### 2026-10-26 - Stage cleared after a save, application icon, samples out of the repository, publishing setup

What changed
- **A completed save now clears what it wrote.** `SweepStageAfterWrite(writtenPaths)`
  (`WorkspaceViewModel.Review.cs`) runs after the reload of every written file and drops each staged
  change whose proposed value is what the file now holds. Without it the stage, the per-file
  "staged" badges and the review list kept showing rows that were already applied, so the next save
  looked like a no-op. `ActionNotice` reports how many rows were swept. New test
  `ACompletedSaveEmptiesTheStageOfEverythingItWrote` (SaveFlowTests) asserts after a save:
  `StagedChangeCount == 0`, no review rows, no per-file staged badge, and that a second save starts
  from an empty stage (it needs `SelectAllReviewCommand` after staging again, because rebuilt review
  rows start unticked by design).
- **Real save evidence from the owner's desktop.** The two landscape samples in `examples/` came back
  modified from a run of the published 16:33 executable, and the byte comparison is the best evidence
  this milestone got: entry count unchanged (241/241 and 209/209), **no entry lost, no entry added,
  240 of 241 and 208 of 209 entries byte-identical by CRC**, and inside `template.xml` exactly the
  intended edits - a single character (`ID(10` to `ID(20`) in one file, and in the other one
  `access internal -> external` plus a `default` gaining `ID(10)`. The container framing did move:
  the file shrank 449,898 -> 438,428 bytes because `System.IO.Compression` recompressed the package,
  which is the documented known risk of the ZIP rewrite and not a content loss. The application's own
  backups (`%LOCALAPPDATA%/PubTemplateEditor/backups`) hold the pre-save bytes and match the pristine
  fixtures by SHA-256, and a third save at 16:16:32 shows the editor accepted a file it had written
  itself without a false outside-edit conflict. This closes the "does Save write anything?" gate for
  real templates; the remaining native gate is opening such a file in PUB itself.
- **Application icon.** `tools/brand/generate-brand-assets.ps1` draws the mark with WPF geometry and
  `RenderTargetBitmap`: a template sheet with an adjustable value line, plus a written-change check
  on the large tiles. It emits `src/PubTemplateEditor.App/Assets/app.ico` (PNG frames 16/24/32/48/64/
  128/256), `Assets/app-256.png` and every MSIX tile in `packaging/assets/` (44, 71, 150, 310,
  310x150, StoreLogo 50, SplashScreen 620x300), replacing the earlier placeholder tiles. Wired as
  `<ApplicationIcon>Assets\app.ico</ApplicationIcon>`, as a `<Resource>` and as
  `Icon="pack://application:,,,/PubTemplateEditor;component/Assets/app.ico"` on all three windows.
  The first attempt used the root namespace `PubTemplateEditor.App` in that URI and crashed the WPF
  test host with `FileNotFoundException` at window construction - the pack URI names the *assembly*,
  which is `PubTemplateEditor` (`AssemblyName`), exactly why `EmbeddedNotices` derives it at runtime.
  `NoticeViewerTests.TheApplicationIconIsEmbeddedAndResolvesForEveryWindow` now resolves the resource
  and constructs a real window; WPF picks the 16x16 frame for the caption, so the assertion checks
  that a usable frame decodes. `BrandAssetTests` verifies the ICO directory (sizes, square frames,
  32 bpp, frames inside the file), that every tile named by the manifest exists at its exact pixel
  size and is not a placeholder, and that the artwork comes from this repository with no download step.
  `NOTICE` records the provenance; no icon pack, icon font or stock image is used anywhere.
- **The native samples left the repository.** `examples/*.dta` is user content: publishing it would
  put a third party's product data on a public host and it is not the GPL's "corresponding source".
  They are now git-ignored, untracked, and removed from the whole history with `git filter-branch
  --index-filter` before the first push, followed by `git reflog expire --expire=now --all` and
  `git gc --prune=now`; `git rev-list --all --objects | grep '\.dta$'` is now empty and
  `NoTemplateFileIsTrackedByGit` guards it. Honest incident record: the rewrite plus aggressive gc
  deleted the working copies, which were still tracked at that moment. Recovery: the stash commit
  still held all five blobs (`git cat-file`), the pristine variants of the two landscape files were
  already captured in `artifacts/user-save-2026-10-26/*.pristine.dta`, the application's own backups
  matched them by SHA-256, and `workspace/JazzTools_ReportTemplates/...` holds the owner's originals.
  `examples/` is back to the five untouched fixtures, ignored by git.
  Tests no longer depend on them: `tests/TestSampleSupport.cs` gives each test the native file when
  the disk has one and a synthetic `.dta`-shaped container (template XML + schema entry + non-XML
  `__RPE_MAPPINGS__`) otherwise, the native evidence theories carry no rows when the samples are
  absent, and CI asserts that none is tracked. `docs/SAMPLE_FINDINGS.md` explains why the knowledge,
  not the bytes, ships.
- **Publishing setup.** Repository URL `https://github.com/SKajto/PUB-Template-Parameter-Editor` is
  recorded in `Directory.Build.props` (`RepositoryUrl`, `PublishRepositoryUrl`), in
  `ProductInfo.RepositoryUrl`/`ReleaseSourceUrl`, in the About text and in `NOTICE`; the reference
  app's renamed location (`SKajto/DTA-Editor`) is recorded as such. Added `README.md` for a public
  repository: what the tool does, layout, build/publish commands, the sample policy, and an explicit
  list of open gates. `packaging/AppxManifest.template.xml` now names `StoreLogo` next to the other
  tiles.
- **CI on the free plan and SignPath preparation.** `ci.yml` triggers on `main` *and* `master`
  (this repository's branch is `master`), gained the "no template content may be published" check and
  a header explaining why the job stays inside the free plan's fair use: one Windows runner, no
  matrix, 7-day CI retention, no paid runner, no Docker step, WPF tests inside `dotnet test`.
  `release.yml` gained the same content guard, the free-plan note, and a `signpath-preparation` job
  that is off unless `SIGNPATH_ENABLED=true`, fails when the SignPath organisation id, policy id or
  token are missing, writes the signature configuration JSON naming the executable and its source
  URL, and then stops with the exact remaining steps. It deliberately does not reference SignPath's
  GitHub action, because that action's commit SHA has not been resolved and verified - an unpinned
  third-party action would break the immutability rule applied to every other action here. Documented
  in `docs/GITHUB_STORE_RELEASE.md` ("Runner budget on the free GitHub plan", "SignPath Foundation
  signing"): the Store signs the MSIX after certification, so only the GitHub-distributed portable
  EXE needs this; SignPath's free certificates cover non-commercial open-source distribution, so a
  paid Store price and the free signing route have to be reviewed together.

Executed commands and results
- `dotnet test` -> Core 86, Integration 39, App/WPF 83 = **208 tests, 0 failed**; build clean.
- `python .tools/inspect_user_save.py`, `.tools/show_save_diff.py`, `.tools/restore_samples.py` - the
  save forensics and the sample recovery described above.
- `git filter-branch --force --index-filter "git rm -r --cached --ignore-unmatch examples/*.dta"
  --prune-empty -- master`, then `refs/original` deleted, reflog expired, `git gc --prune=now`.
- `dotnet publish ... -p:PublishProfile=PortableWin_x64` after the icon wiring - see the entry below
  for the rebuilt executable.

Open gates after this step
- Push to `SKajto/PUB-Template-Parameter-Editor` and the first Actions run on the real host: the
  pinned SHAs, the runner image's .NET 10 SDK and the Windows SDK tools that `packaging/build-msix.ps1`
  needs are verified only by the run itself, not by this machine.
- SignPath: project registration and approval, then a verified commit SHA for their signing action.
- Whether the dark-mode caption/border and the hover cards look right on the desktop, and the native
  PUB round trip (open a saved file in PUB) - both need a human and the product.

### 2026-10-26 - Published to GitHub, and the free-plan CI ran the whole pipeline green

Publishing
- Remote `origin = https://github.com/SKajto/PUB-Template-Parameter-Editor` (anonymous
  `git ls-remote` works, so the repository is public; the GitHub API reports
  `license: GPL-3.0`, `default_branch: main`, `private: false`). It was not empty: it held one
  GitHub-created commit `9fe2612 Initial commit` containing only a LICENSE file.
- Local branch renamed `master` -> `main` to match the existing default branch.
- `git rebase --onto origin/main <root>` was tried and abandoned: the rename-heavy early commits
  produced dozens of add/add-vs-deleted conflicts against an unrelated history. Instead
  `git merge -s ours --allow-unrelated-histories origin/main` keeps their commit as an ancestor with
  an unchanged tree (`git diff HEAD^1 HEAD` is empty). Push was a plain fast-forward:
  `9fe2612..e3bb95f`, no force needed.
- **Licence text fixed on the way.** Comparing our `LICENSE` with the one GitHub created showed the
  same GPLv3 text reflowed into long paragraphs plus one drifted trailing link
  (`/philosophy/why-not-lgpl.html` instead of `/licenses/why-not-lgpl.html`). A shipped licence has
  to be the document, so `LICENSE` is now the canonical FSF text byte for byte (blob `f288702d`,
  identical to the remote's) - which is also why the merge needed no conflict resolution. All tests
  still pass with it, and the app's offline licence view now shows the verbatim text.

CI on the free plan - what the first runs proved and broke
- Run 1 (`40366122547`) failed in *Integration tests*: the native evidence theories had no rows
  without `examples/*.dta`, and `ATemplateWithoutTheSamplesIsNotCountedAsPassed` asserted that the
  samples must exist - a leftover of the old assumption that they ship with the repository. Fixed by
  giving the evidence theories a row that always exists (a container written by
  `TestSamples.WriteSyntheticContainer`) plus one row per native template where the machine has them,
  by turning the reader test into a theory instead of a silent `return`, and by replacing the guard
  with `TheEvidenceSetIsNeverEmptyAndNeverSilentlySmaller`. Explorer tests stopped borrowing their
  numbers from a real template: they write their own one- and three-declaration fixtures, and in the
  process they now assert the documented search rules (names case-sensitive and anchored, so
  `budget` finds nothing while `Budget` finds one row and a name-region attempt never falls through
  to the description; descriptions match anywhere, case-insensitively).
  `Directory.GetFiles(...).Select(Path.GetFileName)` also produced a CI-only CS8619 nullability
  warning, fixed with an explicit lambda.
- Run 2 (`40369621812`) got much further - build, all three test projects, hygiene checks, the new
  "no template content may be published" check and the portable publish all passed - and failed in
  *Draft MSIX*: `makeappx C00CE015`, `StoreLogo` is not a valid `uap:VisualElements` attribute in the
  manifest schema the tool validates, even though the image it pointed at exists and is committed.
  The Store logo is declared once, as `Properties/Logo`, where the schema expects it. Reproduced
  locally in one command (`build-msix.ps1 -Version 0.0.0 -Mode Draft`) and fixed there first.
- Run 3 (`40370781924`) **green**: `windows-latest`, 1 m 46 s, every step successful - checkout and
  `setup-dotnet` at the pinned SHAs, SDK taken from `global.json`, restore, build, Core tests,
  Integration tests, WPF shell tests on the STA host, licensing/hygiene, the template-content guard,
  portable single-file EXE, "no loose symbols", draft MSIX, hashes, artifact upload
  (`ci-outputs-40370781924`, 118,450,977 bytes). That closes the gate "do the pinned actions, the
  runner image and `packaging/build-msix.ps1` work on the real host" - for the CI/Draft path.
- Both CI-caused faults were invisible locally until the samples were moved out of the working tree;
  the suite is now verified both ways on this machine: with samples (Core 86 + Integration 43 +
  App 83 = 212) and with `examples/` relocated to imitate a fresh clone (86 + 27 + 83 = 196), each
  time 0 failed and 0 warnings. That "run the tests without the samples" step is now the standing
  pre-push check, because a fresh clone is what CI sees.

SignPath preparation (not signing yet)
- `release.yml` gained `signpath-preparation`, off unless the repository variable `SIGNPATH_ENABLED`
  is `true`. When on it requires `SIGNPATH_ORGANIZATION_ID`, `SIGNPATH_SIGNATION_POLICY_ID` and the
  environment secret `SIGNPATH_API_TOKEN` (environment `signing-signpath`), writes
  `artifacts-signpath/sign-path-signature-config.json` naming `PubTemplateEditor.exe` and its source
  URL, and then exits 1 with the remaining instructions. It does not reference SignPath's action,
  because their tag->SHA mapping has not been resolved and verified here; an unpinned third-party
  action would break the immutability rule this repository applies everywhere.
- Owner steps for the free Foundation route (terms: https://signpath.org/terms): register the project
  with SignPath so they can confirm it is genuinely open source and non-commercial, using this public
  repository as its identity; then create the environment, add the two variables and the token, pin
  their action SHA after verifying it the way the other pins were verified, and only then set
  `SIGNPATH_ENABLED=true`. Only the downloadable EXE needs it: the Store signs the MSIX itself after
  certification, and nothing built here may be called Store-signed.

Executed commands and results
- `git remote add origin https://github.com/SKajto/PUB-Template-Parameter-Editor.git`,
  `git fetch origin`, `git merge -s ours --allow-unrelated-histories origin/main`,
  `git branch -m master main`, `git push -u origin main`, later pushes `93cd844`, `59cec47`.
- `dotnet test` per project, with and without `examples/` present (numbers above).
- `powershell -File packaging/build-msix.ps1 -Version 0.0.0 -Mode Draft` ->
  `artifacts/packaging/out/PubTemplateEditor_0.0.0_win-x64.msix` 60,982,104 bytes,
  SHA-256 `C9DB95E0...523C`.
- Run status read over the public GitHub REST API (`/actions/runs`, `/jobs`, `/check-runs/{id}/annotations`)
  because no `gh` CLI is installed here; annotations carry step failures but not test detail, which is
  why the sample-absent state is now reproduced locally instead.

Open gates after this step
- The `release-packaging` environment does not exist yet, and `packaging/StoreRelease.settings.json`
  still holds `REPLACE_*` placeholders, so the first tag or manual run of `Release packaging` is
  expected to stop at the REL-04 step - by design, not a regression. No tag has been pushed.
- SignPath registration, then the pinned signer action.
- Native PUB round trip on a file this editor wrote, and the interactive desktop checks (dark-mode
  frame, hover cards, save on the owner's templates).

### 2026-10-26 - SignPath terms checked against the repository, and the executable renamed to the product name

SignPath configuration verified, not guessed
- The conditions page was fetched and read in full (`https://signpath.org/terms`, saved as
  `.tools/signpath_terms.html` for the record). Its project-facing requirements were turned into a
  compliance table in `docs/GITHUB_STORE_RELEASE.md` and into the policy page the conditions ask for.
- The earlier "SignPath preparation" step was **wrong in substance** and is gone: it wrote a
  `sign-path-signature-config.json` in this repository and referenced variables named
  `SIGNPATH_SIGNATION_POLICY_ID`. SignPath's real integration is the action
  `signpath/github-action-submit-signing-request` (tags resolved from the GitHub tags API:
  `v3`/`v3.0` -> `f6d04783b4569d051e0c80105fe66e82819d0092`), whose verified inputs are
  `api-token`, `organization-id`, `project-slug`, `signing-policy-slug`,
  `artifact-configuration-slug`, `github-artifact-id`, `wait-for-completion`,
  `output-artifact-directory` - it takes **slugs**, and it consumes the *GitHub artifact id* of the
  unsigned build. The file-metadata restrictions live in SignPath's artifact configuration, not in a
  repository file. Their usage shape was cross-checked against SignPath's own demo workflow
  (`SignPath/demo-github-actions/.github/workflows/build-and-sign.yml`): upload unsigned artifact ->
  submit signing request -> upload signed artifact.
- `release.yml` now has the real `sign-portable-exe` job: it needs `package`, runs on
  `ubuntu-latest`, uses the protected environment `signing-signpath` (which is where the Approver
  role is enforced: a maintainer must approve the run before the token is available, and SignPath
  itself waits for an approval per request), permission `actions: read` so the action can fetch that
  artifact, is off unless `SIGNPATH_ENABLED=true`, fails loudly listing each missing variable or
  secret, waits up to an hour for the request, then records unsigned/signed SHA-256, the release
  version, the commit and the SignPath request URL in `SIGNING.txt` and uploads
  `signed-portable-<version>`. It never touches the MSIX, and the note it writes says explicitly
  that the Store is not the signer here.
- Artifact metadata, which their configuration enforces, is produced and verified on our side: the
  release publish passes `-p:Version/-p:AssemblyVersion/-p:FileVersion/-p:InformationalVersion` and
  `-p:IncludeSourceRevisionInInformationalVersion=false` (a `0.1.0+dd3455f…` informational version
  was not what a "same product version in each build" check should compare), and a step reads
  `VersionInfo` back and requires product name `PUB Template Parameter Editor` with
  ProductVersion == FileVersion == the release version. Locally that now reports
  `name=PUB Template Parameter Editor productVersion=0.1.0 fileVersion=0.1.0 desc=PUB Template Parameter Editor`.
- Added `docs/CODE_SIGNING_POLICY.md` with the wording their conditions ask the project home page to
  expose: the sentence "Free code signing provided by SignPath.io, certificate by SignPath
  Foundation", the Author/Review/Approve role table with links, the privacy sentence ("This program
  will not transfer any information to other networked systems unless specifically requested by the
  user or the person installing or operating it"), system-change announcement, uninstallation, and
  the build-provenance/metadata rules. README gained the matching **Code signing policy** section
  that links to it. Added `.github/CODEOWNERS` so the review responsibility their roles require is
  declared, with build/packaging/CI files called out as part of the signed binary's source.
- Two claims of mine that the terms do not support were corrected: a Store price is not forbidden by
  them (GPL allows charging; what they forbid is commercial *dual licensing* of components), and
  there is a "verifiable reputation" expectation for executables people run because of the
  signature - a new repository can simply be declined, with no arbitration. Both are written into
  the docs instead of being glossed over.
- Two gates the conditions create that no workflow can close by itself: the project must already be
  **released in the form to be signed** (no GitHub Release yet), and MFA plus branch protection must
  be enabled by the owner. `StoreRelease.settings.json` no longer keeps a placeholder for the source
  URL (`sourceRepositoryUrl` is the real public repository now); the four identity values stay
  `REPLACE_*` until Partner Center supplies them, and `PackagingConfigurationTests` asserts exactly
  that split. To let a signed portable release exist before those identity values arrive, the
  release workflow gained the `includeStorePackage` choice; a tag push still insists on it.

Executable named after the product (owner instruction)
- `AssemblyName` is now `PUB_Template_Parameter_Editor`, so the downloaded file,
  Task Manager, the MSIX payload and the process name all say the product name. Underscores were
  chosen over spaces deliberately: the name appears in MSBuild output paths, in `makeappx` content
  paths, in PowerShell steps and in the SignPath artifact configuration, where a space invites
  quoting bugs. A spaced name is a one-line change if the owner prefers it.
- Everything that had to follow the rename did: the three window `Icon` pack URIs (they name the
  *assembly*, so `/PUB_Template_Parameter_Editor;component/Assets/app.ico`), the two tests that
  assert those URIs and resolve them at runtime, `packaging/AppxManifest.template.xml`
  `Executable=`, `packaging/build-msix.ps1` payload and required-file checks, the draft package file
  name, the CI steps, and the project's own docs (`docs/WINDOWS_PUBLISH.md`, release/signing/policy
  docs, README). `EmbeddedNotices` derives the assembly name at runtime, so the offline licence
  resources needed no change. The user-data folder stays `%LOCALAPPDATA%\PubTemplateEditor`, because
  renaming it would orphan the settings and the backups that are already there.
- New guard test `TheManifestNamesTheExecutableThatTheProjectActuallyBuilds`: manifest `Executable`
  must equal `<AssemblyName>` + `.exe`, which is the pairing a rename silently breaks and which
  would otherwise show up as a package that installs and never starts.
- Verified locally after the rename: full suite with samples Core 86 + Integration 44 + App/WPF 83 =
  **213** and, with `examples/` moved away, 86 + 28 + 83 = **197**, 0 failed, 0 warnings; portable
  publish produced `PUB_Template_Parameter_Editor.exe` (131,261,563 bytes) which starts and holds
  `[PUB Template Parameter Editor]` with a 32x32 associated icon; `build-msix.ps1 -Version 0.0.0
  -Mode Draft` packed `PUB_Template_Parameter_Editor_0.0.0_win-x64.msix`, SHA-256 `F12A7D05...BE78`.
- `AGENTS.md` and `REQUIREMENTS.md` (GEN-04) still name the old executable; those are the owner's
  instruction documents and were left untouched deliberately - this section is the record that the
  owner's later instruction supersedes that one word.

Open gates after this step
- Owner: enable MFA and branch protection, create the `signing-signpath` environment with a required
  reviewer, register the project with SignPath, then set the four variables and `SIGNPATH_ENABLED`.
- A GitHub Release carrying the executable, source archive and notices (their "already released" and
  "documented" conditions), which needs a version decision from the owner.
- Whether SignPath accepts a brand-new repository at all - their reputation judgement, not a
  checkbox.

### 2026-10-26 - Community standards, and the commits now carry the owner's identity

Community Standards files (the repository's Community page asked for them)
- `CODE_OF_CONDUCT.md` - Contributor Covenant 2.1, taken verbatim from the official site
  (`contributor-covenant.org/version/2/1/code_of_conduct/code_of_conduct.md`) so nothing is
  paraphrased from memory; the single `[INSERT CONTACT METHOD]` placeholder is replaced with the
  private contact routes this repository really has (Security-tab draft advisory, the owner's GitHub
  profile), plus a project section naming who the "community leaders" are, a note that a project
  signed through SignPath Foundation is also bound by their conditions and code of conduct, and the
  distinction that the covenant document is CC BY-SA 4.0 while the software is GPL-3.0-only.
- `CONTRIBUTING.md` - the constraints that actually bite here: zero third-party production packages
  and how to justify one, the Core/Infrastructure/App boundaries, "nothing writes outside the review
  step", template files as untrusted data, no `.dta` in the repository, how to run the tests
  including the sample-free run, the two WPF runtime-only failure modes a change needs a test for,
  and that build/CI files are part of the signed source. Contributions are confirmed under
  GPL-3.0-only.
- `SECURITY.md` - supported versions and a threat model written from what this program really does
  (`DtdProcessing.Prohibit`, `XmlResolver = null`, values treated as text, no network capability,
  fingerprint-checked writes with backups and a journal, the container read in memory rather than
  extracted), private reporting through the Security tab, what to include, an explicit "no real
  templates, a synthetic one is worth more", and an honest "one maintainer, no SLA, no bounty".
- `.github/ISSUE_TEMPLATE/` - `bug_report.yml`, `template_format_problem.yml` (the case this project
  actually needs: a file the editor refuses, asked for as *shape*, never as an attachment),
  `feature_request.yml` with the deferred-scope list and the design boundaries spelled out, and
  `config.yml` with private-security and guidance contact links.
- `.github/PULL_REQUEST_TEMPLATE.md` - behaviour-first description, a verification list that
  includes the sample-free run and the contrast audit, a mandatory "what could not be verified
  here", the constraint checklist (no new package, project boundaries, no `.dta`, licence/SPDX,
  manifest and executable agreement), and a call-out of the workflow/packaging/global.json files as
  the closest review targets.
- `CommunityStandardsTests` (Integration) keeps this honest: the files exist and say something
  specific to this project (the security policy must mention `DtdProcessing` and the no-attachment
  rule, the PR template must mention the unverified list and `.github/workflows`), **no community
  file contains an unfilled placeholder**, every issue form is a real form, the two file-related
  forms tell the reporter not to attach a template, and every page the README links to exists.

Commit identity
- The history was authored as `Suad <suad@localhost>` (23 commits), `PUB Template Editor
  <dev@localhost>` (2) and GitHub's own initial commit as `SKajto
  <43079991+SKajto@users.noreply.github.com>` (1). The account's canonical private identity was read
  from the GitHub users API (id 43079991), which is the same address the initial commit already
  carries, so rewriting to it makes GitHub attribute every commit to the owner instead of showing an
  unmatched name.
- Repository-local `user.name`/`user.email` are set to that identity in `.git/config` (not a tracked
  file), so new commits are right from the start; the existing history is rewritten with
  `git filter-branch --env-filter` over `main`, then `refs/original` is deleted, the reflog expired
  and `git gc --prune=now` run, and the branch is updated with `git push --force-with-lease` - a
  forced update of a branch that existed for two hours and that nobody else can have based on yet.

### 2026-10-26 - Bulk editing now copies the access and the description too (owner-reported bug)

What was wrong
- The owner's report: "Apply value to selected templates" copied only the default value; changes to
  internal/external (the access property) and to the description were not applied to the other
  templates. That was the design at the time - `REQUIREMENTS.md` line "Bulk editing | Copy only the
  value" and `BulkPlan.Plan(..., field)` even **threw** `ArgumentException` for
  `FieldKind.Access`/`Description` - but the owner has now superseded that requirement, so the
  restriction was removed rather than worked around in the UI. `REQUIREMENTS.md` and `AGENTS.md` are
  the owner's documents and keep the old wording; this section is the record that the later
  instruction wins.

How it works now
- `BulkPlan` plans **per field**: a target's name-level status is `Eligible` (exactly one declaration
  with that exact name), `Missing`, `Ambiguous` or `NotLoaded`, and `BulkTarget.Fields` carries one
  `BulkFieldTarget` per bulk field - default value, access, description - saying whether this template
  can receive it, its current value, and the parser's reason when it cannot. `ReadOnlyShape` is gone
  from the name-level enum: a template with no `default` property is still a target for the access and
  the description, and the card for it names the field it cannot take.
- `BulkPlan.Stage(name, proposedByField, selected, records, drafts)` stages each chosen field on each
  selected target and returns `(target, field, result)` triples, so a no-op (`ClearedAsNoOp`) and a
  refusal are both reported per field instead of silently disappearing. Guards that stay: an ambiguous
  or unread target never stages, a field the plan marked unwritable is refused even if a forged target
  list asks for it, an access value outside `internal / external` is refused, and at least one field
  must be chosen (`ArgumentException` otherwise).
- The panel: three switches, each with its own input - the default value text, an access ComboBox
  (`internal`/`external`) and a description text - prefilled with the focused parameter's current
  values, and each switch **off** to start with if the source itself cannot offer that field. All three
  start on when they are copyable, which is what the report asked for. The summary counts per field
  (`default value: 3 of 3 · access: 3 of 3 · description: 2 of 3`) instead of one aggregate number,
  because the interesting case is a template that takes the value but has nothing to write the
  description into. Ticks are remembered per file path so switching a field does not discard the
  user's target selection. Buttons: "Apply fields to templates" / "Apply chosen fields to selected
  templates".
- The notice after a run names the fields: `Staged 6 change(s) across 2 template(s): default value 2,
  access 2, description 2.` plus how many were no-ops and how many were refused.

Tests (suite now Core 89 + Integration 48 + App/WPF 86 = **223**; 89 + 32 + 86 = 207 with
`examples/` moved away; 0 warnings)
- Core: `ValueAccessAndDescriptionAreStagedTogetherForTheSelectedTargets` (5 staged + 1 honest no-op,
  parsed baseline untouched), `ChoosingOneFieldAloneStagesOnlyThatField`,
  `AFieldThatIsAbsentOrOutsideTheVocabularyIsRefusedOnItsOwn` (no access property, no description
  property, `access="public"`), `BulkNeedsAFieldAndAnAccessValueInTheObservedVocabulary`, and the
  per-field half of `EveryTargetGetsAStatusAndIneligibleTargetsGetAReason`.
- WPF shell: `BulkCopiesAccessAndDescriptionOntoTheSameNamedParameter` (three fields into two
  templates, review rows name all three per file, `beta` in the same file untouched, the re-read form
  shows the staged access/description) and
  `BulkWritesTheFieldsATargetCanHoldAndLeavesTheRestExplained` (no `default` property: 2 staged, 1
  refused and the reason shown).
- Save path: `BulkAppliedFieldsAreWrittenAndTheRestOfTheContainerSurvives` - bulk value+access+
  description into a real `.dta` container, every non-template entry byte-identical, the source file
  byte-unchanged, the written XML keeps this file's own access shape (`value="internal"`, no
  structure invented) and the description stays a constant expression.
- Fixture lesson again: an access written as
  `<property name="access"><expression type="constant">internal</expression></property>` is *not*
  editable in this editor, so a bulk run started from such a parameter correctly begins with the
  access switch off - the first version of the save-flow test tripped over exactly that.

Side finding while re-running the evidence tests: the owner had saved again at 19:56 UTC, which
changed `examples/requirements_specification_yps.dta` (234/162/72 no longer matched) and
`..._yps_landscape.dta`. Both originals were restored from the application's own backups
(`20261026-195652-…bak`) and their SHA-256 equals the pristine fixtures recorded earlier
(`a6a48a02…`, `280d4c24…`) - the backup layer preserved real templates byte for byte, and the
evidence fixtures are back to the verified state. Published `PUB_Template_Parameter_Editor.exe`
(131,273,851 bytes, product version 0.1.0) starts with the right title.

### 2026-10-26 - No Microsoft Store channel: the distribution is a SignPath-signed GitHub Release

Why
- The owner rejected a sentence in the README - "The MSIX for the Microsoft Store is signed by
  Microsoft after certification instead" - and then stated the channel directly: **the application
  will not be distributed via the Microsoft Store**, and **only SignPath.io is used for signing**.
  The claim was not just badly worded: it described a route this project no longer takes, and a
  sentence like that makes an unsigned build look trusted.

What changed
- **Channel:** the GitHub Release built by the `Release packaging` workflow, carrying
  `PUB_Template_Parameter_Editor.exe`, the complete corresponding source archive, the notices and the
  hash/provenance record. **Signing:** SignPath.io with a SignPath Foundation certificate - the
  executable, and an optional package too, in the same job. No publisher CA certificate, no Store
  product, no Store identity, no Store fees, and no artifact of this project may be described with a
  store signature.
- `docs/GITHUB_STORE_RELEASE.md` is **removed** and replaced by `docs/RELEASE_PACKAGING.md`: channels
  and who signs them, the makeappx lessons that stay relevant for local packaging, the four workflow
  jobs and their permissions, the pinned-and-verified action table (including SignPath's
  `v3.0 = f6d04783b4569d051e0c80105fe66e82819d0092`), the free-plan runner budget table, the
  SignPath-conditions checklist, the owner's open configuration gates, and version/provenance rules.
- `ci.yml`: the draft-MSIX step is gone (CI no longer spends runner time or artifact storage on a
  package nobody ships), the job is renamed "Build, test and publish the portable payload", hashes
  and artifacts no longer reference `artifacts/packaging/out`, and the artifact upload step is
  labelled plainly "unsigned". The packaging tooling is still covered by the packaging-configuration
  tests, so removing the step does not remove the coverage.
- `release.yml`: `includeStorePackage` becomes **`buildMsix` defaulting to false** (packaged-app
  testing, opt-in); the identity check now reads "reject unresolved packaging identity when a package
  is built" instead of the old REL-04 submission framing; the optional package is uploaded as its own
  artifact and, when it exists, is submitted to SignPath in the same signing job - if the artifact
  configuration on SignPath's side does not authorise package signing, their service rejects the
  request and the job fails rather than publishing an unsigned package under a signed name. The
  `SIGNING.txt` note now records the channel instead of a store claim.
- New job **`publish-github-release`**: runs only when the repository variable
  `PUBLISH_GITHUB_RELEASE=true` **and** `needs.sign-portable-exe.result == 'success'`, with
  `contents: write` and `actions: read` on the protected `release-packaging` environment. It downloads
  this run's artifacts with the runner's preinstalled GitHub CLI, picks the **signed** executable by
  path (refusing to publish if no signed copy is present), and creates `v<version>` with the signed
  binary, the source archive, `SHA256SUMS.txt` and `SIGNING.txt`, with release notes that state the
  signer, the commit, GPL-3.0-only and the absence of a Store channel. Using `gh release create`
  instead of a third-party release action keeps the pinned-action list unchanged.
- Wording swept across the project's own documents: README (status, layout table, build section,
  program name, code signing section, uninstall), `docs/CODE_SIGNING_POLICY.md` (what is signed and
  what is not, uninstallation, open gates, the non-commercial condition), `SECURITY.md`,
  `CONTRIBUTING.md`, the PR template, `packaging/build-msix.ps1` and `StoreRelease.settings.json`
  comments (the `REPLACE_*` identity placeholders stay on purpose, and are now described as exactly
  that rather than as "pending Partner Center values"), and `packaging/AppxManifest.template.xml`.
  The supplied baseline documents (`REQUIREMENTS.md`, `docs/PROJECT_SETUP.md`,
  `docs/WINDOWS_PUBLISH.md`, `docs/TEST_MATRIX.md`, `docs/GPL_RELEASE.md`) keep their original text
  under a clearly marked "Channel update, 2026-10-26" note, except `WINDOWS_PUBLISH.md`, whose
  signing paragraph made the claim the owner rejected and was corrected rather than annotated.
  `AGENTS.md` is the instruction file and was left untouched - this section is the record that its
  "primary release is Store MSIX" line no longer describes the project.
- New guard test `ChannelPolicyTests` (4 tests): no README/doc/workflow/packaging text may contain
  the affirmative claims ("signed by Microsoft", "Microsoft signs", "Store signs", "Store signing
  happens", "after Partner Center certification", "Store submission", "Store-signed package"); the
  real channel must be named where it matters (GitHub Release, SignPath, `gh release create`, the
  refusal to publish an unsigned binary, `buildMsix` opt-in, and **no** `build-msix.ps1` call in CI);
  the old Store design document must be gone while the baseline documents carry the update note; and
  every relative link in README must resolve - which is what catches a doc rename that leaves links
  behind. Writing it caught two of my own negations ("nothing here is signed by Microsoft", "no
  channel in which Microsoft signs") that a blunt phrase check cannot distinguish from a claim, and
  those sentences were reworded instead of weakening the test.

Verified locally: Core 89 + Integration 52 + App/WPF 86 = **227** tests, 0 failed, 0 warnings; with
`examples/` moved away, 89 + 36 + 86 = **211**, 0 failed. Both workflows still parse as YAML; the
release workflow now has jobs `package`, `sign-portable-exe`, `publish-github-release`.

Still open, in order: SignPath registration and the four variables + token; MFA and branch
protection; a version tag plus `PUBLISH_GITHUB_RELEASE=true` for the first signed release; the native
PUB round trip. Publishing a GitHub Release and registering with SignPath are owner actions - they
need the account and the agreement, and neither is done from here without an explicit instruction.

### 2026-10-26 - Code signing policy trimmed, privacy wording corrected (owner review)

- The README section had grown past its purpose, and one sentence was wrong. It copied the wording
  SignPath suggests as an example ("will not transfer any information to other networked systems
  *unless specifically requested by the user or the person installing or operating it*"), which
  implies the program might transfer data on request. It does not: it transfers nothing, under any
  circumstance, and has no network capability at all.
- Both documents now state only what is needed, in the owner's terms:
  - signing: "signed with a free certificate provided by SignPath Foundation (through SignPath.io)",
    with one honest status note - the subscription is being set up, so builds are unsigned until it is
    active;
  - privacy: no user data, and no data of any kind, are collected or transferred by this application;
  - system changes: the application does not alter the system - no installer, no service, no startup
    entry, no registry change, no elevation; it writes only the template files confirmed in Review
    Save and its own files under `%LOCALAPPDATA%\PubTemplateEditor`;
  - uninstall: delete the program's folder.
- `docs/CODE_SIGNING_POLICY.md` keeps only what the SignPath conditions actually ask a project to
  publish: the SignPath sentence and its pending status, what is signed and what is not, privacy,
  system changes and uninstallation, the three roles (Author / Reviewer / Approver) with links and the
  MFA requirement, four bullets on build provenance and file metadata, and the open gates for the
  owner. Dropped: the long conditions table, the reputation and licence discussion and the channel
  essay - the first two live in `docs/RELEASE_PACKAGING.md`, where they belong, and duplication is
  what let the two copies disagree in the first place.
- History note: an earlier entry in this file quotes the "unless specifically requested" sentence as
  what was written at the time. It is left as the record and superseded by this entry.
- Verified: Integration 52 tests green, including `ChannelPolicyTests` (no forbidden signing claim) and
  `CommunityStandardsTests` (README links resolve, the required wording is present); 0 warnings.

## Deferred scope

Linux UI, additional architectures, parameter creation/deletion/renaming, presets, recursive folders, in-app PUB generation, browser hosting and custom installer/updater services are outside this initial Windows release. Keep the domain core independent without building a second UI.
