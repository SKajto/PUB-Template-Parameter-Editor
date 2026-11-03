# Read-only crash evidence. Writes nothing. Run as the normal user, right after reproducing the crash.
$app = 'PUB_Template_Parameter_Editor'

'== Windows Error Reporting reports'
foreach ($d in "$env:LOCALAPPDATA\Microsoft\Windows\WER\ReportArchive",
               "$env:LOCALAPPDATA\Microsoft\Windows\WER\ReportQueue",
               "$env:ProgramData\Microsoft\Windows\WER\ReportArchive",
               "$env:ProgramData\Microsoft\Windows\WER\ReportQueue") {
    Get-ChildItem -LiteralPath $d -Recurse -File -Filter '*.wer' -ErrorAction SilentlyContinue |
        Where-Object { (Get-Content -LiteralPath $_.FullName -Raw -ErrorAction SilentlyContinue) -match $app } |
        ForEach-Object {
            '--- ' + $_.FullName
            Get-Content -LiteralPath $_.FullName |
                Where-Object { $_ -match 'AppName|AppVersion|Mod Name|Mod Version|Mod Path|Exception Code|Offset|Report Version' }
        }
}

'== Application event log (last 48 h, entries naming the application)'
Get-WinEvent -FilterHashtable @{ LogName = 'Application'; StartTime = (Get-Date).AddHours(-48) } -ErrorAction SilentlyContinue |
    Where-Object { $_.Message -match $app } |
    Select-Object -First 12 |
    ForEach-Object {
        '--- ' + $_.TimeCreated + '  ' + $_.ProviderName + '  id ' + $_.Id
        ($_.Message -split "`r?`n" | Select-Object -First 14)
    }

'== the folder where the executable is, and what the payload says about itself'
Get-ChildItem -LiteralPath "$env:USERPROFILE" -Filter "$app.exe" -Recurse -Depth 4 -ErrorAction SilentlyContinue |
    Select-Object -First 3 |
    ForEach-Object {
        $_.FullName
        '  bytes      : ' + $_.Length
        '  fileVersion: ' + $_.VersionInfo.FileVersion
        '  signature  : ' + (Get-AuthenticodeSignature $_.FullName).Status
    }

'== app settings (user-scoped; the working folder and the recent list)'
$s = Join-Path $env:LOCALAPPDATA 'PubTemplateEditor\settings.json'
if (Test-Path -LiteralPath $s) { 'path: ' + $s; Get-Content -LiteralPath $s -Raw } else { 'no settings file yet: ' + $s }

'== Windows error-reporting policy that could hide the report'
foreach ($k in 'HKLM:\SOFTWARE\Microsoft\Windows\Windows Error Reporting',
               'HKCU:\SOFTWARE\Microsoft\Windows\Windows Error Reporting') {
    if (Test-Path -LiteralPath $k) {
        '--- ' + $k
        (Get-ItemProperty -LiteralPath $k |
            Format-List DontShowUI, Disabled,DWerv, DumpFolder, DumpType, DebugPopupsEnabled -ErrorAction SilentlyContinue | Out-String).Trim()
    }
}
