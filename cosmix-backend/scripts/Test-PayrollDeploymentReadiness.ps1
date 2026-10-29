<#
Read-only deployment inventory. No hosts/workers are started, migrations applied,
OTP sent, keys printed, or employee rows read. SettingsPaths are explicit JSON
layers in lowest-to-highest priority; this is not proof of a running host's settings.
Live probes require their individual switches. SQL uses Windows authentication and
only __EFMigrationsHistory. Existing Test-AcceptanceReadiness.ps1 remains separate.
#>
[CmdletBinding()]
param(
    [string[]]$SettingsPaths = @(),
    [switch]$IncludeEnvironment,
    [string]$SqlServer,
    [string]$Database,
    [switch]$ProbeDevices,
    [switch]$ProbeFace,
    [string]$AdbPath = "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe",
    [string]$OutputPath
)
$ErrorActionPreference = 'Stop'
$backend = Split-Path -Parent $PSScriptRoot
$repository = Split-Path -Parent $backend
$checks = [Collections.Generic.List[object]]::new()
$settings = @{}
function Add-Check([string]$Id,[string]$Status,[string]$Detail) {
    $checks.Add([pscustomobject]@{id=$Id;status=$Status;detail=$Detail})
}
function Flatten($Node,[string]$Prefix='') {
    foreach($entry in $Node.GetEnumerator()) {
        $key = if($Prefix){$Prefix+':'+$entry.Key}else{[string]$entry.Key}
        if($entry.Value -is [System.Collections.IDictionary]) { Flatten $entry.Value $key }
        else { $settings[$key]=$entry.Value }
    }
}
function Enabled([string]$Key) { return [string]$settings[$Key] -ieq 'true' }
function Present([string]$Key) { return -not [string]::IsNullOrWhiteSpace([string]$settings[$Key]) }
function Safe-Json([string]$Path) {
    try {
        $item=Get-Item -LiteralPath $Path
        if($item.PSIsContainer -or $item.Length -gt 1048576){return $null}
        for($cursor=$item;$null -ne $cursor;){
            if($cursor.Attributes -band [IO.FileAttributes]::ReparsePoint){return $null}
            $cursor=if($cursor -is [IO.DirectoryInfo]){$cursor.Parent}else{$cursor.Directory}
        }
        $parsed=Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -AsHashtable
        if($parsed -isnot [System.Collections.IDictionary]){return $null}
        return $parsed
    }catch{return $null}
}
if($SettingsPaths.Count -eq 0){$SettingsPaths=@(Join-Path $backend 'src/Cosmix.Api/appsettings.json')}
$loaded=0
foreach($path in $SettingsPaths){
    $json=Safe-Json $path
    if($null -eq $json){Add-Check 'configuration-layer' 'Blocked' 'An explicitly selected JSON layer could not be read safely.'}
    else {Flatten $json; $loaded++}
}
if($IncludeEnvironment){
    # Only override keys relevant to this inventory; never serialize environment values.
    foreach($key in @('MobileAuthentication:Enabled','WebAuthentication:Enabled','MobileAuthentication:Sms:Provider',
        'MobileAuthentication:Sms:Endpoint','MobileAuthentication:Sms:BearerToken','SelfHostedFace:Enabled',
        'SelfHostedFace:CompreFace:BaseUri','AttendanceJobs:Enabled','LocalTesting:Enabled',
        'PrivateFiles:Directory','PrivateFiles:KeyFile','PrivateFiles:KeyReference')){
        $value=[Environment]::GetEnvironmentVariable($key.Replace(':','__'))
        if($null -ne $value){$settings[$key]=$value}
    }
}
Add-Check 'configuration-scope' 'Observed' "$loaded explicit JSON layers loaded; selected environment overrides: $([bool]$IncludeEnvironment). Running-host effective configuration is not established."
foreach($pair in @(@('mobile-login','MobileAuthentication:Enabled'),@('web-login-recovery','WebAuthentication:Enabled'),@('report-notification-dispatch','AttendanceJobs:Enabled'))){
    if(Enabled $pair[1]){Add-Check $pair[0] 'Observed' 'Enabled in inspected configuration; behavior not exercised.'}
    else{Add-Check $pair[0] 'Blocked' 'Disabled or missing in inspected configuration.'}
}
if(Enabled 'LocalTesting:Enabled'){Add-Check 'production-profile' 'Blocked' 'LocalTesting selected; synthetic adapters cannot establish production readiness.'}
else{Add-Check 'production-profile' 'Review' 'No LocalTesting switch observed; deployment profile and environment require operator verification.'}
$smsUri=$null
$smsShape=$settings['MobileAuthentication:Sms:Provider'] -ceq 'GenericHttp' -and
    [uri]::TryCreate([string]$settings['MobileAuthentication:Sms:Endpoint'],[UriKind]::Absolute,[ref]$smsUri) -and
    $smsUri.Scheme -eq 'https' -and -not $smsUri.UserInfo -and -not $smsUri.Query -and -not $smsUri.Fragment -and
    (Present 'MobileAuthentication:Sms:BearerToken')
