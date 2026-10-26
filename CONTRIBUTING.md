# Contributing

Thanks for being here. This is a small desktop application with strong constraints, and
most of the work in a pull request is showing that a change respects them.

By contributing you confirm that the contribution is yours to submit and that you
license it under **GPL-3.0-only**, the licence of this repository.

## What the project is

An offline Windows desktop editor for the variable declarations of PUB template
(`.dta`) files: C#, WPF, .NET 10, Windows x64. Nothing runs as a service, nothing talks
to a network, nothing needs elevation. Edits are staged in memory and reach the disk
only through the explicit **Review Save** step.

## Setting up

```powershell
dotnet restore PubTemplateEditor.slnx
dotnet build PubTemplateEditor.slnx -c Release
dotnet test  PubTemplateEditor.slnx -c Release
```

`global.json` pins the SDK; a newer or older one is a deliberate change and should be
discussed in the pull request. Windows is required for the WPF test project.

## Ground rules that come from the design

- **Zero third-party production packages.** Use what .NET and WPF already provide. If a
  requirement genuinely cannot be met that way, record the dependency, why it is needed
  and what it costs in [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt) and in the pull
  request. Test-only tooling is allowed and is not shipped.
- **Project boundaries.** `Core` has no WPF and no file IO; `Infrastructure` owns containers,
  files, backups and settings; the App project holds views and view models. Parsing, patching
  and saving do not live in window code-behind.
- **Never write to a template outside the review step.** Loading, comparing, editing and bulk
  staging must not touch disk. Writes go through the save service, which keeps a backup and a
  recovery journal, verifies per file, and preserves unrelated bytes: the patcher replaces exact
  source spans, it does not re-serialise the document.
- **XML is data, never code.** The reader prohibits DTDs and resolves nothing externally. Values
  and descriptions are treated as text everywhere they are shown.
- **No template content in the repository.** `examples/*.dta` is user content and is
  git-ignored; CI fails if a `.dta` is tracked. Tests use `tests/TestSampleSupport.cs`, which
  falls back to synthetic containers, and the evidence theories simply have no native rows in a
  fresh clone.
- **Unsupported constructs stay read-only** with an explanation rather than being normalised.

## Testing

- Run the whole suite. Then run it once with `examples/` renamed away: that is what a fresh
  clone and CI see, and it is the difference between a suite that looks green and one that is.
- Add a test with every behaviour change, using immutable fixtures and temporary copies. Never
  point a write test at a real template file.
- For UI changes, the WPF tests run on an STA host; theme changes are checked by the contrast
  audit (`ContrastAuditTests`) which must report no offenders in either theme.
- Two things WPF only fails at runtime, so a test is expected for each: a `StaticResource`
  referenced before it is defined inside the same dictionary, and a pack URI whose assembly part
  is wrong (it names the *assembly*, which is `PUB_Template_Parameter_Editor`, not the root
  namespace).

## Pull requests

Fill in the template. In short: describe the behaviour change, name the tests that prove it,
state what you could not verify (native PUB round trip, Store certification, a real desktop
session), and do not add build outputs, binaries or template files.

Build scripts, `global.json`, `Directory.Build.props` and `.github/workflows/*` are reviewed as
carefully as program code: a signature over a release attests to the automated build of those
files as much as of the C# sources.

Commits should be self-contained and describe *why*. English is used for code, comments and
commit messages so that the whole history stays readable to everyone.

## Conduct

Questions about behaviour in this repository go to
[CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md). Report problems with the application's security
through [SECURITY.md](SECURITY.md) instead of a public issue.
