# Pure offline verification: no SQL, HTTP, process startup, credentials or private evidence reads.
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'HrOutstationO1Profile.ps1')
$script:count=0
function Pass([bool]$ok,[string]$label){if(!$ok){throw $label};$script:count++}
function Reject([scriptblock]$action,[string]$label){
    $rejected=$false;try{& $action|Out-Null}catch{$rejected=$true}
    Pass $rejected $label
}
$p=Get-O1Profile
$o=@{Server=$p.Server;Database='Cosmix_Testing';Owner=$p.Owner;Operator=$p.Owner;Family=$p.Family
    Root=$p.Root;Url=$p.Url;ProcessId=25768;State='ONLINE';Access='MULTI_USER';ReadOnly=$false
    RootExists=$false;PortOccupied=$false;LinkedPath=$false;Triggers=0;Synonyms=0;ExternalDependencies=0
    AccountCount=3;MarkerCount=0;PermissionCount=5;P1RoleCount=2;Transport='Named pipe';Encryption='FALSE'
    Files=@($p.DataFile,$p.LogFile)}
Assert-O1Target $o;Pass $true 'approved exact target'
foreach($field in @('Server','Database','Owner','Operator','Family','Root','Url','State','Access','Transport','Encryption')){
    $copy=$o.Clone();$copy[$field]='wrong';Reject {Assert-O1Target $copy} "reject $field"
}
foreach($field in @('ProcessId','Triggers','Synonyms','ExternalDependencies','AccountCount','MarkerCount','PermissionCount','P1RoleCount')){
    $copy=$o.Clone();$copy[$field]=999;Reject {Assert-O1Target $copy} "reject $field"
}
foreach($field in @('ReadOnly','RootExists','PortOccupied','LinkedPath')){
    $copy=$o.Clone();$copy[$field]=$true;Reject {Assert-O1Target $copy} "reject $field"
}
$copy=$o.Clone();$copy.Root=(Get-P1Profile).Root;Reject {Assert-O1Target $copy} 'P1 root refused'
$copy=$o.Clone();$copy.Root='C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261020O1'
Reject {Assert-O1Target $copy} 'original O1 root refused for retry'
Pass ($p.Root -ceq 'C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261020O1Continue01') 'only approved Continue01 root'
$copy=$o.Clone();$copy.Root='C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261020O1Retry01'
Reject {Assert-O1Target $copy} 'previous Retry01 root refused'
$copy=$o.Clone();$copy.Root='C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261020O1Retry02'
Reject {Assert-O1Target $copy} 'previous Retry02 root refused'
$copy=$o.Clone();$copy.Files=@($p.DataFile,'C:\wrong.ldf');Reject {Assert-O1Target $copy} 'wrong file refused'
Pass ((ConvertTo-O1Guid $p.HrAccount.ToUpperInvariant()) -ceq $p.HrAccount) 'semantic GUID case'
Reject {ConvertTo-O1Guid 'bad'} 'malformed GUID'
Reject {ConvertTo-O1Guid ([guid]::Empty)} 'zero GUID'
$state=@{Requests=0;Logins=0;Keys=@{}}
1..100|ForEach-Object {Use-O1Budget $state '/api/v1/me/permissions'}
Reject {Use-O1Budget $state '/api/v1/me/permissions'} 'request101 refused'
$state=@{Requests=0;Logins=0;Keys=@{}}
Use-O1Budget $state '/api/v1/auth/web/login';Use-O1Budget $state '/api/v1/auth/web/login'
Reject {Use-O1Budget $state '/api/v1/auth/web/login'} 'third MFA refused'
foreach($key in @('O1-request-duplicate','O1-decision-stale','O1-decision-self','O1-claim-stale',
    'O1-claim-duplicate','O1-claim-decision-stale','O1-claim-decision-self')){
    $state=@{Requests=0;Logins=0;Keys=@{}};Use-O1Budget $state '/test' $key
    Reject {Use-O1Budget $state '/test' $key} "second probe $key refused"
}
foreach($key in @('O1-request','O1-claim')){
    $state=@{Requests=0;Logins=0;Keys=@{}};1..4|ForEach-Object {Use-O1Budget $state '/test' $key}
    Reject {Use-O1Budget $state '/test' $key} "fifth $key refused"
}
$b=@{DeploymentId='aaaaaaaa-0001-4000-8000-000000000001';ClaimId='bbbbbbbb-0002-4000-8000-000000000002'
    DeploymentVersion='AAAAAAAAAAE=';ApprovalVersion='AAAAAAAAAAI=';Fingerprint=('A'*64)}