if($smsShape){Add-Check 'external-sms' 'Observed' 'External HTTPS gateway selection and credential presence observed; adapter validation/carrier delivery untested.'}
else{Add-Check 'external-sms' 'Blocked' 'External HTTPS SMS configuration incomplete or synthetic; no SMS was sent.'}
try{
    $privateDir=[string]$settings['PrivateFiles:Directory'];$privateKey=[string]$settings['PrivateFiles:KeyFile']
    $metadata=(Present 'PrivateFiles:KeyReference') -and (Test-Path -LiteralPath $privateDir -PathType Container) -and
        (Test-Path -LiteralPath $privateKey -PathType Leaf) -and (Get-Item -LiteralPath $privateKey).Length -eq 32
}catch{$metadata=$false}
if($metadata){Add-Check 'private-storage' 'Observed' 'Storage directory and 32-byte key metadata exist; ACLs, key validity, read/write, restore and rotation require acceptance.'}
else{Add-Check 'private-storage' 'Blocked' 'Private storage/key metadata incomplete; no key bytes were read.'}

$migrationPath=Join-Path $backend 'src/Cosmix.Infrastructure/Database/Migrations'
$migrationFiles=@(Get-ChildItem -LiteralPath $migrationPath -File | Where-Object {$_.Name -match '^\d{14}_.+\.cs$' -and $_.Name -notlike '*.Designer.cs'} | Sort-Object Name)
$migrations=@($migrationFiles | ForEach-Object {$_.BaseName})
$manifest=@($migrationFiles | ForEach-Object {[pscustomobject]@{id=$_.BaseName;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}})
Add-Check 'source-migrations' 'Observed' "$($migrations.Count) migration source files inventoried; checksums are not applied-file attestations."
if($SqlServer -and $Database){
    try{
        $sql=Get-Command sqlcmd -CommandType Application -ErrorAction Stop | Select-Object -First 1
        $lines=@(& $sql.Source -S $SqlServer -d $Database -E -b -l 5 -t 10 -h -1 -W -Q 'SET NOCOUNT ON; SELECT MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;' 2>&1)
        if($LASTEXITCODE -ne 0){throw 'schema-read-failed'}
        $applied=@($lines | ForEach-Object {([string]$_).Trim()} | Where-Object {$_ -match '^\d{14}_[A-Za-z0-9_]+$'})
        $missing=@($migrations | Where-Object {$_ -notin $applied});$unknown=@($applied | Where-Object {$_ -notin $migrations})
        if($applied.Count -eq 0 -or $missing.Count -or $unknown.Count){Add-Check 'applied-migrations' 'Blocked' "Applied=$($applied.Count); pending=$($missing.Count); unknown=$($unknown.Count)."}
        else{Add-Check 'applied-migrations' 'Observed' "All $($applied.Count) source migration IDs recorded in selected database; model and physical-column drift require the API payroll readiness probe."}
    }catch{Add-Check 'applied-migrations' 'Blocked' 'Read-only migration query failed; connection and diagnostic values suppressed.'}
}else{Add-Check 'applied-migrations' 'NotRun' 'Supply both SqlServer and Database for read-only migration history comparison.'}

