# Definitions only. Caller loads HrOutstationO1Profile.ps1 and supplies retained data.
# SQL-correlated reconstruction, not recovery of lost HTTP response bodies.
# Caller must pass the result to Assert-O1AuditRows with the retained run window.
function Get-O1RetainedAuditExpectations($Http,$SessionRows,$Challenges,$Deliveries,$Bindings,$AuditRows) {
    $p=Get-O1Profile
    Assert-O1 (@($Http).Count -eq 45) 'Retained O1 HTTP sequence must contain exactly 45 requests.'
    Assert-O1 (@($SessionRows).Count -eq 2 -and @($Challenges).Count -eq 2 -and
        @($Deliveries).Count -eq 2) 'Retained O1 authentication populations differ.'
    $tripId=ConvertTo-O1Guid $Bindings.DeploymentId
    $claimId=ConvertTo-O1Guid $Bindings.ClaimId
    Assert-O1 ($tripId -cne $claimId) 'Retained O1 business identities collide.'
    $trip=$p.Api+'/'+$tripId;$claim=$trip+'/claims/'+$claimId
    $auth='/api/v1/auth/web/';$permissions='/api/v1/me/permissions'
    $sequence=[Collections.Generic.List[object]]::new()
    function Add-O1RetainedStep($method,$path,$status,$key='',$who=0,$event='',$target=''){
        $sequence.Add(@{Method=$method;Path=$path;Status=$status;Key=$key;Who=$who;Event=$event;Target=$target})
    }
    foreach($who in 0,1){
        Add-O1RetainedStep GET $permissions 401
        Add-O1RetainedStep GET ($auth+'csrf') 200
        Add-O1RetainedStep POST ($auth+'login') 202 '' $who 'web.otp.requested'
        Add-O1RetainedStep GET $permissions 401
        Add-O1RetainedStep POST ($auth+'local-testing/otp') 200
        Add-O1RetainedStep POST ($auth+'otp/verify') 200 '' $who 'web.session.created'
        Add-O1RetainedStep GET ($auth+'csrf') 200
        Add-O1RetainedStep GET $permissions 200 '' $who 'access.current_permissions.read'
    }
    foreach($i in 1,2){Add-O1RetainedStep POST $p.Api 200 O1-request 0 attendance.deployment.request $tripId}
    Add-O1RetainedStep POST $p.Api 409 O1-request
    Add-O1RetainedStep POST $p.Api 409 O1-request-duplicate
    Add-O1RetainedStep POST ($trip+'/decision') 409 O1-decision-stale
    Add-O1RetainedStep POST ($trip+'/decision') 403 O1-decision-self
    foreach($i in 1,2){Add-O1RetainedStep POST ($trip+'/decision') 200 O1-decision 1 attendance.deployment.decide $tripId}
    Add-O1RetainedStep GET ($trip+'/calculation') 200 '' 0 attendance.deployment.preview $p.HrEmployee
    Add-O1RetainedStep POST ($trip+'/claims') 409 O1-claim-stale
    foreach($i in 1,2){Add-O1RetainedStep POST ($trip+'/claims') 200 O1-claim 0 attendance.deployment.claim.request $claimId}
    Add-O1RetainedStep POST ($trip+'/claims') 409 O1-claim
    Add-O1RetainedStep POST ($trip+'/claims') 409 O1-claim-duplicate
    Add-O1RetainedStep GET $trip 200 '' 0 attendance.deployment.detail $p.HrEmployee
    Add-O1RetainedStep POST ($claim+'/decision') 409 O1-claim-decision-stale
    Add-O1RetainedStep POST ($claim+'/decision') 403 O1-claim-decision-self
    foreach($i in 1,2){Add-O1RetainedStep POST ($claim+'/decision') 200 O1-claim-decision 1 attendance.deployment.claim.decide $claimId}
    Add-O1RetainedStep POST ($trip+'/claims') 200 O1-claim 0 attendance.deployment.claim.request $claimId
    Add-O1RetainedStep GET $trip 200 '' 0 attendance.deployment.detail $p.HrEmployee
    Add-O1RetainedStep GET ($trip+'/calculation') 200 '' 0 attendance.deployment.preview $p.HrEmployee
    Add-O1RetainedStep GET ($claim+'/evidence') 200 '' 0 attendance.deployment.claim.evidence $p.HrEmployee
    foreach($who in 0,1){
        Add-O1RetainedStep POST ($auth+'logout') 400
        Add-O1RetainedStep POST ($auth+'logout') 204 '' $who session.logout
        Add-O1RetainedStep GET $permissions 401
    }
    $traces=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    for($i=0;$i -lt $sequence.Count;$i++){
        $actual=$Http[$i];$wanted=$sequence[$i]
        Assert-O1 ($actual.Method -ceq $wanted.Method -and $actual.Path -ceq $wanted.Path -and
            $actual.Status -eq $wanted.Status -and [string]$actual.Key -ceq $wanted.Key) "Retained O1 request sequence differs at index $i."
        Assert-O1Trace $actual.Trace
        Assert-O1 ($traces.Add([string]$actual.Trace)) 'Retained O1 trace reused.'
    }
    Assert-O1ConcurrentTraces $Http[16].Trace $Http[17].Trace
    Assert-O1ConcurrentTraces $Http[26].Trace $Http[27].Trace

    $states=[Collections.Generic.List[object]]::new()
    $ids=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach($who in 0,1){
        $actor=if($who -eq 0){$p.HrAccount}else{$p.AdminAccount}
        $employee=if($who -eq 0){$p.HrEmployee}else{$p.AdminEmployee}
        $roles=if($who -eq 0){@('HR','ATTHUMA13B03','ATTHUMA13B06',$p.RequesterCode)}else{@('ADMIN',$p.AdminCode)}
        # Only the independently captured verify trace selects a SQL session.
        $session=@($SessionRows|Where-Object {$_.CorrelationId -ceq $Http[$who*8+5].Trace})
        Assert-O1 ($session.Count -eq 1) 'Retained O1 session trace is missing or ambiguous.'
        $session=$session[0]
        $sid=ConvertTo-O1Guid $session.Id;$cid=ConvertTo-O1Guid $session.SmsOtpChallengeId
        Assert-O1 ((ConvertTo-O1Guid $session.UserAccountId) -ceq $actor -and
            ![string]::IsNullOrWhiteSpace([string]$session.RevokedAt)) 'Retained O1 session actor/revocation differs.'
        $challenge=@($Challenges|Where-Object {(ConvertTo-O1Guid $_.Id) -ceq $cid})
        $delivery=@($Deliveries|Where-Object {(ConvertTo-O1Guid $_.ChallengeId) -ceq $cid})
        Assert-O1 ($challenge.Count -eq 1 -and $delivery.Count -eq 1) 'Retained O1 challenge/delivery binding differs.'
        Assert-O1 ((ConvertTo-O1Guid $challenge[0].UserAccountId) -ceq $actor -and
            $challenge[0].Purpose -ceq 'WebLogin' -and
            ![string]::IsNullOrWhiteSpace([string]$challenge[0].ConsumedAt) -and
            $delivery[0].Status -eq 1 -and $delivery[0].Attempts -eq 1 -and $delivery[0].Cleared -eq 1) 'Retained O1 challenge/delivery state differs.'
        $did=ConvertTo-O1Guid $delivery[0].Id
        foreach($id in @($sid,$cid,$did)){Assert-O1 ($ids.Add($id)) 'Retained O1 authentication identity reused.'}
        $states.Add(@{Actor=$actor;Employee=$employee;Roles=$roles;Session=$sid;Challenge=$cid;Delivery=$did})
    }
    $expected=[Collections.Generic.List[object]]::new()
    function Add-O1RetainedExpected($event,$actor,$role,$type,$target,$employee,$device,$trace,$proof=$false){
        $expected.Add(@{EventType=$event;ActorUserId=$actor;ActorRoleCode=$role;TargetType=$type;TargetId=$target
            EmployeeScopeId=$employee;DeviceReference=$device;CorrelationId=$trace;Protected=[bool]$proof
            EvidenceBasis='SQLCorrelated'})
    }
    for($i=0;$i -lt $sequence.Count;$i++){
        $step=$sequence[$i]
        if(!$step.Event){continue}
        $state=$states[$step.Who];$trace=$Http[$i].Trace
        switch -CaseSensitive ($step.Event){
            'web.otp.requested' {
                Add-O1RetainedExpected $step.Event anonymous authentication otp-challenge $state.Challenge $null $null $trace
            }
            'web.session.created' {
                Add-O1RetainedExpected $step.Event $state.Actor authentication session $state.Session $null $null $trace
            }
            'access.current_permissions.read' {
                foreach($role in $state.Roles){
                    Add-O1RetainedExpected $step.Event $state.Actor $role UserAccount $state.Actor $state.Employee ('session:'+$state.Session) $trace
                }
            }
            'session.logout' {
                foreach($role in $state.Roles){
                    Add-O1RetainedExpected $step.Event $state.Actor $role session $state.Session $state.Employee ('session:'+$state.Session+';device:') $trace
                }
            }
            default {
                $role=if($step.Who -eq 1){'ADMIN'}else{$p.RequesterCode}
                Add-O1RetainedExpected $step.Event $state.Actor $role Attendance $step.Target $p.HrEmployee $state.Session $trace ($step.Method -ceq 'POST')
            }
        }
    }
    foreach($state in $states){
        Add-O1RetainedExpected otp.delivery.accepted sms-dispatcher system otp-delivery $state.Delivery $null $null ([guid]$state.Delivery).ToString('N')
    }
    Assert-O1 (@($AuditRows).Count -eq $expected.Count -and $expected.Count -eq 32) 'Retained O1 audit cardinality differs.'
    # Concurrent creates may commit on either connection; sequential replays cannot be originals.
    $originalIndices=@{
        'attendance.deployment.request'=@(16,17)
        'attendance.deployment.decide'=@(22)
        'attendance.deployment.claim.request'=@(26,27)
        'attendance.deployment.claim.decide'=@(33)
    }
    foreach($operation in @('attendance.deployment.request','attendance.deployment.decide',
        'attendance.deployment.claim.request','attendance.deployment.claim.decide')){
        $candidates=@($expected|Where-Object {$_.EventType -ceq $operation})
        $original=@($AuditRows|Where-Object {$_.EventType -ceq $operation})
        Assert-O1 ($original.Count -eq 1) 'Retained O1 original write audit is missing or ambiguous.'
        Assert-O1 (@($candidates|Where-Object {$_.CorrelationId -ceq $original[0].CorrelationId}).Count -eq 1) 'Retained O1 original write trace is not a captured successful command.'
        Assert-O1 (@($originalIndices[$operation]|Where-Object {
            $Http[$_].Trace -ceq $original[0].CorrelationId
        }).Count -eq 1) 'Retained O1 original write trace belongs to a replay-only request.'
        foreach($candidate in $candidates){
            if($candidate.CorrelationId -cne $original[0].CorrelationId){
                $candidate.EventType+='.replay';$candidate.Protected=$false
            }
        }
    }
    return ,$expected.ToArray()
}