$plan=@{}
foreach($key in @('O1-request','O1-request-duplicate','O1-decision','O1-decision-stale','O1-decision-self',
    'O1-claim','O1-claim-stale','O1-claim-duplicate','O1-claim-decision','O1-claim-decision-stale','O1-claim-decision-self')){
    $c=Get-O1Command $key $b;$plan[$key]=$c
    Pass (Test-O1Route POST $c.Path $c.Body $key $plan $b) "accept $key"
    $changed=@{};foreach($f in $c.Body.Keys){$changed[$f]=$c.Body[$f]};$changed.extra='denied'
    Pass (!(Test-O1Route POST $c.Path $changed $key $plan $b)) "extra field $key refused"
    $changed.Remove('extra');$changed.reason=$p.ChangedReason
    Pass ((Test-O1Route POST $c.Path $changed $key $plan $b) -eq ($key -cin @('O1-request','O1-claim'))) "changed body $key bounded"
    $missing=@{};foreach($f in $c.Body.Keys){$missing[$f]=$c.Body[$f]};$missing.Remove('reason');$missing.bad=1
    Pass (!(Test-O1Route POST $c.Path $missing $key $plan $b)) "missing field $key refused"
}
foreach($route in @($p.Api,$p.Api+'/wrong',$p.Api.Replace($p.HrEmployee,$p.AdminEmployee),
    '/api/v1/access/roles','/api/v1/hr/policies','/health','https://example.invalid/')){
    Pass (!(Test-O1Route GET $route $null '' $plan $b)) "unapproved route $route"
}
Pass (!(Test-O1Route POST '/api/v1/auth/web/login' @{email='superadmin@example.invalid';password='offline'} '' $plan $b)) 'SA login denied'
Pass (Test-O1Route POST '/api/v1/auth/web/login' @{email='hr@example.invalid';password='offline'} '' $plan $b) 'HR login allowed'
Assert-O1ConcurrentTraces '0HABC:00000001' '0HABD:00000001';Pass $true 'separate connection traces'
Reject {Assert-O1ConcurrentTraces '0HABC:00000001' '0HABC:00000002'} 'same connection refused'
foreach($bad in @('','HR-O1-fake','http://fake','a:xyz',"a:123`n")){Reject {Assert-O1Trace $bad} 'bad trace refused'}
$at=[datetimeoffset]'2026-10-20T12:00:00Z'
$expected=@{EventType='attendance.deployment.request';ActorUserId=$p.HrAccount;ActorRoleCode=$p.RequesterCode
    TargetType='Attendance';TargetId=$b.DeploymentId;EmployeeScopeId=$p.HrEmployee
    DeviceReference='cccccccc-0003-4000-8000-000000000003';CorrelationId='0HABC:00000001';Protected=$true}
$row=$expected.Clone();$row.Remove('Protected');$row.Id='dddddddd-0004-4000-8000-000000000004'
$row.OccurredAt=$at.ToString('o');$row.ProtectedChangesReference='offline-only-proof'
$row.ActorUserId=$p.HrAccount.ToUpperInvariant();$row.TargetId=$b.DeploymentId.ToUpperInvariant()
Assert-O1AuditRows @($row) @($expected) $at $at.AddMinutes(1);Pass $true 'exact audit + uppercase GUIDs'
foreach($field in @('EventType','ActorUserId','ActorRoleCode','TargetType','TargetId','EmployeeScopeId','DeviceReference','CorrelationId')){
    $bad=$row.Clone();$bad[$field]='wrong'
    Reject {Assert-O1AuditRows @($bad) @($expected) $at $at.AddMinutes(1)} "audit $field refused"
}
Reject {Assert-O1AuditRows @($row,$row) @($expected) $at $at.AddMinutes(1)} 'extra audit refused'
$bad=$row.Clone();$bad.ProtectedChangesReference=$null
Reject {Assert-O1AuditRows @($bad) @($expected) $at $at.AddMinutes(1)} 'missing proof refused'
$bad=$row.Clone();$bad.OccurredAt=$at.AddDays(-1)
Reject {Assert-O1AuditRows @($bad) @($expected) $at $at.AddMinutes(1)} 'old audit refused'
$roleRows=@();$n=0
foreach($code in @('HR','ATTHUMA13B03','ATTHUMA13B06',$p.RequesterCode)){
    $n++;$roleRows+=@{Id="eeeeeeee-000$n-4000-8000-00000000000$n";Code=$code}
}
Assert-O1EffectiveRoles $roleRows $true;Pass $true 'HR exact four effective assignments'
Reject {Assert-O1EffectiveRoles ($roleRows+@{Id=[guid]::NewGuid();Code='OTHER'}) $true} 'extra role refused before writes'
Reject {Assert-O1EffectiveRoles @($roleRows[0],$roleRows[1],$roleRows[2],$roleRows[2]) $true} 'duplicate role assignment refused'
$adminRoles=@(@{Id='eeeeeeee-0011-4000-8000-000000000011';Code='ADMIN'},@{Id='eeeeeeee-0012-4000-8000-000000000012';Code=$p.AdminCode})
Assert-O1EffectiveRoles $adminRoles $false;Pass $true 'Admin exact two effective assignments'
$rateKeys=Get-O1NewRateKeys ([byte[]](1..32))
Pass ($rateKeys.Count -eq 16 -and @($rateKeys|Select-Object -Unique).Count -eq 16) 'sixteen exact candidates across two loopback IPs; live budget remains twelve'
Reject {Get-O1NewRateKeys ([byte[]](1..31))} 'short rate key refused'
Pass (@($rateKeys|Where-Object {$_ -cnotmatch '^(web-(request|password|verify)-(ip|email)|request-(ip|phone)):[A-F0-9]{64}$'}).Count -eq 0) 'rate identifiers bounded HMAC'
# Known vectors for OtpProtector.Digest: key bytes 01..20 hex, key version hr-o1,
# uppercase HMAC-SHA256 over UTF8("digest:v1:hr-o1:" + the exact input).
$ipVectors=@{
    '127.0.0.1'='57A2D689A7AFE929F421E6FE131650B037BAB9C878551FB36F23E81B4F293469'
    '::1'='97DFC574059EAB77CE9D241666ECA1C28F27236F4C40D2C305368D7429451A86'
}
$phoneVectors=@{
    '+12025550102'='8CC64830FE84F8156F29660AFA7A27E23DA18ED5EC90884DB185C32AED9B43D8'
    '+12025550103'='C514309E1417F80578EF2BECF33C0C7FD98B0BFF06479FE6E0327D9B827EF5C2'
}
$emailVectors=@(
    '6507E6FF46922557336B49988B10BF1C1967738B800B7374AE554016BA48F585',
    '482F892A551D2936D0BB915186E4EB4DFA866D79E9A7261D9F549F03A9B9BD3E'
)
$expectedRateKeys=@()
foreach($prefix in @('web-request','web-password','web-verify')){
    foreach($digest in $ipVectors.Values){$expectedRateKeys+=$prefix+'-ip:'+$digest}
    foreach($digest in $emailVectors){$expectedRateKeys+=$prefix+'-email:'+$digest}
}
foreach($ip in $ipVectors.Keys){
    $expectedRateKeys+='request-ip:'+$ipVectors[$ip]
    Pass (('request-ip:'+$ipVectors[$ip]) -cin $rateKeys) "canonical shared IP vector $ip"
}
foreach($phone in $phoneVectors.Keys){
    $expectedRateKeys+='request-phone:'+$phoneVectors[$phone]
    Pass (('request-phone:'+$phoneVectors[$phone]) -cin $rateKeys) "canonical approved phone vector $phone"
}
Pass ((($rateKeys|Sort-Object -CaseSensitive) -join '|') -ceq
    (($expectedRateKeys|Sort-Object -CaseSensitive) -join '|')) 'entire finite key set matches canonical digest vectors'
