[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'HrPermissionP1Profile.ps1')
function Assert([bool]$value,[string]$message){if(!$value){throw $message}}
$profile=Get-P1Profile
$root=$profile.Root
Assert ($root -ceq 'C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261020P1Retry01') 'Only the approved completed retry can be reconciled.'
$accountsPath='C:\Users\Huma.Khan\AppData\Local\Cosmix\LocalTesting\Test-Accounts.json'
$sourceSettings='C:\Users\Huma.Khan\AppData\Local\Cosmix\LocalTesting\Dashboard-20261019-Diagnostic.json'
Assert ([Security.Principal.WindowsIdentity]::GetCurrent().Name -ieq $profile.Owner) 'Owner changed.'

# Import definitions, never either runner's setup, authentication or write sequence.
foreach($source in @(
    @{Path='Invoke-HrPermissionP1.ps1';Names=@('New-P1SqlConnection','Read-Sql','Protected-Snapshot','Prior-Snapshot','Verify-P1State','Verify-P1Bounds')},
    @{Path='Invoke-HrVehicleV1.ps1';Names=@('Assert-PrivateSource','Assert-Snapshot','Record-Counts')},
    @{Path='Invoke-HrReportsR1.ps1';Names=@('New-Rows')}
)){
    $tokens=$null;$errors=$null
    $ast=[Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot $source.Path),[ref]$tokens,[ref]$errors)
    Assert ($errors.Count -eq 0) 'Source parse failed.'
    foreach($name in $source.Names){
        $definitions=@($ast.FindAll({param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -ceq $name},$true))
        Assert ($definitions.Count -eq 1) 'Unexpected source definition.'
        . ([scriptblock]::Create($definitions[0].Extent.Text))
    }
}
function Rows([string]$query){
    $value=ConvertFrom-Json -InputObject (Read-Sql $query) -AsHashtable -NoEnumerate -DateKind String
    Assert ($value -is [array] -and $value.Count -le 10000) 'Unexpected reconciliation rows.'
    return ,$value
}
function IdList($rows){
    Assert (@($rows).Count -gt 0) 'Empty captured identity set.'
    return (@($rows|ForEach-Object {"'"+([guid]$_.Id).ToString('D')+"'"}) -join ',')
}
$results=Join-Path $root 'results'
$start=Get-Content -LiteralPath (Join-Path $results 'start.json') -Raw|ConvertFrom-Json -AsHashtable -DateKind String
$failure=Get-Content -LiteralPath (Join-Path $results 'failure.json') -Raw|ConvertFrom-Json -AsHashtable -DateKind String
$post=Get-Content -LiteralPath (Join-Path $results 'post-host-preservation.json') -Raw|ConvertFrom-Json -AsHashtable
$hostFinal=Get-Content -LiteralPath (Join-Path $results 'host-final.json') -Raw|ConvertFrom-Json -AsHashtable
Assert ($start.PacketSha256 -ceq '44C6513F86133A5B987808C734AAC221F548414FB1CB629FDFD5B1C53BD6DB30') 'Run packet differs.'
Assert ($failure.Budget.Requests -eq 65 -and $failure.Budget.Logins -eq 5 -and
    $failure.Checks.Count -eq 52 -and @($failure.Checks|Where-Object {!$_.Passed}).Count -eq 0 -and
    $failure.HttpResults.Count -eq 65 -and $post.Unchanged -eq $true) 'Recorded run differs.'
