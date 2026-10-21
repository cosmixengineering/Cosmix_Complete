# Prepared login/MFA/CSRF acceptance only. No host start, provisioning, role or attendance mutations.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ManifestPath,
    [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$ApprovedManifestSha256,
    [Parameter(Mandatory)][string]$SourceServer,
    [ValidateSet('Inspect','Execute')][string]$Mode = 'Inspect',
    [ValidateSet('RealSyntheticWebMfa')][string]$ActionApproval
)
$ErrorActionPreference = 'Stop'
$VerbosePreference = 'SilentlyContinue'; $DebugPreference = 'SilentlyContinue'
if ($PSVersionTable.PSVersion.Major -lt 7 -or -not $IsWindows) { throw 'Windows PowerShell 7 or later required.' }
$manifestFull = [IO.Path]::GetFullPath($ManifestPath)
$manifestRoot = 'C:\Users\Huma.Khan\Downloads\Cosmix_Erp_software\cosmix-backend\docs\attendance-completion\'
if (-not $manifestFull.StartsWith($manifestRoot, [StringComparison]::OrdinalIgnoreCase) -or
    [IO.Path]::GetExtension($manifestFull) -ine '.json') { throw 'Explicit non-secret JSON manifest must be in the owned checkout documentation folder.' }
for ($cursor = $manifestFull; $cursor; $cursor = [IO.Path]::GetDirectoryName($cursor)) {
    if ((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked manifest paths are forbidden.' }
    if ([IO.Path]::GetDirectoryName($cursor) -eq $cursor) { break }
}
$manifestItem = Get-Item -LiteralPath $manifestFull -Force
if ($manifestItem.PSIsContainer -or $manifestItem.Length -lt 1 -or $manifestItem.Length -gt 1048576) { throw 'Invalid manifest size/type.' }
$bootstrapBytes = [IO.File]::ReadAllBytes($manifestFull)
if ([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bootstrapBytes)) -ine $ApprovedManifestSha256) { throw 'Manifest approval hash mismatch.' }
$bootstrap = [Text.Encoding]::UTF8.GetString($bootstrapBytes).TrimStart([char]0xFEFF) | ConvertFrom-Json -AsHashtable -Depth 50
if ((Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'Huma-Acceptance-Common.ps1') -Algorithm SHA256).Hash -cne
    $bootstrap.ArtifactHashes['docs/attendance-completion/Huma-Acceptance-Common.ps1']) { throw 'Library approval hash mismatch.' }
