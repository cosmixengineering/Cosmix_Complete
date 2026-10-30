#requires -Version 7.4
Set-StrictMode -Version Latest

function Get-CosmixIisPlan {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$PackageDirectory,
        [string]$SiteName = 'CosmixErp', [string]$AppPoolName = 'CosmixAppPool')
    foreach ($name in @($SiteName, $AppPoolName)) {
        if ($name -cnotmatch '^[A-Za-z][A-Za-z0-9_-]{0,63}$') { throw 'Use a simple dedicated IIS site/pool name.' }
    }
    $root = (Get-Item -LiteralPath $PackageDirectory -Force).FullName
    for ($cursor = [IO.DirectoryInfo]::new($root); $null -ne $cursor; $cursor = $cursor.Parent) {
        if ($cursor.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Release paths must not contain links.' }
    }
    $manifest = Get-Content -LiteralPath (Join-Path $root 'package-manifest.json') -Raw | ConvertFrom-Json
    $origin = [uri]$manifest.origin
    $address = $null
    if (-not $origin.IsAbsoluteUri -or $origin.Scheme -ne 'https' -or $origin.Port -ne 443 -or
        $origin.UserInfo -or $origin.Query -or $origin.Fragment -or $origin.AbsolutePath -ne '/' -or
        -not [Net.IPAddress]::TryParse($origin.Host, [ref]$address) -or
        $address.AddressFamily -ne [Net.Sockets.AddressFamily]::InterNetwork -or
        $manifest.apiBase -ne '/api/v1' -or $manifest.schemaVersion -ne 1) {
        throw 'This installer requires the verified LAN IPv4 HTTPS package and /api/v1.'
    }
    $sitePath = Join-Path $root 'site'
    if (-not (Test-Path -LiteralPath $sitePath -PathType Container)) { throw 'The package site directory is missing.' }
    if (Get-ChildItem -LiteralPath $sitePath -Recurse -Force | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint } | Select-Object -First 1) {
        throw 'The site package must not contain links.'
    }
    [pscustomobject]@{ PackageDirectory=$root; SitePath=$sitePath; SiteName=$SiteName; AppPoolName=$AppPoolName;
        Origin=$origin.GetLeftPart([UriPartial]::Authority); Address=$address.ToString(); Binding=($address.ToString()+':443:');
        StartSite=$false; ApplyMigrations=$false }
}

function Assert-CosmixCertificateIdentity {
    [CmdletBinding()]
    param([Parameter(Mandatory)][Security.Cryptography.X509Certificates.X509Certificate2]$Certificate,
        [Parameter(Mandatory)][string]$Address)
    $now = [DateTime]::UtcNow
    if (-not $Certificate.HasPrivateKey -or $Certificate.NotBefore.ToUniversalTime() -gt $now -or
        $Certificate.NotAfter.ToUniversalTime() -le $now.AddDays(7)) {
        throw 'An installed private-key certificate valid for at least seven more days is required.'
    }
    # Common Name fallback is deliberately disabled: an IP must be an iPAddress SAN.
    if (-not $Certificate.MatchesHostname($Address, $false, $false)) { throw 'Certificate IP SAN does not match the package address.' }
    $eku = @($Certificate.Extensions | Where-Object { $_.Oid.Value -eq '2.5.29.37' })
    if ($eku.Count -ne 1 -or '1.3.6.1.5.5.7.3.1' -notin @($eku[0].EnhancedKeyUsages | ForEach-Object Value)) {
        throw 'Certificate must explicitly allow Server Authentication.'
    }
}

Export-ModuleMember -Function Get-CosmixIisPlan, Assert-CosmixCertificateIdentity
