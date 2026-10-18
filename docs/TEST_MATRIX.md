# PUB Template Parameter Editor verification matrix

Automate deterministic core and filesystem checks. Exercise workflows through the real Electron application where practical. Perform PUB checks in the actual target PUB environment and packaged runtime checks on both OS baselines. Maintain requirement traceability as implementation develops.

| Test | Requirements | Procedure and expected result |
| --- | --- | --- |
| T01 Ordinary-user startup | GEN-01,04,05,07 | Extract each packaged build on supported Windows/Linux without Node/Python installed; launch as ordinary user; GUI opens with no authentication or elevation |
| T02 Preferences | GEN-02, FILE-01 | Choose Dark and a folder, restart, verify both persist; delete/move the folder and verify recoverable selection |
| T03 Explicit reading | FILE-02,03,04,05 | Filter and select templates, press Read; only displayed scope loads; active focus is independent from multi-selection |
| T04 Declaration fidelity | PAR-01 | Independently enumerate declarations; compare GUI counts/names/access/default/description; expression references do not appear as extra variables |
| T05 Filters and navigation | FILE-08, PAR-02,06,07 | Combine text/access filters, apply a draft, switch templates and hide it; draft survives; unapplied form content is not silently lost |
| T06 No unintended writes | PAR-06, BULK-05, SAVE-01 | Hash all originals; load, edit, compare, bulk-stage and enter/leave review; hashes remain identical |
| T07 Value fidelity | PAR-04,05, SAVE-06 | Round-trip empty, spaces, tabs, XML characters, Unicode, multiline text and script-like strings; values preserved logically, none executed |
| T08 Minimal patches | PAR-08, SAVE-06 | Change each supported field separately; only authorized source spans differ; preserve comments, entity representations elsewhere, scripts, images and unknown properties; no-op produces no write |
| T09 Format variants | FILE-06, PAR-08 | Exercise native DTA, exported XML adapter, malformed files, unsupported encodings and complex expressions; correct support status, untouched unsupported content, no app crash |
| T10 Compare | CMP-01..04 | Compare exact name against selected/all loaded templates; verify value/access/description diffs, empty vs missing and visible draft markers |
| T11 Bulk boundaries | BULK-01..05 | Include matching, missing, duplicate-name and pre-edited targets; stage only checked eligible targets and only their defaults; description/access stay unchanged |
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

Performance acceptance should use a representative folder agreed with the user. At minimum, loading the supplied approximately 2 MB XML samples and comparing their roughly 218 declarations must not block normal window interaction. Record measurements before inventing a large-folder speed guarantee.
