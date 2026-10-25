# PUB Template Parameter Editor fresh project starter

Revision 4, 25 October 2026. This is a complete instruction package for creating a new application from an empty project directory. It contains specifications and configuration examples, not application source or a compiled executable. No prior implementation or completed milestone is assumed.

## Project target

- C# with WPF and .NET 10 LTS, Windows 11 x64 first.
- Public GitHub maintenance under GPLv3 and GitHub Actions for validation and packaging.
- Primary distribution through Microsoft Store as MSIX with the runtime bundled.
- Separate one-file self-contained EXE profile for development or optional portable releases.
- Modern UI, persisted light/dark mode, local template editing without application authentication or template uploads.

Store packaging includes manifests, visual assets and notices. Users install one Store application without manually assembling modules or installing .NET. The portable profile bundles the runtime into one EXE and may extract native components to user temporary storage. Settings, backups and journals are ordinary user data.

## Start a new project

1. Create an empty local folder, for example PubTemplateEditor, and extract the contents of this package's top-level folder into it so AGENTS.md and REQUIREMENTS.md are at the project root.
2. Open that directory in Codex and provide the text in CODEX_START_PROMPT.md. Codex should create the solution/projects and initialize Git if needed.
3. Supply representative native PUB .dta files in the ignored samples/ directory and the target PUB version when available. The package contains XML reference findings and one synthetic structural example, not a full native DTA fixture set.
4. Follow PLAN.md beginning with S0. Add the public GitHub remote and Partner Center identity when those real values are available; missing account details do not prevent core development or draft packaging.
5. Verify edited files in PUB, run Windows GUI tests and complete actual Store-installed acceptance before declaring release readiness. External publishing is a separately authorized release action.

## Read these files

| File | Purpose |
| --- | --- |
| AGENTS.md | Persistent instructions for the new coding project |
| CODEX_START_PROMPT.md | Initial task to give Codex |
| REQUIREMENTS.md | Application, Windows, CI, Store and GPL requirements |
| ARCHITECTURE.md | WPF project structure and runtime/file services |
| PLAN.md | Fresh implementation milestones S0 through S8 |
| docs/PROJECT_SETUP.md | Empty-directory setup and required external inputs |
| docs/TEST_MATRIX.md | Acceptance and regression scenarios |
| docs/PRESERVATION_NOTES.md | File preservation, drafts, review and recovery model |
| docs/SAMPLE_FINDINGS.md and docs/SAMPLE_ANALYSIS.json | Dated XML format reference observations |
| docs/GITHUB_STORE_RELEASE.md | CI, package identity/versioning and Store release design |
| docs/GPL_RELEASE.md | License, corresponding source and notices |
| docs/WINDOWS_PUBLISH.md | Optional single-EXE publishing configuration |
| examples/ | Configuration references and synthetic XML illustration |

## Inputs to establish

The actual public repository URL, copyright holder, target PUB version, representative native DTA files and Partner Center identity/product IDs are not supplied by this package. Record these inputs as they become available and never invent them. GPL-3.0-only is the provisional license identifier; -or-later is a separate owner choice. Store price also remains an owner decision.

Microsoft Store signs MSIX after certification. A pre-Store CI artifact or optional GitHub EXE does not automatically have that signature. Keep actual Store acquisition/execution, account setup and device-policy checks separate from build success. Recheck current release-tool prerequisites when implementing the workflows.
