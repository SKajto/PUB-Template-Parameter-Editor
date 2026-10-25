# Initial task for Agent

Create DTA Parameter Editor as a  Windows desktop application. Use C# with WPF and .NET 10 LTS, maintain the project on GitHub under GPLv3, use GitHub Actions for build/test/package, and distribute primarily as Microsoft Store MSIX with a self-contained runtime. Keep a separate self-contained one-file DTAParamEditor.exe publish profile for development or optional portable releases. Windows 11 x64 is the initial target; Linux support is optional.

Read AGENTS.md, REQUIREMENTS.md, ARCHITECTURE.md, PLAN.md, docs/PROJECT_SETUP.md, docs/GITHUB_STORE_RELEASE.md and docs/GPL_RELEASE.md. Create a new solution and the App/Core/Infrastructure/test projects in the task directory. Initialize Git if needed and keep unrelated user files intact. No application source, established CI, Store listing or completed tests are assumed to exist.

Begin S0: scaffold the projects, create a minimal WPF shell with embedded light/dark resources, record the supported .NET 10 SDK and build commands, and establish portable publishing plus a draft MSIX packaging path. Set up basic GitHub validation configuration early. Inspect real sample bytes in S1 before implementing DTA-specific edits. Use docs/SAMPLE_FINDINGS.md as limited XML reference evidence only.

Implement the features in REQUIREMENTS.md: remembered folder and theme, template explorer, explicit parameter reading, text/access filtering, value/access/description editing, comparison, value-only bulk staging, dirty indicators and selectable field-level save review. Names remain read-only; internal/external means access. Back preserves drafts; review Save is the only template-writing action. Preserve unrelated source content and implement conflict detection, backups and recovery.

Build meaningful format and workflow tests using redistributable fixtures and temporary copies. Add modern WPF styling and keyboard operation with built-in controls. Keep the Core independent of WPF and do not introduce an embedded browser or separately installed runtime.

Create Windows GitHub Actions workflows for PR validation, trusted release packaging and protected Store submission. Use real Partner Center values when supplied; reject placeholders in a submission build. Store signs MSIX after certification; do not require a purchased CA certificate for that route or describe raw CI packages/portable EXEs as Store-signed. Prepare the first Store listing/submission and check current update automation prerequisites.

Use GPL-3.0-only provisionally unless the owner explicitly selects GPL-3.0-or-later. Add accurate licensing/notices and exact-version source/build/package archives without maintainer secrets. Embed offline licenses and source links in About and include source directions in Store metadata.

Follow PLAN.md from S0 onward, demonstrating each meaningful increment and updating its progress record. Missing sample files, Windows access or account credentials should leave the affected gate open while independent work continues. Do not create external accounts, accept agreements or trigger live public/Store releases from this initial implementation task. Report actual checks and limitations honestly.