if($ProbeDevices){
    try{
        $devices=@(& $AdbPath devices 2>&1)
        if($LASTEXITCODE -ne 0){throw 'adb-failed'}
        $online=@($devices | Where-Object {[string]$_ -match '^\S+\s+device$'})
        $emulators=@($online | Where-Object {[string]$_ -match '^emulator-'}).Count
        $candidates=$online.Count-$emulators
        Add-Check 'android-inventory' 'Observed' "Online emulators=$emulators; other device candidates=$candidates. Device serials suppressed."
        Add-Check 'three-physical-phones' 'NotAccepted' 'ADB inventory cannot establish physical hardware or successful three-phone scenarios; signed device matrix required.'
    }catch{Add-Check 'android-inventory' 'Blocked' 'ADB inventory unavailable; no device acceptance inferred.'}
}else{Add-Check 'android-inventory' 'NotRun' 'Pass ProbeDevices for current ADB inventory.'}
if($ProbeFace){
    $faceUri=$null
    if([uri]::TryCreate([string]$settings['SelfHostedFace:CompreFace:BaseUri'],[UriKind]::Absolute,[ref]$faceUri) -and $faceUri.Scheme -in @('http','https') -and -not $faceUri.UserInfo){
        $client=[Net.Sockets.TcpClient]::new()
        try{
            $task=$client.ConnectAsync($faceUri.Host,$faceUri.Port)
            if($task.Wait(4000) -and $client.Connected){Add-Check 'face-network' 'Observed' 'Configured face TCP port reachable; TLS, health, liveness and real biometric match not tested.'}
            else{Add-Check 'face-network' 'Blocked' 'Configured face TCP port did not respond within four seconds.'}
        }catch{Add-Check 'face-network' 'Blocked' 'Configured face TCP port unavailable; endpoint credentials and exception details suppressed.'}
        finally{$client.Dispose()}
    }else{Add-Check 'face-network' 'Blocked' 'No valid configured face URI found in selected layers.'}
}else{Add-Check 'face-network' 'NotRun' 'Pass ProbeFace to test only TCP reachability of the selected face configuration.'}
Add-Check 'background-job-runtime' 'NotRun' 'OTP is gated by MobileAuthentication; attendance dispatch is opt-in. Document expiry worker starts after 15 seconds without an enable switch. Monitor worker failures, retries and queue age; no workers were run here.'
Add-Check 'recovery-and-restart' 'NotRun' 'Verify phone/password recovery, independent owner recovery, session revocation and persistent data-protection keys across restart on the deployment host.'
Add-Check 'backup-restore' 'NotRun' 'Restore SQL, private evidence and retained encryption/data-protection keys together in an isolated recovery drill.'
Add-Check 'actual-month-and-signoff' 'NotAccepted' 'Actual salary sheet, accounting ledger, bank-approved format, physical devices and HR/Finance/Management evidence remain independent acceptance gates.'
$settings.Clear()
$report=[pscustomobject]@{schema_version=1;generated_at_utc=[DateTimeOffset]::UtcNow.ToString('o');mode='ReadOnlyInventory';
    tool_sha256=(Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash.ToLowerInvariant();
    production_ready=$false;company_acceptance_established=$false;checks=@($checks.ToArray());source_migrations=$manifest}
$text=$report | ConvertTo-Json -Depth 7
if($OutputPath){
    $full=[IO.Path]::GetFullPath($OutputPath)
    $root=[IO.Path]::GetFullPath($repository).TrimEnd('\','/')
    if($full.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $full)){
        throw 'Use a new output file outside the repository; existing evidence is never overwritten.'
    }
    for($cursor=[IO.DirectoryInfo]::new([IO.Path]::GetDirectoryName($full));$null -ne $cursor;$cursor=$cursor.Parent){
        if($cursor.Exists -and ($cursor.Attributes -band [IO.FileAttributes]::ReparsePoint)){
            throw 'Output parents must not contain links or reparse points.'
        }
    }
    $stream=[IO.File]::Open($full,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try{$bytes=[Text.Encoding]::UTF8.GetBytes($text);$stream.Write($bytes,0,$bytes.Length)}finally{$stream.Dispose()}
}
$text
