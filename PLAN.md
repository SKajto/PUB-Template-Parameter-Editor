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

Done when reading/filter combinations and persistence work in the actual Windows GUI, counts match verified samples and original file hashes remain unchanged.

## S3 Add edits comparison and bulk staging

- Enable default value, access classification and supported description edits with explicit Apply edits. Keep names read-only and default strings exact.
- Add dirty indicators, no-op normalization and safe navigation/close/re-read handling for unapplied and staged edits.
- Compare the selected declaration across selected/all loaded templates, with exact-name matching, field/line differences, distinct missing/empty states and visible draft values.
- Add eligible bulk-target selection and explicit value-only Apply changes. Preserve target access/description and explain ineligible ambiguous declarations.

Done when only selected field/target proposals change in memory, filtering/navigation retains drafts and all original template files remain untouched.

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

## Deferred scope

Linux UI, additional architectures, parameter creation/deletion/renaming, presets, recursive folders, in-app PUB generation, browser hosting and custom installer/updater services are outside this initial Windows release. Keep the domain core independent without building a second UI.
