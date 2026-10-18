# PUB Template Parameter Editor

This is an offline Electron editor for PUB `.dta` templates. It reads the observed native ZIP variant, stages parameter edits, compares loaded templates, supports explicit value-only bulk staging, and writes only changes included in Review Save. Startup recovery for interrupted saves is implemented but still needs live workflow verification. PUB compatibility, complete Review Save GUI testing, and runtime validation on the planned Ubuntu 24.04 baseline remain open.

## Development environment

The scaffold was created with Electron Forge's Webpack + TypeScript template. The Vite plugin was avoided because current Forge documentation marks it experimental.

Versions used for this build:

- Node.js 24.21.0
- npm 11.19.0
- Electron 44.7.2
- Electron Forge 8.0.1
- React 19.3.1
- TypeScript 6.0.3

The repository includes `package-lock.json`. The successful scaffold command was:

`npx --yes create-electron-app@latest desktop --template=webpack --typescript`

React was added with `npm install --save react react-dom` and `npm install --save-dev @types/react @types/react-dom`.

Run commands from this directory:

| Command                                       | Status                                                                                                                        |
| --------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------- |
| `npm ci`                                      | Verified from the lockfile on Windows and WSL Ubuntu 26.04.1                                                                   |
| `npm start`                                   | Runs the development GUI; resizable panels, Compare selection, and bulk no-op Apply were visually checked                     |
| `npm run typecheck`                           | Verified; passed                                                                                                              |
| `npm run lint`                                | Verified; passed                                                                                                              |
| `npm test`                                    | Verified; 39 tests pass, including source-span, ZIP writer, crash-recovery, native reader, search, compare, bulk, and mode tests |
| `npm run make`                                | Verified on Windows x64; creates a ZIP containing the Authenticode-signed app executable (self-signed, not publicly trusted)  |
| `npm run make -- --platform=linux --arch=x64` | Native WSL Ubuntu 26.04.1 x64 build; extracted app executable has mode 755 and user verified WSLg startup. Ubuntu 24.04 remains unverified |

The rebuilt Linux ZIP is `out/linux-delivery/PUB Template Parameter Editor-linux-x64-0.1.0.zip` (123,087,499 bytes; SHA-256 `b713ea362b302beec149232e8599efbbcf2a1203a3a9e0f14dacd08641b65c6e`). Saving to `/mnt/c` initially failed because DrvFS rejected an unnecessary chmod; the save service now compares effective modes before attempting chmod. All 39 unit tests pass, and a synthetic `saveBatch` smoke test on DrvFS verified a temporary text file. Re-test the complete Review Save GUI flow with this rebuilt package. Proprietary `.dta` templates are git-ignored and must not be uploaded.

Four standard-library unit tests cover the sample inspector. From the project root, run `python -m unittest discover -s tools/tests -v`; inspect the supplied samples with `python tools/inspect_dta.py examples/*.dta`. The application now stages edits, visibly marks changed parameters as Unsaved, compares exact-name declarations across loaded templates, and offers value-only bulk staging with explicit eligible target checkboxes. Review Save handles included/excluded fields and is the only file-writing action. Dirty close, folder change, and re-read now offer explicit Apply, Save for review, Discard, and Cancel choices; returning from review does not automatically continue the interrupted navigation. Parameter-name search is case-sensitive and anchored at the beginning; `*` matches any-length text after that prefix, including when the matched text is followed by more name characters. Description search is case-insensitive and matches anywhere unless the query occurs only inside the parameter name after a leading prefix. Save confirmations can be dismissed and auto-close. The main-process save path uses the source-span patcher, a fail-closed native ZIP writer, fingerprints, backups, per-file replacement, verification, a journal, and in-process rollback. Tests write only temporary copies; the original sample archives remain unchanged. Startup recovery now detects unresolved journals and offers an explicit restore; it restores only when the current file matches the recorded prepared output, verifies the backup, and leaves unknown external changes untouched. Recovery and path-boundary behavior have unit tests; startup recovery was also exercised in the packaged Windows app with isolated app data and a temporary sample copy. The close/folder/re-read prompts and full Review Save still need live end-to-end testing. The current Windows ZIP was rebuilt with the signing hook; its main executable has an Authenticode signature, but the self-signed certificate remains untrusted by Windows. The Linux package launched through WSLg on Ubuntu 26.04.1; the planned Ubuntu 24.04 baseline and full save workflow still need verification.

## Windows Authenticode signing

`npm run make` runs the Windows signing hook after packaging and before creating the ZIP. The hook signs the main application executable with a SHA-256 Authenticode signature from a code-signing certificate in the current user's Personal certificate store. `PUB_EDITOR_SIGNING_THUMBPRINT` or `PUB_EDITOR_SIGNING_SUBJECT` can select an installed certificate. The hook fails if no suitable certificate is installed. No PFX or private key is stored in the repository, and the hook does not add the certificate to a trusted root store.

The current build was signed with a local self-signed development certificate. Its PFX under `.cert/` is Git-ignored; no private key is committed. The executable signature was verified, and the ZIP contains the identical signed executable. Windows does not trust this self-signed certificate by default, so it does not establish a trusted publisher; depending on how the ZIP is obtained, SmartScreen may warn or block it. Self-signed certificates are for development/internal trust only; public distribution requires a certificate chaining to a trusted code-signing CA. SignPath Foundation setup is planned after repository registration. See [Microsoft's Windows code-signing guidance](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options).

Current artifacts:

- Signed executable: `out/PUB Template Parameter Editor-win32-x64/PUB Template Parameter Editor.exe`
- ZIP containing that signed executable: `out/make/zip/win32/x64/PUB Template Parameter Editor-win32-x64-0.1.0.zip`

The ZIP container itself is not Authenticode-signed; the application executable inside it is.

## Current safety and limits

- Electron renderer sandboxing is enabled; context isolation is enabled and Node integration is disabled.
- Filesystem access remains in the main process behind a narrow typed preload API. Renderer edits are drafts; only Review Save reaches the main-process file writer. The native ZIP writer accepts the exact observed sample structure and fails closed on unsupported ZIP variants.
- Production dependencies currently report 0 npm audit vulnerabilities. The full development dependency tree reports 11 findings (2 moderate, 7 high, 2 critical); dependency cleanup is unresolved.
- The project root has a local Git repository but no remote. Do not push it to the unrelated GetDocs remote configured in the user's parent directory.
- See the root `PLAN.md` for milestone status and `docs/NATIVE_DTA_FINDINGS.md` for actual native DTA sample evidence and ZIP preservation limits.