. (Join-Path $PSScriptRoot 'Huma-Acceptance-Common.ps1')
$context = Read-HumaContext $ManifestPath $ApprovedManifestSha256 $SourceServer
Assert-HumaMigrationTarget $context
$m = $context.Manifest
if ($Mode -ceq 'Inspect') {
    [pscustomobject]@{ Mode = 'OfflineAuthInspect'; SqlExecuted = $false; HttpExecuted = $false;
        PrivateFilesRead = $false; Scope = 'Three real web password + local SMS OTP + identity-bound CSRF sessions'; WritesApproved = $false }
    return
}
Assert-Huma ($ActionApproval -ceq 'RealSyntheticWebMfa') 'Separate human authorization for authentication writes is required.'
Assert-HumaWriteApproval $context Auth
Assert-Huma ($m.Approvals.OwnedGuardedHost -eq $true -and $m.Approvals.ExclusiveSqlWindow -eq $true) 'Root-reviewed guarded host and exclusive SQL window required.'
$connection = $null; $run = $null; $credentials = $null; $client = $null; $phase = 'Preflight'
try {
    $connection = Open-HumaSql $context
    Assert-HumaState (Get-HumaInventory $connection $context) $context Target
    $schemaPath = Get-HumaPath (Join-Path $m.ArtifactRoot ($m.UpgradeRunId + '-upgrade\schema-result.json')) $m.ArtifactRoot -File
    Assert-Huma ($m.UpgradeRunId -cmatch '^HUMA20261012[A-Z0-9]{8,24}$') 'Exact completed upgrade run is required.'
    Assert-HumaPrivate $schemaPath
    Assert-HumaHash $schemaPath $m.SchemaReceiptSha256
    $schema = Get-Content -LiteralPath $schemaPath -Raw | ConvertFrom-Json -AsHashtable
    Assert-Huma ($schema.ManifestSha256 -ceq $m.UpgradeManifestSha256 -and $schema.RunId -ceq $m.UpgradeRunId -and
        $schema.Database -ceq 'Cosmix_Testing' -and $schema.ActualServerName -ceq $m.ActualServerName -and
        $schema.Owner -ceq $m.Owner -and $schema.TargetMigration -ceq $m.TargetMigration -and
        $schema.TargetMigrationCount -eq $m.TargetMigrationCount) 'Completed schema receipt is outside the reviewed target.'
    $hostPath = Get-HumaPath (Join-Path $m.ArtifactRoot 'owned-host.json') $m.ArtifactRoot -File
    Assert-HumaPrivate $hostPath
    Assert-HumaHash $hostPath $m.HostReceiptSha256
    $hostReceipt = Get-Content -LiteralPath $hostPath -Raw | ConvertFrom-Json -AsHashtable -Depth 30
    $uri = [uri]$m.BaseUri
    Assert-Huma ($uri.IsAbsoluteUri -and $uri.Scheme -ceq 'https' -and $uri.Host -ceq 'localhost' -and
        $uri.AbsolutePath -ceq '/' -and -not $uri.Query -and -not $uri.Fragment -and -not $uri.UserInfo -and
        $uri.Port -ge 1024 -and $hostReceipt.BaseUri -ceq $m.BaseUri -and
        $hostReceipt.SourceServer -ceq $context.SourceServer -and $hostReceipt.Database -ceq 'Cosmix_Testing' -and
        $hostReceipt.ActualServerName -ceq $m.ActualServerName -and $hostReceipt.Owner -ceq $m.Owner -and
        $hostReceipt.Environment -ceq 'Development' -and $hostReceipt.LocalTestingEnabled -eq $true -and
        $hostReceipt.SmsProvider -ceq 'LocalTesting' -and $hostReceipt.ExternalSmsConfigured -eq $false -and
        $hostReceipt.AttendanceJobsEnabled -eq $false -and $hostReceipt.AttendanceZonesEnabled -eq $false -and
        $hostReceipt.ZoneRetentionEnabled -eq $false -and $hostReceipt.ZoneLocationStorageEnabled -eq $false -and
        $hostReceipt.MissingCheckoutRemindersEnabled -eq $false -and $hostReceipt.EffectiveConfigurationReviewed -eq $true -and
        $hostReceipt.RetainedKeyringReviewed -eq $true) 'Owned-host effective configuration receipt is incomplete or unsafe.'
    $settingsPath = [IO.Path]::GetFullPath([string]$hostReceipt.SettingsPath)
    Assert-Huma ($settingsPath -ceq ($m.ExistingPrivateRoot + '\settings.json') -or
        $settingsPath -ceq ($m.ArtifactRoot + '\runtime-settings.json')) 'Unapproved local settings file.'
    $settingsRoot = if ($settingsPath -ceq ($m.ExistingPrivateRoot + '\settings.json')) { $m.ExistingPrivateRoot } else { $m.ArtifactRoot }
    $null = Get-HumaPath $settingsPath $settingsRoot -File
    Assert-HumaPrivate $settingsPath
    Assert-HumaHash $settingsPath $m.HostSettingsSha256
    Assert-Huma ($hostReceipt.SettingsSha256 -ceq $m.HostSettingsSha256) 'Host settings binding differs.'
    $assemblyRelative = 'src/Cosmix.Api/bin/Release/net10.0/Cosmix.Api.dll'
    $assembly = Get-HumaPath (Join-Path $context.Repository $assemblyRelative) $context.Repository -File
    foreach ($name in @('Cosmix.Api','Cosmix.Domain','Cosmix.Application','Cosmix.Infrastructure')) {
        $relative = 'src/Cosmix.Api/bin/Release/net10.0/' + $name + '.dll'
        Assert-Huma ($m.ArtifactHashes.Contains($relative)) 'Reviewed runtime assembly hash is missing.'
    }
    Assert-Huma ($hostReceipt.ApiAssemblySha256 -ceq $m.ArtifactHashes[$assemblyRelative]) 'Host assembly binding differs.'
    $process = Get-Process -Id ([int]$hostReceipt.ProcessId) -ErrorAction Stop
    $processDetails = Get-CimInstance Win32_Process -Filter ('ProcessId = ' + [int]$hostReceipt.ProcessId)
    $expectedCommand = '^"?[^"\r\n]*\\dotnet\.exe"?\s+"' + [regex]::Escape($assembly) + '"\s+--urls\s+"?' + [regex]::Escape($m.BaseUri.TrimEnd('/')) + '"?\s*$'
    Assert-Huma ($process.ProcessName -ceq 'dotnet' -and
        [Math]::Abs(($process.StartTime.ToUniversalTime() - ([DateTimeOffset]$hostReceipt.StartedAtUtc).UtcDateTime).TotalSeconds) -lt 2 -and
        $processDetails.CommandLine -imatch $expectedCommand) 'Owned host process/start time/assembly command changed.'
    $listeners = @(Get-NetTCPConnection -State Listen -LocalPort $uri.Port -ErrorAction Stop)
    Assert-Huma ($listeners.Count -gt 0 -and @($listeners | Where-Object { $_.OwningProcess -ne $process.Id -or $_.LocalAddress -notin @('127.0.0.1','::1') }).Count -eq 0) 'HTTPS port is not exclusively owned on loopback by the reviewed process.'
    Assert-Huma ($m.RetainedKeyFiles -is [array] -and $m.RetainedKeyFiles.Count -ge 2) 'Retained private key dependency inventory is required.'
    foreach ($key in $m.RetainedKeyFiles) {
        $path = Get-HumaPath $key.Path $m.ExistingPrivateRoot -File
        Assert-HumaPrivate $path
        Assert-Huma ((Get-Item -LiteralPath $path).Length -eq 32 -and $key.Reference -cmatch '^[A-Za-z0-9._-]{1,80}$') 'Retained key metadata is invalid. No key is generated/read/rotated by this check.'
    }
    $accountsPath = Get-HumaPath (Join-Path $m.ExistingPrivateRoot 'Test-Accounts.json') $m.ExistingPrivateRoot -File
    Assert-HumaPrivate $accountsPath
    Assert-HumaHash $accountsPath $m.AccountsSha256
    $null = Read-HumaContext $ManifestPath $ApprovedManifestSha256 $SourceServer
    $run = Start-HumaAttempt $context auth
    $phase = 'PrivateCredentials'
    try { $credentials = @(Get-Content -LiteralPath $accountsPath -Raw | ConvertFrom-Json -Depth 20) }
    catch { throw 'Private accounts could not be parsed; contents suppressed.' }
    Assert-Huma ($credentials.Count -eq 3 -and
        (@($credentials.Email | Sort-Object) -join '|') -ceq 'admin@example.invalid|hr@example.invalid|superadmin@example.invalid') 'Exactly the three supplied private synthetic credentials are required.'
    $helper = Get-HumaPath (Join-Path $context.Repository 'docs/access-provisioning/LocalAcceptanceHttp.ps1') $context.Repository -File
    Assert-HumaHash $helper $m.ArtifactHashes['docs/access-provisioning/LocalAcceptanceHttp.ps1']
    . $helper
    $phase = 'RealWebMfa'
    foreach ($account in $credentials) {
        $null = Assert-HumaSqlIdentity $connection $context
        $client = New-LocalAcceptanceSession -BaseUri $uri -Account $account
        $permissions = Invoke-LocalAcceptanceRequest -Client $client -Method GET -Path '/api/v1/me/permissions'
        Assert-Huma ($permissions.StatusCode -eq 200 -and $client.CsrfToken) 'Real MFA session/current permissions/CSRF check failed.'
        Write-HumaNewJson (Join-Path $run ($account.Email.Split('@')[0] + '-auth-result.json')) @{
            PasswordChallenge = $true; PreMfaDenied = $true; LocalOtpProof = $true; AuthenticatedCsrf = $true;
            CurrentPermissionsStatus = 200; ManifestSha256 = $context.ManifestSha256; AtUtc = [DateTimeOffset]::UtcNow.ToString('O') }
        $permissions = $null; $client = $null
    }
    Write-Output 'Three real synthetic web MFA/CSRF sessions verified. Auth state persists; private receipts contain no credentials/tokens.'
} catch {
    if ($run) { Write-HumaNewJson (Join-Path $run 'stopped.json') @{ Phase = $phase; ManifestSha256 = $context.ManifestSha256;
        Recovery = 'Retain authentication state and evidence. No automatic login retry or database/credential reset.' } }
    throw 'Guarded auth acceptance stopped; retain partial evidence. Sensitive request/response/provider details suppressed.'
} finally { $credentials = $null; $account = $null; $client = $null; $permissions = $null; if ($connection) { $connection.Dispose() } }
