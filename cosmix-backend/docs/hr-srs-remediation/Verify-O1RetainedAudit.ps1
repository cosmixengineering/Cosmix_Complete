# Offline synthetic-only verifier. No SQL, host, network or private-file access.
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'HrOutstationO1Profile.ps1')
$helper=Join-Path $PSScriptRoot 'O1RetainedAudit.Helpers.ps1'
if(!(Test-Path -LiteralPath $helper)){throw 'RED: retained audit helper is not implemented.'}
. $helper
$script:checks=0
function Check-Retained([bool]$ok,[string]$name){
    if(!$ok){throw "FAIL $name"}
    $script:checks++;Write-Output "PASS $name"
}
function New-RetainedFixture {
    $p=Get-O1Profile
    $at=[datetimeoffset]'2026-10-20T12:00:00Z'
    $http=[Collections.Generic.List[object]]::new()
    $audits=[Collections.Generic.List[object]]::new()
    $sessions=[Collections.Generic.List[object]]::new()
    $challenges=[Collections.Generic.List[object]]::new()
    $deliveries=[Collections.Generic.List[object]]::new()
    $b=@{DeploymentId='40102001-0001-4000-8000-000000000001';ClaimId='40102001-0001-4000-8000-000000000002'}
    $trip=$p.Api+'/'+$b.DeploymentId;$claim=$trip+'/claims/'+$b.ClaimId
    function Add-Http($method,$path,$status,$key=''){
        $row=@{Method=$method;Path=$path;Status=$status;Key=$key;Trace=('O1TEST{0:D7}:00000001' -f $http.Count)}
        $http.Add($row);return $row
    }
    function Add-Audit($event,$actor,$role,$type,$target,$employee,$device,$trace,$proof=$false){
        $audits.Add(@{Id=('50102001-0001-4000-8000-{0:D12}' -f ($audits.Count+1))
            EventType=$event;ActorUserId=$actor;ActorRoleCode=$role;TargetType=$type;TargetId=$target
            EmployeeScopeId=$employee;DeviceReference=$device;CorrelationId=$trace;OccurredAt=$at.ToString('o')
            ProtectedChangesReference=$(if($proof){'synthetic-proof-'+$audits.Count}else{$null})})
    }
    foreach($who in 0,1){
        $actor=if($who -eq 0){$p.HrAccount}else{$p.AdminAccount}
        $employee=if($who -eq 0){$p.HrEmployee}else{$p.AdminEmployee}
        $roles=if($who -eq 0){@('HR','ATTHUMA13B03','ATTHUMA13B06',$p.RequesterCode)}else{@('ADMIN',$p.AdminCode)}
        $sid='60102001-0001-4000-8000-{0:D12}' -f ($who+1)
        $cid='70102001-0001-4000-8000-{0:D12}' -f ($who+1)
        $did='80102001-0001-4000-8000-{0:D12}' -f ($who+1)
        $null=Add-Http GET '/api/v1/me/permissions' 401
        $null=Add-Http GET '/api/v1/auth/web/csrf' 200
        $r=Add-Http POST '/api/v1/auth/web/login' 202
        Add-Audit 'web.otp.requested' anonymous authentication otp-challenge $cid $null $null $r.Trace
        $null=Add-Http GET '/api/v1/me/permissions' 401
        $null=Add-Http POST '/api/v1/auth/web/local-testing/otp' 200
        $r=Add-Http POST '/api/v1/auth/web/otp/verify' 200
        Add-Audit 'web.session.created' $actor authentication session $sid $null $null $r.Trace
        $sessions.Add(@{Id=$sid;UserAccountId=$actor;SmsOtpChallengeId=$cid;CorrelationId=$r.Trace;RevokedAt=$at.ToString('o')})
        $challenges.Add(@{Id=$cid;UserAccountId=$actor;Purpose='WebLogin';ConsumedAt=$at.ToString('o')})
        $deliveries.Add(@{Id=$did;ChallengeId=$cid;Status=1;Attempts=1;Cleared=1})
        Add-Audit 'otp.delivery.accepted' sms-dispatcher system otp-delivery $did $null $null ([guid]$did).ToString('N')
        $null=Add-Http GET '/api/v1/auth/web/csrf' 200
        $r=Add-Http GET '/api/v1/me/permissions' 200
        foreach($role in $roles){Add-Audit 'access.current_permissions.read' $actor $role UserAccount $actor $employee ('session:'+$sid) $r.Trace}
    }
    function Add-Write($key,$path,$event,$target,$admin,$replay){
        $r=Add-Http POST $path 200 $key
        $actor=if($admin){$p.AdminAccount}else{$p.HrAccount}
        $role=if($admin){'ADMIN'}else{$p.RequesterCode}
        $sid=$sessions[$(if($admin){1}else{0})].Id
        if($replay){$event+='.replay'}
        Add-Audit $event $actor $role Attendance $target $p.HrEmployee $sid $r.Trace (!$replay)
    }
    function Add-Read($path,$event){
        $r=Add-Http GET $path 200
        Add-Audit $event $p.HrAccount $p.RequesterCode Attendance $p.HrEmployee $p.HrEmployee $sessions[0].Id $r.Trace
    }
    Add-Write O1-request $p.Api attendance.deployment.request $b.DeploymentId $false $true
    Add-Write O1-request $p.Api attendance.deployment.request $b.DeploymentId $false $false
    $null=Add-Http POST $p.Api 409 O1-request
    $null=Add-Http POST $p.Api 409 O1-request-duplicate
    $null=Add-Http POST ($trip+'/decision') 409 O1-decision-stale
    $null=Add-Http POST ($trip+'/decision') 403 O1-decision-self
    Add-Write O1-decision ($trip+'/decision') attendance.deployment.decide $b.DeploymentId $true $false
    Add-Write O1-decision ($trip+'/decision') attendance.deployment.decide $b.DeploymentId $true $true
    Add-Read ($trip+'/calculation') attendance.deployment.preview
    $null=Add-Http POST ($trip+'/claims') 409 O1-claim-stale
    Add-Write O1-claim ($trip+'/claims') attendance.deployment.claim.request $b.ClaimId $false $true
    Add-Write O1-claim ($trip+'/claims') attendance.deployment.claim.request $b.ClaimId $false $false
    $null=Add-Http POST ($trip+'/claims') 409 O1-claim
    $null=Add-Http POST ($trip+'/claims') 409 O1-claim-duplicate
    Add-Read $trip attendance.deployment.detail
    $null=Add-Http POST ($claim+'/decision') 409 O1-claim-decision-stale
    $null=Add-Http POST ($claim+'/decision') 403 O1-claim-decision-self
    Add-Write O1-claim-decision ($claim+'/decision') attendance.deployment.claim.decide $b.ClaimId $true $false
    Add-Write O1-claim-decision ($claim+'/decision') attendance.deployment.claim.decide $b.ClaimId $true $true
    Add-Write O1-claim ($trip+'/claims') attendance.deployment.claim.request $b.ClaimId $false $true
    Add-Read $trip attendance.deployment.detail
    Add-Read ($trip+'/calculation') attendance.deployment.preview
    Add-Read ($claim+'/evidence') attendance.deployment.claim.evidence
    foreach($who in 0,1){
        $actor=if($who -eq 0){$p.HrAccount}else{$p.AdminAccount}
        $employee=if($who -eq 0){$p.HrEmployee}else{$p.AdminEmployee}
        $roles=if($who -eq 0){@('HR','ATTHUMA13B03','ATTHUMA13B06',$p.RequesterCode)}else{@('ADMIN',$p.AdminCode)}
        $null=Add-Http POST '/api/v1/auth/web/logout' 400
        $r=Add-Http POST '/api/v1/auth/web/logout' 204
        foreach($role in $roles){Add-Audit session.logout $actor $role session $sessions[$who].Id $employee ('session:'+$sessions[$who].Id+';device:') $r.Trace}
        $null=Add-Http GET '/api/v1/me/permissions' 401
    }
    return @{Http=$http.ToArray();SessionRows=$sessions.ToArray();Challenges=$challenges.ToArray()
        Deliveries=$deliveries.ToArray();Bindings=$b;AuditRows=$audits.ToArray();At=$at}
}
function Test-Retained($f){
    $e=Get-O1RetainedAuditExpectations $f.Http $f.SessionRows $f.Challenges $f.Deliveries $f.Bindings $f.AuditRows
    Assert-O1AuditRows $f.AuditRows $e $f.At.AddMinutes(-1) $f.At.AddMinutes(1)
    return ,$e
}
function Reject-Retained($name,[scriptblock]$mutate){
    $f=New-RetainedFixture
    & $mutate $f
    $rejected=$false
    try {$null=Test-Retained $f}catch{$rejected=$true}
    Check-Retained $rejected $name
}
function Move-RetainedOriginal($f,$event,[int]$httpIndex){
    $original=@($f.AuditRows|Where-Object {$_.EventType -ceq $event})[0]
    $replacement=@($f.AuditRows|Where-Object {$_.EventType -ceq ($event+'.replay') -and
        $_.CorrelationId -ceq $f.Http[$httpIndex].Trace})[0]
    if(!$original -or !$replacement){throw 'Invalid synthetic original/replay test pair.'}
    $original.EventType=$event+'.replay';$original.ProtectedChangesReference=$null
    $replacement.EventType=$event;$replacement.ProtectedChangesReference='synthetic-promoted-original'
}
$f=New-RetainedFixture
$expected=Test-Retained $f
Check-Retained ($f.Http.Count -eq 45 -and $expected.Count -eq 32) '45 HTTP rows reconstruct exactly 32 audit expectations'
Check-Retained (@($expected|Where-Object {$_.EvidenceBasis -cne 'SQLCorrelated'}).Count -eq 0) 'expectations explicitly labelled SQL-correlated'
Check-Retained (@($expected|Where-Object Protected).Count -eq 4) 'exactly four original writes require proof'
$f=New-RetainedFixture
$pair=@($f.AuditRows|Where-Object {$_.EventType -like 'attendance.deployment.request*'})
$pair[0].EventType='attendance.deployment.request';$pair[0].ProtectedChangesReference='synthetic-swapped'
$pair[1].EventType='attendance.deployment.request.replay';$pair[1].ProtectedChangesReference=$null
$null=Test-Retained $f
Check-Retained $true 'either concurrent response can carry the original write trace'
$f=New-RetainedFixture
Move-RetainedOriginal $f 'attendance.deployment.claim.request' 26
$null=Test-Retained $f
Check-Retained $true 'either concurrent claim response can carry the original write trace'
Reject-Retained 'second sequential deployment decision cannot be original' {
    param($f) Move-RetainedOriginal $f 'attendance.deployment.decide' 23
}
Reject-Retained 'second sequential claim decision cannot be original' {
    param($f) Move-RetainedOriginal $f 'attendance.deployment.claim.decide' 34
}
Reject-Retained 'post-approval claim replay cannot be original' {
    param($f) Move-RetainedOriginal $f 'attendance.deployment.claim.request' 35
}
$f=New-RetainedFixture
$f.SessionRows=@($f.SessionRows[1],$f.SessionRows[0])
$f.Challenges=@($f.Challenges[1],$f.Challenges[0])
$f.Deliveries=@($f.Deliveries[1],$f.Deliveries[0])
$f=ConvertFrom-Json -InputObject ($f|ConvertTo-Json -Depth 12) -AsHashtable -DateKind String
$f.At=[datetimeoffset]$f.At
$null=Test-Retained $f
Check-Retained $true 'serialized retained inputs and unordered SQL metadata accepted'
$tokens=$null;$parseErrors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile($helper,[ref]$tokens,[ref]$parseErrors)
Check-Retained ($parseErrors.Count -eq 0 -and $ast.EndBlock.Statements.Count -eq 1 -and
    $ast.EndBlock.Statements[0] -is [Management.Automation.Language.FunctionDefinitionAst]) 'helper contains only a function definition'
