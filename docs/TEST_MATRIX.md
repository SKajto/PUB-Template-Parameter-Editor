# PUB Template Parameter Editor verification matrix

> [!NOTE]
> **Channel update, 2026-10-26 (owner decision).** This application is **not** distributed through
> the Microsoft Store: no Store product or identity is reserved, and nothing is store-signed. The
> shipped artifact is the self-contained single-file `PUB_Template_Parameter_Editor.exe` attached to a
> GitHub Release, and it carries **no code signature** either - no signing service is available to a
> project this new, so releases publish SHA-256 hashes instead. The MSIX packaging in `packaging/`
> remains local tooling. Current wording: [[docs/RELEASE_PACKAGING.md](RELEASE_PACKAGING.md)]. The store-specific lines below are kept as
> written history rather than rewritten after the fact.


Automate deterministic core and filesystem checks. Exercise workflows through the real WPF application where practical. Perform PUB checks in the actual target PUB environment and packaged runtime checks on the supported Windows baseline and intended managed Windows environment. Maintain requirement traceability as implementation develops.

| Test | Requirements | Procedure and expected result |
| --- | --- | --- |
| T01 Store ordinary-user startup | GEN-01,04,05,07 | Acquire the real Store package on supported Windows 11 x64 without separately installed .NET/SDK or development tools; launch as ordinary user; GUI opens without app authentication, elevation or missing dependencies |
| T02 Preferences | GEN-02, FILE-01 | Choose Dark and a folder, restart, verify both persist; delete/move the folder and verify recoverable selection |
| T03 Explicit reading | FILE-02,03,04,05 | Filter and select templates, press Read; only displayed scope loads; active focus is independent from multi-selection |
| T04 Declaration fidelity | PAR-01 | Independently enumerate declarations; compare GUI counts/names/access/default/description; expression references do not appear as extra variables |
| T05 Filters and navigation | FILE-08, PAR-02,06,07 | Combine text/access filters, apply a draft, switch templates and hide it; draft survives; unapplied form content is not silently lost |
| T06 No unintended writes | PAR-06, BULK-05, SAVE-01 | Hash all originals; load, edit, compare, bulk-stage and enter/leave review; hashes remain identical |
| T07 Value fidelity | PAR-04,05, SAVE-06 | Round-trip empty, spaces, tabs, XML characters, Unicode, multiline text and script-like strings; values preserved logically, none executed |
| T08 Minimal patches | PAR-08, SAVE-06 | Change each supported field separately; only authorized source spans differ; preserve comments, entity representations elsewhere, scripts, images and unknown properties; no-op produces no write |
| T09 Format variants | FILE-06, PAR-08 | Exercise native DTA, exported XML adapter, malformed files, unsupported encodings and complex expressions; correct support status, untouched unsupported content, no app crash |
| T10 Compare | CMP-01..04 | Compare exact name against selected/all loaded templates; verify value/access/description diffs, empty vs missing and visible draft markers |
| T11 Bulk boundaries | BULK-01..05 | Include matching, missing, duplicate-name and pre-edited targets; stage only checked targets; the value, the access and the description are offered as three switches and each target receives only the fields it can hold, with the refused ones named |
| T12 Dirty normalization | PAR-06 | Change and restore a field to baseline; dirty status clears for field, parameter and file when appropriate |
| T13 Review Back | SAVE-01..04 | Edit multiple files/fields, deselect changes, press Back; no disk writes and all proposals remain available |
| T14 Review selection | SAVE-02..06,10 | Exclude description but include value for one variable, save; value changes and description stays baseline; excluded draft clears after complete success |
| T15 Empty delivery | SAVE-05 | Deselect all changes; Save disabled; Back available; no file touched |
| T16 Dirty navigation | FILE-07 | Close, change folder and re-read with drafts; Save routes to review, Discard clears only deliberately, Cancel stays; Back from review never silently discards |
| T17 Concurrent change | SAVE-07,12 | Modify an original externally after load and before save; block overwrite and preserve drafts; exercise a change immediately before replacement where feasible |
| T18 Preflight failure | SAVE-07 | One target is read-only or output invalid; no original in the batch changes and all drafts remain |
| T19 Mid-batch failure | SAVE-08,09,11 | Inject replacement failure after one file; recover where possible, report actual per-file outcomes and preserve unresolved drafts; never claim overall success |
| T20 Interrupted recovery | SAVE-09,11 | Terminate a subprocess between replacements; restart and detect journal; restore/reconcile against backups and fingerprints without silent overwrites |
| T21 PUB compatibility | Release acceptance | Open edited native DTA files in target PUB, generate a representative document, verify intended changes plus retained scripts/images/layout; record version and actual result |
| T22 UI usability | GEN-03 | Test both themes, keyboard focus, labels, resizable panels, readable multiline diffs, busy/progress states and failure messages |
| T23 Optional portable one-file distribution | GEN-04,08 | Enumerate clean publish output; isolate only the EXE and exercise dialogs, both themes and core file workflows. No companion DLL, JSON deployment file, theme, icon, WebView2, package manager or installer is required |
| T24 User-scoped files | GEN-05,10 | Run with executable folder read-only but selected template folder writable. Settings/journals use LocalApplicationData; bundled native extraction uses documented user space without manual environment configuration; app never asks for elevation |
| T25 Managed-device policy | GEN-09 | Run the actual Store-installed app on the target managed Windows machine; record optional portable EXE behavior separately. Record allowed or blocked, event/rule/path and hash. Signing or packaging alone does not count as a passed launch test |
| T26 Domain fixture coverage | Release acceptance | Create redistributable fixtures with independently specified expected declarations, field diffs and save selections. Verify the C# core against them, keep source fixtures immutable and add cases for observed format variants |
| T27 Actual WPF workflow | GEN-03 and feature requirements | Exercise explorer, filters, keyboard focus, field edits, compare, bulk dialog and review in WPF on Windows. A core-library test or WSL console result is not a UI pass |
| T28 Runtime servicing | GEN-04 | Record the SDK/runtime patch bundled into the release. Rebuild the self-contained EXE for runtime security updates and repeat packaged/Store and policy smoke checks; do not assume installing a shared runtime patches the bundle |

