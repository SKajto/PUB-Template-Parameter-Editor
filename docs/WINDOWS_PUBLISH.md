# Publishing the Windows executable

The shipped artifact is the self-contained single-file `PUB_Template_Parameter_Editor.exe`, attached to
a GitHub Release together with its SHA-256 and the corresponding source archive. It needs no installer,
no service, no elevation and no separately installed .NET runtime.

## Project settings

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

WPF is not trimming- or AOT-supported, so those stay off; `PublishProfile=PortableWin_x64` carries the
same switches. Embed resources through the build actions instead of shipping content directories, and
do not suppress single-file warnings without resolving their cause. Native components extract under
the user's `%TEMP%\.net`; settings and journals live under user application data — which is why the
executable never claims to run with no runtime files at all.

## Commands actually used

```powershell
dotnet build PubTemplateEditor.slnx -c Release
dotnet test  PubTemplateEditor.slnx -c Release
dotnet publish src/PubTemplateEditor.App/PubTemplateEditor.App.csproj `
  -c Release -p:PublishProfile=PortableWin_x64 `
  -p:Version=<v> -p:AssemblyVersion=<v> -p:FileVersion=<v> -p:InformationalVersion=<v> `
  -p:IncludeSourceRevisionInInformationalVersion=false `
  -o artifacts/publish/portable-win-x64
Get-ChildItem artifacts/publish/portable-win-x64 -Recurse -File
Get-FileHash artifacts/publish/portable-win-x64/PUB_Template_Parameter_Editor.exe -Algorithm SHA256
```

Publish into an empty directory so unintended companion files show up, and read the version back out of
the file's metadata (`release.yml` asserts it). `dotnet run` is not acceptance: build success is not a
Windows run, and a WPF build from a non-Windows host still needs real Windows execution.

## Acceptance checks

1. Enumerate the publish directory; explain every file that has to be there. No loose `.pdb` or `.xml`.
2. Copy only the exe to an empty directory on a supported Windows machine with no .NET installed and
   start it by double-click as a standard user.
3. Both themes, folder selection, persisted preferences, read/compare/bulk/review-save — offline, on
   test copies.
4. Same with the program directory read-only (user data, TEMP and the test folder writable): no UAC
   prompt, no manual extraction configuration.
5. Record startup time, artifact size, OS build, runtime patch level and what was extracted.
6. Run it in the intended managed environment under its existing policy; if blocked, record the exact
   event/rule, path, hash and whether the exe or an extracted file was stopped. Do not change device
   policy to make a test pass.
7. If it dies on another machine with no message, run `tools/diagnostics/crash-evidence.ps1` there
   (read-only: WER reports, the Application event log, the payload's own version and signature, the
   user's settings, error-reporting policy) and record the faulting module before changing code. A managed
   fault now reaches `%LOCALAPPDATA%\PubTemplateEditor\crash.log` with its type, message and stack, which
   is what identified the folder-dialog fault; a fault that ends the process inside native shell code
   would leave only the WER and event-log entries, so collect both.
8. Keep observed results and open gates separate.

## Signing and servicing

The released executable is signed with the one certificate recorded in
`packaging/CertumSigning.settings.json`, in a maintainer's session; the build this document describes,
and every CI build, is unsigned. SmartScreen may still prompt for a signed file that has no reputation
yet, so the release publishes the SHA-256 of the signed file to check it against, and
[CODE_SIGNING.md](CODE_SIGNING.md) covers the tooling and the certificate's expiry. Rebuild and
re-publish this payload for runtime security patches — there is no updater and nothing writes into the
program directory.

## Platform documentation checked 2026-10-25

- .NET single-file deployment and native extraction: https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview
- .NET 10 LTS support policy: https://dotnet.microsoft.com/en-us/platform/support/policy
- WPF (Windows-only): https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/
- Trimming limitations: https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/incompatibilities
- Defender ASR rules: https://learn.microsoft.com/en-us/defender-endpoint/attack-surface-reduction-rules-reference
