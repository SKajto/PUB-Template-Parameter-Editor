# PUB Template Parameter Editor requirements

> [!NOTE]
> **Channel update, 2026-10-26 (owner decision).** This application is **not** distributed through
> the Microsoft Store: no Store product or identity is reserved, and no artifact of this project is
> Store-signed. The shipped artifact is the self-contained single-file
> `PUB_Template_Parameter_Editor.exe` attached to a GitHub Release, signed by SignPath.io with a
> certificate by SignPath Foundation. The MSIX packaging in `packaging/` remains as local tooling.
> Current wording: [docs/RELEASE_PACKAGING.md](RELEASE_PACKAGING.md) and
> [docs/CODE_SIGNING_POLICY.md](CODE_SIGNING_POLICY.md). The Store-specific lines below are kept as
> written history rather than rewritten after the fact.


Status: fresh Windows C# project baseline, revision 4, 25 October 2026. Build the application from the requirements below; no implementation or completed checks are assumed. The user description is authoritative. Added interpretations are explicit implementation defaults.

## Purpose and scope

Provide a local Windows desktop GUI, implemented in C# with WPF on .NET 10 LTS, for inspecting, comparing, and editing declared internal and external variables in IBM PUB templates. Windows 11 x64 is the first release target. Linux is deferred and is not a first-release acceptance gate; keep the domain core independent of WPF where practical. Support changing a variable's default value, access classification, and description. Support copying a default value to matching variables across multiple templates. Require a separate review and explicit save before changing template files.

The displayed term Parameter means a declared variable. It does not mean an expression reference or a document generation runtime value. Parameter names are read-only in the first release. Creating, deleting, and renaming declarations, editing expressions, template layout editing, PUB execution, and server integration are outside the requested scope.

## Explicit interpretations

| Topic | Proposed behavior |
| --- | --- |
| Value | Declared default value, not a value assigned by a template script during generation |
| Type | UI label Internal or External maps to the declaration's access property; the separate XML type property is preserved |
| Matching variable | Exact, case-sensitive declared name in the supported declaration scope; ambiguous duplicate names are not bulk-editable |
| Bulk editing | Copy only the value. Access classification and description remain per-template edits |
| Comparison fields | Compare value, access classification, and description; present only actual differences and a distinct missing state |
| Review granularity | One selectable change per template, variable, and edited field |
| Deselect in review | Exclude from delivery and revert that field to its baseline only when a save completes successfully; Back never discards the draft |
| Compare drafts | Compare current working values, marking unsaved values visibly; retain disk baseline for save review |
| Successful save | Return to the main explorer, preserve folder and loaded files, refresh baselines, and clear delivered or explicitly reverted changes |
| Startup | Restore theme and folder; list files but wait for Read parameters before parsing declarations |
| Folder depth | List templates directly in the selected folder. Recursive traversal is a later option |
| Settings | Persist theme and last folder immediately under the user's LocalApplicationData directory; do not persist draft values automatically |
| Distribution | Primary channel is Microsoft Store MSIX with self-contained WPF payload and Store-managed installation/updates. Retain a one-EXE publish profile for development or optional GitHub portable releases; it is not Store-signed automatically |
| License | GPL version 3. Use GPL-3.0-only provisionally unless the owner explicitly selects GPL-3.0-or-later |
| Windows focus | Windows 11 x64 first. WPF is Windows-only; Linux support is deferred, not implicitly retained |

## General requirements

| ID | Requirement and acceptance criterion |
| --- | --- |
| GEN-01 | Open directly to the GUI without authentication, account setup, or a network service |
| GEN-02 | Provide Light and Dark themes. Changing theme persists the choice across restart; first launch may follow the OS preference |
| GEN-03 | Use a modern, consistent desktop layout with resizable panels, readable typography, visible focus, and clear empty, loading, error, and dirty states |
| GEN-04 | Deliver the Windows x64 WPF application through Microsoft Store as MSIX with a self-contained .NET payload, requiring no separately installed .NET Desktop Runtime/SDK, Node.js, Python, browser runtime, database or manual dependency setup. Retain the single-file PubTemplateEditor.exe publish profile for development or optional portable distribution |
| GEN-05 | Run as the current Windows user with an asInvoker manifest. Require no app-requested UAC elevation, service registration, or administrator rights for app operation. Support standard-user Store installation subject to the organization's Store/deployment policies |
| GEN-06 | Perform the editing functions offline. Bundle application assets locally and keep settings in a user-writable location. Store acquisition and platform-managed updates may use the network; the application does not upload template content |
| GEN-07 | Document the tested supported Windows 11 version/build and x64 architecture. Prove Store-installed execution on Windows without an installed .NET runtime or SDK and separately verify the retained portable profile; Linux/WSL checks do not establish Windows GUI compatibility |
| GEN-08 | Embed application themes/resources where appropriate and include Store-required manifest/visual assets and license notices in MSIX. Verify no manual runtime/dependency installation. For optional portable delivery, verify one-EXE execution from an otherwise empty directory; a real MSIX package includes more than an EXE |
| GEN-09 | Test the actual Store-installed package under the target organization's existing policy and record portable-EXE results separately. Packaging or signing alone does not establish acceptance under the target device policy. If blocked, record evidence and continue independent work while the managed-device acceptance gate remains open |
| GEN-10 | Permit documented user-scoped settings, backups, journals, and .NET native bundle extraction. Do not require write access beside the executable or a custom extraction environment variable. A blocked extraction location is a failed environment check, not permission to weaken policy |

