# Release packaging and signing

Decisions in effect since 2026-10-26, set by the project owner:

- **The Microsoft Store is not a distribution channel for this application.** No Store product is
  reserved, no Partner Center identity values exist, and nothing here may claim a Store signature or
  Store certification. The Store-channel design that used to live in this document is dropped
  rather than left half-true.
- **Distribution is the GitHub Release** produced by the `Release packaging` workflow: the
  self-contained single-file `PUB_Template_Parameter_Editor.exe`, the complete corresponding source
  archive, the notices and the hash/provenance record.
- **Signing is done by SignPath.io with a certificate by SignPath Foundation** - for the distributed
  executable and, if a package is ever built for publication, for that package too. No other signing
  path is used, and no publisher CA certificate is bought.
- `packaging/build-msix.ps1` and the tile assets stay in the repository as **local packaging
  tooling**: they are useful for testing the app as a packaged app on a dev machine, and they are the
  starting point if a packaged/sideloaded channel is ever chosen. Building one is opt-in in the
  release workflow and is not part of CI.

## Channels and what signs them

| Artifact | Where it goes | Signature |
| --- | --- | --- |
| `PUB_Template_Parameter_Editor.exe` (single file, self-contained) | GitHub Release asset | SignPath.io / SignPath Foundation certificate (pending registration, see below) |
| source archive `PUB_Template_Parameter_Editor-<version>-source.tar.gz`, `LICENSE`, `NOTICE`, third-party notices, `SHA256SUMS.txt` | GitHub Release assets | not applicable - they carry hashes, not signatures |
| `PUB_Template_Parameter_Editor_<version>_win-x64.msix` | not published; built only on request | unsigned unless it is also submitted to SignPath; a locally built package is never presented as signed |
| anything built by CI or by a contributor | CI artifact download | **unsigned** |

No channel in this project lets anyone else sign our artifacts for us, and no artifact of this
project may be described with a store signature.

## Build and packaging implementation

- One release version string drives everything: the workflow passes
  `-p:Version/-p:AssemblyVersion/-p:FileVersion/-p:InformationalVersion` to the publish and turns off
  the source-revision suffix, then reads `VersionInfo` back and requires product name
  `PUB Template Parameter Editor` with `ProductVersion == FileVersion == <release version>`. SignPath's
  artifact configuration enforces the same rule on their side, and their conditions ask for one
  product version per build.
- The executable and the assembly carry the product name (`AssemblyName` =
  `PUB_Template_Parameter_Editor`), so the file a user downloads, the process in Task Manager and the
  manifest of an optional package all agree.
- The self-contained payload bundles the .NET runtime and WPF. Those upstream binaries stay unsigned -
  they are not signed with this project's subscription - and are allowed to be *included* in a signed
  package by SignPath's conditions.
- A packaged build runs `makeappx` from the Windows SDK on the runner. Two lessons that only the
  tool teaches: the manifest is validated against the SDK's schema, so `StoreLogo` is not a valid
  `uap:VisualElements` attribute (error `C00CE015`) and the package logo belongs in
  `Properties/Logo`; and `MaxVersionTested` in the manifest must never exceed the SDK whose schema the
  packaging tool validates.

## Workflow responsibilities

| Workflow / job | Trigger and permissions | Output |
| --- | --- | --- |
| `CI validation` (`ci.yml`) | pull requests and pushes to `main`; read-only token; no signing secret | build, all three test projects, licence/repo-hygiene checks, "no template content may be published", portable publish, "no loose symbols", hashes, short-retention artifacts. It does **not** build a package: the Store channel is gone, and CI should not spend runner time or storage on an artifact nobody ships |
| `Release packaging` → `package` | tag `vMAJOR.MINOR.PATCH` or maintainer `workflow_dispatch`, protected environment `release-packaging` (a maintainer approves the run) | tests, publish with verified file metadata, hashes of the unsigned payload, the payload as its own artifact for signing, source archive, notices, `SHA256SUMS.txt`; optional MSIX when `buildMsix=true` |
| `Release packaging` → `sign-portable-exe` | same run, only when `SIGNPATH_ENABLED=true`; protected environment `signing-signpath` with a required reviewer; `contents: read`, `actions: read` | SignPath signing request for exactly the artifact `package` built, the signed payload artifact, and `SIGNING.txt` with unsigned/signed hashes, version, commit and the SignPath request URL |
| `Release packaging` → `publish-github-release` | only when the repository variable `PUBLISH_GITHUB_RELEASE=true` **and** the signing job succeeded; `contents: write` in this job only | a GitHub Release for the tag with the signed executable, the source archive, the notices and the hash record - the corresponding-source obligation of GPL-3.0 is met by the same release page |

