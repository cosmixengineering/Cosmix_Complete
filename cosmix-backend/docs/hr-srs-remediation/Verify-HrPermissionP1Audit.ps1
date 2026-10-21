$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'HrPermissionP1AuditVerifier.ps1')
$checks=0
function Pass([bool]$ok,[string]$name){if(!$ok){throw $name};$script:checks++}
function Reject([scriptblock]$action,[string]$name){
    $rejected=$false;try{& $action|Out-Null}catch{$rejected=$true}
    Pass $rejected $name
}
$next=1
function New-TestId { $id='abcdef01-abcd-abcd-abcd-'+$script:next.ToString('D12');$script:next++;return $id }
$start=[datetimeoffset]'2026-10-20T10:00:00Z'
$actors=@(
    @{Id=(New-TestId);EmployeeId=(New-TestId);LoginName='superadmin@example.invalid'},
    @{Id='94a83b62-5b56-4450-9175-2a7a9e3261bf';EmployeeId='7c505086-87ea-4fd2-980e-823ecccbe36a';LoginName='admin@example.invalid'},
    @{Id='8475187f-bf3b-4f9c-a3ed-4bac32253005';EmployeeId='35d19922-70a8-4c80-8578-e1029360e9fc';LoginName='hr@example.invalid'}
)
$roles=@(@{Id=(New-TestId);Code='TEST-HR-P1-20261020-ADMIN'},@{Id=(New-TestId);Code='TEST-HR-P1-20261020-REQUESTER'})
$rp=@()
foreach($code in @('attendance.leaves.benefits.review','attendance.vehicle.policy.manage','attendance.deployments.read','attendance.deployments.decide')){
    $rp+=@{RoleId=$roles[0].Id;Code=$code}
}
foreach($code in @('attendance.deployments.read','attendance.deployments.request')){$rp+=@{RoleId=$roles[1].Id;Code=$code}}
$assignments=@()
for($i=0;$i -lt 2;$i++){$assignments+=@{Id=(New-TestId);RoleId=$roles[$i].Id;UserAccountId=$actors[$i+1].Id;EmployeeId=$actors[$i+1].EmployeeId}}
$receipts=@()
foreach($suffix in @('admin','requester')){
    foreach($step in @('create','grant','assign')){
        $operation=switch($step){create{'access.role.created'}grant{'access.role.permissions.granted'}assign{'access.assignment.created'}}
        $receipts+=@{Id=(New-TestId);Key="P1-$step-$suffix";Operation=$operation;ActorUserId=$actors[0].Id}
    }
}
$sessions=@();$challenges=@();$deliveries=@()
foreach($index in @(0,1,2,1,2)){
    $cid=New-TestId;$sid=New-TestId
    $sessions+=@{Id=$sid;UserAccountId=$actors[$index].Id;SmsOtpChallengeId=$cid;CorrelationId="SYNTHETIC:$sid";CreatedAt=$start.AddMinutes($sessions.Count+1)}
    $challenges+=@{Id=$cid;UserAccountId=$actors[$index].Id;Purpose='WebLogin'}
    $deliveries+=@{Id=(New-TestId);ChallengeId=$cid;Status=1;Attempts=1}
}
$expected=Get-P1AuditExpected $actors $roles $rp $assignments $receipts $sessions $challenges $deliveries
$audits=@(foreach($entry in $expected){
    @{
        Id=(New-TestId);EventType=$entry.EventType;ActorUserId=$entry.ActorUserId;ActorRoleCode=$entry.ActorRoleCode
        TargetType=$entry.TargetType;TargetId=$entry.TargetId;EmployeeScopeId=$entry.EmployeeScopeId;DeviceReference=$entry.DeviceReference
        CorrelationId=$(if($entry.RequiredCorrelation){$entry.RequiredCorrelation}else{"REQUEST:$($entry.Group)"})
        OccurredAt=$(if($entry.ExactTime){$entry.ExactTime}else{$start.AddMinutes(10)})
        ProtectedChangesReference=$(if($entry.RequiresProtected){"synthetic-protected-reference:$($entry.Group)"}else{$null})
    }
})
$arguments=@{Actors=$actors;Roles=$roles;RolePermissions=$rp;Assignments=$assignments;Receipts=$receipts;
    Sessions=$sessions;Challenges=$challenges;Deliveries=$deliveries;StartedAt=$start;StoppedAt=$start.AddHours(1)}
$result=Assert-P1AuditEvidence -Audits $audits @arguments
Pass ($result.Passed -and $result.AuditCount -eq 51 -and $result.HttpCorrelationGroups -eq 32 -and $result.DeliveryCorrelations -eq 5) 'exact 51-event matrix'
$totals=@{'web.otp.requested'=5;'otp.delivery.accepted'=5;'web.session.created'=5;'access.current_permissions.read'=11;
    'access.account.read'=2;'access.role.created'=2;'access.role.created.replay'=2;'access.role.permission.granted'=6;
    'access.role.permissions.granted.replay'=2;'access.assignment.created'=2;'access.assignment.created.replay'=2;'session.logout'=7}
