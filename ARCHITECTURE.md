# Windows C# application architecture

## Decision

Fresh project baseline: use Store/MSIX as the primary channel. Read docs/GITHUB_STORE_RELEASE.md and docs/GPL_RELEASE.md alongside this architecture. The retained single-EXE profile is separate from MSIX packaging.


Use C# and WPF on .NET 10 LTS, with a net10.0-windows application and a win-x64 self-contained single-file release. Use a supported patched .NET 10 SDK and record its actual version in global.json when implementing. Keep PublishTrimmed=false and PublishAot=false; do not force trimming or Native AOT on WPF to reduce size. The primary MSIX contains a self-contained payload; the separate portable deliverable is one EXE with runtime components bundled. Neither requires manual .NET installation.

Choose WPF because its built-in controls, XAML styling, data binding and resource dictionaries fit a desktop template explorer, editable forms, grouped review and persisted light/dark themes. This is a design decision for the current Windows focus, not a claim that C# automatically changes Defender trust.

No production third-party NuGet packages are required by the planned baseline. Prefer .NET APIs and small application-owned code where practical. Do not introduce custom framework layers or fragile replacement parsers just to avoid a justified dependency: if a real format need exceeds built-in APIs, report it and propose the smallest supported change. Developer-only test packages do not constitute user-installed application dependencies.

## Project structure

| Project or area | Responsibility |
| --- | --- |
| src/PubTemplateEditor.App | WPF views, view models, commands, dialogs, themes and composition |
| src/PubTemplateEditor.Core | Models, declarations, source-preserving patches, comparisons, drafts and review selection; no WPF dependency |
| src/PubTemplateEditor.Infrastructure | Filesystem adapters, settings, hashing, save orchestration and recovery |
| tests/PubTemplateEditor.Core.Tests | Meaningful format, diff, draft and review regression tests |
| tests/PubTemplateEditor.Integration.Tests | Temporary-file saves, conflicts, failure injection and recovery |
| docs | Verified behavior, build commands, project setup, build/release instructions and evidence |

Core and Infrastructure can target net10.0 while the App targets net10.0-windows. They build into internal assemblies that are bundled inside the release EXE; do not distribute them as companion DLLs. A lightweight MVVM approach with INotifyPropertyChanged and ICommand is sufficient. Use dependency injection through constructors/composition without requiring a container package.

## Built-in facilities

| Need | Preferred baseline |
| --- | --- |
| Explorer and parameters | WPF TreeView/ListView and DataGrid with explicit selection semantics |
| Panels | Grid and GridSplitter; keep views resizable and keyboard accessible |
| Themes | Embedded light/dark ResourceDictionaries and DynamicResource bindings |
| Folder dialog | WPF Microsoft.Win32.OpenFolderDialog on the chosen supported .NET version |
| Settings and journals | System.Text.Json and LocalApplicationData/PubTemplateEditor |
| Paths and files | System.IO with explicit read/write errors and same-volume temporary output |
| Content fingerprints | System.Security.Cryptography.SHA256 |
| XML validation and reading | System.Xml XmlReader with DtdProcessing.Prohibit and XmlResolver=null |
| Source preservation | Application-owned XML-aware lexical spans paired with structural validation; never regex-only editing |
| Archive adapter if evidenced | System.IO.Compression, with preservation guarantees verified on actual DTA files |
| UI responsiveness | Async commands, cancellation and background CPU work; marshal bound collection updates to WPF Dispatcher |
| Diff display | Field-level old/new values and line-based highlights rendered by WPF; no browser/editor component |

XmlReader and LINQ to XML do not supply a complete lossless byte-editing solution. Preserve baseline bytes and map structurally verified tokens to exact byte spans. Pay attention to UTF-16 C# string offsets versus UTF-8 byte offsets, encodings, character references, quotes, comments and CDATA. Validate semantic changes with a structural parser, and assert byte identity outside authorized edits. Do not use XDocument.Save as the default whole-template rewrite path.

## Implement the domain behavior

Read docs/PRESERVATION_NOTES.md and REQUIREMENTS.md. Implement and test exact-name matching, access/default/description mapping, value-only bulk staging, dirty normalization, per-field review and accurate save recovery. UI events invoke commands on services; do not place all parsing and saving logic in window code-behind. Template content remains text and must never be evaluated as JavaScript, XAML or executable content.

## User data and startup

Execute asInvoker. Use Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) for settings and recovery data. Do not write preferences beside the EXE, to Program Files or to machine-wide registry keys. Permit template writes only after review Save and only where the user has access. User data files are distinct from distributed application dependencies. In packaged mode, verify actual identity-scoped settings/journal paths, update persistence and uninstall retention; the MSIX installation directory is read-only.

Start without an application service, web server, browser runtime, database, command-line wrapper or special environment variable. Microsoft Store manages installation for the primary MSIX channel; a portable build can still run without installation. The self-contained bundle includes native runtime components using IncludeNativeLibrariesForSelfExtract=true. Microsoft documents Windows extraction under the user's temporary .net directory by default. Test this actual behavior under organization policy rather than assuming it is accepted. Do not change extraction locations to evade policy.

## Publishing and maintenance

Package the self-contained WPF payload as MSIX on a Windows GitHub Actions runner using a verified Windows Application Packaging Project or Windows SDK packaging pipeline. Include the actual Partner Center package identity, visual assets, license notices and the narrowly justified desktop capabilities (including runFullTrust where required). Do not convert the desktop app into UWP solely for packaging. Verify packaged file access and any native extraction. A Store package may contain ordinary payload files even when the separate portable artifact is one EXE; avoid unproven nesting assumptions and test the chosen MSIX payload layout.


Follow docs/WINDOWS_PUBLISH.md and validate a clean publish directory. Embed XAML/resources and managed dependencies; embed debug symbols or retain separate developer symbols outside the user deliverable. Do not add an application plugin loader. Use normal FileStream/Windows filesystem APIs for saving, with documented per-file replacement guarantees and explicit backup/journal recovery.

A self-contained release bundles its runtime, so security servicing requires rebuilding and redistributing the EXE with the supported patched runtime. A shared .NET runtime update does not patch the bundled copy. Record the release runtime version, commit/source tag, package identity/version, artifact hashes and actual Store-installed policy-test outcome. The Store signs MSIX after certification; preserve the separate result for optional GitHub EXE distribution.

## Platform boundary

WPF only runs on Windows. Linux and WSL may be used for portable-core tests where feasible, but cannot prove WPF startup or close Windows acceptance gates. Linux UI implementation, alternative rendering toolkits and additional CPU architectures are deferred.