Two rules that keep the trust boundary honest:

- the job that runs untrusted pull-request code never has the SignPath token, and no job that has it
  checks out PR code;
- a release is never published while its binary is unsigned: `publish-github-release` requires the
  signing job to have succeeded, so an unsigned binary cannot become the official download by
  accident. `gh release create` (preinstalled on the runner) is used with `GITHUB_TOKEN`, so no
  third-party release action enters the workflow.

Pin every action to a verified immutable commit and re-verify before changing a pin. The current
verified set, resolved from the GitHub tags API on 2026-10-25/26:

| Action | Tag | Commit |
| --- | --- | --- |
| `actions/checkout` | v7.0.1 | `3d3c42e5aac5ba805825da76410c181273ba90b1` |
| `actions/setup-dotnet` | v6.0.0 | `a98b56852c35b8e3190ac28c8c2271da59106c68` |
| `actions/upload-artifact` | v7.0.2 | `cf430e030ddbb5b0abf93d22962f4752f3646cd9` |
| `signpath/github-action-submit-signing-request` | v3.0 | `f6d04783b4569d051e0c80105fe66e82819d0092` |

## Runner budget on the free GitHub plan

The repository is public, so GitHub-hosted runners - Windows included - are available at no cost
under the fair-use policy for public repositories. The workflows stay inside that policy:

| Choice | Why it stays free |
| --- | --- |
| one `windows-latest` runner per run, no matrix | Windows runners are not multiplied |
| `concurrency` with `cancel-in-progress` on CI | superseded pushes stop instead of queueing |
| `retention-days: 7` for CI artifacts, `90` for release artifacts | artifact storage counts against the repository's storage budget |
| no package build in CI | the packaging step is the slowest and largest part of a run, and its output is not a shipped channel |
| no self-hosted or paid (larger) runner | a self-hosted machine would also break the trusted-release boundary |
| no Docker build step | only the SDK from `global.json` is needed |
| WPF tests inside `dotnet test` on an STA host | no GUI session, no extra runner |

A full CI run (restore, build, 207-223 tests, portable publish, hygiene checks) takes under two
minutes on `windows-latest`; a release run with signing is dominated by the human approval waits, not
by runner time.

## SignPath Foundation signing

Signing is provided by SignPath.io with a certificate issued to SignPath Foundation, on the
conditions at <https://signpath.org/terms>. The project-facing statement is
[CODE_SIGNING_POLICY.md](CODE_SIGNING_POLICY.md), because the conditions ask the project's home page
to carry a section named "Code signing policy".

### What the conditions require, and where this project meets them

| Condition | Status here |
| --- | --- |
| No malware or potentially unwanted programs | met - offline editor, no network capability, no services, no elevation |
| OSI-approved licence, no commercial dual licensing of any component | met - GPL-3.0-only everywhere |
| No proprietary code; System Libraries (GPL v3 sec. 1) allowed | met - only this project's sources; the owner's `.dta` template data is excluded from the repository, and the .NET runtime/WPF are Microsoft's own, included unsigned |
| Actively maintained | met - CI on `main`; the gate is the owner's continuing activity |
| Already released in the form to be signed | **open** - the first GitHub Release has to be made |
| Functionality documented on the download page | met in README/About text; the release page notes still have to be written when the release is made |
| Sign own projects and own binaries only | met - CI builds this repository's sources; upstream .NET binaries are included unsigned, never signed here |
| No hacking tools; respect privacy and security | met - the program transfers no information; the policy page states it |
| Announce system changes | met - writes only through Review Save, with backups and a journal |
| Provide uninstallation | met - delete the folder, optionally remove the user data folder; documented in README and the policy page |
| MFA on the repository and SignPath accounts | **owner setting** |
| Author / Reviewer / Approver roles | documented in the policy page; `.github/CODEOWNERS` plus branch protection and required reviewers of `signing-signpath` |
| "Code signing policy" section on the home page | met - README links to the policy page with the SignPath sentence, roles and privacy statement |
| Artifact configuration: product name = project name, one product version per build | met in `release.yml` (metadata passed in and read back or the run fails) and enforced by SignPath's artifact configuration |
| Binaries built from source in a verifiable way | met - CI builds, tests, hashes and records commit plus SDK; SignPath's connector reads the job and artifact identity |
| Every release needs manual approval for signing | met in design - protected environments with required reviewers, and SignPath waits for an approval per request |
| Do not fight the system; help investigate accusations | accepted - their action and connector are used, no local signing shortcuts |

