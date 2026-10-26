# GitHub Actions and Microsoft Store release design

Fresh project release design, 25 October 2026. This is an implementation specification for Agent in the real GitHub repository. No workflow is deployed and no Store account or product is configured by this planning package.

## Channels and signatures

The primary channel is Microsoft Store MSIX, containing a self-contained WPF/.NET 10 payload. Microsoft signs/re-signs the package after certification; the Store handles customer acquisition, installation and packaged updates. This channel does not require the maintainer to buy or supply a CA-trusted code-signing certificate for submission.

MSIX is a packaging format, not a signing authority. The CI package before Store certification is not automatically Microsoft-signed or trusted for arbitrary sideload installation. The Store package signature covers the package integrity; it does not imply every inner PE has a separate Microsoft Authenticode signature. A GitHub portable EXE does not inherit a Store signature. Do not advertise a raw CI MSIX as an ordinary-user signed installer.

Store installation still depends on device configuration. Verify managed-device acquisition and execution rather than promising Store trust overrides ASR, WDAC/App Control, disabled Store access or other enterprise controls. Record the actual package identity, artifact hashes and observed policy outcome for this project.

## Build and packaging implementation

Two constraints that only the packaging tool itself teaches, both hit while wiring CI:

- `makeappx` validates the manifest against the schema of the Windows SDK on the runner. An attribute
  that looks plausible and whose image exists is still rejected: `StoreLogo` is not valid on
  `uap:VisualElements` (error `C00CE015`). The Store logo belongs in `Properties/Logo`; the tile
  attributes on `uap:VisualElements`/`uap:DefaultTile` are the only place the schema accepts them.
  A draft MSIX build is therefore part of CI, not an optional extra.
- `MaxVersionTested` in the template comes from `packaging/StoreRelease.settings.json` and must never
  exceed the SDK whose schema the runner's packaging tool validates.

Before pushing, run the test suite once with `examples/` moved out of the working tree. A fresh clone
and CI never see the native samples, and a suite that is only green on the maintainer's disk hides
exactly the tests that matter most.

1. Use a documented Windows GitHub Actions runner image with the selected .NET 10 SDK and Windows SDK packaging tools. Record actual versions. Create and verify the packaging configuration for the new solution.
2. Build/test the WPF application and portable core. Produce a self-contained win-x64 payload without requiring runtime installation by customers. No Electron/browser runtime is introduced.
3. Implement MSIX using a Windows Application Packaging Project or Windows SDK MakeAppx pipeline, whichever is verified against the actual solution. Include schema-valid identity, executable/entry point/runtime behavior, required desktop capabilities, minimum OS, visual assets, offline license/notices and correctly scoped resources.
4. Use the exact reserved Partner Center Package Identity Name and Publisher. Product ID and publisher display name are distinct values. Validate real values instead of inventing them. A draft config can contain placeholders; a submission build must reject them.
5. Build a Store submission artifact without requiring a public certificate. Configure the chosen packaging project/tool for Store submission rather than accidentally requiring a missing PFX. Validate the accepted artifact format against the actual Partner Center route.
6. Inspect package contents, schema, dependencies, identity, version, architecture and assets. Run applicable Windows App Certification Kit checks and record limitations on hosted runners. Hosted-runner unit tests are not a substitute for the interactive GUI and real Store-install lifecycle.
7. Test self-contained runtime/native extraction in packaged mode. Do not force an unverified single-file payload layout just because the optional portable profile is single-file; MSIX may contain normal payload files and package assets. End users must not manage dependencies manually.

No app settings, journals or template edits belong in the read-only MSIX installation directory. Verify real identity-scoped user data paths and upgrade/uninstall behavior. External selected templates must not be removed by package uninstall. Backups/journals need an explicit retention behavior and must not be silently lost during an update.

## Workflow responsibilities

