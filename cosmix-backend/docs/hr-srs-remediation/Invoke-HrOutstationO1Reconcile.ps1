[CmdletBinding()]
param()
# SELECT-only reconciliation of the completed Continue01 run. Never starts a host or repeats HTTP.
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'HrOutstationO1Profile.ps1')
. (Join-Path $PSScriptRoot 'O1ReadOnlyReconciliation.Helpers.ps1')
. (Join-Path $PSScriptRoot 'O1RetainedAudit.Helpers.ps1')
$profile=Get-O1Profile
$root=$profile.Root
$results=Join-Path $root 'results'
$output=Join-Path $results 'read-only-reconciliation.json'
function Assert-ResultDirectory {
    Assert-O1 (Test-Path -LiteralPath $results -PathType Container) 'Existing results directory required.'
    for($part=$results;$part;$part=[IO.Path]::GetDirectoryName($part)){
        Assert-O1 (!((Get-Item -LiteralPath $part).Attributes -band [IO.FileAttributes]::ReparsePoint)) 'Linked output ancestor.'
    }
    $sid=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    $acl=Get-Acl -LiteralPath $results
    Assert-O1 ($acl.GetOwner([Security.Principal.SecurityIdentifier]).Value -ceq $sid) 'Results directory owner differs.'
    $inheritable=$false
    foreach($rule in $acl.GetAccessRules($true,$true,[Security.Principal.SecurityIdentifier])){
        if($rule.AccessControlType -eq [Security.AccessControl.AccessControlType]::Allow){
            Assert-O1 ($rule.IdentityReference.Value -ceq $sid) 'Results directory grants another identity access.'
            if($rule.InheritanceFlags -band [Security.AccessControl.InheritanceFlags]::ObjectInherit){$inheritable=$true}
        }
    }
    Assert-O1 $inheritable 'New result must inherit private owner access.'
}
Assert-ResultDirectory
Assert-O1 (!(Test-Path -LiteralPath $output)) 'Supplemental evidence already exists; never overwrite.'
Assert-O1 ([Security.Principal.WindowsIdentity]::GetCurrent().Name -ieq $profile.Owner) 'Operator differs.'
Assert-O1 (@(Get-NetTCPConnection -LocalPort 7481 -State Listen -ErrorAction SilentlyContinue).Count -eq 0) 'Test host must remain stopped.'

foreach($source in @(
    @{File='Invoke-HrVehicleV1.ps1';Names=@('Assert','Assert-PrivateSource')},
    @{File='Invoke-HrPermissionP1.ps1';Names=@('Read-Sql')},
    @{File='Invoke-HrOutstationO1.ps1';Names=@('New-P1SqlConnection','Json-Rows')}
)){
    $tokens=$null;$errors=$null
    $ast=[Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot $source.File),[ref]$tokens,[ref]$errors)
    Assert-O1 ($errors.Count -eq 0) 'Helper parse failed.'
    foreach($name in $source.Names){
        $definitions=@($ast.FindAll({param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -ceq $name},$true))
        Assert-O1 ($definitions.Count -eq 1) 'Ambiguous helper definition.'
        . ([scriptblock]::Create($definitions[0].Extent.Text))
    }
}
$inputHashes=@{}
foreach($name in @('start.json','failure.json','post-host-preservation.json','host-final.json')){
    $path=Join-Path $results $name
    Assert-PrivateSource $path
    $inputHashes[$name]=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
}
$start=Get-Content -LiteralPath (Join-Path $results 'start.json') -Raw|ConvertFrom-Json -AsHashtable -DateKind String
$failure=Get-Content -LiteralPath (Join-Path $results 'failure.json') -Raw|ConvertFrom-Json -AsHashtable -DateKind String
$preserved=Get-Content -LiteralPath (Join-Path $results 'post-host-preservation.json') -Raw|ConvertFrom-Json -AsHashtable -DateKind String
$hostFinal=Get-Content -LiteralPath (Join-Path $results 'host-final.json') -Raw|ConvertFrom-Json -AsHashtable
Assert-O1 ($start.PacketSha256 -ceq '04827FD43379985B51EEFFB36308F6D0910BAD701A65EEC7EAC13139962C35E0' -and !$start.SeedExecuted) 'Run packet differs.'
Assert-O1 ($failure.Budget.Requests -eq 45 -and $failure.Budget.Logins -eq 2 -and
    @($failure.Checks).Count -eq 38 -and @($failure.Checks|Where-Object {!$_.Passed}).Count -eq 0) 'HTTP evidence differs.'