foreach($unapproved in @(
    'request-phone:BA2E2C961F574339C2C6467A7221DF76C656A7470F8FB311BDEF45334200C81C',
    'request-phone:4C180CB4A1D631B9EC3A50E73818B35E3AB18977DD9DE1437394F7498C735546',
    'request-ip:F51620EACA3986F4B7C941505F66CCFDD37E4F4067DEEC7F2F8065FB1CFD56DA'
)){
    Pass ($unapproved -cnotin $rateKeys) 'SA phone, phone-prefixed digest, and non-loopback IP excluded'
}
$otherKeyRates=Get-O1NewRateKeys ([byte[]](32..1))
Pass (@($otherKeyRates|Where-Object {$_ -cin $rateKeys}).Count -eq 0) 'different crypto key cannot reuse excluded buckets'
$wrongVersion=[Convert]::ToHexString([Security.Cryptography.HMACSHA256]::HashData(
    [byte[]](1..32),[Text.Encoding]::UTF8.GetBytes('digest:v1:hr-p1:+12025550102')))
Pass (('request-phone:'+$wrongVersion) -cnotin $rateKeys) 'other key version excluded'
$actualSingleIpKeys=@($expectedRateKeys|Where-Object {!$_.EndsWith($ipVectors['127.0.0.1'],[StringComparison]::Ordinal)})
Pass ($actualSingleIpKeys.Count -eq 12 -and
    @($actualSingleIpKeys|Where-Object {$_.StartsWith('web-',[StringComparison]::Ordinal)}).Count -eq 9 -and
    @($actualSingleIpKeys|Where-Object {$_.StartsWith('request-',[StringComparison]::Ordinal)}).Count -eq 3) 'one actual loopback IP yields exactly nine web plus three shared buckets'
$d=@{Id=$b.DeploymentId;EmployeeId=$p.HrEmployee;EmployeeAssignmentId='20102001-0006-4000-8000-000000000006'
    StartsAtUtc='2026-10-18T04:00:00Z';EndsAtUtc='2026-10-19T12:00:00Z';FinalizedClaimId=$b.ClaimId;Revision=4
    RecordedAt=$at;ApprovalRequestId='eeeeeeee-0021-4000-8000-000000000021';EvidenceReference='request-proof'}
$c=@{Id=$b.ClaimId;EmployeeId=$p.HrEmployee;EmployeeAssignmentId=$d.EmployeeAssignmentId;Kind=5
    DeploymentCategoryCode='TEST_VO';DeploymentApprovalRequestId=$d.ApprovalRequestId;WorkSessionId=$null
    ClaimedAmount=200;Currency='PKR';EligibilityPeriodStart=$d.StartsAtUtc;EligibilityPeriodEnd=$d.EndsAtUtc
    EligibilityKey=('deployment:'+([guid]$b.DeploymentId).ToString('N'));PayableDate='2026-10-19';PayrollPeriodStart=$null
    PolicyVersionId=$p.PolicyVersion;RequestedByUserId=$p.HrAccount;SubmissionVersion=1;SettlementState=0;LastSettledAt=$null
    CreatedAt=$at;SubmittedAt=$at;ApprovalRequestId='eeeeeeee-0022-4000-8000-000000000022'
    EvidencePrivateReference='request-proof';CalculationPrivateReference='calculation-proof'}
$sub=@{Id='eeeeeeee-0023-4000-8000-000000000023';AllowanceClaimId=$c.Id;Version=1;ApprovalRequestId=$c.ApprovalRequestId
    PolicyVersionId=$p.PolicyVersion;ClaimedAmount=200;Currency='PKR';PayableDate='2026-10-19';PayrollPeriodStart=$null
    CorrectionReason=$null;SubmittedByUserId=$p.HrAccount;SubmittedAt=$at;CalculationPrivateReference='calculation-proof'
    SnapshotPrivateReference='calculation-proof';EvidencePrivateReference='request-proof'}