Assert ($hostFinal.Exited -eq $true -and $hostFinal.ProcessId -eq 18788 -and !$hostFinal.CleanupPerformed) 'Owned host state differs.'
Assert (@(Get-NetTCPConnection -LocalPort 7481 -State Listen -ErrorAction SilentlyContinue).Count -eq 0) 'Test port is in use.'
$identity=Read-Sql "SELECT CONCAT(CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')),'|',DB_NAME(),'|',CONVERT(int,SERVERPROPERTY('ProcessID')),'|',SUSER_SNAME(d.owner_sid),'|',CONVERT(nvarchar(36),r.family_guid),'|',d.state_desc) FROM sys.databases d JOIN sys.database_recovery_status r ON r.database_id=d.database_id WHERE d.database_id=DB_ID();"
Assert ($identity -ceq 'Software-PC1\LOCALDB#9F3001F7|Cosmix_Testing|25768|COSMIXRND\Huma.Khan|D9C34627-18A2-41CF-A804-821E39516303|ONLINE') 'SQL target differs.'
Assert ((Read-Sql 'SELECT STRING_AGG(CONVERT(nvarchar(max),physical_name),''|'') WITHIN GROUP(ORDER BY file_id) FROM sys.database_files;') -ieq ($profile.DataFile+'|'+$profile.LogFile)) 'SQL files differ.'
$currentCounts=Record-Counts
foreach($key in $post.Counts.Keys){Assert ($currentCounts[$key] -eq $post.Counts[$key]) "Post-run $key changed; stop."}
Assert ($currentCounts.Migrations -eq 25) 'No migration is allowed.'
$from=([datetimeoffset]::Parse($start.Utc)).ToString('o')
$until=([datetimeoffset]::Parse($failure.Utc)).ToString('o')
Assert ([datetimeoffset]$until -gt [datetimeoffset]$from) 'Invalid run interval.'
$interval="CONVERT(datetimeoffset,'$from') AND CONVERT(datetimeoffset,'$until')"
$actors=Rows "SELECT (SELECT Id,EmployeeId,LoginName,SecurityVersion FROM auth.UserAccounts ORDER BY LoginName FOR JSON PATH);"
Assert ($actors.Count -eq 3 -and [guid]$actors[0].Id -eq [guid]$profile.AdminAccount -and
    [guid]$actors[1].Id -eq [guid]$profile.HrAccount -and $actors[2].LoginName -ceq 'superadmin@example.invalid') 'Actor binding differs.'