| T29 Identity/version validation | REL-02,04,06 | Package from a tagged commit; match real Partner Center identity/publisher/product and version map; reject placeholders, duplicates and decreasing versions |
| T30 CI trust boundary | REL-03,08 | Inspect/run fork PR validation without Store secrets/write token; verify only the restricted trusted release job can access submission credentials |
| T31 Submission signing status | REL-05,11 | Inspect CI MSIX status and actual Store-delivered signature separately; do not label the optional GitHub EXE Microsoft-signed or expect CI MSIX sideload trust |
| T32 Store update lifecycle | REL-07,10 | Create an earlier installed test release of this project and update it to a higher version; preserve settings and selected external templates, use correct package version, and do not write to the package install directory |
| T33 Uninstall retention | REL-07 | Uninstall in an approved test environment; external DTA files survive and settings/backups/journal retention matches the documented behavior |
| T34 Exact-source release | REL-01, LIC-03,04 | Download the linked release source archive and rebuild without maintainer credentials; required project/resource/submodule/build/package inputs exist; map to the binary commit/version |
| T35 Offline notices | LIC-01,02,05 | About/Licenses shows the selected GPL text/copyright/warranty and actual bundled-component notices offline; source URL and app/package versions are accurate |
| T36 Listing/initial setup | REL-09, LIC-03,06 | Verify actual product/price/automation eligibility and first submission steps; listing provides clear source/license directions and no conflicting app-added restrictions |
| T37 CI artifact provenance | REL-02,03 | Trusted release package/source/hash record derives from one selected commit; avoid implicit promotion of untrusted PR artifacts; record Store-modified signature/hash separately |

Performance acceptance should use a representative folder agreed with the user. At minimum, loading the supplied approximately 2 MB XML samples and comparing their roughly 218 declarations must not block normal window interaction. Record measurements before inventing a large-folder speed guarantee.