| Workflow/job | Trigger and permissions | Required output |
| --- | --- | --- |
| CI validation | pull_request and trusted branch pushes; read-only token; no Store credentials | Build/test results, format/manifest checks, candidate package where possible |
| Release packaging | Trusted validated tag or maintainer-selected workflow_dispatch commit; no Store credentials | MSIX submission artifact, optional portable EXE, complete corresponding source archive, notices, hashes and version/commit record |
| GitHub release publication | Explicit release task with narrowly scoped repository write permission | Binary/source/notices linked to the exact release; clearly mark pre-Store signing status |
| Store update submission | Trusted release artifact/commit and restricted Store environment | Submission identifier/status and artifact identity; never report upload as certification completion |

Pin GitHub Actions to verified immutable references, or the repository's supported dependency-lock mechanism. Record the runner/tool versions and validate release version strings before using them in paths/commands. Do not execute PR code/artifacts in a context holding Store secrets (including unsafe pull_request_target/workflow_run combinations). Do not assume a tag alone is trusted if anyone can create it. Limit concurrency so two updates cannot race the package version/submission.

The workflow files must be implemented and checked against the actual repository paths and chosen packaging tool. This package deliberately does not provide a runnable workflow with guessed action SHAs, app IDs or credentials. Codex should produce those reviewable workflow files in the real project once it has the required evidence.

## Runner budget on the free GitHub plan

The repository is public, and public repositories get GitHub-hosted runners - Windows included -
without a metered minute allowance, under the fair-use policy. The workflows are written so that
nothing pushes into paid territory:

| Choice | Why it stays free |
| --- | --- |
| one `windows-latest` runner per run, no matrix | Windows runners are not multiplied; a build matrix would be the fastest way to exhaust fair use |
| `concurrency` group with `cancel-in-progress` on CI | superseded pushes stop instead of queueing extra runs |
| `retention-days: 7` for CI artifacts, `90` for release artifacts | artifact storage counts against the repository's storage budget |
| no self-hosted or larger (paid) runner | a self-hosted machine would also break the trusted-release boundary |
| no Docker-based build step | the build needs only the SDK from `global.json`, so no image pulls and no Linux/Windows split |
| WPF tests run on an STA host inside `dotnet test` | no GUI session, no RDP job, no extra runner |

The consequence to keep in mind: a `windows-latest` job on the free plan still starts within the
usual queue, and the whole suite (build plus 208 tests plus publish plus MSIX draft) is designed to
finish in a few minutes so that a run never looks abusive. If the plan ever changes to private, the
same jobs fit inside the private-plan Windows minute budget as they are single-runner.

## SignPath Foundation signing

Two different signature questions exist and they must not be merged:

- **MSIX for the Microsoft Store** needs no publisher certificate from us. The Store signs the
  package after certification. A CI-built MSIX is unsigned, and neither a CI MSIX nor a GitHub EXE
  may ever be labelled Store-signed.
- **The portable single-file `PUB_Template_Parameter_Editor.exe`** that GitHub serves is the binary
  that benefits from a real Authenticode signature (mark-of-the-web and SmartScreen behaviour), and
  that is the one SignPath Foundation signs free of charge for open-source projects.

### What the conditions require, and where this project meets them

Checked against <https://signpath.org/terms> on 2026-10-26. The project-facing page is
[CODE_SIGNING_POLICY.md](CODE_SIGNING_POLICY.md), because the conditions ask the *project's home
page* to carry a section named "Code signing policy".

