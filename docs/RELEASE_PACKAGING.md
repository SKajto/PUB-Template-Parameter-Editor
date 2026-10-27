# Build, package and publish

How the two artifacts are produced and validated. Commands and verification status live in
[PLAN.md](../PLAN.md); what a fresh clone needs is in [PROJECT_SETUP.md](PROJECT_SETUP.md).

## Distribution channels

| Channel | Artifact | Status |
| --- | --- | --- |
| **GitHub Release** | `PUB_Template_Parameter_Editor.exe`, one self-contained single-file 64-bit Windows executable | the distribution channel: created by the `publish-github-release` job when `PUBLISH_GITHUB_RELEASE=true` |
| MSIX package | optional build for packaged-app testing | **not a channel** - it needs a real package identity that this project does not have, and no store publishes it. `packaging/` and `build-msix.ps1` are kept as local tooling, `buildMsix` defaults to `false`, and the placeholder identity values stay unresolved so nothing can be built from them by accident |
| CI build | the same executable, uploaded as a workflow artifact | testing only; not distributed |

## Releases are unsigned, and say so

There is **no code signing** for this project: nothing signs these binaries, and no Authenticode
certificate has been acquired (a commercial certificate is paid and identity-verified, which makes it
a maintainer decision). The consequences are stated instead of hidden:

- every binary this repository produces - CI artifact, GitHub Release asset, anything built locally -
  is **unsigned**;
- Windows SmartScreen shows *Windows protected your PC* on first run of the downloaded executable
  because the file is unsigned and came from the internet, and antivirus products may warn too;
- each release publishes **SHA-256 hashes** (`SHA256SUMS.txt`, and the executable's hash in the
  release notes) so a download can be checked against what the workflow built;
- release notes say in plain words that the build is unsigned and how to verify it;
- the executable's **file metadata** (product name, and one version everywhere in the build) is set
  from the release version and verified by CI, because that is the only version information a reader
  of an unsigned binary can check against the tag.

If signing is ever added, every label has to change in the same commit - README, SECURITY.md, the
release notes, the packaging notes and `ChannelPolicyTests`, which asserts that no document claims a
signature that does not exist and that no signing service is configured in the workflows.

## Portable executable

The portable profile is a single self-contained executable with no installer and no required .NET
installation:

```xml
<PublishSingleFile>true</PublishSingleFile>
<SelfContained>true</SelfContained>
<IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
<PublishTrimmed>false</PublishTrimmed>
<PublishAot>false</PublishAot>
<DebugType>embedded</DebugType>
```

WPF is not trimming- or AOT-supported, so those switches stay off and the payload stays large.
Native libraries are embedded in the EXE and extracted to **user-scoped** temporary storage at
runtime; that is normal single-file behaviour and the reason the executable does not claim to be a
program that runs with no runtime files at all.

## MSIX (local tooling)

```powershell
.\packaging\generate-placeholder-assets.ps1
.\packaging\build-msix.ps1 -Version 0.1.0 -Mode Placeholder   # local layout/deployment test only
```

`Mode Strict` refuses to run while identity values are unresolved. The manifest declares
`runFullTrust` in an `asInvoker` application, so no service and no elevation is involved, and its
`Executable=` value must match the built assembly name - asserted by `PackagingConfigurationTests`.

## Workflows and pins

| File | Jobs | Trigger |
| --- | --- | --- |
| `.github/workflows/ci.yml` | build, test (all three projects), sample-policy and licensing checks, portable publish | push, pull request |
| `.github/workflows/release.yml` | `package`, `publish-github-release` | release tag or manual run, `release-packaging` environment approval |

Actions are pinned to immutable commit SHAs rather than moving tags. The pins below were resolved
from the actions' own tag metadata on 2026-10-25/26 and verified against
`actions/.github/PRERELEASE.md`; they match the action versions that CI actually ran with. Bump all
pins together in a pull request that CI validates.

| Action | Tag resolved | Pin |
| --- | --- | --- |
| `actions/checkout` | v7.0.1 | `3d3c42e5aac5ba805825da76410c181273ba90b1` |
| `actions/setup-dotnet` | v6.0.0 | `a98b56852c35b8e3190ac28c8c2271da59106c68` |
| `actions/upload-artifact` | v7.0.2 | `cf430e030ddbb5b0abf93d22962f4752f3646cd9` |

No third-party action is used anywhere, including for publishing: the release job drives GitHub's
preinstalled CLI with the job's own token.

## Free-plan runner budget

The free plan gives a public repository unlimited GitHub-hosted runner minutes under a fair-use
policy, 1 GiB of artifact storage and 500 MB of packages storage. This project uses one Windows
runner per run (no matrix, no larger runners, no self-hosted machine), 90-day artifact retention and
no package registry at all. A full CI run takes about two minutes; artifacts held by recent runs have
stayed well inside the storage allowance. A `Release packaging` run additionally needs
`Settings → Environments → release-packaging → Protection rules` with a required reviewer, and
`vars.PUBLISH_GITHUB_RELEASE=true` to publish.

## What a release contains

`release-<version>`: the portable executable, the complete corresponding source archive
(GPL-3.0-only obligation), `LICENSE`/`NOTICE` and the bundled third-party notices, `SHA256SUMS.txt`
(plus the explicit line that the build is not code-signed) and `file-metadata.txt`. Attaching the
source archive to the same release is what keeps the GPL's "complete corresponding source next to the
binary" requirement satisfied by the same channel the binary is distributed through.
