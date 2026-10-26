# PUB Template Parameter Editor

A Windows desktop editor for the variable declarations of PUB template files
(`.dta`). It is an offline tool: no service, no web view, no telemetry. Edits are
staged in memory and reach the disk only through an explicit review step.

**Status:** development milestones S0–S5 are implemented and covered by 227 automated
tests (211 of them run without the local template samples, which are not shipped).
The Windows CI pipeline is green on GitHub: build, all three test projects, the
licensing and content checks, and the portable single-file executable.
Distribution is a GitHub Release, signed through SignPath.io; the signing
subscription and a native PUB round trip are open gates — see [PLAN.md](PLAN.md).

## What it does

- Lists the templates of one folder and reads their declarations without writing anything.
- Shows parameters in a table: name (read-only), internal/external access, description, default value.
- Stages value, access and description changes; Compare and bulk field application (each of the
  three fields on its own, matched by exact parameter name) work over the same stage.
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
| `.github/workflows/` | CI validation, release packaging, signing and release publishing (all pinned by commit SHA) |

## Build and test

Requires the .NET 10 SDK (pinned by `global.json`) on Windows.

```powershell
dotnet restore PubTemplateEditor.slnx
dotnet build PubTemplateEditor.slnx -c Release
dotnet test  PubTemplateEditor.slnx -c Release

# portable single-file executable
dotnet publish src/PubTemplateEditor.App/PubTemplateEditor.App.csproj `
  -c Release -p:PublishProfile=PortableWin_x64 -o artifacts/publish/portable-win-x64

# optional: package the app locally (not a distribution channel)
powershell -File packaging/build-msix.ps1 -Version 0.1.0 -Mode Draft
```

The shipped artifact is `PUB_Template_Parameter_Editor.exe` - a single self-contained
file, attached to a GitHub Release. Packaging to MSIX is kept as local tooling for
running the app as a packaged app; it is not published, and no identity values exist
for it.

The program is called `PUB_Template_Parameter_Editor.exe` - the executable, the
assembly and (for a locally built package) the manifest entry all carry the product
name. Underscores instead of spaces keep the name usable on the command line, in
scripts and inside package paths.

The test suite prefers real PUB templates when the machine has them, and writes
synthetic containers of the same shape when it does not — see
`tests/TestSampleSupport.cs`. Before pushing, run the suite once with `examples/`
renamed away: that is what a fresh clone and CI see.

## Code signing policy

Everything this project publishes is signed the same way: a free certificate
provided by [SignPath.io](https://signpath.io), certificate by
[SignPath Foundation](https://signpath.org). That covers the downloadable
executable, and a package would be signed through the same job if a packaged
channel were ever used. No other signer is used, and nothing here is
Store-signed.

- **Privacy:** this program will not transfer any information to other networked
  systems unless specifically requested by the user or the person installing or
  operating it. It is offline: no account, no update service, no telemetry.
- **System changes:** it installs nothing, needs no elevation, and changes template
  files only through Review Save, which shows every pending change, keeps a backup
  of each file it replaces and can be cancelled per file.
- **Uninstall:** delete the program's folder - there is no installer, no service and no
  startup entry. Its user data lives in `%LOCALAPPDATA%\PubTemplateEditor`
  (settings, backups and the recovery journal) and can be removed separately.
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
- [docs/RELEASE_PACKAGING.md](docs/RELEASE_PACKAGING.md) — CI, release packaging, signing, GitHub Releases
- [docs/CODE_SIGNING_POLICY.md](docs/CODE_SIGNING_POLICY.md) — who approves signing, what is signed, privacy
- [docs/GPL_RELEASE.md](docs/GPL_RELEASE.md) — source-availability obligations
- [docs/SAMPLE_FINDINGS.md](docs/SAMPLE_FINDINGS.md) — what the native templates look like