$headers=@(
    @{Id=$d.ApprovalRequestId;SubjectId=$d.Id;RequestType='attendance.deployment';RequesterUserId=$p.HrAccount;Stage=1
        State=1;RouteRuleId=$null;SnapshotPrivateReference='request-proof';RequestedAt=$at;ClosedAt=$at.AddSeconds(10)},
    @{Id=$c.ApprovalRequestId;SubjectId=$c.Id;RequestType='attendance.allowance';RequesterUserId=$p.HrAccount;Stage=1
        State=1;RouteRuleId=$null;SnapshotPrivateReference='calculation-proof';RequestedAt=$at;ClosedAt=$at.AddSeconds(10)}
)
$decisions=@(
    @{Id='eeeeeeee-0024-4000-8000-000000000024';ApprovalRequestId=$d.ApprovalRequestId;Decision=1
        ActorUserId=$p.AdminAccount;ActorRoleCode='ADMIN';Reason=$p.Reason;DecidedAt=$at.AddSeconds(10);ProtectedChangeReference='decision-1'},
    @{Id='eeeeeeee-0025-4000-8000-000000000025';ApprovalRequestId=$c.ApprovalRequestId;Decision=1
        ActorUserId=$p.AdminAccount;ActorRoleCode='ADMIN';Reason=$p.Reason;DecidedAt=$at.AddSeconds(10);ProtectedChangeReference='decision-2'}
)
Assert-O1BusinessRows @($d) @($c) @($sub) $headers $decisions $b $at $at.AddMinutes(1)
Pass $true 'exact persisted seven-row business chain'
foreach($field in @('EmployeeId','EmployeeAssignmentId','FinalizedClaimId','StartsAtUtc','EndsAtUtc','Revision','EvidenceReference')){
    $bad=$d.Clone();$bad[$field]='wrong'
    Reject {Assert-O1BusinessRows @($bad) @($c) @($sub) $headers $decisions $b $at $at.AddMinutes(1)} "deployment $field"
}
foreach($field in @('EmployeeId','EmployeeAssignmentId','PolicyVersionId','ClaimedAmount','Currency','Kind',
    'SettlementState','PayableDate','DeploymentApprovalRequestId','CalculationPrivateReference','EvidencePrivateReference','SubmissionVersion','EligibilityKey')){
    $bad=$c.Clone();$bad[$field]='wrong'
    Reject {Assert-O1BusinessRows @($d) @($bad) @($sub) $headers $decisions $b $at $at.AddMinutes(1)} "claim $field"
}
foreach($field in @('AllowanceClaimId','ApprovalRequestId','PolicyVersionId','ClaimedAmount','CalculationPrivateReference','SnapshotPrivateReference','EvidencePrivateReference')){
    $bad=$sub.Clone();$bad[$field]='wrong'
    Reject {Assert-O1BusinessRows @($d) @($c) @($bad) $headers $decisions $b $at $at.AddMinutes(1)} "submission $field"
}
foreach($field in @('SubjectId','RequesterUserId','RequestType','State','SnapshotPrivateReference')){
    $bad=$headers[0].Clone();$bad[$field]='wrong'
    Reject {Assert-O1BusinessRows @($d) @($c) @($sub) @($bad,$headers[1]) $decisions $b $at $at.AddMinutes(1)} "header $field"
}
foreach($field in @('ApprovalRequestId','ActorUserId','ActorRoleCode','Decision','Reason','ProtectedChangeReference')){
    $bad=$decisions[0].Clone();$bad[$field]=''
    Reject {Assert-O1BusinessRows @($d) @($c) @($sub) $headers @($bad,$decisions[1]) $b $at $at.AddMinutes(1)} "decision $field"
}
$bad=$decisions[0].Clone();$bad.ActorUserId=$p.HrAccount
Reject {Assert-O1BusinessRows @($d) @($c) @($sub) $headers @($bad,$decisions[1]) $b $at $at.AddMinutes(1)} 'self decision persistence refused'
$manifest=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Outstation-O1-Fixture-Manifest-20261020.json') -Raw|ConvertFrom-Json -AsHashtable
Pass ($manifest.sources.Count -eq 9 -and @($manifest.sources.id|Select-Object -Unique).Count -eq 9) 'nine source IDs'
foreach($source in $manifest.sources){$null=ConvertTo-O1Guid $source.id;Pass $true 'source GUID'}
Pass ($manifest.sources[7].authority -eq 2 -and $null -eq $manifest.sources[7].approvalRequestId -and
    $manifest.sources[7].publishedAt -ceq '$seedUtc') 'historical effective dates not fabricated approval'
Pass ($manifest.privateRoot -ceq $p.Root -and $manifest.continuation.employeeDraftRevision -eq 6 -and
    $manifest.continuation.employeeRowVersion -ceq '0000000000338B54' -and
    $manifest.continuation.seedAllowed -eq $false -and @($manifest.continuation.allowedEmployeeChanges).Count -eq 0) 'approved continuation forbids seed and employee changes'
Pass ($manifest.status -ceq 'CONTINUE01_USER_APPROVED_NOT_EXECUTED_BY_THIS_AGENT' -and
    ($manifest.continuation.preservedRoots -join '|') -ceq 'Huma20261020O1|Huma20261020O1Retry01|Huma20261020O1Retry02') 'continuation preserves all three old O1 roots'