foreach($actor in $actors){$actor.Id=([guid]$actor.Id).ToString('D');$actor.EmployeeId=([guid]$actor.EmployeeId).ToString('D')}
$saActor=$actors[2]
$actorIds=IdList $actors
$roles=Rows "SELECT (SELECT Id,Code,Name,SystemRole,CreatedByUserId,CreatedAt FROM auth.Roles WHERE Code IN('$($profile.AdminCode)','$($profile.RequesterCode)') ORDER BY Code FOR JSON PATH);"
Assert ($roles.Count -eq 2) 'Expected exactly two P1 roles.'
$roleIds=IdList $roles
$rolePermissions=Rows "SELECT (SELECT rp.Id,rp.RoleId,p.Code,rp.GrantedByUserId,rp.GrantedAt FROM auth.RolePermissions rp JOIN auth.Permissions p ON p.Id=rp.PermissionId WHERE rp.RoleId IN($roleIds) ORDER BY rp.RoleId,p.Code FOR JSON PATH);"
$assignments=Rows "SELECT (SELECT Id,RoleId,UserAccountId,EmployeeId,AssignedByUserId,EffectiveFrom,EffectiveTo FROM auth.UserRoleAssignments WHERE RoleId IN($roleIds) ORDER BY RoleId FOR JSON PATH);"
Assert ($assignments.Count -eq 2) 'Expected exactly two assignments.'
$assignmentResults=@()
$bindings=@{AdminRole=([guid]$roles[0].Id).ToString('D');RequesterRole=([guid]$roles[1].Id).ToString('D')}
foreach($actor in @($actors[0],$actors[1])){
    $a=@($assignments|Where-Object {[guid]$_.UserAccountId -eq [guid]$actor.Id})
    Assert ($a.Count -eq 1) 'Assignment actor differs.'
    Assert (([datetimeoffset]$a[0].EffectiveTo-[datetimeoffset]$a[0].EffectiveFrom) -eq [timespan]::FromHours(24) -and
        [datetimeoffset]$a[0].EffectiveFrom -ge [datetimeoffset]$from -and [datetimeoffset]$a[0].EffectiveFrom -le [datetimeoffset]$until) 'Assignment window differs.'
    $assignmentResults+=,@{assignment=@{id=([guid]$a[0].Id).ToString('D')}}
    if(!$bindings.ContainsKey('EffectiveFrom')){$bindings.EffectiveFrom=$a[0].EffectiveFrom;$bindings.EffectiveTo=$a[0].EffectiveTo}
    Assert ($a[0].EffectiveFrom -ceq $bindings.EffectiveFrom -and $a[0].EffectiveTo -ceq $bindings.EffectiveTo) 'Different assignment windows.'
}
$sessionRows=Rows "SELECT (SELECT Id,UserAccountId,SecurityVersion,SmsOtpChallengeId,CorrelationId,CreatedAt,RevokedAt FROM auth.UserSessions WHERE CreatedAt BETWEEN $interval ORDER BY CreatedAt FOR JSON PATH,INCLUDE_NULL_VALUES);"
Assert ($sessionRows.Count -eq 5) 'Expected five captured run sessions.'
$sessions=@($sessionRows|ForEach-Object {@{Id=([guid]$_.Id).ToString('D')}})
foreach($actor in $actors){
    $owned=@($sessionRows|Where-Object {[guid]$_.UserAccountId -eq [guid]$actor.Id}|Sort-Object CreatedAt)
    $count=if($actor.Id -ceq $saActor.Id){1}else{2}
    Assert ($owned.Count -eq $count) 'Session actor count differs.'
    $before=[int]$owned[0].SecurityVersion
    Assert ([int]$actor.SecurityVersion -eq $before+$count-1 -and [int]$owned[-1].SecurityVersion -eq [int]$actor.SecurityVersion) 'Session-bound security increment differs.'
    $actor.SecurityVersion=$before
}
$sessionIds=IdList $sessionRows
$challenges=Rows "SELECT (SELECT Id,UserAccountId,IssuedAt,ConsumedAt,Purpose FROM auth.MobileOtpChallenges WHERE IssuedAt BETWEEN $interval ORDER BY IssuedAt FOR JSON PATH);"
Assert ($challenges.Count -eq 5) 'Expected five run challenges.'
$challengeIds=IdList $challenges
$deliveries=Rows "SELECT (SELECT Id,ChallengeId,CreatedAt,SubmittedAt,Status,Attempts FROM auth.OtpDeliveries WHERE ChallengeId IN($challengeIds) ORDER BY CreatedAt FOR JSON PATH);"
$receipts=Rows "SELECT (SELECT Id,[Key],Operation,ActorUserId,CreatedAt,CompletedAt FROM workflow.IdempotencyReceipts WHERE [Key] LIKE 'P1-%' ORDER BY [Key] FOR JSON PATH);"
$audits=Rows "SELECT (SELECT Id,EventType,ActorUserId,ActorRoleCode,TargetType,TargetId,CorrelationId,OccurredAt,EmployeeScopeId,DeviceReference,ProtectedChangesReference FROM audit.AuditEvents WHERE OccurredAt BETWEEN $interval ORDER BY OccurredAt,Id FOR JSON PATH,INCLUDE_NULL_VALUES);"
$assignmentIds=IdList $assignments
$codes=(@(($profile.AdminPermissions+$profile.RequesterPermissions)|Select-Object -Unique)|ForEach-Object {"'$_'"}) -join ','
$newPredicates=@{
    'auth.Roles'="Id IN($roleIds)";'auth.Permissions'="Code IN($codes)";'auth.RolePermissions'="RoleId IN($roleIds)"
    'auth.UserRoleAssignments'="Id IN($assignmentIds)";'auth.UserAccessScopes'="UserRoleAssignmentId IN($assignmentIds)"
    'workflow.IdempotencyReceipts'="[Key] LIKE 'P1-%'";'auth.UserSessions'="Id IN($sessionIds)"
    'auth.RefreshTokens'="UserSessionId IN($sessionIds)";'auth.MobileOtpChallenges'="Id IN($challengeIds)"
    'auth.OtpDeliveries'="ChallengeId IN($challengeIds)";'audit.AuditEvents'="OccurredAt BETWEEN $interval"
}
$baselineCounts=$start.AccessCounts;$historicalIds=@{}
foreach($table in $newPredicates.Keys){
    $rows=Rows "SELECT COALESCE((SELECT Id FROM $table WHERE NOT($($newPredicates[$table])) ORDER BY Id FOR JSON PATH),N'[]');"
    Assert ($rows.Count -eq [int]$baselineCounts[$table]) "Historical row population differs for $table."
    $historicalIds[$table]=ConvertTo-Json -InputObject @($rows|ForEach-Object Id) -Compress
}
$protected=$start.ProtectedSnapshots;$prior=$start.PriorFiles;$beforeCounts=$start.CountsBefore;$budget=$failure.Budget
# Candidate retained rate keys are accepted only if their complete protected hash
# still equals the recorded pre-run hash. Missing/changed history fails that comparison.
$retainedRates=Rows "SELECT COALESCE((SELECT [Key] FROM auth.AuthRateBuckets WHERE WindowStartsAt<CONVERT(datetimeoffset,'$from') ORDER BY [Key] FOR JSON PATH),N'[]');"
$originalRateKeys=ConvertTo-Json -InputObject @($retainedRates|ForEach-Object Key) -Compress
$wrongCorrelationAssertion=$false
try {Verify-P1State} catch {
    Assert ($_.Exception.Message -ceq 'Unrelated new audit.') ('Different state verification failure: '+$_.Exception.Message)
    $wrongCorrelationAssertion=$true
}
Assert $wrongCorrelationAssertion 'Expected original audit-contract mismatch was not reproduced.'
Assert ($audits.Count -eq 51 -and $currentCounts.Audits-$beforeCounts.Audits -eq 51) 'Audit count differs.'

