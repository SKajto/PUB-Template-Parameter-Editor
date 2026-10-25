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

## Deferred scope

Linux UI, additional architectures, parameter creation/deletion/renaming, presets, recursive folders, in-app PUB generation, browser hosting and custom installer/updater services are outside this initial Windows release. Keep the domain core independent without building a second UI.