Two things worth saying plainly instead of smoothing over:

- SignPath weighs a project's **verifiable reputation** for executables that people run because of the
  signature. A new repository can simply be declined; that decision is theirs, and there is no
  arbitration. The practical path is to publish the release, gather use and requests, and apply with
  the public repository and the release page as evidence.
- The free Foundation certificates are for open-source, non-commercially-licensed distribution.
  Charging for copies is not forbidden by GPL, but commercial dual licensing of components is, and a
  commercial arrangement would need SignPath's paid offering rather than the Foundation certificate.

### Open configuration gates (owner actions, not workflow actions)

1. Register the project with SignPath and obtain the organisation id, project slug, signing policy
   slug and artifact configuration slug; create the `signing-signpath` environment with a required
   reviewer and the `SIGNPATH_API_TOKEN` secret; set `SIGNPATH_ORGANIZATION_ID`,
   `SIGNPATH_PROJECT_SLUG`, `SIGNPATH_SIGNING_POLICY_SLUG`,
   `SIGNPATH_ARTIFACT_CONFIGURATION_SLUG`; then set `SIGNPATH_ENABLED=true`.
2. Enable MFA and branch protection with required review on `main`.
3. Decide the version and tag it (`v0.1.0`), then set `PUBLISH_GITHUB_RELEASE=true` so the first
   release is created from a signed build.
4. If a packaged/sideloaded channel is ever wanted: confirm with SignPath that their
   certificate/appx-signing configuration matches the package identity in
   `packaging/StoreRelease.settings.json`, and resolve those identity values first. Until then the
   identity placeholders stay, and `buildMsix=true` is expected to fail on them.

## Version and provenance

- The release tag `vMAJOR.MINOR.PATCH` is the release identity; the app version comes from
  `Directory.Build.props` (`VersionPrefix`) overridden per run, and the same string ends up in the
  file metadata, the release title and the source archive name.
- Each run records: commit, tag/branch, app version, .NET SDK version (`dotnet --version`), Windows
  runner image, SHA-256 of the unsigned payload, SHA-256 of the signed payload, and the SignPath
  request URL. That record is the difference between "this binary came from that source" and an
  assertion nobody can check.
- For the optional package only: the MSIX version scheme is `MAJOR.MINOR.PATCH.0`, and identity values
  must be resolved before a package build is allowed to run.

## GPL source availability

The GitHub Release carries the complete corresponding source archive next to the binary, and the
in-app About view names the repository and the source URL for the running version. See
[GPL_RELEASE.md](GPL_RELEASE.md) for the licensing checklist (its Store-specific lines are marked where
the Store decision changes them).

## Sources checked for this document

- SignPath Foundation conditions for open source projects, <https://signpath.org/terms>, read in full
  on 2026-10-26.
- SignPath's action definition and tag→commit mapping
  (`signpath/github-action-submit-signing-request`, `action.yml`, tags API) and their own demo
  workflow `SignPath/demo-github-actions/.github/workflows/build-and-sign.yml`, 2026-10-26.
- GitHub-hosted runner pricing/limits for public repositories and the preinstalled GitHub CLI, as
  documented in the Actions runner images, 2026-10-26.
- `makeappx` manifest schema validation behaviour, observed directly on this machine on 2026-10-26
  (the `C00CE015` error for an invalid attribute).
