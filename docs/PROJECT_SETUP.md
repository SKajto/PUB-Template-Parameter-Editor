# Setting up this repository

For anyone building or continuing this project - human or AI agent. The original kickoff briefs are
[START_HERE.md](../START_HERE.md), [AGENTS.md](../AGENTS.md) and
[CODEX_START_PROMPT.md](../CODEX_START_PROMPT.md); this file is only the current setup.

## What you need

Windows 11 x64, the .NET 10 SDK pinned by `global.json`, Git, and a WPF-capable MSBuild toolchain
(the .NET SDK is enough to build and test; Visual Studio is convenience only).

```powershell
dotnet restore PubTemplateEditor.slnx
dotnet build PubTemplateEditor.slnx -c Release
dotnet test  PubTemplateEditor.slnx -c Release          # three projects, no network needed

# the distributed artifact
dotnet publish src/PubTemplateEditor.App/PubTemplateEditor.App.csproj `
  -c Release -p:PublishProfile=PortableWin_x64 -o artifacts/publish/portable-win-x64
```

## Layout

| Path | Responsibility |
| --- | --- |
| `src/PubTemplateEditor.Core` | declarations, comparison, drafts, review model, source patching - no WPF, no file IO |
| `src/PubTemplateEditor.Infrastructure` | container access, folder scan, settings, hashing, save/backup/recovery |
| `src/PubTemplateEditor.App` | WPF shell, view models, themes, packaging of notices |
| `tests/` | Core unit tests, integration tests on temporary copies, WPF shell tests on an STA host |
| `packaging/` | MSIX manifest, tile assets, packaging script (local tooling, not a distribution channel) |
| `.github/workflows/` | CI validation, release packaging, GitHub Release publishing |
| `tools/brand/` | draws the application icon and tile artwork |
| `docs/` | design, format, release and process notes |

## Rules the repository enforces

- No template files (`examples/*.dta`) and no build output may be committed; CI and
  `SamplePolicyTests` check this. Tests write synthetic containers when the real files are absent.
- Run the test suite once with `examples/` moved away before pushing: that is what a fresh clone and
  CI see.
- Every GitHub Action is pinned to a verified commit SHA; see
  [RELEASE_PACKAGING.md](RELEASE_PACKAGING.md) for the current pins.
- Third-party packages in the shipped application: none. Justification and notices are required if a
  requirement genuinely needs one.

## Inputs the owner supplies (code and tests do not depend on them)

Native PUB templates for evidence testing (kept local), and the version tag plus
`PUBLISH_GITHUB_RELEASE=true` for a release. The released executable is **not code-signed** -
[RELEASE_PACKAGING.md](RELEASE_PACKAGING.md) explains why. Track the state of all of this in
[PLAN.md](../PLAN.md).
