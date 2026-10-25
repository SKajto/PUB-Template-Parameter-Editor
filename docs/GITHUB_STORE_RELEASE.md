# GitHub Actions and Microsoft Store release design

Fresh project release design, 25 October 2026. This is an implementation specification for Agent in the real GitHub repository. No workflow is deployed and no Store account or product is configured by this planning package.

## Channels and signatures

The primary channel is Microsoft Store MSIX, containing a self-contained WPF/.NET 10 payload. Microsoft signs/re-signs the package after certification; the Store handles customer acquisition, installation and packaged updates. This channel does not require the maintainer to buy or supply a CA-trusted code-signing certificate for submission.

MSIX is a packaging format, not a signing authority. The CI package before Store certification is not automatically Microsoft-signed or trusted for arbitrary sideload installation. The Store package signature covers the package integrity; it does not imply every inner PE has a separate Microsoft Authenticode signature. A GitHub portable EXE does not inherit a Store signature. Do not advertise a raw CI MSIX as an ordinary-user signed installer.

Store installation still depends on device configuration. Verify managed-device acquisition and execution rather than promising Store trust overrides ASR, WDAC/App Control, disabled Store access or other enterprise controls. Record the actual package identity, artifact hashes and observed policy outcome for this project.

## Build and packaging implementation

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