| Condition | Status here |
| --- | --- |
| No malware or potentially unwanted programs | met - offline editor, no network capability, no services, no elevation |
| OSI-approved licence, no commercial dual licensing of any component | met - GPL-3.0-only everywhere, recorded in `Directory.Build.props`, LICENSE, NOTICE |
| No proprietary code; System Libraries (GPL v3 sec. 1) allowed | met - only this project's sources; the owner's `.dta` template data is excluded from the repository, and the .NET runtime/WPF are Microsoft system-library-style inclusions that stay under their own terms |
| Actively maintained | met - issues/CI on the main branch; the gate is the owner's continuing activity |
| Already released in the form to be signed | **open** - no GitHub Release carries the executable yet |
| Functionality documented on the download page or Store entry | met in README/Store metadata text; the release page text still has to be written when the release is made |
| Sign own projects and own binaries only | met - CI builds this repository's sources; upstream .NET binaries are *included unsigned*, never signed with our subscription |
| No hacking tools, respect privacy and security | met - the program transfers no information; the policy page states it in the wording the conditions suggest |
| Announce system changes | met - writes happen only through Review Save, with backups and a journal; described in the policy page |
| Provide uninstallation | met - Store uninstall, and portable uninstall documented in README/policy |
| MFA for repository and SignPath accounts | **owner setting** - required before the subscription is used |
| Author / Reviewer / Approver role structure | documented in the policy page; enforced by `.github/CODEOWNERS`, branch protection (owner) and required reviewers of the `signing-signpath` environment |
| Code signing policy section on the home page | met - README section links to `docs/CODE_SIGNING_POLICY.md` with the required SignPath sentence, roles, and privacy statement |
| Artifact configuration: product name = project name, one product version per build | met in `release.yml` - the release version is passed into the publish and the produced executable's `VersionInfo` (ProductName/ProductVersion/FileVersion) is read back and must match, or the run fails |
| Binaries built from source in a verifiable way | met - the release workflow builds, tests, hashes and records commit plus SDK; SignPath's connector reads the job and artifact identity |
| Every release needs manual approval for signing | met in design - the `package` job runs in the protected `release-packaging` environment, and the signing job in `signing-signpath`, both requiring maintainer approval; the request also waits inside SignPath for an Approver |
| Do not fight the system / investigate accusations | accepted by using their action and pipeline connector rather than any local signing shortcut |

Their "verifiable reputation" expectation is worth stating plainly: for a program that people
download and run because of the signature, SignPath weighs the project's standing, and a brand-new
repository can be declined. That decision is theirs and there is no arbitration mechanism, so the
realistic path is to publish the release, get some usage and requests, and apply with the public
repository and the release page as evidence.

### How the workflow signs

`.github/workflows/release.yml`:

1. `package` (windows-latest, protected environment `release-packaging`) restores, builds, runs all
   three test projects, publishes the single-file executable with the release version in its
   metadata, verifies that metadata, hashes the payload and uploads it as the artifact
   `unsigned-portable-<version>`. Store packaging is a separate step and can be skipped
   (`includeStorePackage=false`) so a signed portable release can exist before the Partner Center
   identity values are in place; a tag push always builds the Store package and refuses unresolved
   `REPLACE_*` identity values (REL-04).
2. `sign-portable-exe` (ubuntu-latest, protected environment `signing-signpath`, `actions: read`)
   runs only when the repository variable `SIGNPATH_ENABLED=true`. It refuses to start unless
   `SIGNPATH_ORGANIZATION_ID`, `SIGNPATH_PROJECT_SLUG`, `SIGNPATH_SIGNING_POLICY_SLUG`,
   `SIGNPATH_ARTIFACT_CONFIGURATION_SLUG` and the environment secret `SIGNPATH_API_TOKEN` are all
   present, then submits the signing request for exactly the artifact the `package` job built, waits
   for the Approver's decision in SignPath, records unsigned and signed SHA-256, the release version,
   the commit and the request URL in `SIGNING.txt`, and uploads
   `signed-portable-<version>`.
3. Nothing in this workflow ever signs an MSIX, and the recorded note states that the signed binary
   came from SignPath.io with a SignPath Foundation certificate - not from the Store.

The action is pinned to an immutable commit, resolved from the GitHub tags API on 2026-10-26:
`signpath/github-action-submit-signing-request@f6d04783b4569d051e0c80105fe66e82819d0092` (tag
`v3.0`). Its verified inputs are `api-token`, `organization-id`, `project-slug`,
`signing-policy-slug`, `artifact-configuration-slug`, `github-artifact-id`,
`wait-for-completion`, `output-artifact-directory` - note that they take **slugs**, not the numeric
ids, and that the file-metadata restrictions live in SignPath's artifact configuration, not in a
file in this repository.