$expectedSources=$manifest.continuation.sourceHashes
Assert-O1SourceHashes $expectedSources $expectedSources.Clone();Pass $true 'nine hard-pinned source hashes accepted'
foreach($table in Get-O1SourceTables){
    $bad=$expectedSources.Clone();$bad[$table]='F'*64
    Reject {Assert-O1SourceHashes $expectedSources $bad} "changed retained source $table refused"
    $bad=$expectedSources.Clone();$bad.Remove($table)
    Reject {Assert-O1SourceHashes $expectedSources $bad} "missing source $table cannot trigger seed"
    Pass ((Get-O1RowLimits)[$table] -eq 0) "no new source rows permitted in $table"
}
$bad=$expectedSources.Clone();$bad['hr.Employees']='A'*64
Reject {Assert-O1SourceHashes $expectedSources $bad} 'extra source hash refused'
$bad=$expectedSources.Clone();$bad['hr.Policies']='bad'
Reject {Assert-O1SourceHashes $expectedSources $bad} 'malformed source hash refused'
Assert-O1SourceProofHash $manifest.continuation.sourceProofSha256;Pass $true 'retained proof file hard pin'
Reject {Assert-O1SourceProofHash ('A'*64)} 'wrong prior proof hash refused before parse'
Pass ($manifest.continuation.sourceProofPath -ceq $p.SourceProofPath -and
    $manifest.limits.newSourceRows -eq 0 -and $manifest.limits.totalSeedSqlRowChanges -eq 0 -and
    $manifest.limits.existingEmployeeAggregateUpdates -eq 0) 'source proof location and zero seed budget'
$times=@(
    @{SourceTable='hr.EmployeeAssignments';SourceColumn='RecordedAt';OriginalTimestamp='2026-10-20T12:00:00Z'},
    @{SourceTable='hr.PolicyVersions';SourceColumn='PublishedAt';OriginalTimestamp='2026-10-20T12:00:00Z'},
    @{SourceTable='hr.PolicyAssignments';SourceColumn='RecordedAt';OriginalTimestamp='2026-10-20T12:00:00Z'}
)
$observedAt=[datetimeoffset]'2026-10-20T14:00:00Z'
Assert-O1SourceTimestamps $times $observedAt;Pass $true 'retained timestamp predates continuation, never rebased'
foreach($wrong in @('2026-10-20T15:00:00Z','2026-10-19T12:00:00Z','2026-10-20T12:01:00Z','2026-10-20T12:00:00+05:00')){
    $bad=$times[0].Clone();$bad.OriginalTimestamp=$wrong
    Reject {Assert-O1SourceTimestamps @($bad,$times[1],$times[2]) $observedAt} 'wrong original source time denied'
}
$bad=$times[0].Clone();$bad.SourceColumn='CreatedAt'
Reject {Assert-O1SourceTimestamps @($bad,$times[1],$times[2]) $observedAt} 'invented CreatedAt column refused'
$employeeBefore=@{Id=$p.HrEmployee;Status=2;OnboardingStatus=2;JoinedOn='2026-01-15'
    DraftRevision=6;RowVersion='0000000000338B54'}
$employeeFinal=$employeeBefore.Clone()
Assert-O1EmployeeMetadata $employeeBefore Baseline;Pass $true 'baseline exactly retained revision6'
Assert-O1EmployeeMetadata $employeeFinal Final $employeeBefore;Pass $true 'final original rowversion unchanged'
Reject {Assert-O1EmployeeMetadata $employeeBefore Seed $employeeBefore} 'seed stage rejected'
foreach($field in @('Id','Status','OnboardingStatus','JoinedOn','RowVersion')){
    $bad=$employeeBefore.Clone();$bad[$field]='wrong'
    Reject {Assert-O1EmployeeMetadata $bad Baseline} "employee baseline $field denied"
}
foreach($revision in @(0,4,5,7)){
    $bad=$employeeBefore.Clone();$bad.DraftRevision=$revision
    Reject {Assert-O1EmployeeMetadata $bad Baseline} "baseline revision $revision denied"
}
$bad=$employeeFinal.Clone();$bad.RowVersion='000000000000AB21'
Reject {Assert-O1EmployeeMetadata $bad Final $employeeBefore} 'continuation rowversion drift denied'
$bad=$employeeFinal.Clone();$bad.DraftRevision=7
Reject {Assert-O1EmployeeMetadata $bad Final $employeeBefore} 'continuation extra revision denied'
$bad=$employeeBefore.Clone();$bad.RowVersion='0000000000000000'
Reject {Assert-O1EmployeeMetadata $bad Baseline} 'zero rowversion denied'
$bad=$employeeBefore.Clone();$bad.Id=$bad.Id.ToUpperInvariant()
Assert-O1EmployeeMetadata $bad Baseline;Pass $true 'metadata GUID uppercase accepted'
# Compile the three actual pure domain sources in memory, without a project or host.
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$sources=@('AllowancePolicyRules.cs','DeploymentValuation.cs','DeploymentContinuity.cs')
$code="global using System;`nglobal using System.Collections.Generic;`nglobal using System.Linq;`n"
foreach($source in $sources){
    $text=Get-Content -LiteralPath (Join-Path $repo "src/Cosmix.Domain/HR/Allowances/$source") -Raw
    $text=$text.Replace('namespace Cosmix.Domain.HR.Allowances;','namespace Cosmix.Domain.HR.Allowances {')+"`n}"
    # Put each file's usings inside its namespace when combining source units.
    $lines=$text -split '\r?\n';$usings=@($lines|Where-Object {$_ -match '^using '})
    $text=($lines|Where-Object {$_ -notmatch '^using '}) -join "`n"
    $text=$text.Replace('namespace Cosmix.Domain.HR.Allowances {',"namespace Cosmix.Domain.HR.Allowances {`n"+($usings -join "`n"))
    $code+=$text+"`n"
}
if(!('Cosmix.Domain.HR.Allowances.AllowancePolicyRulesCodec' -as [type])){
    Add-Type -TypeDefinition $code -CompilerOptions '/nullable:enable'
}
$json=$manifest.allowanceRules|ConvertTo-Json -Depth 20 -Compress
$parsed=[Cosmix.Domain.HR.Allowances.AllowancePolicyRulesCodec]::ValidateAndCanonicalize($json)
Pass $parsed.IsValid ('actual policy parser rejected: '+($parsed.Errors -join ','))
$reparsed=[Cosmix.Domain.HR.Allowances.AllowancePolicyRulesCodec]::ValidateAndCanonicalize($parsed.CanonicalJson)
Pass ($reparsed.IsValid -and $reparsed.CanonicalJson -ceq $parsed.CanonicalJson) 'canonical policy stable'
$breaks=[Cosmix.Domain.HR.Allowances.DeploymentHomeBreak[]]@()
$zone=[TimeZoneInfo]::FindSystemTimeZoneById('Asia/Karachi')
$value=[Cosmix.Domain.HR.Allowances.DeploymentValuation]::Calculate(
    [datetimeoffset]$manifest.http.create.startsAtUtc,[datetimeoffset]$manifest.http.create.endsAtUtc,
    $breaks,$parsed.Rules.Deployment,[decimal]100,'PKR',$zone)
