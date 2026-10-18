# PUB Template Parameter Editor development package

This package translates the requested application into requirements and an executable development plan for Codex. It contains planning documents, not an implemented application or an installed Codex skill. The architecture is a recommendation. Interpretations introduced to resolve ambiguity are explicitly listed in REQUIREMENTS.md.

## How to proceed

1. Extract this package into a new project directory on your development computer and initialize a Git repository there. Keep these files at the repository root.
2. Put copies of representative native PUB `.dta` files into a local `samples/` directory. Include different templates with overlapping variable names. Keep authoritative templates elsewhere. Samples are ignored by the supplied `.gitignore`; do not publish them accidentally.
3. Open the project directory in your chosen Codex development environment. Supply the task in CODEX_START_PROMPT.md. The first milestone establishes native DTA format and packaging feasibility before the editor is built.
4. After milestone M0, review its compatibility findings and any unresolved format limitations. Continue with M1 and then the later milestones in PLAN.md. Each milestone must leave a demonstrable result and update the plan.
5. At the PUB compatibility milestone, open edited copies in your installed PUB version and generate a representative document. Codex cannot claim this verification unless it actually has access to that environment and records the result.
6. Test the packaged application on both target operating systems using a standard user account without development runtimes installed. Packaging a binary and successfully running it are separate checks.

The application should run without a separately installed programming runtime. Its development environment will require build dependencies. Supported Linux distributions and versions must be established during M0; the project cannot promise support for every Linux installation.

## Package contents

| File | Purpose |
| --- | --- |
| REQUIREMENTS.md | User requirements, detailed behavior, acceptance criteria, and explicit interpretations |
| ARCHITECTURE.md | Recommended technology and the file preservation strategy |
| PLAN.md | Ordered development milestones with completion checks |
| AGENTS.md | Persistent instructions for Codex working in this repository |
| CODEX_START_PROMPT.md | Initial development task to give Codex |
| docs/TEST_MATRIX.md | Meaningful verification scenarios |
| docs/SAMPLE_FINDINGS.md | Actual observations from the three available XML samples |
| docs/SAMPLE_ANALYSIS.json | Machine-readable sample counts and structure findings |
| examples/minimal-variable-example.xml | Synthetic XML illustration, not a complete valid PUB template |

## Recommended application

Use an Electron desktop application with React and TypeScript. Electron bundles Chromium and Node.js, supports Windows and Linux, and provides desktop file access. Keep the editor local and offline. Distribute separate Windows and Linux packages containing the runtime. The tradeoff is a larger download and memory footprint than a small native utility.

Use a Windows portable archive first and a Linux extracted archive first. Verify runtime libraries and sandbox behavior on the supported Linux baseline early. Do not rely solely on AppImage or instruct users to disable the Chromium sandbox to make the application run. A per-user installer may be added after portable builds pass.

## Items that still need real environment evidence

- Native `.dta` sample bytes and their container structure. Existing XML samples establish variable mappings, but do not establish the native DTA packaging format.
- PUB version used for compatibility testing.
- Target Linux distribution and version. Provisional validation targets are Windows 11 x64 and Ubuntu 24.04 LTS x64; these are proposed baselines, not confirmed user requirements.

## Sources checked

- Electron introduction: https://www.electronjs.org/docs/latest/
- Electron application packaging: https://www.electronjs.org/docs/latest/tutorial/application-distribution
- Electron process and renderer guidance: https://www.electronjs.org/docs/latest/tutorial/security
- OpenAI execution plans: https://developers.openai.com/cookbook/articles/codex_exec_plans

These sources support framework and development workflow statements. PUB variable mappings in this package come from the actual XML samples, not from an assumed universal schema. Dependency versions must be verified and locked during implementation.
