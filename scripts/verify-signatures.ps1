[CmdletBinding()]
param(
    [switch]$RequireSigned,
    [AllowEmptyString()][string]$ExpectedPublisher = ''
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$projectRoot = Split-Path -Parent $PSScriptRoot
if ($RequireSigned -and [string]::IsNullOrWhiteSpace($ExpectedPublisher)) {
    throw 'Publisherul asteptat este obligatoriu pentru o publicare semnata.'
}
foreach ($path in @('artifacts/windows-x64/Ter22.Remote.exe', 'artifacts/installer/Ter22-Remote-Setup-0.1.2.exe')) {
    $file = Get-Item -LiteralPath (Join-Path $projectRoot $path)
    $signature = Get-AuthenticodeSignature -LiteralPath $file.FullName
    Write-Output "$($file.Name): $($signature.Status)"
    if ($RequireSigned) {
        if ($signature.Status -ne 'Valid' -or $null -eq $signature.SignerCertificate -or
            $null -eq $signature.TimeStamperCertificate) {
            throw "Semnatura sau marca temporala lipseste/nu este valida: $($file.Name)"
        }
        $publisher = $signature.SignerCertificate.GetNameInfo([Security.Cryptography.X509Certificates.X509NameType]::SimpleName, $false)
        if ($publisher -cne $ExpectedPublisher) { throw "Publisher neasteptat pentru $($file.Name)." }
        Write-Output "Publisher: $publisher"
        Write-Output "Certificate thumbprint: $($signature.SignerCertificate.Thumbprint)"
        Write-Output "Timestamp authority: $($signature.TimeStamperCertificate.Subject)"
    }
}
if (-not $RequireSigned) {
    Write-Output 'DEVELOPMENT: semnarea nu este configurata. Nu este o livrare cu publisher verificat.'
}
