#requires -Version 5.1
[CmdletBinding()]
param([Parameter(Mandatory)][string]$PackageDirectory)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $PackageDirectory).Path
$site = Join-Path $root 'site'
$manifest = Get-Content -LiteralPath (Join-Path $root 'package-manifest.json') -Raw | ConvertFrom-Json
$origin = [uri]$manifest.origin
if ($origin.Scheme -ne 'https' -or $origin.Port -ne 443 -or $manifest.apiBase -ne '/api/v1') {
    throw 'Package origin/API configuration is invalid.'
}
$seen = @{}
foreach ($entry in $manifest.files) {
    $path = [IO.Path]::GetFullPath((Join-Path $site $entry.path))
    if (-not $path.StartsWith($site.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase) -or $seen.ContainsKey($path)) {
        throw 'Manifest contains an invalid or duplicate path.'
    }
    $seen[$path] = $true
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $entry.sha256) { throw "Checksum mismatch: $($entry.path)" }
}
$files = @(Get-ChildItem -LiteralPath $site -Recurse -File)
if ($files.Count -ne $seen.Count) { throw 'Unlisted files found in site directory.' }
foreach ($file in $files) { if (-not $seen.ContainsKey($file.FullName)) { throw 'Unlisted site file.' } }
if (Test-Path -LiteralPath (Join-Path $site 'wwwroot/web.config')) { throw 'Unexpected frontend child IIS config.' }
if (@(Get-ChildItem -LiteralPath $site -Filter 'appsettings.*.json').Count) { throw 'Machine-specific settings were packaged.' }
[xml]$config = Get-Content -LiteralPath (Join-Path $site 'web.config') -Raw
$hosting = $config.configuration.location.'system.webServer'.aspNetCore
$settings = @{}
foreach ($variable in $hosting.environmentVariables.environmentVariable) {
    if ($settings.ContainsKey($variable.name)) { throw 'Duplicate IIS environment setting.' }
    $settings[$variable.name] = $variable.value
}
if ($hosting.hostingModel -ne 'inprocess' -or $hosting.stdoutLogEnabled -ne 'false' -or
    $settings['ASPNETCORE_ENVIRONMENT'] -ne 'Production' -or $settings['WebFrontend__Enabled'] -ne 'true' -or
    $settings['AllowedHosts'] -ne $origin.Host -or $settings['ASPNETCORE_HTTPS_PORT'] -ne '443' -or
    $settings['Cors__AllowedOrigins__0'] -ne $origin.GetLeftPart([UriPartial]::Authority)) {
    throw 'IIS hosting settings do not match the HTTPS package profile.'
}
[pscustomobject]@{ Origin=$manifest.origin; ApiBase=$manifest.apiBase; VerifiedFiles=$files.Count; HashMismatches=0; LiveServerTested=$false }
