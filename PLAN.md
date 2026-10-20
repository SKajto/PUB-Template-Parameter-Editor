# PUB Template Parameter Editor implementation plan

Status: M0 remains in progress. The source-span patcher, fail-closed native DTA ZIP writer, staged editor, and review-gated main-process save path work for the observed sample ZIP variant. A Linux x64 package has been built and launched in WSLg on Ubuntu 26.04.1; Ubuntu 24.04 runtime validation, full Review Save GUI testing, and PUB compatibility remain open. Startup recovery is implemented and unit-tested, with broader live verification still open. Update this file after each milestone with decisions, files changed, executed checks, results, and unresolved constraints. Preserve evidence rather than reporting a milestone as complete because its code exists.

## M0 Establish format and packaging feasibility

- Obtain representative native DTA samples and record the user's PUB version. Inspect bytes and container structure. Compare declarations with the known XML examples.
- Verify target OS baselines. Provisional targets: Windows 11 x64 and Ubuntu 24.04 LTS x64. Resolve Linux ordinary-user sandbox and library requirements with a minimal packaged application.
- Choose a supported Electron scaffolding, React/TypeScript build, XML parser with source position support, packaging tool, and test tools. Verify versions using official documentation and commit a lockfile.
- Implement a small format inspection spike, not the complete interface. Determine whether minimal declaration changes can be made without rewriting unrelated content.
- Record actual development and test commands in AGENTS.md. Add reproducible development setup instructions to the application README when implementation begins.

### Progress record (15 October 2026)

- Inspected local `.dta` fixtures byte-for-byte. Proprietary identifiers and sample measurements have been removed from tracked findings; sanitized adapter scope and preservation limits are recorded in `docs/NATIVE_DTA_FINDINGS.md`.
- A no-op ZIP repack test on temporary copies preserved member order and every uncompressed member payload, and confirmed that samples remained unchanged. Python `zipfile` changed flag bits, external attributes and compressed sizes; this is not a production-safe container editing strategy.
- Created the Electron Forge Webpack + TypeScript shell in `desktop/` and added React. Webpack was selected because Forge's current Vite plugin documentation calls it experimental. Resolved versions: Node.js 24.21.0, npm 11.19.0, Electron 44.7.2, Forge 8.0.1, React 19.3.1 and TypeScript 6.0.3.
- `npm run typecheck` and `npm run lint` pass. `npm run make` produced `desktop/out/make/zip/win32/x64/PUB Template Parameter Editor-win32-x64-0.1.0.zip` (157,855,711 bytes). The extracted app was launched on Windows 11 x64 while not elevated; the GUI was visually verified and shows no template access or save action.
- `npm run make -- --platform=linux --arch=x64` completed on Windows and produced a 122,957,681-byte ZIP. Its Linux executable has no Unix executable mode bits, so this cross-build is not accepted as a [ADDRESS] release or runtime test. Linux packaging and startup need a Linux host and agreed baseline.
- `npm audit --omit=dev` reports zero vulnerabilities. The full development tree currently reports 6 high findings in the Forge Webpack development-server chain (`braces` via `chokidar`/`micromatch`); npm offers no compatible fix, so the breaking `--force` suggestion remains unapplied.
- Native Linux packaging now runs on WSL Ubuntu 26.04.1 x64. The rebuilt ZIP is 123,087,499 bytes (SHA-256 `b713ea362b302beec149232e8599efbbcf2a1203a3a9e0f14dacd08641b65c6e`); its extracted executable has mode 755, and the user verified WSLg startup. This does not validate the planned Ubuntu 24.04 baseline.
- Saving to `/mnt/c` initially failed because DrvFS rejected an unconditional `chmod` on the same-directory temporary output. The writer now compares effective modes before changing them and skips unnecessary chmod calls, including during restore. All 39 application tests pass, and a local synthetic DrvFS save smoke test verified a temporary `.txt` target. No proprietary `.dta` templates were uploaded. Re-test the complete GUI Review Save flow with the rebuilt package.
- Initialized a Git repository at the project root to isolate the project from the user's parent-directory repository. No commit or remote is configured. The parent remote is for GetDocs; nothing was pushed.
- `AGENTS.md` still contains its pre-scaffold command placeholder because the protected-file approval prompt timed out. Verified commands and current limitations are recorded in `desktop/README.md`; the instruction file was not edited through another route.
- Added `tools/inspect_dta.py`, a read-only native ZIP/XML sample inspector, with standard-library unit tests. The utility is for local fixtures only and is not the application's production parser or editor; proprietary fixtures are not tracked.
- `desktop/src/core/xml-source-patcher.ts` now supplies the tested structural XML patcher. `desktop/src/core/dta-zip-writer.ts` writes only the observed ZIP32 variant and fails closed on unsupported layouts. A save service preflights fingerprints, creates synced same-volume outputs and original backups, journals each replacement, verifies outputs, and attempts rollback on in-process failure. Tests write to temporary copies and confirmed all original samples remained unchanged.
- Remaining M0 work: record/agree the PUB version and supported Linux baseline; validate Linux packaging/runtime on Ubuntu 24.04; verify edited templates in PUB. Broader DTA ZIP variants remain unsupported; crash-startup recovery is implemented but still needs broader live workflow verification.

