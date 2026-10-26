<!--
Delete this comment block when you are done. It is guidance, not part of the description.
Keep one coherent change per pull request; a change that mixes a format fix with a UI
redesign is hard to review and hard to revert.
-->

## What changes, in behaviour terms

<!-- What a user of the application would notice. Not which files you touched. -->

## Why it is worth having

<!-- The task or defect this serves. Link the issue ("Closes #123") when there is one. -->

## How it was verified

- [ ] `dotnet build PubTemplateEditor.slnx -c Release` - 0 warnings, 0 errors
- [ ] `dotnet test PubTemplateEditor.slnx -c Release` - all projects pass, and the count is reported below
- [ ] the suite also passes with `examples/` renamed away (a fresh clone has no template samples)
- [ ] new tests cover the behaviour change, on temporary copies, never on a real template
- [ ] UI change: both themes checked; the contrast audit reports no offenders
- [ ] save-path change: a real `.dta` container round trip is covered, backups and journal included

Tests run: Core __ / Integration __ / App(WPF) __ → total __.

**What could not be verified here:** <!-- say it plainly. Native PUB round trip, Store
certification, an interactive desktop session and real Partner Center values are routinely
unavailable; an unverified gate is acceptable, an implied pass is not. -->

## Constraints this respects

- [ ] no new third-party package in the shipped application (if one was unavoidable, it is
      justified in the description and listed in `THIRD-PARTY-NOTICES.txt`)
- [ ] `Core` still has no WPF and no file IO; parsing/saving stays out of window code-behind
- [ ] nothing writes to a template outside the review step; unrelated bytes stay byte-identical
- [ ] template files stay untrusted data: no DTD, no external resolution, values treated as text
- [ ] no `.dta` file, build output, binary or `.pdb` is added to the repository
- [ ] licences, notices and the SPDX identifier `GPL-3.0-only` are still correct
- [ ] the executable name, the manifest and the packaging scripts still agree

## If the pull request touches these, say so explicitly

`.github/workflows/**`, `packaging/**`, `Directory.Build.props`, `global.json`,
`*.pubxml`, `tools/brand/**` — a signed release attests to the automated build of these files as
much as of the C# sources, so they get the closest review. Note also that WPF fails a forward
`StaticResource` reference and a wrong pack-URI assembly name **at runtime, not at build time**, so
such a change needs a test that constructs the window.
