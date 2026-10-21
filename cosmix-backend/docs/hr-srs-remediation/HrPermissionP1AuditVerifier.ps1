# Pure, separately invoked reconciliation of already-created P1 rows. No IO or execution.
# Supply baseline-diff arrays (Actors are the three existing accounts). See public parameter
# names below; RolePermissions needs joined PermissionCode (or Code). This does not establish row
# provenance, decrypt evidence, or replace the parent's business/baseline reconciliation.
function Assert-P1AuditCondition([bool]$condition,[string]$message){
    if(!$condition){throw "P1 audit evidence: $message"}
}
function ConvertTo-P1AuditGuid($value){
    $id=[guid]::Empty
    Assert-P1AuditCondition ([guid]::TryParse([string]$value,[ref]$id) -and $id -ne [guid]::Empty) 'invalid object identity'
    return $id.ToString('D')
}
function Get-P1AuditTuple($row){
    $actor=if($row.ActorUserId -cin @('anonymous','sms-dispatcher')){[string]$row.ActorUserId}else{ConvertTo-P1AuditGuid $row.ActorUserId}
    $target=if($row.TargetType -ceq 'RolePermission'){
        $parts=([string]$row.TargetId).Split(':')
        Assert-P1AuditCondition ($parts.Count -eq 2) 'invalid permission target'
        (ConvertTo-P1AuditGuid $parts[0])+':'+$parts[1]
    }else{ConvertTo-P1AuditGuid $row.TargetId}
    $employee=if($null -eq $row.EmployeeScopeId -or $row.EmployeeScopeId -is [DBNull]){''}else{ConvertTo-P1AuditGuid $row.EmployeeScopeId}
    return ConvertTo-Json -InputObject @([string]$row.EventType,$actor,[string]$row.ActorRoleCode,
        [string]$row.TargetType,$target,$employee,[string]$row.DeviceReference) -Compress
}
function Get-P1AuditExpected {
    param($Actors,$Roles,$RolePermissions,$Assignments,$Receipts,$Sessions,$Challenges,$Deliveries)
    $sizes=@(@($Actors).Count,@($Roles).Count,@($RolePermissions).Count,@($Assignments).Count,
        @($Receipts).Count,@($Sessions).Count,@($Challenges).Count,@($Deliveries).Count)
    Assert-P1AuditCondition (($sizes -join ',') -ceq '3,2,6,2,6,5,5,5') 'input cardinalities differ'
    $identities=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach($population in @(@{Rows=$Actors},@{Rows=$Roles},@{Rows=$Assignments},@{Rows=$Receipts},
        @{Rows=$Sessions},@{Rows=$Challenges},@{Rows=$Deliveries})){
        foreach($item in $population.Rows){
            Assert-P1AuditCondition ($identities.Add((ConvertTo-P1AuditGuid $item.Id))) 'duplicate scoped identity'
        }
    }
    $accounts=@{}
    foreach($name in @('superadmin','admin','hr')){
        $matches=@($Actors|Where-Object {$_.LoginName -ceq "$name@example.invalid"})
        Assert-P1AuditCondition ($matches.Count -eq 1) 'synthetic actor binding differs'
        $accounts[$name]=@{Id=(ConvertTo-P1AuditGuid $matches[0].Id);Employee=(ConvertTo-P1AuditGuid $matches[0].EmployeeId)}
    }
    Assert-P1AuditCondition ($accounts.admin.Id -ceq '94a83b62-5b56-4450-9175-2a7a9e3261bf' -and
        $accounts.admin.Employee -ceq '7c505086-87ea-4fd2-980e-823ecccbe36a' -and
        $accounts.hr.Id -ceq '8475187f-bf3b-4f9c-a3ed-4bac32253005' -and
        $accounts.hr.Employee -ceq '35d19922-70a8-4c80-8578-e1029360e9fc' -and
        @($accounts.Values.Employee|Select-Object -Unique).Count -eq 3) 'approved account identities differ'
    $roleCodes=@{admin='TEST-HR-P1-20261020-ADMIN';hr='TEST-HR-P1-20261020-REQUESTER'}
    $permissionCodes=@{
        admin=@('attendance.leaves.benefits.review','attendance.vehicle.policy.manage','attendance.deployments.read','attendance.deployments.decide')
        hr=@('attendance.deployments.read','attendance.deployments.request')
    }
    $roleIds=@{};$assignmentIds=@{};$sessionMap=@{}
    foreach($name in @('admin','hr')){
        $role=@($Roles|Where-Object {$_.Code -ceq $roleCodes[$name]})
        Assert-P1AuditCondition ($role.Count -eq 1) 'role code differs'
        $roleIds[$name]=ConvertTo-P1AuditGuid $role[0].Id
        $links=@($RolePermissions|Where-Object {(ConvertTo-P1AuditGuid $_.RoleId) -ceq $roleIds[$name]})
        $linkCodes=@($links|ForEach-Object {if($_.PermissionCode){$_.PermissionCode}else{$_.Code}})
        Assert-P1AuditCondition ($links.Count -eq $permissionCodes[$name].Count -and
            (($linkCodes|Sort-Object -CaseSensitive) -join '|') -ceq
            (($permissionCodes[$name]|Sort-Object -CaseSensitive) -join '|')) 'permission links differ'
        $assignment=@($Assignments|Where-Object {(ConvertTo-P1AuditGuid $_.RoleId) -ceq $roleIds[$name]})
        Assert-P1AuditCondition ($assignment.Count -eq 1 -and
            (ConvertTo-P1AuditGuid $assignment[0].UserAccountId) -ceq $accounts[$name].Id -and
            (ConvertTo-P1AuditGuid $assignment[0].EmployeeId) -ceq $accounts[$name].Employee) 'assignment binding differs'
        $assignmentIds[$name]=ConvertTo-P1AuditGuid $assignment[0].Id
    }
    foreach($name in @('superadmin','admin','hr')){
        $owned=@($Sessions|Where-Object {(ConvertTo-P1AuditGuid $_.UserAccountId) -ceq $accounts[$name].Id}|Sort-Object {[datetimeoffset]$_.CreatedAt})
        $count=if($name -ceq 'superadmin'){1}else{2}
        Assert-P1AuditCondition ($owned.Count -eq $count) 'session ownership/cardinality differs'
        if($count -eq 2){Assert-P1AuditCondition ([datetimeoffset]$owned[0].CreatedAt -lt [datetimeoffset]$owned[1].CreatedAt) 'session order ambiguous'}
        $sessionMap[$name]=$owned
    }
    $expected=[Collections.Generic.List[object]]::new()
    function Add-Expected($event,$actor,$role,$type,$target,$employee,$device,$group,$correlation='',$protected=$false,$at=$null){
        $expected.Add([pscustomobject]@{EventType=$event;ActorUserId=$actor;ActorRoleCode=$role;TargetType=$type;
            TargetId=$target;EmployeeScopeId=$employee;DeviceReference=$device;Group=$group;
            RequiredCorrelation=$correlation;RequiresProtected=$protected;ExactTime=$at})
    }
    $usedProofs=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach($name in @('superadmin','admin','hr')){
        for($i=0;$i -lt $sessionMap[$name].Count;$i++){
            $session=$sessionMap[$name][$i];$sid=ConvertTo-P1AuditGuid $session.Id
            $cid=ConvertTo-P1AuditGuid $session.SmsOtpChallengeId
            Assert-P1AuditCondition ($usedProofs.Add($cid)) 'reused challenge'
            $challenge=@($Challenges|Where-Object {(ConvertTo-P1AuditGuid $_.Id) -ceq $cid})
            Assert-P1AuditCondition ($challenge.Count -eq 1 -and $challenge[0].Purpose -ceq 'WebLogin' -and
                (ConvertTo-P1AuditGuid $challenge[0].UserAccountId) -ceq $accounts[$name].Id) 'challenge binding differs'
            $delivery=@($Deliveries|Where-Object {(ConvertTo-P1AuditGuid $_.ChallengeId) -ceq $cid})
            Assert-P1AuditCondition ($delivery.Count -eq 1 -and $delivery[0].Status -eq 1 -and $delivery[0].Attempts -eq 1) 'delivery binding differs'
            $did=ConvertTo-P1AuditGuid $delivery[0].Id
            Assert-P1AuditCondition (![string]::IsNullOrWhiteSpace([string]$session.CorrelationId)) 'missing session issuance correlation'
            Add-Expected 'web.otp.requested' 'anonymous' 'authentication' 'otp-challenge' $cid $null $null "otp:$cid"
            Add-Expected 'otp.delivery.accepted' 'sms-dispatcher' 'system' 'otp-delivery' $did $null $null "delivery:$did" ([guid]$did).ToString('N')
            Add-Expected 'web.session.created' $accounts[$name].Id 'authentication' 'session' $sid $null $null "created:$sid" $session.CorrelationId $false $session.CreatedAt
            $effectiveRoles=@(if($name -ceq 'superadmin'){'SUPERADMIN'}elseif($name -ceq 'admin'){'ADMIN'}else{'HR';'ATTHUMA13B03';'ATTHUMA13B06'})
            if($i -eq 1){$effectiveRoles+=,$roleCodes[$name]}
            foreach($code in $effectiveRoles){
                Add-Expected 'access.current_permissions.read' $accounts[$name].Id $code 'UserAccount' $accounts[$name].Id $accounts[$name].Employee "session:$sid" "permissions:$sid"
                if($i -eq $sessionMap[$name].Count-1){
                    Add-Expected 'session.logout' $accounts[$name].Id $code 'session' $sid $accounts[$name].Employee "session:$sid;device:" "logout:$sid"
                }
            }
        }
    }
    $sa=$accounts.superadmin.Id;$saSession=ConvertTo-P1AuditGuid $sessionMap.superadmin[0].Id
    $receiptKeys=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach($name in @('admin','hr')){
        $suffix=if($name -ceq 'admin'){'admin'}else{'requester'}
        Add-Expected 'access.account.read' $sa 'SUPERADMIN' 'UserAccount' $accounts[$name].Id $accounts[$name].Employee $saSession "account:$name"
        Add-Expected 'access.role.created' $sa 'SUPERADMIN' 'Role' $roleIds[$name] $null $saSession "role:$name"
        foreach($code in $permissionCodes[$name]){
            Add-Expected 'access.role.permission.granted' $sa 'SUPERADMIN' 'RolePermission' ($roleIds[$name]+':'+$code) $null $saSession "grant:$name" '' $true
        }
        Add-Expected 'access.assignment.created' $sa 'SUPERADMIN' 'UserRoleAssignment' $assignmentIds[$name] $accounts[$name].Employee $saSession "assign:$name" '' $true
        foreach($step in @('create','grant','assign')){
            $key="P1-$step-$suffix"
            $operation=switch($step){create{'access.role.created'}grant{'access.role.permissions.granted'}assign{'access.assignment.created'}}
            $receipt=@($Receipts|Where-Object {$_.Key -ceq $key})
            Assert-P1AuditCondition ($receipt.Count -eq 1 -and $receipt[0].Operation -ceq $operation -and
                (ConvertTo-P1AuditGuid $receipt[0].ActorUserId) -ceq $sa -and $receiptKeys.Add($key)) 'receipt binding differs'
            $employee=if($step -ceq 'assign'){$accounts[$name].Employee}else{$null}
            Add-Expected ($operation+'.replay') $sa 'SUPERADMIN' 'IdempotencyReceipt' (ConvertTo-P1AuditGuid $receipt[0].Id) $employee $saSession "replay:$key"
        }
    }
    return ,$expected.ToArray()
}
function Assert-P1AuditEvidence {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Audits,[Parameter(Mandatory)]$Actors,[Parameter(Mandatory)]$Roles,
        [Parameter(Mandatory)]$RolePermissions,[Parameter(Mandatory)]$Assignments,[Parameter(Mandatory)]$Receipts,
        [Parameter(Mandatory)]$Sessions,[Parameter(Mandatory)]$Challenges,[Parameter(Mandatory)]$Deliveries,
        [Parameter(Mandatory)][datetimeoffset]$StartedAt,[Parameter(Mandatory)][datetimeoffset]$StoppedAt
    )
    Assert-P1AuditCondition (@($Audits).Count -eq 51 -and $StartedAt.Offset -eq [timespan]::Zero -and
        $StoppedAt.Offset -eq [timespan]::Zero -and $StoppedAt -gt $StartedAt -and
        $StoppedAt-$StartedAt -le [timespan]::FromHours(24)) 'audit count/window differs'
    $expected=Get-P1AuditExpected $Actors $Roles $RolePermissions $Assignments $Receipts $Sessions $Challenges $Deliveries
    Assert-P1AuditCondition ($expected.Count -eq 51) 'internal manifest cardinality differs'
    $remaining=[Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
    foreach($entry in $expected){$remaining.Add((Get-P1AuditTuple $entry),$entry)}
    $ids=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $correlations=@{};$proofs=@{};$counts=@{}
    foreach($row in $Audits){
        Assert-P1AuditCondition ($ids.Add((ConvertTo-P1AuditGuid $row.Id))) 'duplicate audit identity'
        $key=Get-P1AuditTuple $row
        Assert-P1AuditCondition ($remaining.ContainsKey($key)) 'unexpected or duplicate event/actor/role/target/session'
        $entry=$remaining[$key];$null=$remaining.Remove($key)
        $at=[datetimeoffset]$row.OccurredAt
        Assert-P1AuditCondition ($at -ge $StartedAt -and $at -le $StoppedAt -and
            ($null -eq $entry.ExactTime -or $at -eq [datetimeoffset]$entry.ExactTime)) 'event time differs'
        $correlation=[string]$row.CorrelationId
        Assert-P1AuditCondition ($correlation -cmatch '\A[A-Za-z0-9:_-]{1,100}\z' -and
            (!$entry.RequiredCorrelation -or $correlation -ceq $entry.RequiredCorrelation)) 'correlation binding differs'
        if($correlations.ContainsKey($entry.Group)){
            Assert-P1AuditCondition ($correlations[$entry.Group] -ceq $correlation) 'one request has split correlations'
        }else{
            Assert-P1AuditCondition (@($correlations.Values|Where-Object {$_ -ceq $correlation}).Count -eq 0) 'different requests reuse correlation'
            $correlations[$entry.Group]=$correlation
        }
        $reference=[string]$row.ProtectedChangesReference
        if($entry.RequiresProtected){
            Assert-P1AuditCondition (![string]::IsNullOrWhiteSpace($reference) -and $reference.Length -le 500) 'missing bounded protected reference'
            if($proofs.ContainsKey($entry.Group)){Assert-P1AuditCondition ($proofs[$entry.Group] -ceq $reference) 'split protected evidence within grant'}
            else{$proofs[$entry.Group]=$reference}
        }else{Assert-P1AuditCondition ([string]::IsNullOrEmpty($reference)) 'unexpected protected reference'}
        if(!$counts.ContainsKey($row.EventType)){$counts[$row.EventType]=0}
        $counts[$row.EventType]++
    }
    Assert-P1AuditCondition ($remaining.Count -eq 0 -and $correlations.Count -eq 37) 'missing expected audit/request groups'
    return [pscustomobject]@{Passed=$true;AuditCount=51;HttpCorrelationGroups=32;DeliveryCorrelations=5;EventCounts=$counts}
}
