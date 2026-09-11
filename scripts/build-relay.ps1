[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    & dotnet publish src/Ter22.Relay/Ter22.Relay.csproj -c Release -r linux-x64 --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none `
        -o artifacts/relay-linux-x64
    if ($LASTEXITCODE -ne 0) { throw 'Compilarea releului a esuat.' }
    Write-Host 'Releu Linux: artifacts/relay-linux-x64/Ter22.Relay'
} finally { Pop-Location }