Done when native DTA structure is evidenced, editing strategy is feasible, and a minimal packaged GUI starts under standard-user accounts on both target platforms. If a platform cannot be tested, explicitly leave that gate open. If native samples are unavailable, complete XML exploration and document the gate; do not invent DTA packaging.

## M1 Deliver a read-only explorer

- Implement folder selection, remembered folder and theme, template sidebar, filename filtering, read selection and explicit Read parameters.
- Read declarations via the verified adapter. Display the active template's parameters, name/description search, Internal/External filtering, and read-only details.
- Show progress, loading/empty states, per-template errors and counts. Keep active selection separate from multi-selection.

### Current implementation record

- The application contains the folder explorer, remembered folder/theme settings, explicit read selection, native DTA reader, filters, details, and progress. The packaged Windows GUI was launched and visually checked in the M0 record; the current development GUI was also opened and the wider/resizable details panel was verified.
- Current automated checks pass: `npm test` (38 tests, including native sample parsing/writing and crash recovery on temporary copies), `npm run typecheck`, and `npm run lint`. The Python sample inspector's four tests also pass.
- M1 is not declared complete: combined UI filter/navigation behavior, settings across restart, malformed-plus-valid batch behavior, and an Electron end-to-end GUI suite still need direct verification.

Done when sample declarations match independent counts, search/filter combinations work, settings survive restart, and malformed files do not block valid templates.

## M2 Add local staged editing

- Enable value, access and supported description edits. Keep names read-only.
- Add Apply edits, in-memory changes and dirty indicators. Normalize changes that return to baseline.
- Preserve drafts across selections and filters; implement dirty-work handling for close, folder changes and re-read.
- Implement and test lossless patch generation without exposing direct saving yet.

### Current implementation record

- The detail editor stages value, access, and supported description changes in memory; names remain read-only. Drafts are per-file/per-declaration/per-field, normalize back to baseline, survive selection/filter changes, and can be explicitly discarded. Changed rows now show an Unsaved badge until saved, canceled, or restored to baseline.
- Parameter-name queries are case-sensitive and anchored at the beginning; `*` spans arbitrary text without requiring the match to end at the name's end. Description text remains case-insensitive and searchable anywhere. Save confirmations can be dismissed and auto-close.
- Review Save is now wired to the main process. It shows old/proposed values per field, includes/excludes individual changes, preserves drafts on Back/failure, and writes only included changes. The native writer and save service are tested independently on temporary sample copies.
- Dirty close, folder change, and re-read requests now open a choice to apply unapplied forms, save for review, discard explicitly, or keep editing. Returning from Review cancels the pending navigation instead of completing it implicitly. Main-process close interception is driven by a narrow dirty-state IPC signal.
- M2/M4 remain partial until the close/folder/re-read choices are exercised end-to-end in Electron. Full Review Save workflow automation and PUB validation are also outstanding. Saving is per-file replacement, not one global atomic batch.

Done when no file changes during editing and generated outputs change only selected fields. Empty strings, special characters, Unicode and multiline content round-trip. Unsupported constructs remain intact and read-only.

## M3 Add comparison and bulk value staging

- Compare the active declaration with selected or all loaded templates using working values with draft badges.
- Show field differences, equality and missing declarations separately.
- Add eligible-target selection and explicit value-only Apply changes. Preserve each target's description/access.

### Current implementation record

- Compare uses exact, case-sensitive names and working values, distinguishes missing, ambiguous, equal, and differing declarations, and marks draft-derived values. The comparison target selection is separate from the read selection; an empty target set is explicitly described as all loaded templates.
- Bulk staging lists eligible, missing, ambiguous, and read-only matches. It starts with no targets checked, offers explicit Select all eligible, and stages only the checked default-value fields.
- Core comparison/bulk behavior is unit-tested. Live GUI verification confirmed that unchecking one comparison target removes only that row, Compare all restores all loaded templates, and bulk targets start unchecked. A one-target bulk Apply with the unchanged baseline value correctly created no drafts and confirmed the no-op notice. An actual changed-value GUI Apply remains unverified because foreground keyboard approval timed out; unit tests cover changed-value staging. No files were written during this GUI check.

Done when matching is exact, ambiguous targets are blocked, hidden selection semantics are explicit, and only checked targets acquire drafts.

## M4 Add review and recoverable saving