An SBOM is not produced yet. The conditions say such practices may become mandatory later, so if
the subscription is granted, adding a `cyclonedx`-style SBOM step to the release job is the next
configuration change to expect.

## Identity version and provenance

Use a release map such as v1.2.3 -> MSIX 1.2.3.0, validating the applicable Store range/ordering rules. Follow current Store guidance reserving the fourth segment as 0 for the package scheme, and reject collisions/downgrades. Do not use a raw GitHub run number in the reserved field. Prereleases and hotfixes need a documented monotonic mapping.

Record app version, package version, source commit/tag, SDK/runtime patch, packaging tool/Windows SDK version and SHA-256 of produced artifacts. Store processing may change package/signature bytes, so distinguish CI artifact hashes from Store-delivered package information. Corresponding-source identity follows the app version and commit, not equality of a pre/post-signing package hash.

## Account setup and automation prerequisites

A developer Partner Center account and reserved product are required. The owner provides identity values and completes agreements, account verification and listing facts. Initial setup can proceed independently from app development but cannot be invented by Codex.

Microsoft's current documented CLI/Actions update route assumes a live Store app and covers free products. Check current official documentation at implementation time. An open-source app can have a price; do not infer price from GPL. If the owner chooses a paid listing or the app is not yet live, document the supported initial/manual/API route before promising the example automation works.

For the documented Microsoft Store publisher/CLI path, required account/service-principal inputs include tenant ID, client ID, client secret and seller ID; the app/product ID is also needed. Configure the documented Partner Center/Entra association and permissions. Store the credential material in protected GitHub secrets, not source or artifacts. Use an officially supported alternative only after verifying support; do not assume generic GitHub OIDC automatically authenticates Partner Center.

No Store certificate secret is required for the Store-only MSIX submission channel. A separate sideload or portable signing channel would have separate requirements and is outside the default release path. Don't add certificate imports or developer-mode setup on the user's managed machine as a Store prerequisite.

## Initial release and update sequence

1. Prepare the product association, listing, screenshots, ratings, support and privacy statements using true app behavior. Editing is offline and template contents are not uploaded by the app; make no broader unverified privacy claims.
2. Build a validated release from an identified commit; publish its freely accessible corresponding-source archive and include GPL/third-party notices. Add explicit source directions in Store metadata and About.
3. Through the separately authorized release process, submit the initial package to Partner Center and track certification. Fix failures without changing the declared feature or license scope silently.
4. After certification/distribution, acquire and test the real Store-installed package. Verify standard-user first launch without .NET, theme/folder persistence, offline editing, review/save, updates and external-file retention.
5. Enable protected automated updates after account/tool prerequisites are met. An upload/publish command starts a Store process; track outcome and do not claim instant deployment.
6. Submit increasing versions for fixes and supported runtime security rebuilds. Store is the packaged update mechanism; no custom in-app updater modifies installed files.

## Sources checked

- Store MSIX signing and identity/package requirements: https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/app-package-requirements
- Store signing versus sideload signing: https://learn.microsoft.com/en-us/windows/msix/package/sign-msix-package-guide
- Store GitHub Actions/CLI update prerequisites: https://learn.microsoft.com/en-us/windows/apps/publish/msstore-dev-cli/github-actions
- Desktop preparation: https://learn.microsoft.com/en-us/windows/msix/desktop/desktop-to-uwp-prepare
- Package versioning: https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/package-version-numbering
- GitHub Actions trust boundaries: https://docs.github.com/en/actions/reference/security/secure-use

Recheck tool versions and service prerequisites during implementation. These notes are release design decisions grounded in the documented services, not evidence that the actual project is already configured.
