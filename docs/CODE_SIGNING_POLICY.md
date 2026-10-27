# Code signing policy

The policy page the SignPath Foundation conditions ask a project to publish
(<https://signpath.org/terms>). The short version on the project's home page is
[README.md → Code signing policy](../README.md#code-signing-policy).

## Free code signing

> Free code signing provided by [SignPath.io](https://signpath.io), certificate by
> [SignPath Foundation](https://signpath.org).

Status: the subscription is **not active yet**. The project has to be registered with and approved by
SignPath first. Until then every artifact built here is **unsigned**, and no artifact may be
described as signed.

## What is signed

| Artifact | Signature |
| --- | --- |
| `PUB_Template_Parameter_Editor.exe` — the distributed single-file executable, attached to a GitHub Release | SignPath Foundation certificate, submitted by the `sign-portable-exe` job in `.github/workflows/release.yml` |
| `PUB_Template_Parameter_Editor_<version>_win-x64.msix` — built only on request, not a distribution channel | unsigned; it would go through the same job if it were ever published |
| anything built by CI or by a contributor | **unsigned** |

No other signer is used for this project's binaries.

## Privacy

**This application collects and transfers no data.** No user data, no usage or diagnostic data, no
telemetry, no crash reporting. It has no network capability at all: the manifest grants none, and the
program opens no connections, needs no account and phones home for no reason. Nothing it reads —
template files, folder listings, settings — leaves your machine.

## System changes

**This application does not alter the system.** No installer, no service, no startup entry, no
registry or policy change, no elevation: it runs as an ordinary process of the signed-in user. The
only files it writes are

- the template files you explicitly confirm in **Review Save**, which lists every pending change per
  file, keeps a timestamped backup of each file it replaces, and reports per file whether it was
  written, rejected or partially written, and
- its own settings, backups and recovery journal under `%LOCALAPPDATA%\PubTemplateEditor`.

**Uninstall:** delete the program's folder. Delete `%LOCALAPPDATA%\PubTemplateEditor` as well if you
want its settings, backups and journal gone. Nothing else on the system needs cleaning up.

## Team roles

SignPath requires that the people who sign are the people who maintain the code, and that each
release is approved before it is signed.

| Role | Meaning here | Members |
| --- | --- | --- |
| Authors | may change source, build scripts and CI configuration without a further review | repository Owner: <https://github.com/SKajto> |
| Reviewers | every change from a non-committer is reviewed before merge; build scripts and workflow files count as source, because a signature attests to them too | repository Owner: <https://github.com/SKajto>, enforced by branch protection plus `.github/CODEOWNERS` |
| Approvers | decide per release whether a signing request may be submitted — the required-reviewer approval of the `signing-signpath` environment, and the approval inside SignPath | repository Owner: <https://github.com/SKajto> |

Multi-factor authentication is required on the source hosting account and on the SignPath account.

## Build and metadata of a signed binary

- Signed binaries are built by this repository's release workflow from the tagged or
  maintainer-selected commit, never on a developer machine; the run records the commit, the .NET SDK
  version and SHA-256 hashes of the unsigned and signed payloads.
- File metadata is passed in from the release version and read back before signing: product name
  `PUB Template Parameter Editor`, and `ProductVersion == FileVersion == <release version>` in every
  build. SignPath's artifact configuration enforces the same rule on their side.
- The self-contained payload includes Microsoft's .NET runtime and WPF binaries. They are **not**
  signed with this project's subscription — they are included unsigned, which SignPath's conditions
  allow for upstream components.
- Pull-request code never runs in a job that can see the SignPath token.

## Open gates

These are owner actions, not things a workflow can do by itself:

1. Register the project with SignPath, then set the repository variables
   `SIGNPATH_ORGANIZATION_ID`, `SIGNPATH_PROJECT_SLUG`, `SIGNPATH_SIGNING_POLICY_SLUG`,
   `SIGNPATH_ARTIFACT_CONFIGURATION_SLUG`, add the `SIGNPATH_API_TOKEN` secret in the
   `signing-signpath` environment with a required reviewer, and finally `SIGNPATH_ENABLED=true`.
2. Enable MFA and branch protection with required review.
3. Tag a version and set `PUBLISH_GITHUB_RELEASE=true`, so the first release is created from a signed
   build — the workflow refuses to publish a binary that was not signed.

See [RELEASE_PACKAGING.md](RELEASE_PACKAGING.md) for the workflow and channel details.
