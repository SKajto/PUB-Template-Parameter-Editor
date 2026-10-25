# Instructions for building PUB Template Parameter Editor

Create a new Windows desktop application in C# with WPF and .NET 10 LTS. Maintain the open-source source code on GitHub under GPLv3, use GitHub Actions for build/test/package, and distribute primarily through Microsoft Store as MSIX. This is a fresh project with no application implementation or completed milestones assumed.

## Start here

Read REQUIREMENTS.md, ARCHITECTURE.md, PLAN.md, docs/PROJECT_SETUP.md, docs/GITHUB_STORE_RELEASE.md and docs/GPL_RELEASE.md. Create the solution and projects described there in the task directory. Initialize Git when the directory is not already a repository. Keep unrelated user files intact. Begin with S0 in PLAN.md and record actual progress as implementation proceeds.

Use the supplied specifications, reference XML findings and any provided sample files as inputs. Inspect actual template bytes before implementing a format adapter. Reference findings are not application source and do not prove native DTA compatibility. If samples, Windows execution or Partner Center details are missing, proceed with independent work and state the exact open gate. Do not invent sample formats, credentials, IDs or test results.

## Technical constraints

- Production stack is C#/WPF on .NET 10 LTS. Prefer built-in .NET/WPF facilities and zero third-party production packages. Test-only tools are allowed. Record and justify a dependency if a demonstrated requirement cannot reasonably use built-in APIs.
- Create App, Core and Infrastructure projects with lightweight view models, commands and explicit services. Keep Core independent of WPF. Keep parsing/saving logic out of window code-behind.
- Windows 11 x64 is the initial target. Linux UI support is deferred. Portable-core tests on another OS do not prove Windows GUI compatibility.
- Primary release is Store MSIX containing a self-contained WPF payload and required manifest, assets and notices. Keep a separate self-contained single-file PubTemplateEditor.exe profile for development or optional portable distribution.
- For the portable profile use PublishSingleFile=true, SelfContained=true, IncludeNativeLibrariesForSelfExtract=true, PublishTrimmed=false, PublishAot=false and embedded resources/symbols. Document normal user-scoped native extraction rather than claiming zero runtime files.
- Use an asInvoker manifest and user-scoped settings/journals. Do not require an application service, web server, embedded browser, installed .NET runtime or elevated app setup. Store installation and execution remain subject to platform and device policies.
- Use responsive async commands, cancellation/progress where appropriate and Dispatcher updates for WPF-bound state. Prevent duplicate save execution.

## Parameter and save behavior

- Parameter names are read-only. Internal/external maps to access, not the separate XML type property. Default values are strings; do not trim or coerce them. Bulk editing changes default values only.
- Loading, comparing, editing and bulk staging must not write template files. Main Save opens review; only review Save writes selected field changes.
- Back from review preserves drafts. Deselected changes revert only after a completely successful save. Preserve and reconcile unresolved drafts on errors.
- Preserve unrelated source bytes and supported container content. Pair structural XML validation with exact lexical source spans; do not use regex-only editing or whole-template serialization.
- Use XmlReader with DtdProcessing.Prohibit and XmlResolver=null. Treat parameter descriptions/values as text, never executable code or XAML. Keep unsupported constructs read-only with an explanation.
- Preflight all outputs, detect external file changes with content fingerprints, keep backups and a recovery journal, and verify per-file replacement. Do not describe a multi-file batch as globally atomic.
- Build meaningful tests around immutable fixtures and temporary test copies: declaration fidelity, exact value preservation, matching, bulk boundaries, field-level review, Back, conflicts and recovery. Never test writes against authoritative templates.

## Repository licensing and release

- Apply GPLv3 consistently. The provisional SPDX choice is GPL-3.0-only; record it and change to -or-later only if the owner explicitly selects that grant. Use real copyright holder details.
- Add LICENSE, accurate source notices and bundled offline GPL/third-party notices. Link About and Store metadata to exact-version corresponding source. Include source, UI resources and build/package scripts needed to rebuild without maintainer secrets.
- Create separate PR validation, trusted release/package and protected Store-submission workflows. Pin verified Actions dependencies, minimize token permissions and keep Store credentials unavailable to untrusted PR code.
- Use real Partner Center identity/publisher/product values and fail submission builds on unresolved placeholders. Missing values do not block the application, tests or draft package configuration.
- Store signs MSIX after certification; no publisher CA certificate is required for that route. Do not label a CI MSIX or separate GitHub EXE as Store-signed. Verify actual Store-installed execution and lifecycle under the target policy.
- Document first Partner Center setup/submission and current update-tool prerequisites. Do not infer Store price from GPL. Store manages package updates; rebuild self-contained payloads for runtime patches.
- Preserve GPL rights and document concrete release-term issues. Do not add restrictive app EULAs, license gating or Store-only execution checks.
- Prepare workflow files, build artifacts and submission materials. Creating external accounts, accepting agreements and live public/Store publishing require a task that authorizes those actions; they are not part of this project-instruction package.

## Execution and reporting

Implement the plan in verifiable increments and continue through independent tasks without asking for routine implementation confirmation. Use source control for coherent checkpoints. Update PLAN.md after each milestone with executed commands, results, decisions and remaining gates. Record the actual SDK/tool versions and real restore/build/test/publish commands as soon as the scaffold exists.

Run checks appropriate to each change. Distinguish build success, Windows GUI execution, native PUB compatibility, CI package generation, Store certification and actual Store installation. Mark unavailable checks unverified and explain the concrete missing input. Never claim a passed test because the code or configuration was written.