## Folder and template explorer

| ID | Requirement and acceptance criterion |
| --- | --- |
| FILE-01 | Provide a native folder picker and restore the last chosen folder at next startup. A missing or inaccessible remembered folder produces a clear message and permits choosing another |
| FILE-02 | List `.dta` templates in the selected folder in a left sidebar resembling a code editor explorer; sort predictably and show filenames, selection controls, parse status, and unsaved indicators |
| FILE-03 | Allow template filtering by filename and explicit multi-selection for reading. Provide Select all and Clear selection for the currently filtered list; show the resulting selected count |
| FILE-04 | Read declarations only after the user presses Read parameters. Read the selected templates; default to all visible templates if no explicit reading selection exists, and show that scope before execution |
| FILE-05 | Distinguish active template, templates selected for reading, templates selected for comparison, and bulk targets. Focusing a template must not silently change bulk targets |
| FILE-06 | Record per-file read or parse errors without suppressing successfully loaded templates. Keep the GUI responsive during batch reading and show progress |
| FILE-07 | When re-reading files, switching folders, or closing the application would discard drafts, offer Save for review, Discard, and Cancel. Returning from review must not accidentally complete the pending destructive navigation |
| FILE-08 | Preserve drafts when a template becomes hidden by a filter or another template becomes active |

## Parameter browser and editor

| ID | Requirement and acceptance criterion |
| --- | --- |
| PAR-01 | Display declared variables of the active loaded template in the main panel; do not list script expression references as extra declarations |
| PAR-02 | Provide a case-insensitive text search across name and description plus an access filter All, Internal, External. Combine filters and display matched and total counts |
| PAR-03 | Selecting a parameter displays its read-only name, editable description, editable default value, and Internal/External selector |
| PAR-04 | Preserve exact logical default values, including empty strings, leading/trailing whitespace, line breaks, Unicode, quotes, ampersands, and XML-sensitive characters. Do not trim or coerce values |
| PAR-05 | Treat a default that looks like JavaScript as text; never execute template code or variable content |
| PAR-06 | Stage edits in memory only and mark changed fields, parameters, templates, and the overall workspace. Restoring a field to baseline removes its dirty state |
| PAR-07 | Use an explicit Apply edits action to stage local editor values. Navigation must not silently drop unapplied form edits; offer Apply, Discard, Cancel as applicable |
| PAR-08 | Preserve unsupported properties and unusual XML constructs. Show affected fields read-only with a specific explanation when lossless editing has not been established |

## Comparison and bulk changes

| ID | Requirement and acceptance criterion |
| --- | --- |
| CMP-01 | Provide Compare for the selected variable against selected loaded templates or all loaded templates. Indicate when folder templates are not loaded and provide a route to read them |
| CMP-02 | Match exact declared names and show a table with template, comparison status, and field differences against the active template. Explicitly distinguish missing declarations from empty values |
| CMP-03 | Show multiline changes as readable old/new or inline diffs. Avoid silently ignoring whitespace or differences in letter case |
| CMP-04 | Mark values derived from unsaved drafts. Comparisons do not change files or create declarations |
| BULK-01 | Provide Apply value to templates for the selected parameter and its proposed value |
| BULK-02 | List loaded templates with one unambiguous declaration of that name. Show current/proposed values and checkboxes; missing, invalid, or ambiguous candidates are ineligible and explained |
| BULK-03 | Start with no targets selected. Allow individual selection and explicit Select all eligible targets; show target count. The current template is listed and clearly marked, with no implicit inclusion |
| BULK-04 | On Apply changes, stage the value only in the selected targets. Leave descriptions, access classifications, and other variables unchanged. Show a result count |
| BULK-05 | Do not write any file during bulk staging. If a target already has a different staged value, show that proposed replacement in the target preview |

## Review and saving

