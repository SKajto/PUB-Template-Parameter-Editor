# Code signing policy

This page is the code signing policy required by the
[SignPath Foundation conditions for Open Source projects](https://signpath.org/terms).
It states who may approve signing, what the signed program does to a user's system,
how the binaries relate to this source, and what is signed by whom.

## Free code signing

> Free code signing provided by
> [SignPath.io](https://signpath.io), certificate by
> [SignPath Foundation](https://signpath.org).

Status in this project: the subscription is **not active yet**. The project has to be
registered with SignPath Foundation and approved by them, and the binary has to be
released in the form that is to be signed (see [open gates](#open-gates)). Until then
every artifact produced here is **unsigned**, and no artifact may be described as
signed.

What is signed and what is not:

| Artifact | Signature |
| --- | --- |
| `PUB_Template_Parameter_Editor.exe` — the downloadable portable single-file executable | SignPath Foundation certificate, through the `sign-portable-exe` job in `.github/workflows/release.yml` |
| MSIX for the Microsoft Store | signed by Microsoft after Partner Center certification; we need no publisher certificate and we never sign it ourselves |
| any artifact built by CI or by a developer machine | **unsigned** — it must never be presented as Store-signed or SignPath-signed |

## Team roles

Per the SignPath conditions, signing requests are only submitted by people who also
maintain the source, and each release needs an explicit approval.

| SignPath role | Meaning here | Members |
| --- | --- | --- |
| Authors | may change the source, build scripts and CI configuration without an additional review | repository Owners: <https://github.com/SKajto> |
| Reviewers | every change proposed by someone who is not a committer (a pull request) is reviewed by a team member before it can be merged; build scripts and workflow files get special attention, because SignPath treats them as part of the source of a signed binary | repository Owners: <https://github.com/SKajto>, enforced with branch protection and `.github/CODEOWNERS` |
| Approvers | decide per release whether a signing request may be submitted; in the workflow this is the required-reviewer approval of the `signing-signpath` environment before the `sign-portable-exe` job runs | repository Owners: <https://github.com/SKajto> |

Multi-factor authentication is required on both the source hosting account and the
SignPath account, as the conditions demand. Repository permissions, environment
protection rules and branch protection are configured by the repository owner in
GitHub's settings; the workflow declares which environment holds the signing secret,
so a maintainer must approve the run before the token is ever exposed to a job.

## Privacy

**This program will not transfer any information to other networked systems unless
specifically requested by the user or the person installing or operating it.**

It is an offline desktop editor: it opens template files from a folder the user picks,
writes only to files the user confirms in the review step, and stores its own settings,
backups and recovery journal in the user's own profile
(`%LOCALAPPDATA%\PubTemplateEditor`). It contains no account, no update service, no
telemetry, no embedded browser and no network capability. No third-party component in
the payload collects user data on the program's behalf; the bundled .NET runtime and
WPF are Microsoft components under their own documented terms, which the About/Licenses
view shows offline.

## Announcing system changes and uninstallation

- The program does not install anything when it runs, needs no service, and does not
  require elevation. Settings, backups and the recovery journal are written into the
  user's profile only.
- Template files are never modified by loading, comparing, editing or bulk staging.
  The only write path is **Review Save**, which lists every pending change per file,
  lets the user select which ones to apply, keeps a timestamped backup of each file it
  replaces, and reports per file whether it was written, rejected or partially written.
  It detects outside edits by content fingerprint before writing.
- Uninstallation: the Store package is removed with Windows Settings → Apps (or the
  Store's own uninstall). The portable executable is removed by deleting its folder;
  user-scoped data can be deleted with `%LOCALAPPDATA%\PubTemplateEditor`, which holds
  settings, backups and the journal and is never touched by another program.

## Source, build and metadata of a signed binary

- Signed binaries are built by this repository's CI (`Release packaging` workflow) from
  the tagged or maintainer-selected commit, not from a developer machine, and the build
  records the source commit, the .NET SDK version and SHA-256 hashes of every artifact
  (`artifacts/SHA256SUMS.txt`). SignPath's pipeline connector reads those job details, so
  a signature means the binary is the automated build of that source, including its build
  scripts and workflow files.
- File metadata is set from the release version and verified before signing: every
  `PUB_Template_Parameter_Editor.exe` in a build carries product name
  `PUB Template Parameter Editor` and product version equal to the release version that is
  being signed (`release.yml` passes `-p:Version=` and fails if the produced executable's
  `VersionInfo` disagrees). SignPath's artifact configuration enforces the same rule on
  their side.
- Upstream components: the self-contained payload includes the unsigned .NET runtime and
  WPF binaries. Those are not signed with this project's subscription — they are
  Microsoft's own, which the SignPath conditions allow to be included in a signed package.
  No third-party binary is signed here, and the repository ships no code that this project
  does not maintain.
- Every signing request requires manual approval, and the workflow submits requests only
  from the trusted release path: pull-request code never runs with the SignPath token.
- `.github/workflows/release.yml` pins the SignPath action to a verified commit:
  `signpath/github-action-submit-signing-request@f6d04783b4569d051e0c80105fe66e82819d0092`
  (tag `v3.0`, resolved from the GitHub tags API on 2026-10-26). The workflow configuration
  is part of the reviewed source, exactly as the SignPath conditions require.

## Meeting the other conditions

- No malware or potentially unwanted programs; no hacking or vulnerability-scanning
  features.
- OSI-approved licence without commercial dual licensing of any component:
  **GPL-3.0-only** ([LICENSE](../LICENSE), [NOTICE](../NOTICE)).
- No proprietary code in the repository. The owner's PUB template files (`examples/*.dta`)
  are user content and are deliberately not published; the format knowledge gained from
  them is written down in [SAMPLE_FINDINGS.md](SAMPLE_FINDINGS.md), and the tests write
  synthetic containers where the real files are not present.
- Documented functionality: [README.md](../README.md) and
  [REQUIREMENTS.md](../REQUIREMENTS.md) describe what the program does; the release page
  and the Store entry must carry the same description.

## Open gates

These are actions for the project owner, not things a workflow can do by itself:

1. Register the project with SignPath (they verify reputation and control once) and obtain
   the organisation id, project slug, signing policy slug and, if used, the artifact
   configuration slug. Then set the repository variables `SIGNPATH_ORGANIZATION_ID`,
   `SIGNPATH_PROJECT_SLUG`, `SIGNPATH_SIGNING_POLICY_SLUG`
   (and `SIGNPATH_ARTIFACT_CONFIGURATION_SLUG`), the `signing-signpath` environment secret
   `SIGNPATH_API_TOKEN` with required reviewers, and finally `SIGNPATH_ENABLED=true`.
2. Publish the release in the form to be signed — a GitHub Release carrying
   `PUB_Template_Parameter_Editor.exe`, its source archive and notices — because a free Foundation
   certificate requires the project to be already released and documented in that form.
3. Enable branch protection with required review, and MFA for the accounts that hold
   Author, Reviewer and Approver roles.
4. Keep the Store listing consistent with a free open-source certificate: the SignPath
   Foundation offer is for open-source projects; charging for the app, or any commercial
   dual licensing, needs SignPath's paid offering instead of the Foundation certificate.
