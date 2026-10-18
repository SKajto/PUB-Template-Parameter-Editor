# Instructions for Codex

Build the desktop application described in REQUIREMENTS.md. Use ARCHITECTURE.md as the proposed technical approach and PLAN.md as the milestone record. User instructions override this baseline. Explain and record material design changes.

## Working rules

- Inspect actual sample bytes before implementing a format adapter. Available XML exports are not evidence of native DTA container structure.
- Implement the next milestone with its checks and a demonstrable result. Keep PLAN.md current. Do not declare an unexecuted platform or PUB compatibility check passed.
- Keep parameter names read-only. Internal/external maps to access, not the separate XML type property. Default values are strings. Bulk changes affect the value only.
- Read and stage operations must never write templates. Main Save opens review; only review Save writes files. Preserve drafts and track included/excluded fields precisely.
- Preserve unrelated source content. Use structurally identified source spans rather than regex-only editing or whole-template reserialization. Gate unsupported field shapes as read-only.
- Test meaningful file and workflow behavior: no-op preservation, selected-field patches, escaping, matching, bulk boundaries, Back, exclusions, conflicts and recovery.
- Keep original samples immutable and use temporary copies for editing tests. Never use production templates as write-test targets.
- Keep filesystem operations in the main process behind a narrow typed preload bridge. Keep renderer isolation and sandboxing enabled; do not expose arbitrary filesystem or shell calls to the renderer.
- Treat template descriptions and values as text. Never evaluate template scripts or render parameter descriptions as untrusted HTML.
- Require no backend service, authentication or ordinary-use network access. Bundle local assets.
- Package for ordinary users. Do not recommend admin/root installation or disable the Electron sandbox as a routine workaround.
- Explain remaining uncertainty plainly and continue work that does not depend on missing evidence. Ask focused questions only where a decision or required sample cannot be inferred.

## Commands

Run desktop application commands from desktop/:

- npm ci installs the locked dependencies.
- npm start launches the development GUI. Workflow checks are manual; there is no automated Electron end-to-end suite yet.
- npm test, npm run typecheck, and npm run lint run the tests and static checks.
- npm run make creates the Windows x64 ZIP on Windows; signing requires a suitable certificate in the current user’s certificate store.
- On Ubuntu x64, run npm ci then npm run make -- --platform=linux --arch=x64. The build host needs the zip utility. WSL Ubuntu 26.04.1 has been tested; Ubuntu 24.04 remains unverified.

From the repository root, python -m unittest discover -s tools/tests -v runs the sample-inspector tests. Do not commit or upload proprietary .dta templates.

## Completion report

For each milestone report what works, how it was tested, the runnable command or artifact, and unresolved limits. Summarize routine checks. Commit coherent changes when Git is available and authorized. Do not publish, deploy or send messages unless the user requests it.