Pass ($value.State -ceq 'ready' -and $value.DayUnits -eq 2 -and $value.Amount -eq 200 -and $value.Lines.Count -eq 2) 'actual pure domain valuation 2 days PKR200'
$invalid=$json.Replace('"countingBasis":2','"countingBasis":"LocalCalendarDays"')
Pass (!([Cosmix.Domain.HR.Allowances.AllowancePolicyRulesCodec]::ValidateAndCanonicalize($invalid)).IsValid) 'string enum denied by actual parser'
foreach($file in @('Invoke-HrOutstationO1.ps1','HrOutstationO1Profile.ps1','Verify-HrOutstationO1.ps1')){
    $tokens=$null;$errors=$null
    $null=[Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot $file),[ref]$tokens,[ref]$errors)
    Pass ($errors.Count -eq 0) ("parse "+$file+': '+($errors.Message -join ','))
}
$runner=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Invoke-HrOutstationO1.ps1') -Raw
Pass ($runner.IndexOf("if(`$Mode -ceq 'Inspect')") -lt $runner.IndexOf('$hostProcess.Start()')) 'Inspect returns before startup seed'
Pass ($runner.Contains('$ExistingSourceReuseReady -and') -and !$runner.Contains('SeedPreparationReady') -and
    $runner.Contains('$mobile.Enabled=$true') -and $runner.Contains('$web.Enabled=$true')) 'non-seeding readiness and auth enabled gates'
Pass ($runner.Contains('$evidence.Value.requesterId') -and !$runner.Contains('$evidence.Value.requesterAccountId')) 'actual evidence DTO name'
Pass ($runner.Contains("'Huma20261020P1','Huma20261020P1Retry01'") -and $runner.Contains('$historicalIds')) 'prior P1 evidence retained'
Pass ($runner.Contains("'Huma20261020P1Retry01','Huma20261020O1'")) 'original O1 root included in prior preservation'
Pass ($runner.Contains("'Huma20261020O1','Huma20261020O1Retry01'")) 'Retry01 evidence included in prior preservation'
Pass ($runner.Contains("'Huma20261020O1Retry01','Huma20261020O1Retry02'")) 'Retry02 evidence included in prior preservation'
Pass ($runner.Contains("'hr-employee-baseline|'") -and
    !$runner.Contains('$employeeSeed') -and
    $runner.Contains("Assert-O1EmployeeMetadata `$employeeFinal 'Final' `$employeeBaseline")) 'metadata bound in packet and final without seed allowance'
Pass ($runner.IndexOf('Assert-O1SourceProofHash (Get-FileHash') -lt
    $runner.IndexOf('$sourceProof=Get-Content') -and $runner.Contains('Assert-O1SourceHashes $manifest.continuation.sourceHashes $sourceBaseline')) 'file hard pin before parse and independent live hash comparison'
