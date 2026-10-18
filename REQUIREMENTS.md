# PUB Template Parameter Editor requirements

Status: development baseline draft, 15 October 2026. The user description is authoritative. Added interpretations below are implementation defaults that can be revised without changing the stated user goals.

## Purpose and scope

Provide a local GUI for inspecting, comparing, and editing declared internal and external variables in IBM PUB templates. Support changing a variable's default value, access classification, and description. Support copying a default value to matching variables across multiple templates. Require a separate review and explicit save before changing template files.

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
| Settings | Persist theme and last folder immediately in the user's settings location; do not persist draft values automatically |

## General requirements

| ID | Requirement and acceptance criterion |
| --- | --- |
| GEN-01 | Open directly to the GUI without authentication, account setup, or a network service |
| GEN-02 | Provide Light and Dark themes. Changing theme persists the choice across restart; first launch may follow the OS preference |
| GEN-03 | Use a modern, consistent desktop layout with resizable panels, readable typography, visible focus, and clear empty, loading, error, and dirty states |
| GEN-04 | Provide Windows and Linux release packages that can be extracted and run as an ordinary user on documented supported systems, without installing Node.js, Python, a development server, or an application database |
| GEN-05 | Require no admin/root privileges for ordinary installation, startup, settings, reading, or saving files that the user can access |
| GEN-06 | Perform the requested functions offline. Bundle application assets locally and keep settings in a user-writable location |
| GEN-07 | Document supported OS versions, architecture, and Linux system library assumptions. Validate packaged releases on those baselines |

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
| PAR-02 | Provide a case-insensitive search across parameter names and descriptions plus an access filter All, Internal, External. Plain name queries match from the start of the name, not an interior substring; a name containing the query only after a prefix does not match solely because its description contains it. `*` matches any-length text, so `param*Attribute` matches `param01Attribute`, `param02Attribute`, and similar names. Description matches may occur anywhere. Combine filters and display matched and total counts |
| PAR-03 | Selecting a parameter displays its read-only name, editable description, editable default value, and Internal/External selector |
| PAR-04 | Preserve exact logical default values, including empty strings, leading/trailing whitespace, line breaks, Unicode, quotes, ampersands, and XML-sensitive characters. Do not trim or coerce values |
| PAR-05 | Treat a default that looks like JavaScript as text; never execute template code or variable content |
| PAR-06 | Stage edits in memory only and mark changed fields, parameters, templates, and the overall workspace. Show an Unsaved marker beside each changed parameter until its changes are saved, discarded, or restored to baseline; restoring a field to baseline removes its dirty state |
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
| SAVE-10 | On complete success, verify resulting files, update baselines, clear included drafts and revert excluded drafts, show saved template and change counts in a dismissible or auto-expiring confirmation, then return to the main explorer |
| SAVE-11 | On cancel, conflict, failure, or incomplete recovery, retain drafts and review choices for unresolved files. Reconcile working baselines with actual disk state before any retry |
| SAVE-12 | Block overwriting files externally changed after load; provide a reload/compare workflow without silently dropping local drafts |

## Release acceptance

All requirements above are part of the intended first complete release. Milestones may expose smaller working subsets without calling them the finished product. Release requires passing docs/TEST_MATRIX.md, real PUB compatibility checks, and ordinary-user runtime tests on both supported OS baselines. Mark unavailable validations as unverified, with the reason.