- Add full-workspace review grouped by file, declaration and field with selectable delivery changes and Back.
- Implement review snapshot validity and the specified exclude/revert behavior.
- Implement preflight, concurrent-change detection, validated temporary outputs, original backups, per-file replacement, verification and recovery journal.
- Handle failures and startup recovery. Return to the explorer only after complete success; reconcile disk state before retry after any failure.

### Current implementation record

- Review and main-process save are implemented with file fingerprint checks, output validation, backups, a per-batch journal, per-file replacement, output verification, and in-process rollback. Unit tests cover success, concurrent-change blocking, and a mid-batch failure with restoration.
- Startup recovery is implemented in the main process: unresolved journals are detected, the renderer shows an explicit Restore original backups action, and files are restored only when their current fingerprint equals the exact prepared output. Backups and journal paths are validated against the current registered template folder; unknown/external fingerprints remain untouched. Reads and subsequent saves are blocked while recovery is pending.
- Recovery unit tests cover successful restoration, externally changed files, and out-of-workspace journal paths. The packaged Windows app was launched with isolated user data and a simulated interrupted journal; its explicit restore action returned a temporary copy to the exact backup hash and cleared the recovery warning. `npm test` passes 38 tests, with typecheck and lint passing. Full Review Save and PUB remain unverified end-to-end, so M4 remains partial.

Done when selected changes are saved, excluded changes revert only on success, Back preserves drafts, and failure injection demonstrates accurate per-file recovery reports without false success messages.

## M5 Verify PUB compatibility

- Save representative edited copies using the application.
- Open those copies in the target PUB version and generate representative output.
- Include a value edit, access classification edit, description edit, and bulk change across more than one template. Verify template scripts, images, layout and unrelated output are retained. Check any expected generation changes caused by edited values.
- Record PUB version, fixtures, edits performed, results and any limitations in a compatibility report. If access is unavailable, state unverified and provide exact manual steps.

Done only after actual successful PUB checks. XML validity alone cannot close this milestone.

## M6 Finish interface and release packages

- Refine both themes, keyboard operation, panel resizing, diff readability, status messages and empty/error states.
- Run the complete requirement-linked verification matrix and regression checks.
- Produce ordinary-user Windows and Linux packages with bundled runtime/assets, version/license metadata and clear startup instructions.
- Test the packaged binaries independently of the development environment on the agreed OS baselines. Document actual package sizes and environment prerequisites.

### Current implementation record

- Removed unused Squirrel, Debian, and RPM makers plus the unused Squirrel startup package; upgraded Electron, the TypeScript checker, and formatter, and overrode SockJS to patched CommonJS-compatible UUID 11.1.1. `npm ci` no longer reports the supplied deprecated rimraf/glob/uuid versions.
- Added an About dialog launched from the existing footer version control. It shows the app name/version, GPL-3.0-only terms, bundled runtime components and license identifiers; the full GPL text is packaged as a resource. Keeping the control in its existing footer position adds discovery without changing workspace layout.
- Windows `npm run make` now signs the primary app executable in the `postPackage` hook before MakerZIP creates the archive. The local signing script uses the CurrentUser certificate store; no PFX/private key is committed.
- The current Windows ZIP is `desktop/out/make/zip/win32/x64/PUB Template Parameter Editor-win32-x64-0.1.0.zip` (157,903,130 bytes). Its packaged EXE hash matches the signed EXE in the package directory. The packaged app launched and rendered the workspace.
- The development build was signed with a local self-signed certificate; its PFX is Git-ignored and the private key was not committed. The PE signature was verified, but Windows does not trust the self-signed root. SignPath Foundation is not yet configured; the GitHub Actions integration is described below.
- `.github/workflows/windows-signpath.yml` uses standard GitHub Actions runners (free for this public repository): Ubuntu runs tests, typecheck, and lint; Windows builds and uploads the unsigned package. The hosted run for `d190621` passed both jobs; SignPath submission was skipped as configured because the project integration is not enabled yet. The workflow's unsigned-build mode was also tested locally; the EXE is `NotSigned`, and the ZIP contains no `.dta` members. Actual SignPath signing awaits the registered project, linked GitHub integration, API token, and repository variables.
- Linux target runtime, PUB compatibility, full workflow GUI tests, and the remaining release gates are still open.

Done when all requested features pass, PUB compatibility is verified, and both release packages run without admin/root or separately installed programming runtimes on the documented systems.

## Suggested working rhythm

Ask Codex to implement the next milestone, execute its checks and demonstrate the outcome. Review the milestone result before expanding scope. Use Git commits for coherent working changes. For a fresh session, point Codex at START_HERE.md, REQUIREMENTS.md, AGENTS.md and this plan and ask it to inspect the current repository state before continuing. Do not depend on conversational memory.

## Deferred features

Parameter creation/deletion/renaming, presets, recursive folders, automatic draft recovery across ordinary restarts, server connections, PUB execution from the application and packaging for additional architectures are not part of the requested initial scope. Add them only through explicit scope changes.