$tokens=$null;$errors=$null
$tree=[Management.Automation.Language.Parser]::ParseInput($runner,[ref]$tokens,[ref]$errors)
$sourceReaders=@($tree.FindAll({param($n)
    $n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -ceq 'Source-Snapshot'
},$true))
Pass ($sourceReaders.Count -eq 1) 'actual retained source reader definition'
& {
    $queries=[Collections.Generic.List[string]]::new();$missingSource=$false
    function Assert([bool]$ok,[string]$message){Assert-O1 $ok $message}
    function Read-Sql([string]$query){
        $queries.Add($query)
        if($query.StartsWith('SELECT COUNT(*)')){if($missingSource){return '0'};return '1'}
        foreach($row in $manifest.sources){
            if($query.Contains("FROM $($row.table) WHERE Id='$($row.id)'")){
                return $manifest.continuation.sourceHashes[$row.table]
            }
        }
        throw 'Unrecognized synthetic source SELECT.'
    }
    . ([scriptblock]::Create($sourceReaders[0].Extent.Text))
    $captured=Source-Snapshot
    Assert-O1SourceHashes $manifest.continuation.sourceHashes $captured
    Pass ($captured.Count -eq 9 -and $queries.Count -eq 18) 'actual source reader compares retained nine rows despite zero write budgets'
    Pass (@($queries|Where-Object {$_.StartsWith('SELECT CONVERT') -and
        $_.Contains("HASHBYTES('SHA2_256',(SELECT *") -and
        $_.Contains('FOR JSON PATH,INCLUDE_NULL_VALUES')}).Count -eq 9) 'actual SQL source hash shape retains all columns and original timestamps'
    $missingSource=$true
    Reject {Source-Snapshot} 'missing existing source refused, no seed fallback'
}
$fixtureSource=Get-Content -LiteralPath (Join-Path $repo 'src/Cosmix.Api/Development/HrOutstationO1Fixture.cs') -Raw
foreach($table in Get-O1SourceTables){
    Pass ($fixtureSource.Contains($manifest.continuation.sourceHashes[$table])) "parent C# source hash matches manifest $table"
}
$factory=@($tree.FindAll({param($n)
    $n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -ceq 'New-P1SqlConnection'
},$true))
Pass ($errors.Count -eq 0 -and $factory.Count -eq 1) 'exact SQL connection factory definition'
# Construct the actual runner connection without opening it. PowerShell's dictionary
# adapter requires the canonical spaced SQL keyword, not ApplicationName.
. ([scriptblock]::Create($factory[0].Extent.Text))
$connection=New-P1SqlConnection
try {
    Pass ($connection.State -eq [Data.ConnectionState]::Closed) 'factory performs no connection'
    $options=[Data.SqlClient.SqlConnectionStringBuilder]::new($connection.ConnectionString)
    Pass ($options['Application Name'] -ceq 'Cosmix.HrOutstation.O1.Inspect' -and
        $options['Data Source'] -ceq $p.Pipe -and $options['Initial Catalog'] -ceq 'Cosmix_Testing') 'actual factory exact target and diagnostic name'
    Pass ($options['Integrated Security'] -and $options['Pooling'] -and $options['Max Pool Size'] -eq 1 -and
        $options['Connect Timeout'] -eq 30 -and $options['ConnectRetryCount'] -eq 0) 'actual factory bounded Windows pool no retry'
} finally {$connection.Dispose()}
$metadataReaders=@($tree.FindAll({param($n)
    $n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -ceq 'Read-O1EmployeeMetadata'
},$true))
Pass ($metadataReaders.Count -eq 1) 'actual employee metadata reader definition'
& {
    $profile=Get-O1Profile
    $queries=[Collections.Generic.List[string]]::new()
    $mock=$employeeBefore.Clone();$mock.RowVersionByteLength=8
    function Assert([bool]$ok,[string]$message){Assert-O1 $ok $message}
    function Json-Rows([string]$query){$queries.Add($query);return ,@($mock)}
    . ([scriptblock]::Create($metadataReaders[0].Extent.Text))
    $metadata=Read-O1EmployeeMetadata
    Assert-O1EmployeeMetadata $metadata Baseline
    Pass ($metadata.RowVersion -ceq '0000000000338B54') 'eight-byte retained rowversion exact hex preserved'
    Pass ($queries.Count -eq 1 -and $queries[0].Contains('DATALENGTH(RowVersion) AS RowVersionByteLength') -and
        $queries[0].Contains('CASE WHEN DATALENGTH(RowVersion)=8 THEN CONVERT(varchar(16),CONVERT(binary(8),RowVersion),2) ELSE NULL END AS RowVersion')) 'actual query guards byte length before explicit binary hex conversion'
    Pass ($queries[0].Contains("WHERE Id='$($profile.HrEmployee)'") -and
        !$queries[0].Contains('CONVERT(varchar(16),RowVersion,2)')) 'query stays pinned and removes direct timestamp conversion'
    foreach($length in @($null,0,7,9,16)){
        $mock.RowVersionByteLength=$length
        Reject {Read-O1EmployeeMetadata} 'wrong byte length denied even with plausible hex'
    }
    $mock.RowVersionByteLength=8;$mock.RowVersion='rawtimestamp'
    Reject {Assert-O1EmployeeMetadata (Read-O1EmployeeMetadata) Baseline} 'raw timestamp characters still rejected'
}
$snapshots=@($tree.FindAll({param($n)
    $n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -ceq 'Protected-Snapshot'
},$true))
Pass ($snapshots.Count -eq 1) 'actual protected snapshot definition'
& {
    # Exercise the actual snapshot function with pure in-memory SQL substitutes.
    $profile=Get-O1Profile;$actors=@();$newRateKeys=@();$baselineCounts=@{};$historicalIds=@{}
    $queries=[Collections.Generic.List[string]]::new()
    function Assert([bool]$ok,[string]$message){Assert-O1 $ok $message}
    function Json-Rows([string]$query){return ,@(@{schema='hr';table='Employees'})}
    function Read-Sql([string]$query){
        $queries.Add($query)
        if($query.StartsWith('SELECT COUNT(*)')){return '3'}
        if($query.Contains('FROM sys.indexes')){return '[Id]'}
        return 'A'*64
    }
    . ([scriptblock]::Create($snapshots[0].Extent.Text))
    $result=Protected-Snapshot
    Pass ($result.ContainsKey('hr.Employees')) 'one full employee-table preservation hash'
    Pass (@($queries|Where-Object {$_.Contains("name NOT IN('DraftRevision','RowVersion')")}).Count -eq 0) 'no employee metadata exclusions'
    Pass (@($queries|Where-Object {$_.Contains('SELECT * FROM [hr].[Employees] ORDER BY [Id] FOR JSON PATH,INCLUDE_NULL_VALUES')}).Count -eq 1) 'all employees all columns included'
    Pass (@($queries|Where-Object {$_.Contains("WHERE Id<>'$($profile.HrEmployee)'")}).Count -eq 0) 'HR no longer excluded from whole row protection'
    Pass (@($queries|Where-Object {$_.Contains("WHERE Id<>'$($profile.AdminEmployee)'")}).Count -eq 0) 'no other employee exclusion'
}
# Completion regression uses only in-memory exceptions/actions, never a database or private file.
$completion=New-O1CompletionState
Pass (@($completion.Checks.Values|Where-Object {$_ -cne 'Unknown'}).Count -eq 0) 'preservation starts explicitly Unknown'
Pass ($null -eq (Get-O1CompletionFailure $completion)) 'no fabricated initial failure'
try {throw [InvalidOperationException]::new('offline primary seed failure')}
catch {$completion.Primary=$_}
$primary=$completion.Primary
$primaryLine=$primary.InvocationInfo.ScriptLineNumber
Invoke-O1CompletionCheck $completion 'ProtectedSql' {throw [TimeoutException]::new('offline reconciliation timeout')}
Invoke-O1CompletionCheck $completion 'PriorFiles' {$completion.Values.FilesChecked=$true}
Invoke-O1CompletionCheck $completion 'EmployeeMetadata' {throw [InvalidOperationException]::new('offline metadata unavailable')}
Invoke-O1CompletionCheck $completion 'Bounds' {$completion.Values.BoundsChecked=$true}
Pass ([object]::ReferenceEquals((Get-O1CompletionFailure $completion),$primary)) 'original primary ErrorRecord retained over reconciliation failures'
Pass ($completion.Primary.InvocationInfo.ScriptLineNumber -eq $primaryLine) 'primary source location retained'
Pass ($completion.Checks.ProtectedSql -ceq 'Unknown' -and $completion.Checks.EmployeeMetadata -ceq 'Unknown') 'failed reads never claim preservation'
Pass ($completion.Checks.PriorFiles -ceq 'Passed' -and $completion.Values.FilesChecked -and
    $completion.Checks.Bounds -ceq 'Passed' -and $completion.Values.BoundsChecked) 'independent checks continue after read failures'