foreach($key in $totals.Keys){Pass ($result.EventCounts[$key] -eq $totals[$key]) "cardinality $key"}
function Copy-Audits {return ,@($audits|ForEach-Object {$_.Clone()})}
foreach($change in @(
    @{Field='ActorUserId';Value='99999999-9999-9999-9999-999999999999'},
    @{Field='ActorRoleCode';Value='ADMIN'},
    @{Field='TargetId';Value='99999999-9999-9999-9999-999999999999'},
    @{Field='TargetType';Value='UserAccount'},
    @{Field='EventType';Value='web.otp.unapproved'},
    @{Field='EmployeeScopeId';Value=$actors[1].EmployeeId},
    @{Field='DeviceReference';Value='session:99999999-9999-9999-9999-999999999999'},
    @{Field='CorrelationId';Value=''},
    @{Field='OccurredAt';Value=$start.AddHours(2)},
    @{Field='ProtectedChangesReference';Value='unexpected'}
)){
    $bad=Copy-Audits;$bad[0][$change.Field]=$change.Value
    Reject {Assert-P1AuditEvidence -Audits $bad @arguments} "reject changed $($change.Field)"
}
$bad=Copy-Audits;$bad[1].Id=$bad[0].Id
Reject {Assert-P1AuditEvidence -Audits $bad @arguments} 'duplicate audit ID'
$bad=Copy-Audits;$bad[1]=$bad[0].Clone();$bad[1].Id=New-TestId
Reject {Assert-P1AuditEvidence -Audits $bad @arguments} 'duplicate event with new ID'
Reject {Assert-P1AuditEvidence -Audits $audits[0..49] @arguments} 'missing event'
Reject {Assert-P1AuditEvidence -Audits @($audits+$audits[0]) @arguments} 'extra event'
$bad=Copy-Audits
($bad|Where-Object EventType -CEQ 'otp.delivery.accepted'|Select-Object -First 1).CorrelationId='HR-P1-fake'
Reject {Assert-P1AuditEvidence -Audits $bad @arguments} 'delivery requires exact N-format ID, not prefix'
$bad=Copy-Audits
($bad|Where-Object EventType -CEQ 'web.session.created'|Select-Object -First 1).CorrelationId='other-server-trace'
Reject {Assert-P1AuditEvidence -Audits $bad @arguments} 'session issuance must match session record'
$bad=Copy-Audits
($bad|Where-Object EventType -CEQ 'access.role.permission.granted'|Select-Object -First 1).CorrelationId='different-request'
Reject {Assert-P1AuditEvidence -Audits $bad @arguments} 'split multi-grant request'
$bad=Copy-Audits
($bad|Where-Object EventType -CEQ 'access.role.permission.granted'|Select-Object -First 1).ProtectedChangesReference=$null
Reject {Assert-P1AuditEvidence -Audits $bad @arguments} 'missing protected evidence'
$bad=Copy-Audits;$bad[0].CorrelationId=$bad[2].CorrelationId
Reject {Assert-P1AuditEvidence -Audits $bad @arguments} 'cross-request correlation reuse'
$badArgs=$arguments.Clone();$badArgs.RolePermissions=@($rp|ForEach-Object {$_.Clone()});$badArgs.RolePermissions[0].Code='access.accounts.assign'
Reject {Assert-P1AuditEvidence -Audits $audits @badArgs} 'extra permission outside scope'
$badArgs=$arguments.Clone();$badArgs.Sessions=@($sessions|ForEach-Object {$_.Clone()});$badArgs.Sessions[1].SmsOtpChallengeId=$sessions[0].SmsOtpChallengeId
Reject {Assert-P1AuditEvidence -Audits $audits @badArgs} 'reused OTP proof'
$badArgs=$arguments.Clone();$badArgs.Receipts=@($receipts|ForEach-Object {$_.Clone()});$badArgs.Receipts[0].ActorUserId=$actors[1].Id
Reject {Assert-P1AuditEvidence -Audits $audits @badArgs} 'non-SA receipt actor'
$bad=Copy-Audits
foreach($row in $bad){
    if($row.ActorUserId -cnotin @('anonymous','sms-dispatcher')){$row.ActorUserId=$row.ActorUserId.ToUpperInvariant()}
    if($row.TargetType -cne 'RolePermission'){$row.TargetId=$row.TargetId.ToUpperInvariant()}
    if($row.EmployeeScopeId){$row.EmployeeScopeId=$row.EmployeeScopeId.ToUpperInvariant()}
}
Pass ((Assert-P1AuditEvidence -Audits $bad @arguments).Passed) 'semantic SQL GUID casing accepted'
$upperArgs=$arguments.Clone()
foreach($population in @('Actors','Roles','RolePermissions','Assignments','Receipts','Sessions','Challenges','Deliveries')){
    $upperArgs[$population]=@($arguments[$population]|ForEach-Object {
        $copy=$_.Clone()
        foreach($field in @('Id','EmployeeId','RoleId','UserAccountId','SmsOtpChallengeId','ChallengeId')){
            if($copy.ContainsKey($field)){$copy[$field]=([string]$copy[$field]).ToUpperInvariant()}
        }
        $copy
    })
}
Pass ((Assert-P1AuditEvidence -Audits $audits @upperArgs).Passed) 'uppercase SQL object identities with lowercase audit strings accepted'
foreach($file in @('HrPermissionP1AuditVerifier.ps1','Verify-HrPermissionP1Audit.ps1')){
    $tokens=$null;$errors=$null
    $null=[Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot $file),[ref]$tokens,[ref]$errors)
    Pass ($errors.Count -eq 0) "parse $file"
}
Write-Output "PASS $checks P1 audit offline checks. Synthetic data only; no SQL, HTTP, private files or writes."
