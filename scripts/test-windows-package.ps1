[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
    throw 'Verificarea pachetului se executa pe Windows.'
}
$projectRoot = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $projectRoot 'artifacts'
$setup = Join-Path $artifacts 'installer/Ter22-Remote-Setup-0.1.2.exe'
$published = Join-Path $artifacts 'windows-x64/Ter22.Remote.exe'
$installDir = Join-Path ([IO.Path]::GetTempPath()) ('DeskTer-Package-Test-' + [Guid]::NewGuid().ToString('N'))
$installLog = Join-Path $artifacts 'install-smoke.log'
$uninstallLog = Join-Path $artifacts 'uninstall-smoke.log'
$app = $null
$firewallHelper = $null
$firewallRule = $null

try {
    $arguments = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-', '/LANG=romanian',
        "/DIR=`"$installDir`"", "/LOG=`"$installLog`"")
    $installer = Start-Process -FilePath $setup -ArgumentList $arguments -Wait -PassThru
    if ($installer.ExitCode -ne 0) { throw "Instalarea pachetului a esuat: $($installer.ExitCode)" }
    $installed = Join-Path $installDir 'Ter22.Remote.exe'
    if (-not (Test-Path -LiteralPath $installed)) { throw 'Aplicatia nu a fost instalata.' }
    if ((Get-FileHash -LiteralPath $installed -Algorithm SHA256).Hash -ne
        (Get-FileHash -LiteralPath $published -Algorithm SHA256).Hash) {
        throw 'Executabilul instalat difera de cel publicat.'
    }
    Write-Host 'PASS instalare: executabilul instalat are acelasi SHA-256 ca publicarea.'

    $app = Start-Process -FilePath $installed -WorkingDirectory $installDir -PassThru
    if (-not $app.WaitForInputIdle(30000)) { throw 'Interfata nu a devenit disponibila in 30 de secunde.' }
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    do {
        $app.Refresh()
        if ($app.HasExited) { throw "Aplicatia s-a inchis la pornire: $($app.ExitCode)" }
        if ($app.MainWindowHandle -ne [IntPtr]::Zero) { break }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    if ($app.MainWindowHandle -eq [IntPtr]::Zero -or $app.MainWindowTitle -ne 'Ter22 Remote') {
        throw 'Fereastra principala a aplicatiei nu a fost gasita.'
    }
    if (-not $app.Responding) { throw 'Fereastra aplicatiei nu raspunde.' }
    if (-not $app.CloseMainWindow() -or -not $app.WaitForExit(15000)) {
        throw 'Aplicatia nu s-a inchis normal.'
    }
    if ($app.ExitCode -ne 0) { throw "Aplicatia s-a inchis cu eroare: $($app.ExitCode)" }
    Write-Host 'PASS pornire: fereastra principala raspunde si se inchide fara eroare.'

    # The hosted runner is elevated. Exercise the production helper with the installed
    # single-file executable, then remove only the uniquely named test rule in finally.
    $testPort = 45990
    $pathBytes = [Text.Encoding]::UTF8.GetBytes([IO.Path]::GetFullPath($installed).ToUpperInvariant())
    $pathId = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($pathBytes)).Substring(0, 16)
    $firewallRule = "Desk Ter LAN-VPN $pathId TCP $testPort"
    $firewallHelper = Start-Process -FilePath $installed -ArgumentList @('--configure-firewall', "$testPort") -PassThru
    if (-not $firewallHelper.WaitForExit(20000)) { throw 'Configurarea firewallului nu s-a incheiat.' }
    if ($firewallHelper.ExitCode -ne 0) { throw "Configurarea firewallului a esuat: $($firewallHelper.ExitCode)" }
    $rule = Get-NetFirewallRule -PolicyStore PersistentStore -DisplayName $firewallRule -ErrorAction Stop
    $programFilter = $rule | Get-NetFirewallApplicationFilter
    $portFilter = $rule | Get-NetFirewallPortFilter
    $addressFilter = $rule | Get-NetFirewallAddressFilter
    if ($programFilter.Program -ne $installed -or $portFilter.Protocol -ne 'TCP' -or
        $portFilter.LocalPort -ne "$testPort" -or $rule.Action -ne 'Allow' -or
        $rule.Direction -ne 'Inbound' -or $rule.Enabled -ne 'True' -or
        $addressFilter.RemoteAddress -contains 'Any') {
        throw 'Regula firewall nu este restransa la aplicatie, port si surse LAN/VPN.'
    }
    Write-Host 'PASS firewall: helperul executabilului instalat configureaza regula TCP limitata la aplicatie si surse LAN/VPN.'

    $uninstaller = Join-Path $installDir 'unins000.exe'
    $uninstall = Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/LOG=`"$uninstallLog`"") -Wait -PassThru
    if ($uninstall.ExitCode -ne 0) { throw "Dezinstalarea a esuat: $($uninstall.ExitCode)" }
    if (Test-Path -LiteralPath $installed) { throw 'Dezinstalarea a lasat executabilul in directorul de instalare.' }
    Write-Host 'PASS dezinstalare: executabilul a fost eliminat.'
} finally {
    if ($null -ne $firewallHelper) {
        if (-not $firewallHelper.HasExited) { Stop-Process -Id $firewallHelper.Id -Force }
        $firewallHelper.Dispose()
    }
    if ($null -ne $firewallRule) {
        Get-NetFirewallRule -PolicyStore PersistentStore -DisplayName $firewallRule -ErrorAction SilentlyContinue |
            Remove-NetFirewallRule -ErrorAction Stop
    }
    if ($null -ne $app) {
        if (-not $app.HasExited) { Stop-Process -Id $app.Id -Force }
        $app.Dispose()
    }
}
