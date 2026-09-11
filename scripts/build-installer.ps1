[CmdletBinding()]
param([string]$InnoCompiler = '')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path -LiteralPath (Join-Path $projectRoot 'artifacts/windows-x64/Ter22.Remote.exe'))) {
    throw 'Publica mai intai aplicatia cu scripts/build-windows.ps1.'
}
if (-not $InnoCompiler) {
    $found = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($found) { $InnoCompiler = $found.Source }
    else {
        $paths = @(
            "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
            "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
            "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
        )
        foreach ($candidate in $paths) { if (Test-Path -LiteralPath $candidate) { $InnoCompiler = $candidate; break } }
    }
}
if (-not $InnoCompiler -or -not (Test-Path -LiteralPath $InnoCompiler)) {
    throw 'Executabilul a fost creat. Pentru instalator instaleaza Inno Setup 6 sau specifica -InnoCompiler calea\ISCC.exe.'
}
& $InnoCompiler (Join-Path $projectRoot 'installer/Ter22.Remote.iss')
if ($LASTEXITCODE -ne 0) { throw 'Generarea instalatorului a esuat.' }
Write-Host 'Instalator: artifacts/installer/Ter22-Remote-Setup-0.1.2.exe'
