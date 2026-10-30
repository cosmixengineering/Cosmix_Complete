#requires -Version 7.4
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$ProfilePath = (Join-Path $PSScriptRoot '../deploy/iis/company-lan.json'),
    [switch]$SkipRestore
)
$ErrorActionPreference = 'Stop'
$profile = Get-Content -LiteralPath $ProfilePath -Raw | ConvertFrom-Json
$origin = [uri]$profile.origin
if (-not $origin.IsAbsoluteUri -or $origin.Scheme -ne 'https' -or $origin.Port -ne 443 -or
    $origin.AbsolutePath -ne '/' -or $origin.UserInfo -or $origin.Query -or $origin.Fragment -or
    $profile.apiBase -ne '/api/v1') { throw 'Profile requires an HTTPS origin on port 443 and /api/v1.' }
$workspace = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$destination = [IO.Path]::GetFullPath($OutputDirectory)
if (-not [IO.Path]::IsPathFullyQualified($OutputDirectory) -or (Test-Path -LiteralPath $destination) -or
    $destination.StartsWith($workspace.TrimEnd('\','/')+[IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Choose a new absolute output directory outside the source workspace.'
}
for ($cursor = [IO.DirectoryInfo]::new([IO.Path]::GetDirectoryName($destination)); $null -ne $cursor; $cursor = $cursor.Parent) {
    if ($cursor.Exists -and ($cursor.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Publish output must not use linked directories.' }
}
$backend = Join-Path $workspace 'cosmix-backend'
$frontend = Join-Path $workspace 'cosmix_frotned'
$site = Join-Path $destination 'site'
$logs = Join-Path $destination 'verification'
New-Item -ItemType Directory -Path $destination,$site,$logs | Out-Null
& (Join-Path $workspace 'deploy/iis/Test-IisDeployment.ps1') *> (Join-Path $logs 'iis-deployment-regression.log')
if (-not $?) { throw 'IIS deployment helper regression failed.' }
$previousBase = [Environment]::GetEnvironmentVariable('VITE_API_BASE_URL', 'Process')
$previousMaps = [Environment]::GetEnvironmentVariable('VITE_GOOGLE_MAPS_API_KEY', 'Process')
try {
    # Process override wins over a developer's .env: the packaged browser always uses this IIS origin.
    $env:VITE_API_BASE_URL = '/api/v1'
    # Do not accidentally distribute a developer's Maps key from .env.local.
    # A restricted production browser key can be supplied explicitly in the public profile.
    $env:VITE_GOOGLE_MAPS_API_KEY = [string]$profile.googleMapsBrowserKey
    # Build in an isolated copy: npm ci must not delete a running Vite server's node_modules.
    $frontendBuild = $frontend
    if (-not $SkipRestore) {
        $frontendBuild = Join-Path $destination 'build/frontend-source'
        New-Item -ItemType Directory -Path $frontendBuild -Force | Out-Null
        foreach ($entry in Get-ChildItem -LiteralPath $frontend -Force) {
            if ($entry.Name -notin @('node_modules', '.git', 'dist', 'coverage') -and $entry.Name -notlike '.env*') {
                Copy-Item -LiteralPath $entry.FullName -Destination $frontendBuild -Recurse
            }
        }
    }
    Push-Location $frontendBuild
    try {
        if (-not $SkipRestore) {
            & npm ci *> (Join-Path $logs 'frontend-restore.log')
            if ($LASTEXITCODE -ne 0) { throw 'Frontend locked install failed; inspect private build logs.' }
        }
        & npm run verify *> (Join-Path $logs 'frontend-build.log')
        if ($LASTEXITCODE -ne 0) { throw 'Frontend lint/format/type/build verification failed.' }
        & node --test scripts/evidence-record.test.mjs scripts/payroll-readiness.test.mjs *> (Join-Path $logs 'frontend-regression.log')
        if ($LASTEXITCODE -ne 0) { throw 'Frontend evidence regression checks failed.' }
    } finally { Pop-Location }
    Push-Location $backend
    try {
        & dotnet publish src/Cosmix.Api/Cosmix.Api.csproj -c Release --self-contained false -p:UseAppHost=false -p:RestoreLockedMode=true `
            --artifacts-path (Join-Path $destination 'build') -o $site *> (Join-Path $logs 'backend-publish.log')
        if ($LASTEXITCODE -ne 0) { throw 'Backend publish failed.' }
    } finally { Pop-Location }
    $wwwroot = Join-Path $site 'wwwroot'
    New-Item -ItemType Directory -Path $wwwroot -Force | Out-Null
    foreach ($entry in Get-ChildItem -LiteralPath (Join-Path $frontendBuild 'dist') -Force) {
        # The backend owns IIS routing. The standalone static-site config must not become a child IIS application.
        if ($entry.Name -ne 'web.config') { Copy-Item -LiteralPath $entry.FullName -Destination $wwwroot -Recurse }
    }
    $developmentSettings = Join-Path $site 'appsettings.Development.json'
    if (Test-Path -LiteralPath $developmentSettings) { Remove-Item -LiteralPath $developmentSettings }
    $configPath = Join-Path $site 'web.config'
    [xml]$config = Get-Content -LiteralPath $configPath -Raw
    $hosting = $config.configuration.location.'system.webServer'.aspNetCore
    if ($hosting.hostingModel -ne 'inprocess' -or $hosting.stdoutLogEnabled -ne 'false') { throw 'Unexpected IIS hosting settings.' }
    $deploymentSettings = [ordered]@{
        WebFrontend__Enabled = 'true'
        AllowedHosts = $origin.Host
        Cors__AllowedOrigins__0 = $origin.GetLeftPart([UriPartial]::Authority)
        ASPNETCORE_HTTPS_PORT = '443'
    }
    foreach ($entry in $deploymentSettings.GetEnumerator()) {
        $variable = $config.CreateElement('environmentVariable')
        $variable.SetAttribute('name', $entry.Key)
        $variable.SetAttribute('value', $entry.Value)
        $null = $hosting.environmentVariables.AppendChild($variable)
    }
    $config.Save($configPath)
    # Never carry machine-specific settings into a distributable package.
    foreach ($settingsFile in Get-ChildItem -LiteralPath $site -Filter 'appsettings.*.json') {
        Remove-Item -LiteralPath $settingsFile.FullName
    }
    $baseSettingsPath = Join-Path $site 'appsettings.json'
    $baseSettings = Get-Content -LiteralPath $baseSettingsPath -Raw | ConvertFrom-Json
    if ($baseSettings.ConnectionStrings.CosmixDatabase -or $baseSettings.Jwt.SigningKey -or $baseSettings.GoogleMaps.ApiKey) {
        throw 'Source defaults contain deployment secrets. Refusing to package; move secrets into private configuration.'
    }
    foreach ($required in @('Cosmix.Api.dll','Cosmix.Api.runtimeconfig.json','wwwroot/index.html','web.config')) {
        if (-not (Test-Path -LiteralPath (Join-Path $site $required) -PathType Leaf)) { throw 'IIS package is incomplete.' }
    }
    $manifest = Get-ChildItem -LiteralPath $site -File -Recurse | Sort-Object FullName | ForEach-Object {
        [pscustomobject]@{path=[IO.Path]::GetRelativePath($site,$_.FullName).Replace('\','/');sha256=(Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant()}
    }
    [ordered]@{schemaVersion=1;builtAtUtc=[DateTimeOffset]::UtcNow.ToString('o');hosting='IIS in-process, Windows x64';
        frontendVerification='lint, formatting, TypeScript, production build, evidence and payroll readiness regression';
        origin=$origin.GetLeftPart([UriPartial]::Authority);apiBase='/api/v1';productionActivated=$false;files=@($manifest)} | ConvertTo-Json -Depth 4 |
        Set-Content -LiteralPath (Join-Path $destination 'package-manifest.json') -Encoding utf8
    Copy-Item -LiteralPath $ProfilePath -Destination (Join-Path $destination 'hosting-profile.json')
    $instructions = Join-Path $destination 'instructions'
    New-Item -ItemType Directory -Path $instructions | Out-Null
    foreach ($name in @('HOST-192.168.1.20.md', '192.168.1.20-certificate.inf', 'Test-IisPackage.ps1', 'Import-FaceSettings.ps1', 'Setup-CosmixIisHosting.ps1', 'IisDeployment.psm1', 'Test-IisDeployment.ps1')) {
        Copy-Item -LiteralPath (Join-Path $workspace "deploy/iis/$name") -Destination $instructions
    }
    foreach ($name in @('Test-OperationalHealth.ps1', 'Invoke-EncryptedRecoveryBackup.ps1')) {
        Copy-Item -LiteralPath (Join-Path $backend "scripts/$name") -Destination $instructions
    }
    Copy-Item -LiteralPath (Join-Path $backend 'docs/Production-Operations.md') -Destination $instructions
    "IIS package created: $site. No site, certificate, database or production account was changed."
} finally {
    [Environment]::SetEnvironmentVariable('VITE_API_BASE_URL', $previousBase, 'Process')
    [Environment]::SetEnvironmentVariable('VITE_GOOGLE_MAPS_API_KEY', $previousMaps, 'Process')
}
