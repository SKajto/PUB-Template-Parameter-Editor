# Windows portable publishing and Store channel boundary

> [!NOTE]
> **Channel update, 2026-10-26 (owner decision).** This application is **not** distributed through
> the Microsoft Store: no Store product or identity is reserved, and no artifact of this project is
> Store-signed. The shipped artifact is the self-contained single-file
> `PUB_Template_Parameter_Editor.exe` attached to a GitHub Release, signed by SignPath.io with a
> certificate by SignPath Foundation. The MSIX packaging in `packaging/` remains as local tooling.
> Current wording: [docs/RELEASE_PACKAGING.md](RELEASE_PACKAGING.md) and
> [docs/CODE_SIGNING_POLICY.md](CODE_SIGNING_POLICY.md). The Store-specific lines below are kept as
> written history rather than rewritten after the fact.


## Delivery contract

The new project uses Microsoft Store MSIX as the primary distribution channel; read docs/GITHUB_STORE_RELEASE.md for packaging and submission. This document retains the one-EXE portable build profile. The app remains self-contained and does not require a separately installed .NET Desktop Runtime/SDK. Store packages also contain manifests, visual assets and required notices; they need not be reduced to one internal EXE.

For the portable channel, distribute PUB_Template_Parameter_Editor.exe. Its application resources, project assemblies and runtime are bundled. Native components may extract under the user's %TEMP%/.net directory; settings and journals use user application data. No elevated setup or writes beside the EXE are required. Test packaged mode separately; a portable launch does not prove the Store install/identity lifecycle.

## App project settings

The .csproj.example in examples/ is a reference, not an implemented project. Incorporate its settings into the actual application project, preserving appropriate project references and resource metadata. Core/Infrastructure project assemblies must be bundled, and debug symbol settings must not accidentally create runtime prerequisites. Use patched .NET 10 LTS and set a verified SDK version in global.json during implementation.

```xml
<PropertyGroup>
  <OutputType>WinExe</OutputType>
  <TargetFramework>net10.0-windows</TargetFramework>
  <UseWPF>true</UseWPF>
  <AssemblyName>PUB_Template_Parameter_Editor</AssemblyName>
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  <SelfContained>true</SelfContained>
  <PublishSingleFile>true</PublishSingleFile>
  <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
  <PublishTrimmed>false</PublishTrimmed>
  <PublishAot>false</PublishAot>
  <DebugType>embedded</DebugType>
  <ApplicationManifest>app.manifest</ApplicationManifest>
</PropertyGroup>
```

Embed WPF resources through the appropriate build actions rather than copying external theme/content directories. Do not set IncludeAllContentForSelfExtract as a blanket workaround. Do not turn on trimming or AOT for this WPF baseline. Do not suppress single-file compatibility warnings without understanding and resolving their cause.

## Intended commands

These commands assume the proposed project layout has been created. They were not run when producing this planning package. Adapt them to the real repository and record the commands actually verified. Run publishing in an empty output directory to expose unintended companion files.

```powershell
dotnet --info
dotnet restore src/PubTemplateEditor.App/PubTemplateEditor.App.csproj -r win-x64
dotnet build src/PubTemplateEditor.App/PubTemplateEditor.App.csproj -c Release -r win-x64 --no-restore
dotnet test tests/PubTemplateEditor.Core.Tests/PubTemplateEditor.Core.Tests.csproj -c Release
dotnet test tests/PubTemplateEditor.Integration.Tests/PubTemplateEditor.Integration.Tests.csproj -c Release
dotnet publish src/PubTemplateEditor.App/PubTemplateEditor.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -p:PublishAot=false -p:DebugType=embedded -o artifacts/publish/win-x64
Get-ChildItem artifacts/publish/win-x64 -Recurse -File
Get-FileHash artifacts/publish/win-x64/PUB_Template_Parameter_Editor.exe -Algorithm SHA256
```

Do not substitute dotnet run for published-EXE acceptance. Build success is not a Windows runtime test. A WPF build produced on a non-Windows host also requires real Windows execution; if cross-compilation is used, verify the SDK requirements and record that it did not run the Windows UI.

## Acceptance checks

1. Enumerate the clean publish directory. Explain and resolve every unexpected DLL/config/theme/resource file needed at runtime. Store optional developer symbols and metadata separately.
2. Copy only PUB_Template_Parameter_Editor.exe to an empty directory on a supported Windows machine without separately installed .NET/SDK or development tools. Launch directly by double-click as a standard user.
3. Exercise both themes, native folder selection, persisted preferences, file reading and editing, comparison, bulk staging and review/save using test copies. Run offline.
4. Repeat with the executable directory read-only, leaving user application data, TEMP and the selected template test folder writable. No UAC prompt or manual extraction configuration should be required.
5. Observe runtime extraction and record startup time, artifact size, actual supported OS build and bundled runtime patch. Do not assert zero extracted libraries.
6. Test the same EXE in the intended managed environment under its existing policy. If blocked, record exact event/rule, path, new hash and whether the host EXE or an extracted component was blocked. Diagnose the actual observed result and do not apply device-policy changes yourself.
7. Record available results and unverified gates separately. Continue independent development when managed startup is blocked; release readiness must still state the restriction.

## Signing and servicing

The distribution channel is a GitHub Release carrying the single-file executable, and its signature comes from SignPath.io with a certificate by SignPath Foundation (see docs/RELEASE_PACKAGING.md and docs/CODE_SIGNING_POLICY.md). No publisher CA certificate is bought or supplied by the maintainer, and no artifact of this project carries a package-store signature. An artifact built by CI or by a developer is unsigned until submitted to SignPath, and must never be presented otherwise.

A locally built MSIX is packaged-app testing, not a channel: it is unsigned, its identity placeholders are unresolved on purpose, and offering a package for direct installation would need its own trust setup (certificate matching, side-loading policy) that has not been designed or verified. Do not introduce certificate installation, developer mode or policy changes on a user's managed PC as a prerequisite for the executable; and record accurately whatever environment is used for package testing. Managed-device execution of the released executable is still unverified and remains a test gate.

Rebuild the self-contained payload for supported runtime security patches. Submit a new correctly versioned Store package through the documented release pipeline. Do not implement an updater that modifies files inside the installed MSIX directory. Record source tag, commit, tool/runtime versions and artifact hashes for each channel.

## Official documentation verified 25 October 2026

- .NET single-file deployment, native extraction, resources and symbols: https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview
- .NET support policy, .NET 10 LTS: https://dotnet.microsoft.com/en-us/platform/support/policy
- WPF capabilities and Windows-only platform: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/
- WPF trimming limitations: https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/incompatibilities
- Defender ASR rule reference: https://learn.microsoft.com/en-us/defender-endpoint/attack-surface-reduction-rules-reference

These sources establish platform and deployment behavior. PUB mappings remain grounded in the supplied samples and any additional evidence available in the actual project.