Pass ($completion.Errors.Count -eq 2 -and $completion.Errors[0].Stage -ceq 'ProtectedSql' -and
    $completion.Errors[1].Stage -ceq 'EmployeeMetadata') 'separate ordered reconciliation errors retained'
$report=Get-O1CompletionReport $completion
Pass ($report.PrimaryFailure.Type -ceq 'System.InvalidOperationException' -and
    $report.PrimaryFailure.Line -eq $primaryLine -and $report.SecondaryErrors.Count -eq 2) 'bounded report retains primary and secondary diagnostics'
$reportJson=$report|ConvertTo-Json -Depth 10 -Compress
Pass (!$reportJson.Contains('offline primary seed failure') -and
    !$reportJson.Contains('offline reconciliation timeout') -and !$reportJson.Contains('"Record"')) 'report omits raw errors messages and exception objects'
function Invoke-O1OfflineCompletion {
    [CmdletBinding()]param($state)
    $failure=Get-O1CompletionFailure $state
    if($null -ne $failure){$PSCmdlet.ThrowTerminatingError($failure)}
}
$observedFailure=$null
try {Invoke-O1OfflineCompletion $completion}catch{$observedFailure=$_}
Pass ($null -ne $observedFailure -and [object]::ReferenceEquals($observedFailure.Exception,$primary.Exception)) 'terminating error preserves primary exception object'
$secondaryOnly=New-O1CompletionState
Invoke-O1CompletionCheck $secondaryOnly 'ProtectedSql' {throw [TimeoutException]::new('offline secondary only')}
Invoke-O1CompletionCheck $secondaryOnly 'PriorFiles' {}
Pass ([object]::ReferenceEquals((Get-O1CompletionFailure $secondaryOnly),$secondaryOnly.Errors[0].Record)) 'reconciliation-only failure still terminates'
Reject {Invoke-O1OfflineCompletion $secondaryOnly} 'no primary does not swallow failed reconciliation'
$successful=New-O1CompletionState
foreach($name in @($successful.Checks.Keys)){Invoke-O1CompletionCheck $successful $name {}}
Pass ($null -eq (Get-O1CompletionFailure $successful) -and $successful.Errors.Count -eq 0 -and
    @($successful.Checks.Values|Where-Object {$_ -cne 'Passed'}).Count -eq 0) 'only successful checks claim Passed'
Invoke-O1CompletionCheck $completion 'PreservationEvidence' {throw [IO.IOException]::new('offline evidence write failure')}
Pass ([object]::ReferenceEquals((Get-O1CompletionFailure $completion),$primary) -and
    $completion.Checks.PreservationEvidence -ceq 'Unknown') 'evidence write failure cannot mask primary'
Pass ($runner.Contains('$completion.Primary=$_') -and
    $runner.Contains('$PSCmdlet.ThrowTerminatingError($completionFailure)') -and
    !$runner.Contains('throw $failure') -and $runner.Contains("Preservation='Unknown'")) 'runner uses retained-primary terminal flow and Unknown initial evidence'
[pscustomobject]@{Passed=$true;Checks=$script:count;Sql=$false;Http=$false;Host=$false;PrivateEvidence=$false;ActualDomainParser=$true;Valuation='2 calendar days; PKR 200'}
