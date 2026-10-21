[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ReleaseDirectory,
    [ValidateSet('Inspect','Execute')][string]$Mode='Inspect',
    [string]$ReviewedPacketSha256='',
    [switch]$ExistingSourceReuseReady
)
# Parent-operated continuation only. Existing sources are verified, never seeded.
$ErrorActionPreference='Stop'
$VerbosePreference='SilentlyContinue';$DebugPreference='SilentlyContinue'
. (Join-Path $PSScriptRoot 'HrOutstationO1Profile.ps1')
$profile=Get-O1Profile
$root=$profile.Root;$pipe=$profile.Pipe;$owner=$profile.Owner;$base=[uri]($profile.Url+'/')
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$release=[IO.Path]::GetFullPath($ReleaseDirectory);$assembly=Join-Path $release 'Cosmix.Api.dll'
$accountsPath='C:\Users\Huma.Khan\AppData\Local\Cosmix\LocalTesting\Test-Accounts.json'
$sourceSettings='C:\Users\Huma.Khan\AppData\Local\Cosmix\LocalTesting\Dashboard-20261019-Diagnostic.json'
$manifestPath=Join-Path $PSScriptRoot 'Outstation-O1-Fixture-Manifest-20261020.json'
$resultsRelative='results'
$checks=[Collections.Generic.List[object]]::new();$httpResults=[Collections.Generic.List[object]]::new()
$clients=[Collections.Generic.List[object]]::new();$sessions=[Collections.Generic.List[object]]::new()
$auditExpected=[Collections.Generic.List[object]]::new()
$historicalIds=@{};$baselineCounts=@{};$budget=@{Requests=0;Logins=0;Keys=@{}}
$bindings=@{};$plan=@{};$clientState=@{};$committed=@{};$hostProcess=$null;$started=$false;$created=$false
$newRateKeys=@()
$common=Join-Path $PSScriptRoot 'Invoke-HrVehicleV1.ps1'
$p1=Join-Path $PSScriptRoot 'Invoke-HrPermissionP1.ps1'
$reportCommon=Join-Path $PSScriptRoot 'Invoke-HrReportsR1.ps1'
# Definitions only: none of the frozen runners' top-level code is invoked.
foreach($source in @(
    @{Path=$common;Names=@('Assert','Check','Assert-PrivateSource','New-PrivateDirectory','Write-Private',
        'Write-Json','Write-Result','New-Client','Login','Assert-Snapshot','Record-Counts')},
    @{Path=$reportCommon;Names=@('New-Rows')},
    @{Path=$p1;Names=@('Read-Sql','Prior-Snapshot')}
)){
    $tokens=$null;$errors=$null
    $ast=[Management.Automation.Language.Parser]::ParseFile($source.Path,[ref]$tokens,[ref]$errors)
    Assert-O1 ($errors.Count -eq 0) 'Frozen helper parse failed.'
    foreach($name in $source.Names){
        $defs=@($ast.FindAll({param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -ceq $name},$true))
        Assert-O1 ($defs.Count -eq 1) 'Frozen helper definition differs.'
        $definition=$defs[0].Extent.Text
        if($name -ceq 'Login'){
            Assert-O1 ($definition.Contains('$i -lt 20')) 'Login polling definition differs.'
            $definition=$definition.Replace('$i -lt 20','$i -lt 1')
        }
        if($name -ceq 'Prior-Snapshot'){
            Assert-O1 ($definition.Contains("'Huma20261020P1'")) 'Prior evidence definition differs.'
            $definition=$definition.Replace("'Huma20261020P1'","'Huma20261020P1','Huma20261020P1Retry01','Huma20261020O1','Huma20261020O1Retry01','Huma20261020O1Retry02'")
        }
        . ([scriptblock]::Create($definition))
    }
}
# Read-Sql retains P1's SELECT-before-connect, one-column/result and 20-second command bound.
function New-P1SqlConnection {
    $value=Get-P1SqlConnectionString
    Assert-P1SqlConnectionString $value
    $builder=[System.Data.SqlClient.SqlConnectionStringBuilder]::new($value)
    $builder['Application Name']='Cosmix.HrOutstation.O1.Inspect'
    return [System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
}
function Json-Rows([string]$query){
    return ,(ConvertFrom-Json -InputObject (Read-Sql $query) -AsHashtable -NoEnumerate -DateKind String)
}
function Read-O1EmployeeMetadata {
    $rows=Json-Rows "SELECT (SELECT Id,Status,OnboardingStatus,JoinedOn,DraftRevision,DATALENGTH(RowVersion) AS RowVersionByteLength,CASE WHEN DATALENGTH(RowVersion)=8 THEN CONVERT(varchar(16),CONVERT(binary(8),RowVersion),2) ELSE NULL END AS RowVersion FROM hr.Employees WHERE Id='$($profile.HrEmployee)' FOR JSON PATH,INCLUDE_NULL_VALUES);"
    Assert (@($rows).Count -eq 1) 'Exact HR employee row required.'
    Assert ($rows[0].RowVersionByteLength -eq 8) 'Employee SQL rowversion must be exactly eight bytes.'
    return $rows[0]
}
function Protected-Snapshot {
    $limits=Get-O1RowLimits
    $tables=Json-Rows "SELECT (SELECT s.name AS [schema],t.name AS [table] FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE t.is_ms_shipped=0 ORDER BY s.name,t.name FOR JSON PATH);"
    $snapshot=@{}
    foreach($table in $tables){
        $plain=$table.schema+'.'+$table.table
        if($plain -cin @('auth.UserAccounts','auth.AuthRateBuckets')){continue}
        $qualified='['+$table.schema.Replace(']',']]')+'].['+$table.table.Replace(']',']]')+']'
        $literal=$qualified.Replace("'","''")
        if(!$baselineCounts.ContainsKey($plain)){$baselineCounts[$plain]=[int](Read-Sql "SELECT COUNT(*) FROM $qualified;")}
        $order=Read-Sql ("SELECT STRING_AGG(CONVERT(nvarchar(max),QUOTENAME(c.name)),',') WITHIN GROUP(ORDER BY ic.key_ordinal) FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE i.object_id=OBJECT_ID(N'"+$literal+"') AND i.is_primary_key=1;")
        Assert ($order -and $order -ne 'NULL') 'Missing deterministic primary key.'
        $where=''
        if($limits.ContainsKey($plain)){
            if(!$historicalIds.ContainsKey($plain)){
                $rows=Json-Rows "SELECT COALESCE((SELECT Id FROM $qualified ORDER BY Id FOR JSON PATH),N'[]');"
                Assert ($rows -is [array] -and $rows.Count -le 10000 -and $rows.Count -eq $baselineCounts[$plain]) 'Baseline changed or exceeds bound.'
                $historicalIds[$plain]=ConvertTo-Json -InputObject @($rows|ForEach-Object {ConvertTo-O1Guid $_.Id}) -Compress
            }
            $json=$historicalIds[$plain].Replace("'","''")
            $where=" WHERE Id IN(SELECT CONVERT(uniqueidentifier,[value]) FROM OPENJSON(N'$json'))"
        }
        $snapshot[$plain]=Read-Sql "SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM $qualified$where ORDER BY $order FOR JSON PATH,INCLUDE_NULL_VALUES)),2);"
    }
    foreach($actor in $actors){
        $id=ConvertTo-O1Guid $actor.Id
        # SecurityVersion, phone bindings, original access grants and all business data remain protected.
        $cols=Read-Sql "SELECT STRING_AGG(CONVERT(nvarchar(max),QUOTENAME(name)),',') WITHIN GROUP(ORDER BY column_id) FROM sys.columns WHERE object_id=OBJECT_ID('auth.UserAccounts') AND name NOT IN('FailedLoginCount','LockedUntil','RowVersion');"
        $snapshot["account-$id"]=Read-Sql "SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT $cols FROM auth.UserAccounts WHERE Id='$id' FOR JSON PATH,INCLUDE_NULL_VALUES)),2);"
    }
    $allowed=@('web-request-global','web-password-global','web-verify-global','request-global')
    foreach($id in @($profile.AdminAccount,$profile.HrAccount)){foreach($prefix in @('web-request-account:','web-password-account:','web-verify-account:')){$allowed+=$prefix+$id}}
    $allowed+=@($newRateKeys)
    $sql=($allowed|ForEach-Object {"'$_'"}) -join ','
    $snapshot['unrelated-rates']=Read-Sql "SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM auth.AuthRateBuckets WHERE [Key] NOT IN($sql) ORDER BY [Key] FOR JSON PATH,INCLUDE_NULL_VALUES)),2);"
    $snapshot['account-population']=Read-Sql "SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT Id FROM auth.UserAccounts ORDER BY Id FOR JSON PATH)),2);"
    return $snapshot
}
function Add-O1Expected($event,$actor,$role,$type,$target,$employee,$device,$trace,[bool]$proof=$false){
    $auditExpected.Add(@{EventType=$event;ActorUserId=$actor;ActorRoleCode=$role;TargetType=$type;TargetId=$target
        EmployeeScopeId=$employee;DeviceReference=$device;CorrelationId=$trace;Protected=$proof})
}
function Begin-Request($client,[string]$method,[string]$path,$body=$null,[string]$csrf='',[string]$key=''){
    Assert (Test-O1Route $method $path $body $key $plan $bindings) 'Unapproved O1 request.'
    Use-O1Budget $budget $path $key
    if(!$clientState.ContainsKey($client)){$clientState[$client]=@{}}
    if($path -ceq '/api/v1/auth/web/login'){
        $clientState[$client].Email=$body.email
        $clientState[$client].Actor=if($body.email -ceq 'hr@example.invalid'){$profile.HrAccount}else{$profile.AdminAccount}
    }
    $message=[Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::new($method),$path)
    $message.Version=[version]'1.1';$message.VersionPolicy=[Net.Http.HttpVersionPolicy]::RequestVersionExact
    if($null -ne $body){
        $json=$body|ConvertTo-Json -Depth 12 -Compress
        Assert ([Text.Encoding]::UTF8.GetByteCount($json) -le 8192) 'O1 body exceeds bound.'
        $message.Content=[Net.Http.StringContent]::new($json,[Text.Encoding]::UTF8,'application/json')
    }
    if($csrf){$message.Headers.Add('X-CSRF-TOKEN',$csrf)}
    if($key){$message.Headers.Add('Idempotency-Key',$key)}
    $timeout=[Threading.CancellationTokenSource]::new([timespan]::FromSeconds(45))
    return @{Task=$client.SendAsync($message,[Net.Http.HttpCompletionOption]::ResponseHeadersRead,$timeout.Token)
        Message=$message;Timeout=$timeout;Client=$client;Path=$path;Method=$method;Key=$key;Body=$body}
}
function Finish-Request($pending){
    try {
        $response=$pending.Task.GetAwaiter().GetResult()
        try {
            $traces=@($response.Headers.GetValues('X-Correlation-ID'))
            Assert ($traces.Count -eq 1) 'Server trace header absent/ambiguous.'
            $trace=[string]$traces[0];Assert-O1Trace $trace
            Assert (@($httpResults|Where-Object {$_.Trace -ceq $trace}).Count -eq 0) 'Server trace reused.'
            $stream=$response.Content.ReadAsStream();$memory=[IO.MemoryStream]::new()
            try {
                $buffer=[byte[]]::new(4096)
                while(($n=$stream.ReadAsync($buffer,0,$buffer.Length,$pending.Timeout.Token).GetAwaiter().GetResult()) -gt 0){
                    Assert ($memory.Length+$n -le 1MB) 'O1 response exceeds bound.';$memory.Write($buffer,0,$n)
                }
                $text=[Text.Encoding]::UTF8.GetString($memory.ToArray())
            } finally {$memory.Dispose();$stream.Dispose()}
            $value=if($text){ConvertFrom-Json -InputObject $text -AsHashtable -NoEnumerate -DateKind String}else{$null}
            $status=[int]$response.StatusCode;$state=$clientState[$pending.Client]
            $httpResults.Add(@{Method=$pending.Method;Path=$pending.Path;Key=$pending.Key;Status=$status;Trace=$trace})
            if($pending.Path -ceq '/api/v1/auth/web/login' -and $status -eq 202){
                $state.Challenge=ConvertTo-O1Guid $value.challengeId
                Add-O1Expected 'web.otp.requested' 'anonymous' 'authentication' 'otp-challenge' $state.Challenge $null $null $trace
            } elseif($pending.Path -ceq '/api/v1/auth/web/otp/verify' -and $status -eq 200){
                $state.Session=ConvertTo-O1Guid $value.sessionId
                Add-O1Expected 'web.session.created' $state.Actor 'authentication' 'session' $state.Session $null $null $trace
            } elseif(($pending.Path -ceq '/api/v1/me/permissions' -and $status -eq 200) -or
                ($pending.Path -ceq '/api/v1/auth/web/logout' -and $status -eq 204)){
                $hr=$state.Actor -ceq $profile.HrAccount
                $employee=if($hr){$profile.HrEmployee}else{$profile.AdminEmployee}
                $roles=if($hr){@('HR','ATTHUMA13B03','ATTHUMA13B06',$profile.RequesterCode)}else{@('ADMIN',$profile.AdminCode)}
                foreach($role in $roles){
                    if($pending.Method -ceq 'GET'){
                        Add-O1Expected 'access.current_permissions.read' $state.Actor $role 'UserAccount' $state.Actor $employee ("session:"+$state.Session) $trace
                    } else {
                        Add-O1Expected 'session.logout' $state.Actor $role 'session' $state.Session $employee ("session:"+$state.Session+';device:') $trace
                    }
                }
            } elseif($pending.Path.StartsWith($profile.Api,[StringComparison]::Ordinal) -and $status -eq 200){
                $role=$profile.RequesterCode;$target=$profile.HrEmployee;$proof=$false
                if($pending.Method -ceq 'GET'){
                    $event=if($pending.Path.EndsWith('/calculation')){'attendance.deployment.preview'}elseif($pending.Path.EndsWith('/evidence')){'attendance.deployment.claim.evidence'}else{'attendance.deployment.detail'}
                } else {
                    $operations=@{'O1-request'='attendance.deployment.request';'O1-decision'='attendance.deployment.decide'
                        'O1-claim'='attendance.deployment.claim.request';'O1-claim-decision'='attendance.deployment.claim.decide'}
                    Assert ($operations.ContainsKey($pending.Key)) 'A negative probe unexpectedly committed; stop without retry.'
                    $event=$operations[$pending.Key];$target=ConvertTo-O1Guid $value.id
                    if($pending.Key -cin @('O1-decision','O1-claim-decision')){$role='ADMIN'}
                    # Concurrent responses may complete in either order. Verify resolves write/replay using exact receipt/audit traces.
                    $proof=$true
                }
                Add-O1Expected $event $state.Actor $role 'Attendance' $target $profile.HrEmployee $state.Session $trace $proof
            }
            return @{Status=$status;Value=$value;Trace=$trace}
        } finally {$response.Dispose()}
    } finally {$pending.Timeout.Dispose();$pending.Message.Dispose()}
}
function Request($client,[string]$method,[string]$path,$body=$null,[string]$csrf='',[string]$key=''){
    return Finish-Request (Begin-Request $client $method $path $body $csrf $key)
}
function Register-Command([string]$key){$plan[$key]=Get-O1Command $key $bindings;return $plan[$key]}
function Send-Command($session,$command){
    return Request $session.Client POST $command.Path $command.Body $session.Csrf $command.Key
}
function Concurrent-Pair($session,$command){
    # Independent handlers force separate HTTP/1.1 connections; both sends start before either wait.
    $left=New-Client $session.Cookies;$right=New-Client $session.Cookies
    $clientState[$left]=$clientState[$session.Client];$clientState[$right]=$clientState[$session.Client]
    $a=Begin-Request $left POST $command.Path $command.Body $session.Csrf $command.Key
    $b=Begin-Request $right POST $command.Path $command.Body $session.Csrf $command.Key
    $ra=$null;$rb=$null
    try {$ra=Finish-Request $a} finally {$rb=Finish-Request $b}
    Assert-O1ConcurrentTraces $ra.Trace $rb.Trace
    Check ($command.Key+': concurrent same result') ($ra.Status -eq 200 -and $rb.Status -eq 200 -and
        ($ra.Value|ConvertTo-Json -Depth 20 -Compress) -ceq ($rb.Value|ConvertTo-Json -Depth 20 -Compress))
    return $ra
}
function Expect-Denial($session,$command,[int]$status,[string[]]$codes){
    $r=Send-Command $session $command
    Check ($command.Key+': expected rejection') ($r.Status -eq $status -and $r.Value.title -cin $codes)
}
function Changed-Payload($session,$command){
    $copy=@{};foreach($key in $command.Body.Keys){$copy[$key]=$command.Body[$key]};$copy.reason=$profile.ChangedReason
    Expect-Denial $session @{Path=$command.Path;Body=$copy;Key=$command.Key} 409 @('idempotency_key_reused')
}
function Successful-Pair($session,$command){
    $a=Send-Command $session $command;$b=Send-Command $session $command
    Check ($command.Key+': success and exact replay') ($a.Status -eq 200 -and $b.Status -eq 200 -and
        ($a.Value|ConvertTo-Json -Depth 20 -Compress) -ceq ($b.Value|ConvertTo-Json -Depth 20 -Compress))
    return $a
}
function Assert-Calculation($result){
    Check 'exact retained O1 valuation' ($result.Status -eq 200 -and $result.Value.state -ceq 'ready' -and
        $result.Value.valuation.state -ceq 'ready' -and $result.Value.valuation.dayUnits -eq 2 -and
        $result.Value.valuation.amount -eq 200 -and $result.Value.valuation.currency -ceq 'PKR' -and
        (ConvertTo-O1Guid $result.Value.policyVersionId) -ceq $profile.PolicyVersion -and
        @($result.Value.policyDays).Count -eq 2 -and @($result.Value.approvedBreakIds).Count -eq 0)
    foreach($day in $result.Value.policyDays){
        Assert ((ConvertTo-O1Guid $day.policyVersionId) -ceq $profile.PolicyVersion -and
            @($day.assignments).Count -eq 1 -and (ConvertTo-O1Guid $day.assignments[0]) -ceq '20102001-0009-4000-8000-000000000009') 'Policy day source differs.'
    }
}
function Invoke-O1Business {
    $hr=Login $credentials 'hr@example.invalid';$admin=Login $credentials 'admin@example.invalid'
    foreach($s in @($hr,$admin)){
        $kind=if($s -eq $hr){2}else{7};$wanted=if($s -eq $hr){@('attendance.deployments.read','attendance.deployments.request')}else{@('attendance.deployments.read','attendance.deployments.decide')}
        foreach($code in $wanted){
            $matches=@($s.Grants|Where-Object {$_.permission -ceq $code -and $_.scopeKind -eq $kind -and
                ($kind -eq 7 -or [guid]$_.targetId -eq [guid]$profile.HrEmployee)})
            Assert ($matches.Count -eq 1) 'Live deployment grant differs.'
        }
    }
    $create=Register-Command 'O1-request'
    $trip=Concurrent-Pair $hr $create
    $bindings.DeploymentId=ConvertTo-O1Guid $trip.Value.id
    $bindings.DeploymentVersion=$trip.Value.version;$bindings.ApprovalVersion=$trip.Value.approvalVersion
    Changed-Payload $hr $create
    Expect-Denial $hr (Register-Command 'O1-request-duplicate') 409 @('deployment_overlap')
    Expect-Denial $admin (Register-Command 'O1-decision-stale') 409 @('deployment_version_conflict')
    Expect-Denial $hr (Register-Command 'O1-decision-self') 403 @('access_denied')
    $decision=Successful-Pair $admin (Register-Command 'O1-decision')
    Check 'deployment approved' ($decision.Value.state -ceq 'Approved')
    $bindings.DeploymentVersion=$decision.Value.version
    $calc=Request $hr.Client GET "$($profile.Api)/$($bindings.DeploymentId)/calculation"
    Assert-Calculation $calc;$bindings.Fingerprint=$calc.Value.fingerprint
    Expect-Denial $hr (Register-Command 'O1-claim-stale') 409 @('deployment_version_conflict')
    $claimCommand=Register-Command 'O1-claim'
    $claim=Concurrent-Pair $hr $claimCommand
    $bindings.ClaimId=ConvertTo-O1Guid $claim.Value.id
    Changed-Payload $hr $claimCommand
    # Identical body with a distinct key can hit stale-version before the active-claim guard.
    Expect-Denial $hr (Register-Command 'O1-claim-duplicate') 409 @('deployment_version_conflict','deployment_claim_already_exists')
    $detail=Request $hr.Client GET "$($profile.Api)/$($bindings.DeploymentId)"
    Check 'claim parent refreshed' ($detail.Status -eq 200 -and @($detail.Value.claims).Count -eq 1)
    $bindings.DeploymentVersion=$detail.Value.deployment.version;$bindings.ApprovalVersion=$claim.Value.approvalVersion
    Expect-Denial $admin (Register-Command 'O1-claim-decision-stale') 409 @('deployment_version_conflict')
    Expect-Denial $hr (Register-Command 'O1-claim-decision-self') 403 @('access_denied')
    $final=Successful-Pair $admin (Register-Command 'O1-claim-decision')
    Check 'approved claim exact amount' ($final.Value.state -ceq 'Approved' -and $final.Value.amount -eq 200 -and
        $final.Value.currency -ceq 'PKR' -and (ConvertTo-O1Guid $final.Value.policyVersionId) -ceq $profile.PolicyVersion)
    $replay=Send-Command $hr $claimCommand
    Check 'original claim replay retained after approval' ($replay.Status -eq 200 -and
        ($replay.Value|ConvertTo-Json -Depth 20 -Compress) -ceq ($claim.Value|ConvertTo-Json -Depth 20 -Compress))
    $detail=Request $hr.Client GET "$($profile.Api)/$($bindings.DeploymentId)"
    Check 'one finalized claim no home breaks' ($detail.Status -eq 200 -and
        (ConvertTo-O1Guid $detail.Value.deployment.finalizedClaimId) -ceq $bindings.ClaimId -and
        @($detail.Value.claims).Count -eq 1 -and @($detail.Value.homeBreaks).Count -eq 0)
    $retained=Request $hr.Client GET "$($profile.Api)/$($bindings.DeploymentId)/calculation"
    Assert-Calculation $retained
    Check 'retained fingerprint' ($retained.Value.fingerprint -ceq $calc.Value.fingerprint)
    $evidence=Request $hr.Client GET "$($profile.Api)/$($bindings.DeploymentId)/claims/$($bindings.ClaimId)/evidence"
    Check 'claim evidence bound to requester and source' ($evidence.Status -eq 200 -and $evidence.Value.schemaVersion -eq 2 -and
        (ConvertTo-O1Guid $evidence.Value.id) -ceq $bindings.ClaimId -and
        (ConvertTo-O1Guid $evidence.Value.deploymentId) -ceq $bindings.DeploymentId -and
        (ConvertTo-O1Guid $evidence.Value.employeeId) -ceq $profile.HrEmployee -and
        (ConvertTo-O1Guid $evidence.Value.requesterId) -ceq $profile.HrAccount -and
        $evidence.Value.calculation.fingerprint -ceq $calc.Value.fingerprint)
    foreach($session in @($hr,$admin)){
        $retainedCookie=$session.Cookies.GetCookieHeader($base)
        Check 'logout requires CSRF' ((Request $session.Client POST '/api/v1/auth/web/logout' @{}).Status -eq 400)
        Check 'logout' ((Request $session.Client POST '/api/v1/auth/web/logout' @{} $session.Csrf).Status -eq 204)
        $session.LoggedOut=$true
        $jar=[Net.CookieContainer]::new();$jar.SetCookies($base,$retainedCookie.Replace(';',','))
        $replayClient=New-Client $jar;$clientState[$replayClient]=$clientState[$session.Client]
        Check 'retained logout cookie denied' ((Request $replayClient GET '/api/v1/me/permissions').Status -eq 401)
    }
}
function Source-Snapshot {
    $result=@{}
    foreach($row in $manifest.sources){
        $id=ConvertTo-O1Guid $row.id
        Assert ($row.table -cin (Get-O1SourceTables)) 'Unapproved retained source table.'
        Assert ((Read-Sql "SELECT COUNT(*) FROM $($row.table) WHERE Id='$id';") -eq '1') 'Retained source row absent.'
        $result[$row.table]=Read-Sql "SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM $($row.table) WHERE Id='$id' FOR JSON PATH,INCLUDE_NULL_VALUES)),2);"
    }
    return $result
}
function Read-O1SourceTimestamps {
    # These are the actual domain timestamp columns; the masters/Policy have no CreatedAt.
    $rows=Json-Rows "SELECT (SELECT SourceTable,SourceColumn,OriginalTimestamp FROM (SELECT 'hr.EmployeeAssignments' AS SourceTable,'RecordedAt' AS SourceColumn,RecordedAt AS OriginalTimestamp FROM hr.EmployeeAssignments WHERE Id='20102001-0006-4000-8000-000000000006' UNION ALL SELECT 'hr.PolicyVersions','PublishedAt',PublishedAt FROM hr.PolicyVersions WHERE Id='20102001-0008-4000-8000-000000000008' UNION ALL SELECT 'hr.PolicyAssignments','RecordedAt',RecordedAt FROM hr.PolicyAssignments WHERE Id='20102001-0009-4000-8000-000000000009') x ORDER BY SourceTable FOR JSON PATH);"
    Assert-O1SourceTimestamps $rows ([datetimeoffset]::UtcNow)
    return ,$rows
}
function Verify-O1Bounds([bool]$complete=$false){
    $limits=Get-O1RowLimits
    foreach($table in $limits.Keys){
        $new=New-Rows $table;$count=[int](Read-Sql "SELECT COUNT(*) FROM $table WHERE $new;")
        Assert ($count -le $limits[$table] -and (!$complete -or $table -ceq 'audit.AuditEvents' -or $count -eq $limits[$table])) "O1 $table count differs."
    }
    Assert ([int](Read-Sql 'SELECT COUNT(*) FROM auth.AuthRateBuckets;') -le $beforeCounts.RateBuckets+12) 'O1 rate budget exceeded.'
    foreach($actor in $actors){
        Assert ((Read-Sql "SELECT COUNT(*) FROM auth.UserAccounts WHERE Id='$($actor.Id)' AND SecurityVersion=$($actor.SecurityVersion) AND FailedLoginCount=0 AND LockedUntil IS NULL;") -eq '1') 'Original account authority/login state changed.'
    }
    Assert ($budget.Requests -le 100 -and $budget.Logins -le 2) 'O1 HTTP budget exceeded.'
    if($created){
        $files=@(Get-ChildItem -LiteralPath (Join-Path $root 'files') -Recurse -Force)
        Assert (@($files|Where-Object {!$_.PSIsContainer}).Count -le 16) 'O1 private business file budget exceeded.'
        foreach($file in $files){
            Assert (!($file.Attributes -band [IO.FileAttributes]::ReparsePoint)) 'Linked O1 business evidence.'
            if(!$file.PSIsContainer){Assert-PrivateSource $file.FullName}
        }
    }
}
function Verify-O1State {
    Verify-O1Bounds $true
    $business=@{}
    foreach($table in @('hr.OutstationDeployments','hr.AllowanceClaims','hr.AllowanceClaimSubmissions','workflow.ApprovalRequests','workflow.ApprovalDecisions')){
        $new=New-Rows $table
        $business[$table]=Json-Rows "SELECT (SELECT * FROM $table WHERE $new ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES);"
    }
    Assert-O1BusinessRows $business['hr.OutstationDeployments'] $business['hr.AllowanceClaims'] `
        $business['hr.AllowanceClaimSubmissions'] $business['workflow.ApprovalRequests'] `
        $business['workflow.ApprovalDecisions'] $bindings $startedAt ([datetimeoffset]::UtcNow)
    $new=New-Rows 'auth.UserSessions'
    $sessionRows=Json-Rows "SELECT (SELECT Id,UserAccountId,SmsOtpChallengeId,CorrelationId,RevokedAt FROM auth.UserSessions WHERE $new FOR JSON PATH,INCLUDE_NULL_VALUES);"
    $new=New-Rows 'auth.MobileOtpChallenges'
    $challenges=Json-Rows "SELECT (SELECT Id,UserAccountId,Purpose,ConsumedAt FROM auth.MobileOtpChallenges WHERE $new FOR JSON PATH,INCLUDE_NULL_VALUES);"
    $new=New-Rows 'auth.OtpDeliveries'
    $deliveries=Json-Rows "SELECT (SELECT Id,ChallengeId,Status,Attempts,CASE WHEN ProtectedPayload IS NULL THEN 1 ELSE 0 END Cleared FROM auth.OtpDeliveries WHERE $new FOR JSON PATH);"
    foreach($s in $sessions){
        $state=$clientState[$s.Client]
        $row=@($sessionRows|Where-Object {[guid]$_.Id -eq [guid]$s.Id})
        $challenge=@($challenges|Where-Object {[guid]$_.Id -eq [guid]$state.Challenge})
        $delivery=@($deliveries|Where-Object {[guid]$_.ChallengeId -eq [guid]$state.Challenge})
        Assert ($row.Count -eq 1 -and [guid]$row[0].UserAccountId -eq [guid]$state.Actor -and $row[0].RevokedAt -and
            [guid]$row[0].SmsOtpChallengeId -eq [guid]$state.Challenge -and $challenge.Count -eq 1 -and
            [guid]$challenge[0].UserAccountId -eq [guid]$state.Actor -and $challenge[0].Purpose -ceq 'WebLogin' -and
            $challenge[0].ConsumedAt -and $delivery.Count -eq 1 -and $delivery[0].Status -eq 1 -and
            $delivery[0].Attempts -eq 1 -and $delivery[0].Cleared -eq 1) 'O1 authentication source binding differs.'
        $deliveryId=ConvertTo-O1Guid $delivery[0].Id
        Add-O1Expected 'otp.delivery.accepted' 'sms-dispatcher' 'system' 'otp-delivery' $deliveryId $null $null ([guid]$deliveryId).ToString('N')
        $issued=@($auditExpected|Where-Object {$_.EventType -ceq 'web.session.created' -and [guid]$_.TargetId -eq [guid]$s.Id})
        Assert ($issued.Count -eq 1 -and $issued[0].CorrelationId -ceq $row[0].CorrelationId) 'Session server trace differs.'
    }
    $sidSql=(@($sessions|ForEach-Object {"'$(ConvertTo-O1Guid $_.Id)'"}) -join ',')
    $new=New-Rows 'auth.RefreshTokens'
    Assert ((Read-Sql "SELECT COUNT(*) FROM auth.RefreshTokens WHERE $new AND (UserSessionId NOT IN($sidSql) OR RevokedAt IS NULL);") -eq '0') 'Refresh token not bound/revoked.'
    $new=New-Rows 'workflow.IdempotencyReceipts'
    $receipts=Json-Rows "SELECT (SELECT [Key],Operation,ActorUserId,CompletedAt FROM workflow.IdempotencyReceipts WHERE $new FOR JSON PATH);"
    $ops=@{'O1-request'='attendance.deployment.request';'O1-decision'='attendance.deployment.decide';
        'O1-claim'='attendance.deployment.claim.request';'O1-claim-decision'='attendance.deployment.claim.decide'}
    foreach($key in $ops.Keys){
        $r=@($receipts|Where-Object {$_.Key -ceq $key})
        $actor=if($key -cin @('O1-decision','O1-claim-decision')){$profile.AdminAccount}else{$profile.HrAccount}
        Assert ($r.Count -eq 1 -and $r[0].Operation -ceq $ops[$key] -and [guid]$r[0].ActorUserId -eq [guid]$actor -and $r[0].CompletedAt) 'Receipt binding differs.'
    }
    $new=New-Rows 'audit.AuditEvents'
    $audits=Json-Rows "SELECT (SELECT Id,EventType,ActorUserId,ActorRoleCode,TargetType,TargetId,CorrelationId,OccurredAt,EmployeeScopeId,DeviceReference,ProtectedChangesReference FROM audit.AuditEvents WHERE $new FOR JSON PATH,INCLUDE_NULL_VALUES);"
    # Identify exactly one original write per command among its captured successful response traces.
    foreach($op in $ops.Values){
        $candidates=@($auditExpected|Where-Object {$_.EventType -ceq $op})
        $original=@($audits|Where-Object {$_.EventType -ceq $op})
        Assert ($original.Count -eq 1 -and @($candidates|Where-Object {$_.CorrelationId -ceq $original[0].CorrelationId}).Count -eq 1) 'Missing/ambiguous committed trace.'
        foreach($candidate in $candidates){
            if($candidate.CorrelationId -cne $original[0].CorrelationId){$candidate.EventType+='.replay';$candidate.Protected=$false}
        }
    }
    Assert-O1AuditRows $audits $auditExpected.ToArray() $startedAt ([datetimeoffset]::UtcNow)
    $finalEmployee=Read-O1EmployeeMetadata
    Assert-O1EmployeeMetadata $finalEmployee 'Final' $employeeBaseline
    Assert-O1SourceHashes $sourceBaseline (Source-Snapshot)
    Assert-Snapshot $protected (Protected-Snapshot) 'Protected SQL'
    Assert-Snapshot $prior (Prior-Snapshot) 'Prior private files'
}

Assert ($PSVersionTable.PSVersion -ge [version]'7.5' -and $IsWindows) 'PowerShell 7.5+ on Windows required.'
Assert ([Security.Principal.WindowsIdentity]::GetCurrent().Name -ieq $owner) 'Unexpected operator.'
Assert ($release.StartsWith('D:\CosmixVerification\',[StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $assembly -PathType Leaf)) 'Reviewed frozen Release required.'
Assert (!(Test-Path -LiteralPath $root)) 'O1 root exists; no reset/adoption/resume.'
Assert (@(Get-NetTCPConnection -State Listen -LocalPort 7481 -ErrorAction SilentlyContinue).Count -eq 0) 'Port 7481 occupied.'
foreach($path in @($root,$release)){
    for($part=$path;$part;$part=[IO.Path]::GetDirectoryName($part)){
        if(Test-Path -LiteralPath $part){Assert (!((Get-Item -LiteralPath $part).Attributes -band [IO.FileAttributes]::ReparsePoint)) 'Linked ancestor forbidden.'}
    }
}
$process=Get-CimInstance Win32_Process -Filter 'ProcessId=25768'
Assert ($process -and $process.Name -eq 'sqlservr.exe' -and $process.CommandLine.Contains('-sLOCALDB#9F3001F7') -and $process.CommandLine.Contains('\Instances\MSSQLLocalDB\master.mdf')) 'SQL process differs.'
$processOwner=Invoke-CimMethod -InputObject $process -MethodName GetOwner
Assert ($processOwner.ReturnValue -eq 0 -and ($processOwner.Domain+'\'+$processOwner.User) -ieq $owner) 'SQL process owner differs.'
$observed=ConvertFrom-Json -InputObject (Read-Sql "SELECT (SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) AS Server,DB_NAME() AS [Database],SUSER_SNAME(d.owner_sid) AS Owner,ORIGINAL_LOGIN() AS Operator,r.family_guid AS Family,CONVERT(int,SERVERPROPERTY('ProcessID')) AS ProcessId,d.state_desc AS State,d.user_access_desc AS Access,d.is_read_only AS ReadOnly,(SELECT COUNT(*) FROM sys.triggers WHERE is_disabled=0 AND is_ms_shipped=0) AS Triggers,(SELECT COUNT(*) FROM sys.synonyms) AS Synonyms,(SELECT COUNT(*) FROM sys.sql_expression_dependencies WHERE referenced_server_name IS NOT NULL OR referenced_database_name IS NOT NULL) AS ExternalDependencies FROM sys.databases d JOIN sys.database_recovery_status r ON r.database_id=d.database_id WHERE d.database_id=DB_ID() FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);") -AsHashtable
$observed.Files=@((Read-Sql 'SELECT physical_name FROM sys.database_files ORDER BY file_id;') -split '\r?\n')
$transport=(Read-Sql "SELECT CONCAT(net_transport,'|',encrypt_option) FROM sys.dm_exec_connections WHERE session_id=@@SPID;") -split '\|'
$observed.Transport=$transport[0];$observed.Encryption=$transport[1]
$observed.Root=$root;$observed.Url=$profile.Url;$observed.RootExists=$false;$observed.PortOccupied=$false;$observed.LinkedPath=$false
$observed.AccountCount=[int](Read-Sql 'SELECT COUNT(*) FROM auth.UserAccounts;')
$observed.MarkerCount=[int](Read-Sql "SELECT COUNT(*) FROM workflow.IdempotencyReceipts WHERE [Key] LIKE 'O1-%';")
$observed.P1RoleCount=[int](Read-Sql "SELECT COUNT(*) FROM auth.Roles WHERE Code IN('TEST-HR-P1-20261020-ADMIN','TEST-HR-P1-20261020-REQUESTER');")
$observed.PermissionCount=[int](Read-Sql "SELECT COUNT(*) FROM auth.Permissions WHERE Code IN('attendance.leaves.benefits.review','attendance.vehicle.policy.manage','attendance.deployments.read','attendance.deployments.request','attendance.deployments.decide');")
Assert-O1Target $observed
Assert ((Read-Sql 'SELECT COUNT(*) FROM sys.database_files WHERE state<>0 OR type NOT IN(0,1);') -eq '0') 'Database file state differs.'
$expected=@(Get-ChildItem -LiteralPath (Join-Path $repo 'src/Cosmix.Infrastructure/Database/Migrations') -Filter '*.cs'|Where-Object Name -Match '^\d{14}_[^.]+\.cs$'|Sort-Object Name|ForEach-Object BaseName)
$applied=(Read-Sql 'SELECT MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;') -split '\r?\n'
Assert-P1Schema $expected $applied
Assert ($applied[-1] -ceq '20261015144424_AddFinancialAdjustmentsAndOutstationDeployments') 'Final schema differs.'
$actors=Json-Rows "SELECT (SELECT a.Id,a.EmployeeId,a.LoginName,a.Status,a.WebAccessEnabled,a.MfaRequired,a.SecurityVersion,e.ApprovedLoginPhoneNumber,e.FullLegalName,CAST(CASE WHEN a.LockedUntil IS NULL AND a.FailedLoginCount=0 THEN 1 ELSE 0 END AS bit) AS Unlocked FROM auth.UserAccounts a JOIN hr.Employees e ON e.Id=a.EmployeeId ORDER BY a.LoginName FOR JSON PATH);"
Assert-P1Actors $actors
foreach($actor in $actors){$actor.Id=ConvertTo-O1Guid $actor.Id;$actor.EmployeeId=ConvertTo-O1Guid $actor.EmployeeId}
$phoneCounts=@(foreach($a in $actors){
    [int](Read-Sql "SELECT COUNT(*) FROM auth.LoginPhoneBindings b JOIN auth.UserAccounts a ON a.Id=b.UserAccountId JOIN hr.EmployeeOnboardingDecisions d ON d.Id=a.ApprovalDecisionId JOIN hr.EmployeeSubmissions s ON s.Id=d.SubmissionId LEFT JOIN auth.LoginPhoneChanges c ON c.Id=b.LoginPhoneChangeId WHERE a.Id='$($a.Id)' AND d.EmployeeId=a.EmployeeId AND d.Decision=1 AND d.DecidedAt<=SYSUTCDATETIME() AND s.EmployeeId=a.EmployeeId AND b.ApprovedSubmissionId=s.Id AND b.PhoneNumber='$($a.ApprovedLoginPhoneNumber)' AND b.RevokedAt IS NULL AND b.CreatedAt<=SYSUTCDATETIME() AND b.VerifiedAt IS NOT NULL AND b.VerifiedAt>=b.CreatedAt AND b.VerifiedAt<=SYSUTCDATETIME() AND ((b.LoginPhoneChangeId IS NULL AND b.PhoneNumber=s.ApprovedLoginPhoneNumber) OR (c.UserAccountId=a.Id AND c.NewPhoneNumber=b.PhoneNumber AND c.CompletionChallengeId IS NOT NULL AND c.SupersededAt IS NULL AND c.ApprovedAt<=b.CreatedAt AND c.CompletedAt<=SYSUTCDATETIME() AND c.CompletedAt=b.VerifiedAt));")
})
Assert-P1PhoneCounts ([int](Read-Sql 'SELECT COUNT(*) FROM auth.LoginPhoneBindings WHERE RevokedAt IS NULL;')) $phoneCounts
Assert ((Read-Sql 'SELECT COUNT(*) FROM auth.OtpDeliveries WHERE Status NOT IN(1,2,3);') -eq '0') 'Existing nonterminal OTP.'
Assert ((Read-Sql "SELECT COUNT(*) FROM auth.MobileOtpChallenges WHERE Purpose='WebLogin' AND ConsumedAt IS NULL AND RevokedAt IS NULL;") -eq '0') 'Existing challenge would be mutated.'
Assert ((Read-Sql "SELECT COUNT(*) FROM auth.UserRoleAssignments u JOIN auth.Roles r ON r.Id=u.RoleId WHERE u.UserAccountId='$($profile.AdminAccount)' AND r.Code='ADMIN' AND r.SystemRole=1 AND r.IsActive=1 AND u.EffectiveFrom<=SYSUTCDATETIME() AND (u.EffectiveTo IS NULL OR SYSUTCDATETIME()<u.EffectiveTo) AND (u.RevokedAt IS NULL OR SYSUTCDATETIME()<u.RevokedAt);") -eq '1') 'Genuine Admin membership differs.'
foreach($hr in @($false,$true)){
    $id=if($hr){$profile.HrAccount}else{$profile.AdminAccount};$code=if($hr){$profile.RequesterCode}else{$profile.AdminCode}
    $roles=Json-Rows "SELECT (SELECT u.Id,r.Code FROM auth.UserRoleAssignments u JOIN auth.Roles r ON r.Id=u.RoleId WHERE u.UserAccountId='$id' AND r.IsActive=1 AND u.EffectiveFrom<=SYSUTCDATETIME() AND (u.EffectiveTo IS NULL OR SYSUTCDATETIME()<u.EffectiveTo) AND (u.RevokedAt IS NULL OR SYSUTCDATETIME()<u.RevokedAt) ORDER BY r.Code,u.Id FOR JSON PATH);"
    Assert-O1EffectiveRoles $roles $hr
    $scope=if($hr){"s.Kind=2 AND s.EmployeeId='$($profile.HrEmployee)'"}else{'s.Kind=7 AND s.EmployeeId IS NULL'}
    $codes=if($hr){$profile.RequesterPermissions}else{$profile.AdminPermissions}
    $actual=(Read-Sql "SELECT p.Code FROM auth.UserRoleAssignments u JOIN auth.Roles r ON r.Id=u.RoleId JOIN auth.RolePermissions rp ON rp.RoleId=r.Id JOIN auth.Permissions p ON p.Id=rp.PermissionId JOIN auth.UserAccessScopes s ON s.UserRoleAssignmentId=u.Id WHERE u.UserAccountId='$id' AND r.Code='$code' AND r.SystemRole=0 AND r.IsActive=1 AND $scope AND s.DepartmentId IS NULL AND s.TeamId IS NULL AND s.SiteId IS NULL AND s.ProjectId IS NULL AND u.EffectiveFrom=CONVERT(datetimeoffset,'2026-10-20T11:09:40.0589427Z') AND u.EffectiveTo=CONVERT(datetimeoffset,'$($profile.GrantEnds)') AND u.EffectiveFrom<=SYSUTCDATETIME() AND u.EffectiveTo>DATEADD(minute,10,SYSUTCDATETIME()) AND u.RevokedAt IS NULL AND rp.GrantedAt<=SYSUTCDATETIME() AND rp.RevokedAt IS NULL ORDER BY p.Code;") -split '\r?\n'
    Assert (($actual -join '|') -ceq (($codes|Sort-Object -CaseSensitive) -join '|')) 'Exact unexpired P1 grants required; do not renew.'
}
$manifest=Get-Content -LiteralPath $manifestPath -Raw|ConvertFrom-Json -AsHashtable
Assert ($manifest.profile -ceq $profile.Profile -and $manifest.privateRoot -ceq $root -and
    $manifest.sources.Count -eq 9 -and $manifest.continuation.seedAllowed -eq $false -and
    $manifest.continuation.sourceProofPath -ceq $profile.SourceProofPath -and
    $manifest.continuation.sourceProofSha256 -ceq $profile.SourceProofSha256 -and
    $manifest.continuation.employeeDraftRevision -eq 6 -and
    $manifest.continuation.employeeRowVersion -ceq '0000000000338B54' -and
    @($manifest.continuation.allowedEmployeeChanges).Count -eq 0) 'O1 continuation manifest differs.'
foreach($table in @('hr.EmployeeAssignments','hr.Policies','hr.PolicyVersions','hr.PolicyAssignments','hr.OutstationDeployments','hr.AllowanceClaims','hr.AllowanceClaimSubmissions')){
    $expectedCount=if($table -cin (Get-O1SourceTables)){'1'}else{'0'}
    Assert ((Read-Sql "SELECT COUNT(*) FROM $table;") -eq $expectedCount) 'O1 retained source/empty business baseline differs.'
}
foreach($row in $manifest.sources){
    $id=ConvertTo-O1Guid $row.id
    Assert ($row.table -cin (Get-O1SourceTables)) 'Unapproved source table.'
    $code=if($row.code){Assert ($row.code -cmatch '^TEST-VO-[A-Z]+$') 'Invalid source code.';" AND Code='$($row.code)'"}else{''}
    Assert ((Read-Sql "SELECT COUNT(*) FROM $($row.table) WHERE Id='$id'$code;") -eq '1') 'O1 retained source binding missing.'
}
Assert-PrivateSource $profile.SourceProofPath
Assert-O1SourceProofHash (Get-FileHash -LiteralPath $profile.SourceProofPath -Algorithm SHA256).Hash
$sourceProof=Get-Content -LiteralPath $profile.SourceProofPath -Raw|ConvertFrom-Json -AsHashtable
Assert-O1SourceProofHash (Get-FileHash -LiteralPath $profile.SourceProofPath -Algorithm SHA256).Hash
Assert-O1SourceHashes $manifest.continuation.sourceHashes $sourceProof.Sources
Assert-O1EmployeeMetadata $sourceProof.EmployeeSeed 'Baseline'
$sourceBaseline=Source-Snapshot
Assert-O1SourceHashes $manifest.continuation.sourceHashes $sourceBaseline
$sourceOriginalTimestamps=Read-O1SourceTimestamps
Assert-PrivateSource $accountsPath;Assert-PrivateSource $sourceSettings
$credentials=Get-Content -LiteralPath $accountsPath -Raw|ConvertFrom-Json
Assert (@($credentials).Count -eq 3) 'Credential population differs.'
foreach($actor in $actors){
    $credential=@($credentials|Where-Object Email -CEQ $actor.LoginName)
    Assert ($credential.Count -eq 1 -and $credential[0].Password -is [string] -and $credential[0].Password.Length -gt 0 -and $credential[0].Password.Length -le 1024) 'Credential binding differs.'
}
$protected=Protected-Snapshot;$prior=Prior-Snapshot;$beforeCounts=Record-Counts
$employeeBaseline=Read-O1EmployeeMetadata
Assert-O1EmployeeMetadata $employeeBaseline 'Baseline'
$packet=[Collections.Generic.List[string]]::new()
foreach($file in Get-ChildItem -LiteralPath $release -Recurse -Force){
    Assert (!($file.Attributes -band [IO.FileAttributes]::ReparsePoint)) 'Linked release asset.'
    if(!$file.PSIsContainer){$packet.Add($file.FullName+'|'+(Get-FileHash -LiteralPath $file.FullName).Hash)}
}
foreach($file in @($PSCommandPath,$manifestPath,$common,$p1,$reportCommon,
    (Join-Path $PSScriptRoot 'HrOutstationO1Profile.ps1'),(Join-Path $PSScriptRoot 'HrPermissionP1Profile.ps1'),
    (Join-Path $repo 'src/Cosmix.Api/Program.cs'),(Join-Path $repo 'src/Cosmix.Api/Development/LocalTesting.cs'))+
    @(Get-ChildItem -LiteralPath (Join-Path $repo 'src/Cosmix.Api/Development') -Filter 'HrOutstationO1*.cs'|ForEach-Object FullName)){
    $packet.Add($file+'|'+(Get-FileHash -LiteralPath $file).Hash)
}
foreach($name in $protected.Keys|Sort-Object){$packet.Add('protected|'+$name+'|'+$protected[$name])}
foreach($name in $prior.Keys|Sort-Object){$packet.Add('prior|'+$name+'|'+$prior[$name])}
$packet.Add('actors|'+($actors|ConvertTo-Json -Depth 8 -Compress))
$packet.Add('hr-employee-baseline|'+($employeeBaseline|ConvertTo-Json -Compress))
$packet.Add('retained-source-proof|'+$profile.SourceProofSha256)
$packet.Add('retained-source-timestamps|'+($sourceOriginalTimestamps|ConvertTo-Json -Depth 5 -Compress))
$packet.Add('auth|'+($beforeCounts|ConvertTo-Json -Compress))
$packet.Add('ids|'+(($historicalIds.GetEnumerator()|Sort-Object Key|ForEach-Object {"$($_.Key)=$($_.Value)"}) -join '|'))
$packetHash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes((($packet|Sort-Object -CaseSensitive) -join [char]10))))
[pscustomobject]@{Mode=$Mode;PacketSha256=$packetHash;PrivateRootAbsent=$true;Migrations=25;MaxHttpRequests=100;MaxMfaSessions=2;Writes=$false;HostStarted=$false}
if($Mode -ceq 'Inspect'){$credentials=$null;return}
Assert ($ExistingSourceReuseReady -and $ReviewedPacketSha256 -cmatch '^[A-F0-9]{64}$' -and $ReviewedPacketSha256 -ceq $packetHash) 'Existing-source reuse readiness/reviewed packet required; no writes.'
$startedAt=[datetimeoffset]::UtcNow
$completion=New-O1CompletionState
try {
    New-PrivateDirectory $root;$created=$true
    foreach($dir in @('config','files','keys','keys\data-protection','results')){New-PrivateDirectory (Join-Path $root $dir)}
    $source=Get-Content -LiteralPath $sourceSettings -Raw|ConvertFrom-Json -AsHashtable
    $mobile=$source.MobileAuthentication;$mobile.Enabled=$true
    $mobile.Crypto.KeyVersion='hr-o1'
    $mobile.Crypto.VerifierKeyBase64=[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    $mobile.Crypto.EncryptionKeyBase64=[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    $newRateKeys=Get-O1NewRateKeys ([Convert]::FromBase64String($mobile.Crypto.VerifierKeyBase64))
    foreach($rateKey in $newRateKeys){
        Assert ((Read-Sql "SELECT COUNT(*) FROM auth.AuthRateBuckets WHERE [Key]='$rateKey';") -eq '0') 'Fresh O1 rate key collision.'
    }
    $mobile.Sms=@{Provider='LocalTesting';Endpoint='';BearerToken=''}
    $web=$source.WebAuthentication;$web.Enabled=$true
    $config=@{
        Urls=$profile.Url;AllowedHosts='localhost'
        LocalTesting=@{Enabled=$true;ExistingAcceptanceProfile=$profile.Profile;PrivateDirectory=$root;AllowUnencryptedLocalPipe=$true}
        ConnectionStrings=@{CosmixDatabase="Server=$pipe;Database=Cosmix_Testing;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=30;Application Name=Cosmix.HrOutstation.O1"}
        PrivateFiles=@{Directory=(Join-Path $root 'files');KeyFile=(Join-Path $root 'keys\files.key');KeyReference='hr-outstation-o1'}
        Jwt=@{Issuer=$profile.Application;Audience=$profile.Application;SigningKey=[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))}
        MobileAuthentication=$mobile;WebAuthentication=$web
        EmployeeFileScanner=@{Enabled=$false};AttendanceJobs=@{Enabled=$false}
        SelfHostedFace=@{Enabled=$false};FaceCapture=@{Enabled=$false};FaceTemplateStorage=@{Enabled=$false}
        FaceStorageRetention=@{Enabled=$false};FaceStorageRetentionJournal=@{Enabled=$false}
        Cors=@{AllowedOrigins=@('https://localhost:5173')}
    }
    Write-Private 'keys\files.key' ([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    Write-Json 'config\host.json' $config
    $config=$null;$source=$null;$mobile=$null;$web=$null
    Write-Result 'start.json' @{Target=$observed;PacketSha256=$packetHash;ProtectedSnapshots=$protected;PriorFiles=$prior;CountsBefore=$beforeCounts;BaselineCounts=$baselineCounts;HistoricalIds=$historicalIds;EmployeeBaseline=$employeeBaseline;ExistingSourceHashes=$sourceBaseline;OriginalSourceTimestamps=$sourceOriginalTimestamps;SourceProofSha256=$profile.SourceProofSha256;SeedExecuted=$false;Release=$release;Utc=$startedAt}
    $start=[Diagnostics.ProcessStartInfo]::new((Get-Command dotnet -CommandType Application).Source)
    $start.ArgumentList.Add($assembly);$start.UseShellExecute=$false;$start.CreateNoWindow=$true
    $start.WorkingDirectory=$release;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    $start.Environment.Clear()
    foreach($key in @('SystemRoot','WINDIR','USERPROFILE','LOCALAPPDATA','APPDATA','PATH','TEMP','TMP','ProgramFiles','ProgramW6432','DOTNET_ROOT','COMPUTERNAME','USERNAME','USERDOMAIN')){
        $value=[Environment]::GetEnvironmentVariable($key);if($value){$start.Environment[$key]=$value}
    }
    $start.Environment['COSMIX_EXISTING_TESTING_HOST']=$profile.Profile
    $start.Environment['COSMIX_LOCAL_TESTING_SETTINGS']=Join-Path $root 'config\host.json'
    $start.Environment['ASPNETCORE_ENVIRONMENT']='Development'
    $hostProcess=[Diagnostics.Process]::new();$hostProcess.StartInfo=$start
    Assert ($hostProcess.Start()) 'Owned O1 host failed to start.';$started=$true
    $stdout=$hostProcess.StandardOutput.ReadToEndAsync();$stderr=$hostProcess.StandardError.ReadToEndAsync()
    $ready=$false
    for($i=0;$i -lt 120;$i++){
        Assert (!$hostProcess.HasExited) 'Owned host exited during startup.'
        $listeners=@(Get-NetTCPConnection -State Listen -LocalPort 7481 -ErrorAction SilentlyContinue)
        if($listeners.Count){
            Assert (@($listeners|Where-Object {$_.OwningProcess -ne $hostProcess.Id -or $_.LocalAddress -notin @('127.0.0.1','::1')}).Count -eq 0) 'Listener ownership differs.'
            $ready=$true;break
        }
        Start-Sleep -Milliseconds 500
    }
    Assert $ready 'Owned host startup timeout; no automatic restart.'
    Assert-O1EmployeeMetadata (Read-O1EmployeeMetadata) 'Final' $employeeBaseline
    Assert-Snapshot $protected (Protected-Snapshot) 'Post-startup protected SQL; no seeding allowed'
    Assert-O1SourceHashes $sourceBaseline (Source-Snapshot)
    Write-Result 'existing-source-verification.json' @{Sources=$sourceBaseline;Employee=$employeeBaseline;OriginalSourceTimestamps=$sourceOriginalTimestamps;SourceProofSha256=$profile.SourceProofSha256;SeedExecuted=$false}
    Invoke-O1Business
    $credentials=$null
    Verify-O1State
    Write-Result 'acceptance.json' @{Checks=$checks.ToArray();Http=$httpResults.ToArray();Budget=$budget;Bindings=$bindings;AuditRows=$auditExpected.Count;ProtectedUnchanged=$true;PriorFilesUnchanged=$true;ExistingSourcesUnchanged=$true;SeedExecuted=$false;Utc=[datetimeoffset]::UtcNow}
} catch {
    $completion.Primary=$_
    if($created){
        Invoke-O1CompletionCheck $completion 'FailureEvidence' {
            Write-Result 'failure.json' @{Checks=$checks.ToArray();Http=$httpResults.ToArray();Budget=$budget
                FailureType=$completion.Primary.Exception.GetType().FullName
                FailureLine=$completion.Primary.InvocationInfo.ScriptLineNumber
                Preservation='Unknown';Message='Stopped; retained evidence; no reset, replay, or repair.'
                Utc=[datetimeoffset]::UtcNow}
        }
    }
} finally {
    $credentials=$null
    $clientIndex=0
    foreach($client in $clients){
        Invoke-O1CompletionCheck $completion ("ClientDispose:"+$clientIndex) {$client.Dispose()}
        $clientIndex++
    }
    if($started){
        Invoke-O1CompletionCheck $completion 'HostExit' {
            if(!$hostProcess.HasExited){$hostProcess.Kill();$hostProcess.WaitForExit(10000)|Out-Null}
            Assert $hostProcess.HasExited 'Owned host did not exit.'
        }
        Invoke-O1CompletionCheck $completion 'HostStdoutEvidence' {
            Assert ($completion.Checks.HostExit -ceq 'Passed') 'Host exit unconfirmed; no unbounded log wait.'
            Write-Private 'results\host-stdout.log' ([Text.UTF8Encoding]::new($false).GetBytes($stdout.GetAwaiter().GetResult()))
        }
        Invoke-O1CompletionCheck $completion 'HostStderrEvidence' {
            Assert ($completion.Checks.HostExit -ceq 'Passed') 'Host exit unconfirmed; no unbounded log wait.'
            Write-Private 'results\host-stderr.log' ([Text.UTF8Encoding]::new($false).GetBytes($stderr.GetAwaiter().GetResult()))
        }
        Invoke-O1CompletionCheck $completion 'HostFinalEvidence' {
            Write-Result 'host-final.json' @{ProcessId=$hostProcess.Id;ExitStatus=$completion.Checks.HostExit;DatabaseStopped=$false;CleanupPerformed=$false}
        }
        Invoke-O1CompletionCheck $completion 'HostDispose' {$hostProcess.Dispose()}
    } else {
        $completion.Checks.HostExit='NotStarted'
    }
    if($created){
        # Each reconciliation is independent; a SQL timeout must not suppress file/metadata evidence.
        Invoke-O1CompletionCheck $completion 'ProtectedSql' {
            Assert (!$started -or $completion.Checks.HostExit -ceq 'Passed') 'Post-host SQL preservation unknown while host exit unconfirmed.'
            Assert-Snapshot $protected (Protected-Snapshot) 'Post-host protected SQL'
        }
        Invoke-O1CompletionCheck $completion 'PriorFiles' {
            Assert (!$started -or $completion.Checks.HostExit -ceq 'Passed') 'Post-host file preservation unknown while host exit unconfirmed.'
            Assert-Snapshot $prior (Prior-Snapshot) 'Post-host prior files'
        }
        Invoke-O1CompletionCheck $completion 'ExistingSources' {
            Assert (!$started -or $completion.Checks.HostExit -ceq 'Passed') 'Post-host source preservation unknown while host exit unconfirmed.'
            Assert-O1SourceHashes $sourceBaseline (Source-Snapshot)
            $completion.Values.OriginalSourceTimestamps=Read-O1SourceTimestamps
        }
        Invoke-O1CompletionCheck $completion 'EmployeeMetadata' {
            Assert (!$started -or $completion.Checks.HostExit -ceq 'Passed') 'Post-host metadata unknown while host exit unconfirmed.'
            $employeeFinal=Read-O1EmployeeMetadata
            Assert-O1EmployeeMetadata $employeeFinal 'Final' $employeeBaseline
            $completion.Values.EmployeeFinal=$employeeFinal
        }
        Invoke-O1CompletionCheck $completion 'Bounds' {
            Assert (!$started -or $completion.Checks.HostExit -ceq 'Passed') 'Post-host bounds unknown while host exit unconfirmed.'
            Verify-O1Bounds
        }
        Invoke-O1CompletionCheck $completion 'Counts' {$completion.Values.Counts=Record-Counts}
        Invoke-O1CompletionCheck $completion 'PreservationEvidence' {
            Write-Result 'post-host-preservation.json' @{Completion=(Get-O1CompletionReport $completion)
                ProtectedSql=$completion.Checks.ProtectedSql;PriorFiles=$completion.Checks.PriorFiles
                EmployeeBaseline=$employeeBaseline;EmployeeFinal=$completion.Values.EmployeeFinal
                ExistingSourceHashes=$sourceBaseline;OriginalSourceTimestamps=$completion.Values.OriginalSourceTimestamps
                SeedExecuted=$false;Budget=$budget;Counts=$completion.Values.Counts}
        }
    }
}
$completionFailure=Get-O1CompletionFailure $completion
if($null -ne $completionFailure){$PSCmdlet.ThrowTerminatingError($completionFailure)}
