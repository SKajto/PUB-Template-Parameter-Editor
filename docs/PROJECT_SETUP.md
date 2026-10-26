# New project setup

> [!NOTE]
> **Channel update, 2026-10-26 (owner decision).** This application is **not** distributed through
> the Microsoft Store: no Store product or identity is reserved, and no artifact of this project is
> Store-signed. The shipped artifact is the self-contained single-file
> `PUB_Template_Parameter_Editor.exe` attached to a GitHub Release, signed by SignPath.io with a
> certificate by SignPath Foundation. The MSIX packaging in `packaging/` remains as local tooling.
> Current wording: [docs/RELEASE_PACKAGING.md](RELEASE_PACKAGING.md) and
> [docs/CODE_SIGNING_POLICY.md](CODE_SIGNING_POLICY.md). The Store-specific lines below are kept as
> written history rather than rewritten after the fact.


Begin in a fresh task directory containing this instruction package. Create the application from its requirements and supplied format references. Keep unrelated user files intact if the directory is not completely empty.

## Create the repository and solution

1. Initialize a local Git repository if one is not present. Use the supplied .gitignore and add generated-artifact ignores as needed. A public GitHub remote can be configured once its real URL is supplied.
2. Select a supported patched .NET 10 SDK on the development/build machine. Record the actual version in global.json and document verified setup/build commands in the new application README.
3. Create the solution and projects below using the supported SDK templates. Use net10.0-windows for App and net10.0 for the portable core/services where practical. Add the appropriate project references.
4. Configure asInvoker, embedded WPF resources, settings paths and the separate portable publish profile. Implement a minimal runnable window before the complete explorer.
5. Establish MSIX packaging and GitHub CI early, using draft identity placeholders only until real Partner Center values are available. A submission build must reject placeholders.
6. Add LICENSE using the recorded GPLv3 choice, accurate copyright details and dependency notices. Do not publish uncleared sample templates or credentials.

| Project or directory | Initial responsibility |
| --- | --- |
| src/PubTemplateEditor.App | WPF entry point, views, view models, commands and themes |
| src/PubTemplateEditor.Core | Declarations, comparison, drafts, review and source patches |
| src/PubTemplateEditor.Infrastructure | File access, settings, hashing, save/recovery and format containers |
| tests/PubTemplateEditor.Core.Tests | Meaningful format/domain tests |
| tests/PubTemplateEditor.Integration.Tests | Temporary-file workflows, conflicts and recovery |
| packaging/ | MSIX manifest/assets and verified packaging configuration |
| .github/workflows/ | CI, release packaging and protected Store submission |
| samples/ | Ignored local native templates for inspection; never a public fixture source by default |
| tests/Fixtures/ | Redistributable synthetic or explicitly cleared test inputs |

The configuration examples are not a runnable scaffold. Adapt them into the created projects and verify the actual commands; do not mark a build passed from the existence of a .csproj example. Test-only packages are permitted and are not shipped as production dependencies.

## Initial evidence and missing inputs

The package includes dated observations about three XML reference files and one synthetic XML illustration. It does not include those original XML files or native DTA bytes. Obtain representative native templates and verify their format before implementing native write support. Use synthetic inputs to implement and test independent behavior while that evidence is unavailable.

The GitHub URL, real copyright holder, actual target PUB version and Store identity/product details must be supplied or selected by the owner. Track missing inputs in PLAN.md with the work they affect. They do not prevent creation of the solution, core models, responsive UI or draft CI/packaging files.

## Source control and milestones

Commit coherent working increments according to project conventions. Record what was executed and demonstrated. The project starts with no completed milestones; update PLAN.md from S0 onward. Building software and preparing workflows are within the implementation task. Creating external accounts, accepting agreements and publishing publicly require a separately authorized release task.