Assert-O1 ($hostFinal.ProcessId -eq 34620 -and $hostFinal.ExitStatus -ceq 'Passed' -and
    !$hostFinal.CleanupPerformed -and !$hostFinal.DatabaseStopped) 'Host final state differs.'
Assert-O1 ($preserved.ProtectedSql -ceq 'Passed' -and $preserved.PriorFiles -ceq 'Passed') 'Original preservation not verified.'
$process=Get-CimInstance Win32_Process -Filter 'ProcessId=25768'
Assert-O1 ($process.Name -ceq 'sqlservr.exe' -and $process.CommandLine.Contains('-sLOCALDB#9F3001F7')) 'SQL process differs.'
$processOwner=Invoke-CimMethod -InputObject $process -MethodName GetOwner
Assert-O1 ($processOwner.ReturnValue -eq 0 -and ($processOwner.Domain+'\'+$processOwner.User) -ieq $profile.Owner) 'SQL owner differs.'
$identity=Json-Rows "SELECT (SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) AS Server,DB_NAME() AS [Database],CONVERT(int,SERVERPROPERTY('ProcessID')) AS Pid,d.state_desc AS State,SUSER_SNAME(d.owner_sid) AS Owner,ORIGINAL_LOGIN() AS Operator,r.family_guid AS Family FROM sys.databases d JOIN sys.database_recovery_status r ON r.database_id=d.database_id WHERE d.database_id=DB_ID() FOR JSON PATH);"
Assert-O1 (@($identity).Count -eq 1) 'Target identity missing.'
$id=$identity[0]
Assert-O1 ($id.Server -ieq $profile.Server -and $id.Database -ceq 'Cosmix_Testing' -and $id.Pid -eq 25768 -and
    $id.State -ceq 'ONLINE' -and $id.Owner -ieq $profile.Owner -and $id.Operator -ieq $profile.Owner -and
    [guid]$id.Family -eq [guid]$profile.Family) 'Target identity differs.'
Assert-O1 ((Read-Sql 'SELECT COUNT(*) FROM dbo.__EFMigrationsHistory;') -ceq '25') 'Schema count differs.'
$files=(Read-Sql 'SELECT physical_name FROM sys.database_files ORDER BY file_id;') -split '\r?\n'
Assert-O1 (($files -join '|') -ieq 'C:\Users\Huma.Khan\Cosmix_Testing.mdf|C:\Users\Huma.Khan\Cosmix_Testing_log.ldf') 'Database files differ.'

# Fetch bounded PK sets once. Compare exact identities in memory rather than reparsing
# thousands of OPENJSON entries for every row of a NOT IN count query.
$limits=Get-O1RowLimits
$deltas=@{};$counts=@{};$timings=@{}
foreach($table in ($limits.Keys|Sort-Object)){
    $watch=[Diagnostics.Stopwatch]::StartNew()
    Assert-O1 ($start.HistoricalIds.Contains($table)) 'Missing original IDs.'
    # -NoEnumerate retains the JSON array; pass its elements, including the empty case.
    $original=ConvertFrom-Json -InputObject $start.HistoricalIds[$table] -NoEnumerate
    $rows=Json-Rows "SELECT (SELECT TOP(10001) Id FROM $table ORDER BY Id FOR JSON PATH);"
    $current=@($rows|ForEach-Object {[string]$_.Id})
    $delta=Get-O1IdDelta -OriginalIds $original -CurrentIds $current
    Assert-O1 ($delta.Count -le $limits[$table] -and
        ($table -ceq 'audit.AuditEvents' -or $delta.Count -eq $limits[$table])) "New row bound differs: $table"
    $deltas[$table]=$delta;$counts[$table]=$delta.Count
    $timings[$table]=[math]::Round($watch.Elapsed.TotalSeconds,3)
    Write-Output "$table new=$($delta.Count) limit=$($limits[$table]) seconds=$($timings[$table])"
}
function Read-NewRows([string]$table,[string]$columns){
    Assert-O1 ($deltas.ContainsKey($table) -and $deltas[$table].Count -gt 0) 'Missing bounded new IDs.'
    $ids=(@($deltas[$table]|ForEach-Object {"'$(ConvertTo-O1Guid $_)'"}) -join ',')
    return ,(Json-Rows "SELECT (SELECT $columns FROM $table WHERE Id IN($ids) ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES);")
}
$business=@{}
foreach($table in @('hr.OutstationDeployments','hr.AllowanceClaims','hr.AllowanceClaimSubmissions','workflow.ApprovalRequests','workflow.ApprovalDecisions')){
    $business[$table]=Read-NewRows $table '*'
}
$binding=@{DeploymentId=$deltas['hr.OutstationDeployments'][0];ClaimId=$deltas['hr.AllowanceClaims'][0]}
Assert-O1 (@($failure.Http|Where-Object {$_.Path -ceq "$($profile.Api)/$($binding.DeploymentId)/claims/$($binding.ClaimId)/evidence" -and $_.Status -eq 200}).Count -eq 1) 'Persisted IDs not bound to retained HTTP evidence.'
$from=[datetimeoffset]$start.Utc;$until=[datetimeoffset]$failure.Utc
Assert-O1BusinessRows $business['hr.OutstationDeployments'] $business['hr.AllowanceClaims'] `
    $business['hr.AllowanceClaimSubmissions'] $business['workflow.ApprovalRequests'] `
    $business['workflow.ApprovalDecisions'] $binding $from $until
$sessionRows=Read-NewRows 'auth.UserSessions' 'Id,UserAccountId,SmsOtpChallengeId,CorrelationId,RevokedAt'
$challenges=Read-NewRows 'auth.MobileOtpChallenges' 'Id,UserAccountId,Purpose,ConsumedAt'
$deliveries=Read-NewRows 'auth.OtpDeliveries' 'Id,ChallengeId,Status,Attempts,CASE WHEN ProtectedPayload IS NULL THEN 1 ELSE 0 END AS Cleared'
$refresh=Read-NewRows 'auth.RefreshTokens' 'Id,UserSessionId,IssuedAt,ExpiresAt,ConsumedAt,ReplacedByTokenId'
foreach($account in @($profile.HrAccount,$profile.AdminAccount)){
    $session=@($sessionRows|Where-Object {[guid]$_.UserAccountId -eq [guid]$account})
    Assert-O1 ($session.Count -eq 1 -and $session[0].RevokedAt) 'Synthetic session missing or unrevoked.'
    $challenge=@($challenges|Where-Object {[guid]$_.Id -eq [guid]$session[0].SmsOtpChallengeId})
    Assert-O1 ($challenge.Count -eq 1 -and [guid]$challenge[0].UserAccountId -eq [guid]$account -and
        $challenge[0].Purpose -ceq 'WebLogin' -and $challenge[0].ConsumedAt) 'MFA binding differs.'
    $delivery=@($deliveries|Where-Object {[guid]$_.ChallengeId -eq [guid]$challenge[0].Id})
    Assert-O1 ($delivery.Count -eq 1 -and $delivery[0].Status -eq 1 -and $delivery[0].Attempts -eq 1 -and $delivery[0].Cleared -eq 1) 'Local MFA delivery state differs.'
    $token=@($refresh|Where-Object {[guid]$_.UserSessionId -eq [guid]$session[0].Id})
    # RefreshToken is revoked through its parent UserSession, not a token-level RevokedAt column.
    Assert-O1 ($token.Count -eq 1 -and $null -eq $token[0].ConsumedAt -and $null -eq $token[0].ReplacedByTokenId -and
        [datetimeoffset]$token[0].IssuedAt -ge $from -and [datetimeoffset]$token[0].IssuedAt -le $until -and
        [datetimeoffset]$token[0].ExpiresAt -gt [datetimeoffset]$token[0].IssuedAt -and $session[0].RevokedAt) 'Refresh token parent revocation or unrotated state differs.'
    Assert-O1 (@($failure.Http|Where-Object {$_.Trace -ceq $session[0].CorrelationId -and $_.Path -ceq '/api/v1/auth/web/otp/verify' -and $_.Status -eq 200}).Count -eq 1) 'Session trace not bound to MFA verification.'
}
$receipts=Read-NewRows 'workflow.IdempotencyReceipts' '[Key],Operation,ActorUserId,CompletedAt'
$operations=@{'O1-request'='attendance.deployment.request';'O1-decision'='attendance.deployment.decide'
    'O1-claim'='attendance.deployment.claim.request';'O1-claim-decision'='attendance.deployment.claim.decide'}
foreach($key in $operations.Keys){
    $receipt=@($receipts|Where-Object {$_.Key -ceq $key})
    $actor=if($key -cin @('O1-decision','O1-claim-decision')){$profile.AdminAccount}else{$profile.HrAccount}
    Assert-O1 ($receipt.Count -eq 1 -and $receipt[0].Operation -ceq $operations[$key] -and
        [guid]$receipt[0].ActorUserId -eq [guid]$actor -and $receipt[0].CompletedAt) 'Immutable receipt binding differs.'
}
$rateCount=[int](Read-Sql 'SELECT COUNT(*) FROM auth.AuthRateBuckets;')
Assert-O1 ($rateCount -le $start.CountsBefore.RateBuckets+12) 'Rate bucket bound exceeded.'
Assert-O1 ((Read-Sql 'SELECT COUNT(*) FROM auth.UserAccounts;') -eq '3') 'Account population changed.'
Assert-O1 ((Read-Sql 'SELECT COUNT(*) FROM auth.UserAccounts WHERE FailedLoginCount=0 AND LockedUntil IS NULL;') -eq '3') 'An original account remains locked or has failed-login state.'
$hr=Json-Rows "SELECT (SELECT Id,Status,OnboardingStatus,JoinedOn,DraftRevision,CONVERT(varchar(16),CONVERT(binary(8),RowVersion),2) AS RowVersion FROM hr.Employees WHERE Id='$($profile.HrEmployee)' FOR JSON PATH);"
Assert-O1EmployeeMetadata $hr[0] 'Final' $start.EmployeeBaseline
$auditRows=Read-NewRows 'audit.AuditEvents' 'Id,EventType,ActorUserId,ActorRoleCode,TargetType,TargetId,CorrelationId,OccurredAt,EmployeeScopeId,DeviceReference,ProtectedChangesReference'
$httpTraces=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach($request in $failure.Http){Assert-O1 ($httpTraces.Add([string]$request.Trace)) 'Duplicate retained HTTP trace.'}
$allowedEvents=@('web.otp.requested','web.session.created','otp.delivery.accepted','access.current_permissions.read','session.logout',
    'attendance.deployment.preview','attendance.deployment.detail','attendance.deployment.claim.evidence')
$allowedEvents+=@($operations.Values)
$allowedEvents+=@($operations.Values|ForEach-Object {$_+'.replay'})
foreach($audit in $auditRows){
    Assert-O1 ($audit.EventType -cin $allowedEvents -and [datetimeoffset]$audit.OccurredAt -ge $from -and
        [datetimeoffset]$audit.OccurredAt -le $until) 'Unexpected audit event or time.'
    if($audit.EventType -ceq 'otp.delivery.accepted'){
        $delivery=@($deliveries|Where-Object {[guid]$_.Id -eq [guid]$audit.TargetId})
        Assert-O1 ($delivery.Count -eq 1 -and $audit.ActorUserId -ceq 'sms-dispatcher' -and $audit.ActorRoleCode -ceq 'system' -and
            $audit.CorrelationId -ceq ([guid]$delivery[0].Id).ToString('N')) 'Delivery audit binding differs.'
    }else{
        Assert-O1 ($httpTraces.Contains([string]$audit.CorrelationId)) 'Audit not bound to captured request.'
    }
}
foreach($key in $operations.Keys){
    $operation=$operations[$key]
    $committed=@($auditRows|Where-Object {$_.EventType -ceq $operation})
    $requests=@($failure.Http|Where-Object {$_.Key -ceq $key -and $_.Status -eq 200})
    $replays=@($auditRows|Where-Object {$_.EventType -ceq ($operation+'.replay')})
    Assert-O1 ($committed.Count -eq 1 -and $replays.Count -eq $requests.Count-1 -and
        ![string]::IsNullOrWhiteSpace($committed[0].ProtectedChangesReference)) 'Write/replay audit cardinality differs.'
    $actor=if($key -cin @('O1-decision','O1-claim-decision')){$profile.AdminAccount}else{$profile.HrAccount}
    foreach($audit in @($committed)+@($replays)){
        Assert-O1 ([guid]$audit.ActorUserId -eq [guid]$actor -and
            @($requests|Where-Object {$_.Trace -ceq $audit.CorrelationId}).Count -eq 1) 'Write/replay actor or trace differs.'
    }
}
$reconstructed=Get-O1RetainedAuditExpectations $failure.Http $sessionRows $challenges $deliveries $binding $auditRows
Assert-O1AuditRows $auditRows $reconstructed $from $until
$privateFiles=@(Get-ChildItem -LiteralPath (Join-Path $root 'files') -Recurse -File -Force)
Assert-O1 ($privateFiles.Count -le 16) 'Private file bound exceeded.'
foreach($file in $privateFiles){Assert-PrivateSource $file.FullName}
foreach($name in $inputHashes.Keys){
    Assert-O1 ((Get-FileHash -LiteralPath (Join-Path $results $name) -Algorithm SHA256).Hash -ceq $inputHashes[$name]) 'Original run evidence changed.'
}
$report=[ordered]@{
    Utc=[datetimeoffset]::UtcNow.ToString('O');Kind='Supplemental SELECT-only reconciliation'
    OriginalRunStatus='Failed during final SQL bounds query; original failure retained'
    HttpChecksPassed=38;HttpRequests=45;NewLocalMfaSessions=2
    Target=$id;MigrationCount=25;DatabaseWrites=0;HttpRequestsRepeated=0;HostStarted=$false
    InputEvidenceSha256=$inputHashes;NewRows=$counts;IdReadSeconds=$timings
    BusinessPersistence='Passed';AuthRevocation='Passed';Receipts='Passed'
    AuditEventTraceAndWriteReplayChecks='Passed'
    ReconstructedExactAuditExpectations='Passed: 32 events, SQL-correlated to captured 45-request sequence'
    EvidenceLimit='Original transient response values were not retained; runtime response comparisons remain the original 38 passed checks, not newly observed responses'
    OriginalProtectedSql=$preserved.ProtectedSql;OriginalPriorFiles=$preserved.PriorFiles
    HrRevision=6;BusinessEvidenceFiles=$privateFiles.Count;NewRateBuckets=($rateCount-$start.CountsBefore.RateBuckets)
    TestOnlyClaimAmount=200;Currency='PKR';SettlementState=0;PaymentReleased=$false
    Scope='One synthetic deployment and claim only; not full HR or provider acceptance'
}
$bytes=[Text.UTF8Encoding]::new($false).GetBytes(($report|ConvertTo-Json -Depth 12))
Assert-ResultDirectory
$stream=[IO.File]::Open($output,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
try{$stream.Write($bytes,0,$bytes.Length)}finally{$stream.Dispose()}
Assert-PrivateSource $output
Write-Output "Supplemental reconciliation saved: $output"