# The strict audit checker is independent of the failed runner assertion.
. (Join-Path $PSScriptRoot 'HrPermissionP1AuditVerifier.ps1')
$auditResult=Assert-P1AuditEvidence -Audits $audits -Actors $actors -Roles $roles -RolePermissions $rolePermissions `
    -Assignments $assignments -Receipts $receipts -Sessions $sessionRows -Challenges $challenges -Deliveries $deliveries `
    -StartedAt ([datetimeoffset]$from) -StoppedAt ([datetimeoffset]$until)
Assert $auditResult.Passed 'Audit reconciliation failed.'
Verify-P1Bounds
Assert-Snapshot $protected (Protected-Snapshot) 'All retained SQL state'
Assert-Snapshot $prior (Prior-Snapshot) 'Retained private evidence'
[pscustomobject]@{
    Result='P1 functional and SQL postconditions reconciled; original runner exit remains failed'
    HttpChecks=52;HttpRequests=65;LocalMfaSessions=5;AuditEvents=51;AuditEvidence=$auditResult
    CreatedRoles=2;CreatedPermissions=5;CreatedRolePermissionLinks=6;CreatedAssignments=2;CreatedScopes=2;CreatedReceipts=6
    OriginalCorrelationAssertionReproduced=$wrongCorrelationAssertion
    PostHostProtectedSnapshots=$post.ProtectedSnapshots.Count;PriorFilesUnchanged=$true
    HostExited=$true;MigrationsApplied=$false;HttpReplayed=$false;SqlWrites=$false
    AssignmentExpiresAt=$bindings.EffectiveTo
}
