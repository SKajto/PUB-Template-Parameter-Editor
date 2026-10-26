# PUB Template Parameter Editor

A Windows desktop editor for the variable declarations of PUB template files
(`.dta`). It is an offline tool: no service, no web view, no telemetry. Edits are
staged in memory and reach the disk only through an explicit review step.

**Status:** development milestones S0–S5 are implemented and covered by 213 automated
tests (197 of them run without the local template samples, which are not shipped).
The Windows CI pipeline is green on GitHub: build, all three test projects, the
licensing and content checks, a portable single-file executable and a draft MSIX.
Native PUB round-trip compatibility, a real Microsoft Store identity and a signed
downloadable executable are open gates — see [PLAN.md](PLAN.md).

## What it does

- Lists the templates of one folder and reads their declarations without writing anything.
- Shows parameters in a table: name (read-only), internal/external access, description, default value.
- Stages value and access changes; Compare and bulk value application work over the same stage.
- **Review Save** is the only write path: it lists every pending change per file, shows the
  change in place inside the full value, lets you select the rows to apply, and then patches the
  exact source spans of the XML — unrelated bytes and other container entries stay untouched.
- Keeps a timestamped backup and a recovery journal beside the user's settings; detects outside
  edits by content fingerprint before writing.
- Light and dark theme, resizable columns, remembered folders, drag-and-drop of a folder or a
  `.dta`, keyboard shortcuts.

## Layout

| Path | Contents |
| --- | --- |
| `src/PubTemplateEditor.Core` | domain model, template XML parsing, declaration reading, patch planning — no WPF, no file IO |
| `src/PubTemplateEditor.Infrastructure` | `.dta` container access, folder scan, read/write services, backups, settings |
| `src/PubTemplateEditor.App` | WPF shell, view models, themes, window frame tinting, embedded license texts |
| `tests/` | unit tests, integration tests (temp copies), WPF shell tests on an STA host |
| `packaging/` | MSIX manifest, tile assets, `build-msix.ps1` |
| `tools/brand/` | draws the application icon and tile artwork (see `NOTICE`) |
| `docs/` | setup, release and format findings |
| `.github/workflows/` | CI validation, release packaging, Store submission (all pinned by commit SHA) |

## Build and test

Requires the .NET 10 SDK (pinned by `global.json`) on Windows.

```powershell
dotnet restore PubTemplateEditor.slnx
dotnet build PubTemplateEditor.slnx -c Release
dotnet test  PubTemplateEditor.slnx -c Release

# portable single-file executable
dotnet publish src/PubTemplateEditor.App/PubTemplateEditor.App.csproj `
  -c Release -p:PublishProfile=PortableWin_x64 -o artifacts/publish/portable-win-x64

# draft MSIX with placeholder identity
powershell -File packaging/build-msix.ps1 -Version 0.1.0 -Mode Draft
```

The downloadable program is `PUB_Template_Parameter_Editor.exe`; the Store package is
named after the same product. Underscores instead of spaces keep the name usable on
the command line, in scripts and inside package paths.

The test suite prefers real PUB templates when the machine has them, and writes
synthetic containers of the same shape when it does not — see
`tests/TestSampleSupport.cs`. Before pushing, run the suite once with `examples/`
renamed away: that is what a fresh clone and CI see.

## Code signing policy

The downloadable executable is signed with a free certificate provided by
[SignPath.io](https://signpath.io), certificate by
[SignPath Foundation](https://signpath.org). The MSIX for the Microsoft Store
is signed by Microsoft after certification instead.

- **Privacy:** this program will not transfer any information to other networked
  systems unless specifically requested by the user or the person installing or
  operating it. It is offline: no account, no update service, no telemetry.
- **System changes:** it installs nothing, needs no elevation, and changes template
  files only through Review Save, which shows every pending change, keeps a backup
  of each file it replaces and can be cancelled per file.
- **Uninstall:** the Store package through Windows Settings → Apps; the portable
  executable by deleting its folder, with `%LOCALAPPDATA%\PubTemplateEditor`
  holding its settings, backups and recovery journal.
- **Team roles** (Author / Reviewer / Approver), the signing configuration and the
  build provenance are described in
  [docs/CODE_SIGNING_POLICY.md](docs/CODE_SIGNING_POLICY.md).

## Licensing

Copyright (C) PUB Template Parameter Editor contributors. Licensed under
**GPL-3.0-only** ([LICENSE](LICENSE)); see [NOTICE](NOTICE) for the notice that
accompanies the binaries, including the artwork provenance and the status of the
bundled .NET runtime, which is not relicensed.

## Documentation

- [CONTRIBUTING.md](CONTRIBUTING.md), [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md), [SECURITY.md](SECURITY.md)
- [REQUIREMENTS.md](REQUIREMENTS.md), [ARCHITECTURE.md](ARCHITECTURE.md), [PLAN.md](PLAN.md)
- [docs/PROJECT_SETUP.md](docs/PROJECT_SETUP.md) — machine and solution setup
- [docs/GITHUB_STORE_RELEASE.md](docs/GITHUB_STORE_RELEASE.md) — CI, MSIX, Microsoft Store, signing
- [docs/CODE_SIGNING_POLICY.md](docs/CODE_SIGNING_POLICY.md) — who approves signing, what is signed, privacy
- [docs/GPL_RELEASE.md](docs/GPL_RELEASE.md) — source-availability obligations
- [docs/SAMPLE_FINDINGS.md](docs/SAMPLE_FINDINGS.md) — what the native templates look like
