# Build, package and publish

How the two artifacts are produced and validated. Commands and verification status live in
[PLAN.md](../PLAN.md); what a fresh clone needs is in [PROJECT_SETUP.md](PROJECT_SETUP.md).

## Distribution channels

| Channel | Artifact | Status |
| --- | --- | --- |
| **GitHub Release** | `PUB_Template_Parameter_Editor.exe`, one self-contained single-file 64-bit Windows executable | the distribution channel: built and signed by the maintainer (see *Publishing a release*), attached together with its source and hashes |
| MSIX package | optional build for packaged-app testing | **not a channel** - it needs a real package identity that this project does not have, and no store publishes it. `packaging/` and `build-msix.ps1` are kept as local tooling, `buildMsix` defaults to `false`, and the placeholder identity values stay unresolved so nothing can be built from them by accident |
| CI build | the same executable, uploaded as a workflow artifact | testing only; not distributed |

## Signing

One certificate signs this project's releases: the one recorded in
`packaging/CertumSigning.settings.json`. Its thumbprint, issuer, signer name and expiry were read from
the issued certificate, and its public half is committed at
`tools/signing/PUB-Template-Parameter-Editor-code-signing.cer`. The private key stays in the signing
service's own hardware, so it is never a file and never a build secret: `ciSigning` is `false`, no
workflow calls a signing tool (`ChannelPolicyTests` keeps it that way), and everything CI builds reports
`NotSigned`.

| Artifact | Signature |
| --- | --- |
| the executable attached to a GitHub Release | signed: SHA-256 file digest, SHA-256 timestamp digest, RFC 3161 timestamp at the url in the settings file, read back as `Valid` |
| the same executable built by the `CI validation` or `Release packaging` workflow | `NotSigned`, asserted by a release step and recorded in `SIGNING-STATUS.txt` |
| a rebuild from the release source archive | unsigned: the GPL's rights do not depend on the key |
| the optional MSIX | unsigned unless `build-msix.ps1 -Sign` runs on the maintainer's machine; not a distribution channel |

`tools/signing/certum-sign.ps1` is the only thing that signs. It takes no key parameter: it uses the
recorded certificate or nothing, and it refuses while no certificate is recorded, a self-issued
certificate, a certificate without the code-signing purpose, a certificate whose issuer, name or expiry
does not match the recorded values, and a file already signed by another key. After signing it reads the
result back and fails unless the platform reports `Valid` with a timestamp.
`tools/signing/verify-signature.ps1` reads a signature and a hash and writes nothing, so anyone can
check a download against what the release claims.

A signature is appended to the file, so **SHA-256 changes**: every published hash — in `SHA256SUMS.txt`
and in the release notes — is taken from the signed bytes. The certificate expires on the date the
settings file records; the timestamp is what keeps releases published before then verifiable afterwards.
A renewal replaces the recorded values in the same commit that updates the wording in README,
SECURITY.md and these notes, and is reported in [PLAN.md](../PLAN.md).

## Publishing a release

The release is assembled where the key is, which is a maintainer's Windows session. With the SDK
installed, the signing client connected and the repository clean at the commit being released:

```powershell
# tests green at this commit first (docs/PROJECT_SETUP.md), then the payload
 dotnet publish src/PubTemplateEditor.App/PubTemplateEditor.App.csproj -c Release `
  -p:PublishProfile=PortableWin_x64 -p:Version=<version> `
  -p:AssemblyVersion=<version> -p:FileVersion=<version> -p:InformationalVersion=<version> `
  -p:IncludeSourceRevisionInInformationalVersion=false -o artifacts/publish/portable-win-x64

# the file metadata must report <version>, and the payload must still be unsigned before signing
(Get-Item artifacts/publish/portable-win-x64/PUB_Template_Parameter_Editor.exe).VersionInfo
(Get-AuthenticodeSignature artifacts/publish/portable-win-x64/PUB_Template_Parameter_Editor.exe).Status

# sign with the recorded certificate (the client asks for its PIN), then read the result back
powershell -NoProfile -ExecutionPolicy Bypass -File tools/signing/certum-sign.ps1 `
  -FilePath artifacts/publish/portable-win-x64/PUB_Template_Parameter_Editor.exe `
  -Description "PUB Template Parameter Editor <version>"
powershell -NoProfile -ExecutionPolicy Bypass -File tools/signing/verify-signature.ps1 `
  -FilePath artifacts/publish/portable-win-x64/PUB_Template_Parameter_Editor.exe

# tag the released commit, build the corresponding source from the tag, hash the signed bytes
git tag -a v<version> -m "v<version>"
git push origin v<version>
git archive --format=tar.gz -o artifacts/source/PUB_Template_Parameter_Editor-<version>-source.tar.gz v<version>
Get-FileHash -Algorithm SHA256 artifacts/publish/portable-win-x64/PUB_Template_Parameter_Editor.exe
```

Then attach the signed executable, the source archive, `SHA256SUMS.txt`, `SIGNING-STATUS.txt` and
`file-metadata.txt` to the release — with `gh release create v<version> …` where the CLI is available,
or through the repository's Releases API where it is not — and write the notes from those signed bytes:
version, the commit the tag points at, the SHA-256 of the file that is actually attached, and the
command that reads the signature back.

`release.yml` still builds and tests the tagged commit and uploads that build as a workflow artifact,
which is the independent check of the released commit; single-file WPF publishes are not byte-identical,
so it is a build-and-test agreement, not a hash match. Its `publish-github-release` job refuses to create
a release while the signing configuration records a certificate: the only executable CI can attach is
the unsigned one it just built, and the notes would then have to call it something it is not.

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
.\packaging\build-msix.ps1 -Version 1.0.0 -Mode Draft   # local layout/deployment test only
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
(GPL-3.0-only obligation), `LICENSE`/`NOTICE` and the bundled third-party notices, `SHA256SUMS.txt`,
`SIGNING-STATUS.txt` (what CI's own unsigned build reported, and where the published asset's signature
came from) and `file-metadata.txt`. Attaching the
source archive to the same release is what keeps the GPL's "complete corresponding source next to the
binary" requirement satisfied by the same channel the binary is distributed through.