| ID | Requirement and acceptance criterion |
| --- | --- |
| SAVE-01 | Main Save opens review of every pending change in the workspace. No template is written on entry |
| SAVE-02 | Review groups changes by template and parameter and shows old value, new value, field, and a delivery checkbox. All pending changes start selected on first entry |
| SAVE-03 | Allow individual field changes to be selected or deselected. Clearly state that deselected changes will revert on a successful save. Show delivery and revert counts |
| SAVE-04 | Back returns to the prior editing context and preserves all drafts. Preserve review selection during that review/editing session; new or changed field proposals require renewed review |
| SAVE-05 | Review Save is the only action that writes templates. If no changes are selected, disable Save; offer Back so the user can revise the selection or discard through the normal workflow |
| SAVE-06 | Save only the included changes. Preserve all unrelated source bytes and container content within the supported format's proven preservation guarantees |
| SAVE-07 | Before writing, verify permissions, detect on-disk changes since load using a content fingerprint, build and validate all proposed outputs, and create recoverable original backups. A preflight error writes no templates |
| SAVE-08 | Use safe per-file replacement with staged temporary files on the same filesystem. Do not describe multiple file replacements as one globally atomic filesystem transaction |
| SAVE-09 | Maintain a recovery journal and backups for a save batch. On an error during replacement, attempt restoration and show each file's actual saved, restored, failed, or uncertain status. Retain recoverable work and do not display overall success |
| SAVE-10 | On complete success, verify resulting files, update baselines, clear included drafts and revert excluded drafts, show saved template and change counts, then return to the main explorer |
| SAVE-11 | On cancel, conflict, failure, or incomplete recovery, retain drafts and review choices for unresolved files. Reconcile working baselines with actual disk state before any retry |
| SAVE-12 | Block overwriting files externally changed after load; provide a reload/compare workflow without silently dropping local drafts |

## Release acceptance

All requirements above are part of the intended first complete release. Milestones may expose smaller working subsets without calling them the finished product. Release requires passing docs/TEST_MATRIX.md, real PUB compatibility checks, and Store-installed standard-user runtime tests on supported Windows without .NET, packaged lifecycle tests, and the intended managed Windows environment. Optional portable delivery has a separate one-EXE acceptance check. Release also requires the licensing and GitHub/Store controls below. Mark unavailable validations as unverified, with the reason.


## GitHub and Microsoft Store release requirements

| ID | Requirement and acceptance criterion |
| --- | --- |
| REL-01 | Maintain source and build instructions in the project's public GitHub repository. Record a commit/tag for each binary release and publish its exact corresponding source archive including required build/packaging scripts |
| REL-02 | GitHub Actions on Windows builds/tests the C# app and creates the Store submission package reproducibly from the selected commit. Record SDK, Windows SDK/packaging tool versions, artifact hashes and manifest version |
| REL-03 | Separate PR validation, release packaging and Store submission workflows/jobs. PR code never receives Store credentials or release write permissions; submission uses a trusted selected release and restricted environment |
| REL-04 | Associate MSIX Identity Name, Publisher and Store product ID with the real reserved Partner Center product. Use placeholders only in drafts and reject them in a submission build; do not invent values |
| REL-05 | Submit MSIX through the Microsoft Store route. Store signing occurs after certification; a CA-trusted signing certificate is not required for that submission path. A pre-Store CI artifact is not described as a Microsoft-signed installable release |
| REL-06 | Preserve a monotonic Store-compatible four-part package version, document its mapping from release tags, and validate identity/architecture/OS requirements. Retain 0 in the fourth part for the documented Store scheme unless current applicable guidance establishes otherwise |
| REL-07 | Test actual Store acquisition, first launch, ordinary-user operation, offline editing, upgrades and uninstall behavior. Preserve external templates across update/uninstall and document settings/backup retention. Store certification success alone is not application acceptance |
| REL-08 | Use Partner Center setup and release/submission credentials held in protected GitHub secrets or an officially supported equivalent; never commit credentials or require them to build the application source |
| REL-09 | Prepare the first Store submission and listing through Partner Center. Verify current automation prerequisites before enabling update publishing; the documented Microsoft CLI route currently assumes a live app and free product. Do not assume open source implies a free Store price |
| REL-10 | Store handles packaged installation and updates. Do not implement an in-app updater that rewrites the MSIX installation directory. Rebuild the self-contained payload for .NET security updates |
| REL-11 | Clearly distinguish Store-signed MSIX delivery, unsigned/otherwise signed CI submission artifacts and optional GitHub portable EXE releases. Store signing does not automatically Authenticode-sign a separate GitHub EXE or make a sideload package trusted |

## Open source licensing requirements

| ID | Requirement and acceptance criterion |
| --- | --- |
| LIC-01 | Apply the owner's GPLv3 choice consistently in the actual repository license text, source notices and metadata; use GPL-3.0-only provisionally unless the owner explicitly selects GPL-3.0-or-later |
| LIC-02 | Include the complete applicable GPL text and accurate copyright/warranty notices in the distributed app or package, accessible from About/Licenses without network access |
| LIC-03 | Link the Store listing and About view clearly to freely accessible exact-version corresponding source and build/install instructions. A moving default-branch link or opaque binary-only release is insufficient as the chosen release practice |
| LIC-04 | Publish the source/build/packaging materials needed to rebuild and modify the release without maintainer secrets. Include required submodule/generated-source inputs; document independent build/package identities without requiring the maintainer's Store credentials |
| LIC-05 | Retain licenses/notices for bundled .NET, WPF and other components and validate compatibility of any added dependency or asset. Do not label every bundled component GPL merely because the app is GPL |
| LIC-06 | Do not add an app EULA, DRM or license check restricting GPL-granted use, modification or redistribution. Verify the applicable Store distribution terms and any GPLv3 installation-information obligations for the actual arrangement before release; do not assert blanket legal compliance from a public repo alone |
