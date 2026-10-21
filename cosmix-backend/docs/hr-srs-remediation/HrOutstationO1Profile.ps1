# Definitions only. Loading this file performs no live/private IO.
. (Join-Path $PSScriptRoot 'HrPermissionP1Profile.ps1')
function Get-O1Profile {
    $p=Get-P1Profile
    $p.Profile='HumaHrOutstation20261020O1';$p.Application='Cosmix.HrOutstation.O1'
    $p.Root='C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261020O1Continue01'
    $p.SourceProofPath='C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261020O1Retry02\results\seed-snapshot.json'
    $p.SourceProofSha256='8CC27CD2B130AD8F1280E81ABEFE2B57DBE0D452C38D2D777B022827F908ACDC'
    $p.Reason='TEST ONLY O1 outstation business acceptance'
    $p.ChangedReason='TEST ONLY O1 changed payload'
    $p.Api="/api/v1/hr/employees/$($p.HrEmployee)/deployments"
    $p.PolicyVersion='20102001-0008-4000-8000-000000000008'
    $p.GrantEnds='2026-10-21T11:09:40.0589427Z'
    return $p
}
function Assert-O1([bool]$ok,[string]$message){if(!$ok){throw $message}}
function New-O1CompletionState {
    return @{Primary=$null;Checks=[ordered]@{HostExit='Unknown';ProtectedSql='Unknown';PriorFiles='Unknown'
        ExistingSources='Unknown';EmployeeMetadata='Unknown';Bounds='Unknown';Counts='Unknown'}
        Errors=[Collections.Generic.List[object]]::new();Values=@{}}
}
function Invoke-O1CompletionCheck($state,[string]$name,[scriptblock]$action){
    $state.Checks[$name]='Unknown'
    try {
        $null=& $action
        $state.Checks[$name]='Passed'
    } catch {
        # Retain ErrorRecords in memory, but serialize only bounded diagnostic metadata.
        $state.Errors.Add(@{Stage=$name;Record=$_})
    }
}
function Get-O1CompletionFailure($state){
    if($null -ne $state.Primary){return $state.Primary}
    if($state.Errors.Count -gt 0){return $state.Errors[0].Record}
    return $null
}
function Get-O1CompletionReport($state){
    $primary=if($null -ne $state.Primary){
        @{Type=$state.Primary.Exception.GetType().FullName;Line=$state.Primary.InvocationInfo.ScriptLineNumber}
    }else{$null}
    return @{PrimaryFailure=$primary;Checks=$state.Checks
        SecondaryErrors=@($state.Errors|ForEach-Object {
            @{Stage=$_.Stage;Type=$_.Record.Exception.GetType().FullName;Line=$_.Record.InvocationInfo.ScriptLineNumber}
        })}
}
function ConvertTo-O1Guid($value){
    $id=[guid]::Empty
    Assert-O1 ([guid]::TryParse([string]$value,[ref]$id) -and $id -ne [guid]::Empty) 'O1 GUID binding missing.'
    return $id.ToString('D')
}
function Assert-O1EmployeeMetadata($row,[ValidateSet('Baseline','Final')][string]$stage,$baseline=$null){
    Assert-O1 ((ConvertTo-O1Guid $row.Id) -ceq (Get-O1Profile).HrEmployee -and
        $row.Status -eq 2 -and $row.OnboardingStatus -eq 2 -and [string]$row.JoinedOn -ceq '2026-01-15' -and
        [string]$row.RowVersion -cmatch '\A[0-9A-F]{16}\z' -and $row.RowVersion -cne '0000000000000000') 'HR employee metadata binding differs.'
    Assert-O1 ($row.DraftRevision -eq 6 -and $row.RowVersion -ceq '0000000000338B54') 'Continuation requires unchanged HR revision 6 and retained rowversion.'
    if($stage -ceq 'Final'){
        Assert-O1EmployeeMetadata $baseline 'Baseline'
        Assert-O1 ($row.RowVersion -ceq $baseline.RowVersion) 'HR rowversion changed during continuation.'
    }
}
function Get-O1SourceTables {
    return @('hr.Departments','hr.Designations','hr.StaffCategories','hr.EmploymentTypes','hr.Branches',
        'hr.EmployeeAssignments','hr.Policies','hr.PolicyVersions','hr.PolicyAssignments')
}
function Assert-O1SourceProofHash([string]$value){
    Assert-O1 ($value -ceq (Get-O1Profile).SourceProofSha256) 'Retained source proof file hash differs.'
}
function Assert-O1SourceHashes($expected,$actual){
    $tables=Get-O1SourceTables
    foreach($set in @($expected,$actual)){
        Assert-O1 ($set -is [Collections.IDictionary] -and $set.Count -eq 9 -and
            @($set.Keys|Where-Object {$_ -cnotin $tables}).Count -eq 0) 'Exactly nine retained source hashes required.'
        foreach($table in $tables){Assert-O1 ([string]$set[$table] -cmatch '\A[A-F0-9]{64}\z') 'Malformed retained source hash.'}
    }
    foreach($table in $tables){Assert-O1 ($actual[$table] -ceq $expected[$table]) "Retained source hash differs: $table"}
}
function Assert-O1SourceTimestamps($rows,[datetimeoffset]$observedAt){
    $columns=@{'hr.EmployeeAssignments'='RecordedAt';'hr.PolicyVersions'='PublishedAt';'hr.PolicyAssignments'='RecordedAt'}
    Assert-O1 (@($rows).Count -eq 3 -and @($rows.SourceTable|Select-Object -Unique).Count -eq 3) 'Exactly three original source timestamps required.'
    $original=$null
    foreach($row in $rows){
        Assert-O1 ($columns.ContainsKey([string]$row.SourceTable) -and
            $row.SourceColumn -ceq $columns[$row.SourceTable]) 'Original source timestamp column differs.'
        $at=[datetimeoffset]$row.OriginalTimestamp
        Assert-O1 ($at.Offset -eq [timespan]::Zero -and $at -ge [datetimeoffset]'2026-10-20T00:00:00Z' -and
            $at -lt [datetimeoffset]'2026-10-21T00:00:00Z' -and $at -le $observedAt -and
            ($null -eq $original -or $at -eq $original)) 'Retained common seed timestamp differs or is in the future.'
        $original=$at
    }
}
function Assert-O1Target($o){
    $p=Get-O1Profile
    foreach($key in @('Server','Owner','Family','Root','Url')){
        Assert-O1 ($o[$key] -ieq $p[$key]) "O1 target $key differs."
    }
    Assert-O1 ($o.Operator -ieq $p.Owner -and $o.Database -ceq 'Cosmix_Testing' -and
        $o.ProcessId -eq 25768 -and $o.State -ceq 'ONLINE' -and $o.Access -ceq 'MULTI_USER' -and
        $o.ReadOnly -eq $false -and $o.RootExists -eq $false -and $o.PortOccupied -eq $false -and
        $o.LinkedPath -eq $false -and $o.Triggers -eq 0 -and $o.Synonyms -eq 0 -and
        $o.ExternalDependencies -eq 0 -and $o.AccountCount -eq 3 -and $o.MarkerCount -eq 0 -and
        $o.PermissionCount -eq 5 -and $o.P1RoleCount -eq 2 -and $o.Transport -ceq 'Named pipe' -and
        $o.Encryption -cin @('FALSE','TRUE')) 'O1 target state differs.'
    Assert-O1 (@($o.Files).Count -eq 2 -and $o.Files[0] -ieq $p.DataFile -and
        $o.Files[1] -ieq $p.LogFile) 'O1 database files differ.'
}
function Use-O1Budget($state,[string]$path,[string]$key=''){
    $login=$path -ceq '/api/v1/auth/web/login'
    Assert-O1 ($state.Requests -lt 100 -and (!$login -or $state.Logins -lt 2)) 'O1 HTTP/MFA budget exceeded.'
    if($key){
        $limits=@{'O1-request'=4;'O1-decision'=3;'O1-claim'=4;'O1-claim-decision'=3;
            'O1-request-duplicate'=1;'O1-decision-stale'=1;'O1-decision-self'=1;
            'O1-claim-stale'=1;'O1-claim-duplicate'=1;'O1-claim-decision-stale'=1;'O1-claim-decision-self'=1}
        Assert-O1 ($limits.ContainsKey($key) -and [int]$state.Keys[$key] -lt $limits[$key]) 'O1 command budget exceeded.'
        $state.Keys[$key]=[int]$state.Keys[$key]+1
    }
    $state.Requests++
    if($login){$state.Logins++}
}
function Get-O1Command([string]$key,$b){
    $p=Get-O1Profile;$body=$null;$path=$p.Api
    if($key -cin @('O1-request','O1-request-duplicate')){
        $body=[ordered]@{startsAtUtc='2026-10-18T04:00:00Z';endsAtUtc='2026-10-19T12:00:00Z'
            destination='TEST-VO-DESTINATION';deploymentCategory='TEST_VO';writtenEvidence=$p.Reason;reason=$p.Reason}
    } else {
        $trip=ConvertTo-O1Guid $b.DeploymentId;$path+="/$trip"
        if($key -cin @('O1-claim','O1-claim-stale','O1-claim-duplicate')){
            $path+='/claims'
            $body=[ordered]@{expectedDeploymentVersion=$b.DeploymentVersion;expectedFingerprint=$b.Fingerprint
                payableDate='2026-10-19';reason=$p.Reason}
        } elseif($key -cin @('O1-decision','O1-decision-stale','O1-decision-self',
            'O1-claim-decision','O1-claim-decision-stale','O1-claim-decision-self')){
            if($key.StartsWith('O1-claim-',[StringComparison]::Ordinal)){$path+='/claims/'+(ConvertTo-O1Guid $b.ClaimId)}
            $path+='/decision'
            $body=[ordered]@{expectedDeploymentVersion=$b.DeploymentVersion;expectedApprovalVersion=$b.ApprovalVersion
                decision=1;writtenEvidence=$p.Reason;reason=$p.Reason}
        } else {throw 'Unapproved O1 command.'}
        if($key.EndsWith('-stale',[StringComparison]::Ordinal)){$body.expectedDeploymentVersion='AAAAAAAAAAA='}
    }
    return @{Path=$path;Body=$body;Key=$key}
}
function Test-O1Route([string]$method,[string]$path,$body,[string]$key,$plan,$b){
    $p=Get-O1Profile
    if($path -cin @('/api/v1/auth/web/csrf','/api/v1/me/permissions',
        '/api/v1/auth/web/login','/api/v1/auth/web/otp/verify','/api/v1/auth/web/local-testing/otp','/api/v1/auth/web/logout')){
        if($path -ceq '/api/v1/auth/web/login' -and $body.email -cnotin @('hr@example.invalid','admin@example.invalid')){return $false}
        return Test-P1Route $method $path $body $key @{}
    }
    if($method -ceq 'GET'){
        if($null -ne $body -or $key -or !$b.DeploymentId){return $false}
        $trip=ConvertTo-O1Guid $b.DeploymentId
        $paths=@("$($p.Api)/$trip","$($p.Api)/$trip/calculation")
        if($b.ClaimId){$paths+="$($p.Api)/$trip/claims/$(ConvertTo-O1Guid $b.ClaimId)/evidence"}
        return $path -cin $paths
    }
    if($method -cne 'POST' -or !$key -or !$plan.ContainsKey($key)){return $false}
    $expected=$plan[$key]
    if($path -cne $expected.Path -or $body -isnot [Collections.IDictionary] -or $body.Count -ne $expected.Body.Count){return $false}
    foreach($field in $expected.Body.Keys){
        if($field -cnotin @($body.Keys)){return $false}
        if($field -ceq 'reason' -and $key -cin @('O1-request','O1-claim') -and $body[$field] -ceq $p.ChangedReason){continue}
        if(($body[$field]|ConvertTo-Json -Compress) -cne ($expected.Body[$field]|ConvertTo-Json -Compress)){return $false}
    }
    return $true
}
function Get-O1RowLimits {
    return @{
        'hr.Departments'=0;'hr.Designations'=0;'hr.StaffCategories'=0;'hr.EmploymentTypes'=0;'hr.Branches'=0
        'hr.EmployeeAssignments'=0;'hr.Policies'=0;'hr.PolicyVersions'=0;'hr.PolicyAssignments'=0
        'hr.OutstationDeployments'=1;'hr.AllowanceClaims'=1;'hr.AllowanceClaimSubmissions'=1
        'workflow.ApprovalRequests'=2;'workflow.ApprovalDecisions'=2;'workflow.IdempotencyReceipts'=4
        'auth.UserSessions'=2;'auth.RefreshTokens'=2;'auth.MobileOtpChallenges'=2;'auth.OtpDeliveries'=2
        'audit.AuditEvents'=60
    }
}
function Assert-O1Trace([string]$trace){
    Assert-O1 ($trace -cmatch '\A[A-Za-z0-9_-]{1,80}:[0-9A-Fa-f]{1,16}\z') 'Missing/malformed server Kestrel trace.'
}
function Assert-O1ConcurrentTraces([string]$left,[string]$right){
    Assert-O1Trace $left;Assert-O1Trace $right
    Assert-O1 (($left -split ':')[0] -cne ($right -split ':')[0]) 'Concurrency pair did not use distinct connections.'
}
function Assert-O1AuditRows($rows,$expected,[datetimeoffset]$started,[datetimeoffset]$stopped){
    Assert-O1 (@($rows).Count -eq @($expected).Count -and @($rows).Count -le 60 -and
        $stopped -ge $started -and $stopped-$started -lt [timespan]::FromHours(2)) 'O1 audit cardinality/window differs.'
    $remaining=[Collections.Generic.List[object]]::new()
    foreach($e in $expected){$remaining.Add($e)}
    $ids=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach($row in $rows){
        Assert-O1 ($ids.Add((ConvertTo-O1Guid $row.Id))) 'Duplicate O1 audit ID.'
        $matches=@($remaining|Where-Object {
            $_.EventType -ceq $row.EventType -and $_.ActorRoleCode -ceq $row.ActorRoleCode -and
            $_.TargetType -ceq $row.TargetType -and $_.CorrelationId -ceq $row.CorrelationId -and
            (ConvertTo-O1AuditValue $_.ActorUserId) -ceq (ConvertTo-O1AuditValue $row.ActorUserId) -and
            (ConvertTo-O1AuditValue $_.TargetId) -ceq (ConvertTo-O1AuditValue $row.TargetId) -and
            (ConvertTo-O1AuditValue $_.EmployeeScopeId) -ceq (ConvertTo-O1AuditValue $row.EmployeeScopeId) -and
            (ConvertTo-O1AuditValue $_.DeviceReference) -ceq (ConvertTo-O1AuditValue $row.DeviceReference)
        })
        Assert-O1 ($matches.Count -eq 1) 'Unexpected O1 audit event/trace/actor/target/session.'
        $e=$matches[0];$null=$remaining.Remove($e)
        Assert-O1 ([datetimeoffset]$row.OccurredAt -ge $started -and [datetimeoffset]$row.OccurredAt -le $stopped) 'O1 audit outside run.'
        if($e.Protected){Assert-O1 (![string]::IsNullOrWhiteSpace($row.ProtectedChangesReference) -and $row.ProtectedChangesReference.Length -le 500) 'O1 write audit proof absent.'}
        else{Assert-O1 ([string]::IsNullOrEmpty($row.ProtectedChangesReference)) 'Unexpected O1 audit proof.'}
    }
    Assert-O1 ($remaining.Count -eq 0) 'Missing O1 audit.'
}
function ConvertTo-O1AuditValue($value){
    if($null -eq $value){return ''}
    $s=[string]$value;$id=[guid]::Empty
    if([guid]::TryParse($s,[ref]$id)){return $id.ToString('D')}
    if($s -cmatch '\Asession:([0-9a-fA-F-]{36})(;device:)?\z'){
        return 'session:'+([guid]$Matches[1]).ToString('D')+$Matches[2]
    }
    return $s
}
function Assert-O1EffectiveRoles($rows,[bool]$hr){
    $p=Get-O1Profile
    $expected=if($hr){@('HR','ATTHUMA13B03','ATTHUMA13B06',$p.RequesterCode)}else{@('ADMIN',$p.AdminCode)}
    Assert-O1 (@($rows).Count -eq $expected.Count -and
        @($rows|ForEach-Object {ConvertTo-O1Guid $_.Id}|Select-Object -Unique).Count -eq $expected.Count -and
        (($rows.Code|Sort-Object -CaseSensitive) -join '|') -ceq (($expected|Sort-Object -CaseSensitive) -join '|')) 'Unexpected effective role assignment set.'
}
function Get-O1NewRateKeys([byte[]]$verifierKey){
    Assert-O1 ($verifierKey.Length -eq 32) 'O1 verifier key length differs.'
    $keys=@()
    foreach($prefix in @('web-request','web-password','web-verify')){
        foreach($ip in @('127.0.0.1','::1')){
            $digest=[Convert]::ToHexString([Security.Cryptography.HMACSHA256]::HashData($verifierKey,[Text.Encoding]::UTF8.GetBytes("digest:v1:hr-o1:$ip")))
            $keys+=$prefix+'-ip:'+$digest
        }
        foreach($email in @('ADMIN@EXAMPLE.INVALID','HR@EXAMPLE.INVALID')){
            $digest=[Convert]::ToHexString([Security.Cryptography.HMACSHA256]::HashData($verifierKey,[Text.Encoding]::UTF8.GetBytes("digest:v1:hr-o1:web-email:$email")))
            $keys+=$prefix+'-email:'+$digest
        }
    }
    # Web MFA also consumes shared delivery limits. Phone/IP values have no extra digest prefix.
    foreach($ip in @('127.0.0.1','::1')){
        $digest=[Convert]::ToHexString([Security.Cryptography.HMACSHA256]::HashData($verifierKey,[Text.Encoding]::UTF8.GetBytes("digest:v1:hr-o1:$ip")))
        $keys+='request-ip:'+$digest
    }
    foreach($phone in @('+12025550102','+12025550103')){
        $digest=[Convert]::ToHexString([Security.Cryptography.HMACSHA256]::HashData($verifierKey,[Text.Encoding]::UTF8.GetBytes("digest:v1:hr-o1:$phone")))
        $keys+='request-phone:'+$digest
    }
    return ,$keys
}
function Assert-O1BusinessRows($deployments,$claims,$submissions,$headers,$decisions,$b,
    [datetimeoffset]$started,[datetimeoffset]$stopped){
    $p=Get-O1Profile
    Assert-O1 ((@(@($deployments).Count,@($claims).Count,@($submissions).Count,@($headers).Count,@($decisions).Count) -join ',') -ceq '1,1,1,2,2') 'Business row cardinalities differ.'
    $ids=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach($population in @(@{Rows=$deployments},@{Rows=$claims},@{Rows=$submissions},@{Rows=$headers},@{Rows=$decisions})){
        foreach($row in $population.Rows){Assert-O1 ($ids.Add((ConvertTo-O1Guid $row.Id))) 'Duplicate business row identity.'}
    }
    $d=$deployments[0];$c=$claims[0];$s=$submissions[0]
    $trip=ConvertTo-O1Guid $b.DeploymentId;$claim=ConvertTo-O1Guid $b.ClaimId
    $employment='20102001-0006-4000-8000-000000000006'
    Assert-O1 ((ConvertTo-O1Guid $d.Id) -ceq $trip -and (ConvertTo-O1Guid $d.EmployeeId) -ceq $p.HrEmployee -and
        (ConvertTo-O1Guid $d.EmployeeAssignmentId) -ceq $employment -and
        (ConvertTo-O1Guid $d.FinalizedClaimId) -ceq $claim -and $d.Revision -eq 4 -and
        [datetimeoffset]$d.StartsAtUtc -eq [datetimeoffset]'2026-10-18T04:00:00Z' -and
        [datetimeoffset]$d.EndsAtUtc -eq [datetimeoffset]'2026-10-19T12:00:00Z' -and
        [datetimeoffset]$d.RecordedAt -ge $started -and [datetimeoffset]$d.RecordedAt -le $stopped) 'Deployment persistence differs.'
    Assert-O1 ((ConvertTo-O1Guid $c.Id) -ceq $claim -and (ConvertTo-O1Guid $c.EmployeeId) -ceq $p.HrEmployee -and
        (ConvertTo-O1Guid $c.EmployeeAssignmentId) -ceq $employment -and $c.Kind -eq 5 -and
        $c.DeploymentCategoryCode -ceq 'TEST_VO' -and
        (ConvertTo-O1Guid $c.DeploymentApprovalRequestId) -ceq (ConvertTo-O1Guid $d.ApprovalRequestId) -and
        $null -eq $c.WorkSessionId -and $c.ClaimedAmount -eq 200 -and $c.Currency -ceq 'PKR' -and
        [datetimeoffset]$c.EligibilityPeriodStart -eq [datetimeoffset]$d.StartsAtUtc -and
        [datetimeoffset]$c.EligibilityPeriodEnd -eq [datetimeoffset]$d.EndsAtUtc -and
        $c.EligibilityKey -ceq ('deployment:'+([guid]$trip).ToString('N')) -and
        [string]$c.PayableDate -ceq '2026-10-19' -and $null -eq $c.PayrollPeriodStart -and
        (ConvertTo-O1Guid $c.PolicyVersionId) -ceq $p.PolicyVersion -and
        (ConvertTo-O1Guid $c.RequestedByUserId) -ceq $p.HrAccount -and
        $c.SubmissionVersion -eq 1 -and $c.SettlementState -eq 0 -and $null -eq $c.LastSettledAt -and
        [datetimeoffset]$c.CreatedAt -ge $started -and [datetimeoffset]$c.CreatedAt -le $stopped -and
        $c.EvidencePrivateReference -ceq $d.EvidenceReference) 'Claim persistence differs.'
    foreach($reference in @($d.EvidenceReference,$c.CalculationPrivateReference)){
        Assert-O1 (![string]::IsNullOrWhiteSpace($reference) -and $reference.Length -le 500) 'Missing bounded business evidence reference.'
    }
    Assert-O1 ($d.EvidenceReference -cne $c.CalculationPrivateReference) 'Request and calculation proofs conflated.'
    Assert-O1 ((ConvertTo-O1Guid $s.AllowanceClaimId) -ceq $claim -and $s.Version -eq 1 -and
        (ConvertTo-O1Guid $s.ApprovalRequestId) -ceq (ConvertTo-O1Guid $c.ApprovalRequestId) -and
        (ConvertTo-O1Guid $s.PolicyVersionId) -ceq $p.PolicyVersion -and $s.ClaimedAmount -eq 200 -and
        $s.Currency -ceq 'PKR' -and [string]$s.PayableDate -ceq '2026-10-19' -and
        $null -eq $s.PayrollPeriodStart -and $null -eq $s.CorrectionReason -and
        (ConvertTo-O1Guid $s.SubmittedByUserId) -ceq $p.HrAccount -and
        [datetimeoffset]$s.SubmittedAt -eq [datetimeoffset]$c.SubmittedAt -and
        $s.CalculationPrivateReference -ceq $c.CalculationPrivateReference -and
        $s.SnapshotPrivateReference -ceq $c.CalculationPrivateReference -and
        $s.EvidencePrivateReference -ceq $d.EvidenceReference) 'Submission persistence differs.'
    $proofs=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach($pair in @(@{Id=$d.ApprovalRequestId;Subject=$trip;Type='attendance.deployment';Proof=$d.EvidenceReference},
        @{Id=$c.ApprovalRequestId;Subject=$claim;Type='attendance.allowance';Proof=$c.CalculationPrivateReference})){
        $header=@($headers|Where-Object {[guid]$_.Id -eq [guid]$pair.Id})
        $decision=@($decisions|Where-Object {[guid]$_.ApprovalRequestId -eq [guid]$pair.Id})
        Assert-O1 ($header.Count -eq 1 -and $decision.Count -eq 1) 'Approval/decision link differs.'
        $h=$header[0];$v=$decision[0]
        Assert-O1 ((ConvertTo-O1Guid $h.SubjectId) -ceq $pair.Subject -and $h.RequestType -ceq $pair.Type -and
            (ConvertTo-O1Guid $h.RequesterUserId) -ceq $p.HrAccount -and $h.Stage -eq 1 -and
            $h.State -eq 1 -and $null -eq $h.RouteRuleId -and $h.SnapshotPrivateReference -ceq $pair.Proof -and
            [datetimeoffset]$h.RequestedAt -ge $started -and [datetimeoffset]$h.ClosedAt -le $stopped -and
            [datetimeoffset]$h.ClosedAt -ge [datetimeoffset]$h.RequestedAt -and
            [datetimeoffset]$h.ClosedAt -eq [datetimeoffset]$v.DecidedAt -and $v.Decision -eq 1 -and
            (ConvertTo-O1Guid $v.ActorUserId) -ceq $p.AdminAccount -and $v.ActorRoleCode -ceq 'ADMIN' -and
            $v.Reason -ceq $p.Reason -and ![string]::IsNullOrWhiteSpace($v.ProtectedChangeReference) -and
            $v.ProtectedChangeReference.Length -le 500 -and $proofs.Add($v.ProtectedChangeReference)) 'Independent approved decision persistence differs.'
        if($pair.Subject -ceq $claim){Assert-O1 ([datetimeoffset]$h.RequestedAt -eq [datetimeoffset]$c.SubmittedAt) 'Claim submission/header time differs.'}
    }
}
