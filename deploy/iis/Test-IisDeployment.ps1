#requires -Version 7.4
# Offline regression only. No certificate is installed and IIS is never mutated.
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'IisDeployment.psm1') -Force
$script:checks = 0
function Assert([bool]$Value, [string]$Message) {
    if (-not $Value) { throw $Message }
    $script:checks++
}
function Reject([scriptblock]$Action, [string]$Message) {
    $rejected = $false
    try { & $Action | Out-Null } catch { $rejected = $true }
    Assert $rejected $Message
}
$root = Join-Path ([IO.Path]::GetTempPath()) ('cosmix-iis-tests-'+[guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path (Join-Path $root 'site')
function Save-Manifest([string]$Origin='https://192.168.1.20', [string]$ApiBase='/api/v1') {
    @{schemaVersion=1;origin=$Origin;apiBase=$ApiBase} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root 'package-manifest.json')
}
function New-TestCertificate([string]$SanKind='IP', [string]$Eku='1.3.6.1.5.5.7.3.1', [int]$Days=30) {
    $rsa = [Security.Cryptography.RSA]::Create(2048)
    try {
        $request = [Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=192.168.1.20', $rsa,
            [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pkcs1)
        $sans = [Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder]::new()
        if ($SanKind -eq 'IP') { $sans.AddIpAddress([Net.IPAddress]::Parse('192.168.1.20')) }
        elseif ($SanKind -eq 'DNS') { $sans.AddDnsName('192.168.1.20') }
        if ($SanKind -ne 'None') { $request.CertificateExtensions.Add($sans.Build()) }
        if ($Eku) {
            $oids = [Security.Cryptography.OidCollection]::new()
            $null = $oids.Add([Security.Cryptography.Oid]::new($Eku))
            $request.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]::new($oids,$true))
        }
        $certificate = $request.CreateSelfSigned([DateTimeOffset]::UtcNow.AddDays(-1), [DateTimeOffset]::UtcNow.AddDays($Days))
        return $certificate
    } finally { $rsa.Dispose() }
}
try {
    Save-Manifest
    $plan = Get-CosmixIisPlan $root
    Assert ($plan.Binding -eq '192.168.1.20:443:' -and -not $plan.StartSite -and -not $plan.ApplyMigrations) 'Unexpected deployment plan.'
    foreach ($origin in @('http://192.168.1.20','https://192.168.1.20/80','https://192.168.1.20:8443','https://user@192.168.1.20','https://192.168.1.20/?x=1','https://host.example.com')) {
        Save-Manifest $origin
        Reject { Get-CosmixIisPlan $root } "Invalid origin accepted: $origin"
    }
    Save-Manifest 'https://192.168.1.20' '/80'
    Reject { Get-CosmixIisPlan $root } 'Wrong API path accepted.'
    Save-Manifest
    Reject { Get-CosmixIisPlan $root -SiteName '../other' } 'Unsafe IIS site name accepted.'
    $cert = New-TestCertificate
    try {
        Assert-CosmixCertificateIdentity $cert '192.168.1.20'
        Assert $true 'Valid identity rejected.'
        Reject { Assert-CosmixCertificateIdentity $cert '192.168.1.21' } 'Wrong IP certificate accepted.'
        $publicOnly = [Security.Cryptography.X509Certificates.X509Certificate2]::new($cert.RawData)
        try { Reject { Assert-CosmixCertificateIdentity $publicOnly '192.168.1.20' } 'Missing private key accepted.' }
        finally { $publicOnly.Dispose() }
    } finally { $cert.Dispose() }
    foreach ($scenario in @(
        @{SanKind='DNS';Eku='1.3.6.1.5.5.7.3.1';Days=30},
        @{SanKind='None';Eku='1.3.6.1.5.5.7.3.1';Days=30},
        @{SanKind='IP';Eku='1.3.6.1.5.5.7.3.2';Days=30},
        @{SanKind='IP';Eku='';Days=30},
        @{SanKind='IP';Eku='1.3.6.1.5.5.7.3.1';Days=1})) {
        $cert = New-TestCertificate @scenario
        try { Reject { Assert-CosmixCertificateIdentity $cert '192.168.1.20' } 'Invalid certificate identity accepted.' }
        finally { $cert.Dispose() }
    }
    $tokens = $null; $errors = $null
    $null = [Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'Setup-CosmixIisHosting.ps1'), [ref]$tokens, [ref]$errors)
    Assert ($errors.Count -eq 0) 'Installer syntax failure.'
    [pscustomobject]@{Passed=$script:checks;Failed=0;IisMutationTested=$false;CertificateStoreChanged=$false}
} finally {
    # Delete only this test's verified unique directory, in the same shell.
    $resolved = [IO.Path]::GetFullPath($root)
    $temp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar
    if ($resolved.StartsWith($temp,[StringComparison]::OrdinalIgnoreCase) -and [IO.Path]::GetFileName($resolved) -match '^cosmix-iis-tests-[a-f0-9]{32}$') {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