Reject-Retained 'missing HTTP' {param($f) $f.Http=$f.Http[1..44]}
Reject-Retained 'extra HTTP' {param($f) $f.Http+=@($f.Http[0])}
Reject-Retained 'duplicate trace' {param($f) $f.Http[1].Trace=$f.Http[0].Trace}
Reject-Retained 'malformed trace' {param($f) $f.Http[0].Trace="bad`ntrace"}
Reject-Retained 'unexpected route' {param($f) $f.Http[24].Path+='/other'}
Reject-Retained 'unexpected business key' {param($f) $f.Http[16].Key='O1-other'}
Reject-Retained 'negative probe success' {param($f) $f.Http[18].Status=200}
Reject-Retained 'same concurrent connection' {param($f) $f.Http[17].Trace=($f.Http[16].Trace -split ':')[0]+':00000002'}
Reject-Retained 'session actor drift' {param($f) $f.SessionRows[0].UserAccountId=$f.SessionRows[1].UserAccountId}
Reject-Retained 'session trace drift' {param($f) $f.SessionRows[0].CorrelationId=$f.Http[2].Trace}
Reject-Retained 'session count drift' {param($f) $f.SessionRows+=@($f.SessionRows[0])}
Reject-Retained 'unrevoked session' {param($f) $f.SessionRows[0].RevokedAt=$null}
Reject-Retained 'challenge binding drift' {param($f) $f.SessionRows[0].SmsOtpChallengeId=$f.Challenges[1].Id}
Reject-Retained 'challenge actor drift' {param($f) $f.Challenges[0].UserAccountId=$f.Challenges[1].UserAccountId}
Reject-Retained 'unconsumed challenge' {param($f) $f.Challenges[0].ConsumedAt=$null}
Reject-Retained 'delivery attempt drift' {param($f) $f.Deliveries[0].Attempts=2}
Reject-Retained 'uncleared delivery' {param($f) $f.Deliveries[0].Cleared=0}
Reject-Retained 'delivery count drift' {param($f) $f.Deliveries=$f.Deliveries[0..0]}
Reject-Retained 'binding target drift' {param($f) $f.Bindings.ClaimId=$f.Bindings.DeploymentId}
Reject-Retained 'missing original write' {param($f) @($f.AuditRows|Where-Object {$_.EventType -ceq 'attendance.deployment.request'})[0].EventType+='.replay'}
Reject-Retained 'two original writes' {param($f) @($f.AuditRows|Where-Object {$_.EventType -ceq 'attendance.deployment.request.replay'})[0].EventType='attendance.deployment.request'}
Reject-Retained 'original trace from rejected HTTP' {param($f) @($f.AuditRows|Where-Object {$_.EventType -ceq 'attendance.deployment.request'})[0].CorrelationId=$f.Http[18].Trace}
Reject-Retained 'audit actor drift' {param($f) $f.AuditRows[0].ActorUserId='unexpected'}
Reject-Retained 'audit role drift' {param($f) $f.AuditRows[0].ActorRoleCode='ADMIN'}
Reject-Retained 'audit target drift' {param($f) $f.AuditRows[0].TargetId=$f.Bindings.DeploymentId}
Reject-Retained 'audit scope drift' {param($f) $f.AuditRows[0].EmployeeScopeId=(Get-O1Profile).HrEmployee}
Reject-Retained 'audit device drift' {param($f) $f.AuditRows[0].DeviceReference=$f.SessionRows[0].Id}
Reject-Retained 'unapproved audit event' {param($f) $f.AuditRows[0].EventType='unapproved.event'}
Reject-Retained 'audit outside retained window' {param($f) $f.AuditRows[0].OccurredAt=$f.At.AddDays(-1).ToString('o')}
Reject-Retained 'audit trace drift' {param($f) $f.AuditRows[0].CorrelationId=$f.Http[0].Trace}
Reject-Retained 'extra audit' {param($f) $f.AuditRows+=@($f.AuditRows[0])}
Reject-Retained 'missing audit' {param($f) $f.AuditRows=$f.AuditRows[1..31]}
Reject-Retained 'missing protected proof' {param($f) @($f.AuditRows|Where-Object {$_.EventType -ceq 'attendance.deployment.request'})[0].ProtectedChangesReference=$null}
Reject-Retained 'unexpected replay proof' {param($f) @($f.AuditRows|Where-Object {$_.EventType -ceq 'attendance.deployment.request.replay'})[0].ProtectedChangesReference='unexpected'}
Write-Output "Retained audit offline: $script:checks checks passed."
