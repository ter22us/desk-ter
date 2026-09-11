[CmdletBinding()]
param(
    [switch]$Installer,
    [string]$InnoCompiler = ''
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
    throw 'Acest script se executa pe Windows x64.'
}
if (-not [Environment]::Is64BitOperatingSystem) { throw 'Este necesar Windows x64.' }
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'Instaleaza .NET SDK 10 x64 de la https://dotnet.microsoft.com/download/dotnet/10.0 si redeschide PowerShell.'
}
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    & dotnet --version
    if ($LASTEXITCODE -ne 0) { throw 'Nu este disponibil un SDK .NET 10 stabil compatibil cu global.json.' }
    & dotnet run --project tests/Ter22.Tests/Ter22.Tests.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Testele au esuat. Nu se genereaza instalatorul.' }
    $output = Join-Path $projectRoot 'artifacts/windows-x64'
    & dotnet publish src/Ter22.Windows/Ter22.Windows.csproj -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false `
        -p:DebugType=none -o $output
    if ($LASTEXITCODE -ne 0) { throw 'Compilarea aplicatiei Windows a esuat.' }
    Copy-Item -LiteralPath (Join-Path $projectRoot 'GHID_RO.md') -Destination $output -Force
    if ($Installer) {
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
        Write-Host 'Instalator: artifacts/installer/Ter22-Remote-Setup-0.1.1.exe'
    }
    Write-Host 'Aplicatie: artifacts/windows-x64/Ter22.Remote.exe'
} finally { Pop-Location }
