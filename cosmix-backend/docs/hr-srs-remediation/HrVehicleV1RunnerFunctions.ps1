# Definitions only. Dot-sourced by the separate V1 runner, never by a C01-C04 runner.
function Initialize-V1Manifest([string]$path) {
    $script:adminAccount='94a83b62-5b56-4450-9175-2a7a9e3261bf'
    $script:adminEmployee='7c505086-87ea-4fd2-980e-823ecccbe36a'
    $script:hrAccount='8475187f-bf3b-4f9c-a3ed-4bac32253005'
    $script:hrEmployee='35d19922-70a8-4c80-8578-e1029360e9fc'
    $script:issueReference='TEST ONLY V1 issue condition; synthetic reference, no attachment'
    $script:returnReference='TEST ONLY V1 return condition; synthetic reference, no attachment'
    $manifestText=Get-Content -LiteralPath $path -Raw
    $document=[Text.Json.JsonDocument]::Parse($manifestText,[Text.Json.JsonDocumentOptions]::new())
    try {
        Assert ($document.RootElement.ValueKind -eq [Text.Json.JsonValueKind]::Object) 'Manifest must be an object.'
        $names=@($document.RootElement.EnumerateObject() | ForEach-Object Name)
        Assert ($names.Count -eq 8 -and @($names | Sort-Object -Unique).Count -eq 8) 'Duplicate or extra manifest fields.'
    } finally {$document.Dispose()}
    $script:fixture=$manifestText | ConvertFrom-Json -AsHashtable -DateKind String
    Assert ($fixture -is [Collections.IDictionary] -and $fixture.Count -eq 8) 'Exact V1 manifest required.'
    $fixed=@{profile='HumaHrVehicle20261020V1';bindingSource='parent-read-only-inspection-20261020';adminAccountId=$adminAccount;adminEmployeeId=$adminEmployee;hrAccountId=$hrAccount;hrEmployeeId=$hrEmployee}
    foreach($name in $fixed.Keys) {Assert ($fixture[$name] -ceq $fixed[$name]) 'Manifest actor/profile binding differs.'}
    foreach($name in @('issuedAt','returnedAt')) {
        Assert ($fixture[$name] -is [string] -and $fixture[$name] -cmatch '^2026-10-20T\d{2}:\d{2}:\d{2}Z$') 'Explicit reviewed V1 UTC instants required.'
    }
    $script:issuedAt=$fixture.issuedAt
    $script:returnedAt=$fixture.returnedAt
    $issued=[DateTimeOffset]::ParseExact($issuedAt,"yyyy-MM-dd'T'HH:mm:ss'Z'",[Globalization.CultureInfo]::InvariantCulture,[Globalization.DateTimeStyles]::AssumeUniversal)
    $returned=[DateTimeOffset]::ParseExact($returnedAt,"yyyy-MM-dd'T'HH:mm:ss'Z'",[Globalization.CultureInfo]::InvariantCulture,[Globalization.DateTimeStyles]::AssumeUniversal)
    Assert ($issued -lt $returned -and $returned -le [DateTimeOffset]::UtcNow) 'Return must follow issue and cannot be future.'
}

function Exact-Fields($body,[string[]]$names) {
    if($body -isnot [Collections.IDictionary] -or $body.Count -ne $names.Count) {return $false}
    foreach($name in $names) {if(@($body.Keys | Where-Object {$_ -ceq $name}).Count -ne 1){return $false}}
    return $true
}
function Test-V1Request([string]$method,[string]$path,$body,[string]$key) {
    $v='/api/v1/hr/vehicles'
    if($method -ceq 'GET') {
        return $path -ceq $v -or $path -ceq '/api/v1/me/vehicles/custody' -or
            ($script:vehicleId -eq [guid]::Empty.ToString() -and $path -ceq "$v/custody/history?employeeId=$hrEmployee") -or
            ($script:vehicleId -ne [guid]::Empty.ToString() -and $path -ceq "$v/custody/history?vehicleId=$script:vehicleId&employeeId=$hrEmployee")
    }
    if($method -cne 'POST'){return $false}
    if($path -ceq $v) {
        return (Exact-Fields $body @('registrationNumber','vehicleType','makeModel','assetReference')) -and
            $key -ceq "$marker-create" -and $body.registrationNumber -ceq $marker -and $body.assetReference -ceq $marker -and
            $body.vehicleType -ceq 'Motorcycle' -and ($body.makeModel -ceq 'TEST ONLY custody acceptance' -or
                ($script:vehicleId -ne [guid]::Empty.ToString() -and $body.makeModel -ceq 'TEST ONLY custody changed payload'))
    }
    if($path -ceq "$v/custody/issue") {
        return $script:vehicleId -ne [guid]::Empty.ToString() -and
            (Exact-Fields $body @('vehicleId','employeeId','issuedAt','issueOdometerKm','issueConditionReference')) -and
            ($key -ceq "$marker-issue" -or ($script:custodyId -ne [guid]::Empty.ToString() -and $key -ceq "$marker-issue-conflict")) -and
            $body.vehicleId -ceq $script:vehicleId -and $body.employeeId -ceq $hrEmployee -and
            $body.issuedAt -ceq $issuedAt -and $body.issueOdometerKm -eq 1000 -and $body.issueConditionReference -ceq $issueReference
    }
    return $script:custodyId -ne [guid]::Empty.ToString() -and $path -ceq "$v/custody/$script:custodyId/return" -and
        (Exact-Fields $body @('returnedAt','returnOdometerKm','returnConditionReference')) -and
        $key -cin @("$marker-return","$marker-return-conflict") -and $body.returnedAt -ceq $returnedAt -and
        $body.returnOdometerKm -eq 1000 -and $body.returnConditionReference -ceq $returnReference
}

function Assert-V1Bindings {
    $query=@"
SELECT COUNT(*) FROM auth.UserAccounts a JOIN hr.Employees e ON e.Id=a.EmployeeId
WHERE a.Status=3 AND a.WebAccessEnabled=1 AND (a.LockedUntil IS NULL OR a.LockedUntil<=SYSUTCDATETIME())
AND ((a.Id='$adminAccount' AND a.EmployeeId='$adminEmployee' AND a.LoginName='admin@example.invalid')
OR (a.Id='$hrAccount' AND a.EmployeeId='$hrEmployee' AND a.LoginName='hr@example.invalid'));
"@
    Assert ((Read-Sql $query) -eq '2') 'Reviewed account/employee bindings changed.'
    foreach($account in @($adminAccount,$hrAccount)) {
        $query=@"
SELECT COUNT(DISTINCT p.Code) FROM auth.UserAccounts a
JOIN auth.UserRoleAssignments u ON u.UserAccountId=a.Id JOIN auth.Roles r ON r.Id=u.RoleId
JOIN auth.RolePermissions rp ON rp.RoleId=r.Id JOIN auth.Permissions p ON p.Id=rp.PermissionId
JOIN auth.UserAccessScopes s ON s.UserRoleAssignmentId=u.Id
WHERE a.Id='$account' AND r.IsActive=1 AND s.Kind=7
AND p.Code IN('attendance.vehicle.manage','attendance.vehicle.custody.manage','attendance.vehicle.self')
AND u.EffectiveFrom<=SYSUTCDATETIME() AND (u.EffectiveTo IS NULL OR SYSUTCDATETIME()<u.EffectiveTo)
AND (u.RevokedAt IS NULL OR SYSUTCDATETIME()<u.RevokedAt) AND rp.RevokedAt IS NULL AND rp.GrantedAt<=SYSUTCDATETIME();
"@
        Assert ((Read-Sql $query) -eq '3') 'Required existing V1 grants differ; no provisioning.'
    }
}

function Assert-Vehicle($value) {
    Assert ($value.id -eq $script:vehicleId -and $value.registrationNumber -ceq $marker -and
        $value.vehicleType -ceq 'Motorcycle' -and $value.makeModel -ceq 'TEST ONLY custody acceptance' -and
        $value.assetReference -ceq $marker -and $value.isActive -eq $true) 'Vehicle response differs.'
}
function Assert-Custody($value,[bool]$returned) {
    Assert ($value.id -eq $script:custodyId -and $value.vehicleId -eq $script:vehicleId -and
        $value.employeeId -eq $hrEmployee -and $value.registrationNumber -ceq $marker -and
        [DateTimeOffset]$value.issuedAt -eq [DateTimeOffset]$issuedAt -and $value.issueOdometerKm -eq 1000 -and
        $value.issueConditionReference -ceq $issueReference -and $value.isActive -eq (!$returned)) 'Custody response differs.'
    if($returned) {
        Assert ([DateTimeOffset]$value.returnedAt -eq [DateTimeOffset]$returnedAt -and
            $value.returnOdometerKm -eq 1000 -and $value.returnConditionReference -ceq $returnReference) 'Return differs.'
    } else {Assert ($null -eq $value.returnedAt -and $null -eq $value.returnOdometerKm -and $null -eq $value.returnConditionReference) 'Premature return.'}
}
function Require-Id($value) {
    $parsed=[guid]::Empty
    Assert ([guid]::TryParseExact([string]$value,'D',[ref]$parsed) -and $parsed -ne [guid]::Empty) 'Response resource ID invalid.'
    return $parsed.ToString('D')
}
function Invoke-V1BusinessSequence($admin,$hr) {
    $v='/api/v1/hr/vehicles'
    Assert-V1Bindings
    $r=Request $admin.Client GET $v
    Check 'Pre-read has no marker vehicle' ($r.Status -eq 200 -and @($r.Value | Where-Object registrationNumber -CEQ $marker).Count -eq 0)
    $r=Request $admin.Client GET "$v/custody/history?employeeId=$hrEmployee"
    Check 'Beneficiary has no custody history' ($r.Status -eq 200 -and @($r.Value).Count -eq 0)
    $body=@{registrationNumber=$marker;vehicleType='Motorcycle';makeModel='TEST ONLY custody acceptance';assetReference=$marker}
    Check 'Missing CSRF create denied' ((Request $admin.Client POST $v $body '' "$marker-create").Status -eq 400)
    $one=Begin-Request (New-Client $admin.Cookies) POST $v $body $admin.Csrf "$marker-create"
    $two=Begin-Request (New-Client $admin.Cookies) POST $v $body $admin.Csrf "$marker-create"
    [Threading.Tasks.Task]::WhenAll([Threading.Tasks.Task[]]@($one.Task,$two.Task)).GetAwaiter().GetResult() | Out-Null
    $a=Finish-Request $one; $b=Finish-Request $two
    Check 'Concurrent same-key create returns one vehicle' ($a.Status -eq 200 -and $b.Status -eq 200 -and $a.Value.id -eq $b.Value.id)
    $script:vehicleId=Require-Id $a.Value.id
    Assert-Vehicle $a.Value; Assert-Vehicle $b.Value
    $changed=$body.Clone();$changed.makeModel='TEST ONLY custody changed payload'
    $r=Request $admin.Client POST $v $changed $admin.Csrf "$marker-create"
    Check 'Changed payload same key conflicts' ($r.Status -eq 409 -and $r.Value.title -ceq 'idempotency_key_reused')
    $history="$v/custody/history?vehicleId=$script:vehicleId&employeeId=$hrEmployee"
    $r=Request $admin.Client GET $history
    Check 'Immediate pre-issue history empty' ($r.Status -eq 200 -and @($r.Value).Count -eq 0)
    Assert-V1Bindings
    Assert ((Read-Sql "SELECT COUNT(*) FROM hr.VehicleCustodies WHERE VehicleId='$script:vehicleId' OR EmployeeId='$hrEmployee';") -eq '0') 'Custody appeared before issue; stop.'
    $issue=@{vehicleId=$script:vehicleId;employeeId=$hrEmployee;issuedAt=$issuedAt;issueOdometerKm=1000;issueConditionReference=$issueReference}
    $r=Request $admin.Client POST "$v/custody/issue" $issue $admin.Csrf "$marker-issue"
    Check 'One custody issued' ($r.Status -eq 200)
    $script:custodyId=Require-Id $r.Value.id; Assert-Custody $r.Value $false
    $r=Request $admin.Client POST "$v/custody/issue" $issue $admin.Csrf "$marker-issue"
    Check 'Issue exact retry succeeds' ($r.Status -eq 200); Assert-Custody $r.Value $false
    $r=Request $admin.Client POST "$v/custody/issue" $issue $admin.Csrf "$marker-issue-conflict"
    Check 'New-key competing issue conflicts' ($r.Status -eq 409 -and $r.Value.title -ceq 'vehicle_already_in_custody')
    $r=Request $hr.Client GET '/api/v1/me/vehicles/custody'
    Check 'HR own history has sole custody' ($r.Status -eq 200 -and @($r.Value).Count -eq 1); Assert-Custody $r.Value[0] $false
    $r=Request $admin.Client GET '/api/v1/me/vehicles/custody'
    Check 'Admin self read excludes HR custody' ($r.Status -eq 200 -and @($r.Value | Where-Object employeeId -EQ $hrEmployee).Count -eq 0)
    $return=@{returnedAt=$returnedAt;returnOdometerKm=1000;returnConditionReference=$returnReference}
    $path="$v/custody/$script:custodyId/return"
    $r=Request $admin.Client POST $path $return $admin.Csrf "$marker-return"
    Check 'Custody returned once' ($r.Status -eq 200); Assert-Custody $r.Value $true
    $r=Request $admin.Client POST $path $return $admin.Csrf "$marker-return"
    Check 'Return exact retry succeeds' ($r.Status -eq 200); Assert-Custody $r.Value $true
    $r=Request $admin.Client POST $path $return $admin.Csrf "$marker-return-conflict"
    Check 'New-key repeat return conflicts' ($r.Status -eq 409 -and $r.Value.title -ceq 'custody_already_returned')
    foreach($read in @(@{Client=$admin.Client;Path=$history},@{Client=$hr.Client;Path='/api/v1/me/vehicles/custody'})) {
        $r=Request $read.Client GET $read.Path
        Check 'Final history retains one returned custody' ($r.Status -eq 200 -and @($r.Value).Count -eq 1)
        Assert-Custody $r.Value[0] $true
    }
    $r=Request $admin.Client GET $v
    $owned=@($r.Value | Where-Object id -EQ $script:vehicleId)
    Check 'Final vehicle remains active' ($r.Status -eq 200 -and $owned.Count -eq 1); Assert-Vehicle $owned[0]
    Assert ($script:businessRequests -eq 18) 'Exact V1 business request count differs.'
}

function Read-V1Protected([string]$serialized,[string]$purpose) {
    $reference=ConvertFrom-Json -InputObject $serialized -AsHashtable
    Assert ((Exact-Fields $reference @('StorageKey','ObjectVersion','Sha256','KeyReference','SizeBytes')) -and
        $reference.StorageKey -cmatch '^[a-f0-9]{32}\.enc$' -and $reference.ObjectVersion -cmatch '^[a-f0-9]{32}$' -and
        $reference.Sha256 -cmatch '^[A-F0-9]{64}$' -and $reference.KeyReference -ceq 'hr-business-v1' -and
        $reference.SizeBytes -gt 0 -and $reference.SizeBytes -le 65536) 'Protected reference differs.'
    $path=Join-Path (Join-Path $root 'files') $reference.StorageKey
    Assert-PrivateSource $path; Assert-PrivateSource (Join-Path $root 'keys\files.key')
    $envelope=[IO.File]::ReadAllBytes($path)
    Assert ($envelope.Length -eq $reference.SizeBytes+32 -and [Text.Encoding]::ASCII.GetString($envelope,0,4) -ceq 'CXF1') 'Protected envelope differs.'
    $key=[IO.File]::ReadAllBytes((Join-Path $root 'keys\files.key'))
    $plain=[byte[]]::new([int]$reference.SizeBytes)
    try {
        Assert ($key.Length -eq 32) 'Unexpected private key length.'
        $aad=[Text.Json.JsonSerializer]::SerializeToUtf8Bytes([string[]]@('cosmix-private-file/v1',$purpose,$reference.StorageKey,$reference.ObjectVersion,$reference.Sha256,$reference.KeyReference,[string]$reference.SizeBytes),[Text.Json.JsonSerializerOptions]::Default)
        $cipher=[Security.Cryptography.AesGcm]::new($key,16)
        try {$cipher.Decrypt([byte[]]$envelope[4..15],[byte[]]$envelope[32..($envelope.Length-1)],[byte[]]$envelope[16..31],$plain,$aad)}
        finally {$cipher.Dispose()}
        Assert ([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($plain)) -ceq $reference.Sha256) 'Protected content hash differs.'
        return [Text.Encoding]::UTF8.GetString($plain)
    } finally {[Array]::Clear($key,0,$key.Length);[Array]::Clear($plain,0,$plain.Length)}
}

function Assert-BusinessState {
    Assert ($script:vehicleId -ne [guid]::Empty.ToString() -and $script:custodyId -ne [guid]::Empty.ToString()) 'Owned IDs unavailable.'
    Assert ((Read-Sql "SELECT COUNT(*) FROM hr.Vehicles WHERE Id='$script:vehicleId' AND RegistrationNumber='$marker' AND VehicleType='Motorcycle' AND MakeModel='TEST ONLY custody acceptance' AND AssetReference='$marker' AND IsActive=1;") -eq '1') 'Vehicle SQL state differs.'
    Assert ((Read-Sql "SELECT COUNT(*) FROM hr.Vehicles WHERE RegistrationNumber='$marker';") -eq '1') 'Vehicle SQL count differs.'
    Assert ((Read-Sql "SELECT COUNT(*) FROM hr.VehicleCustodies WHERE VehicleId='$script:vehicleId' OR EmployeeId='$hrEmployee';") -eq '1') 'Unexpected custody SQL count.'
    Assert ((Read-Sql "SELECT COUNT(*) FROM hr.VehicleCustodies WHERE Id='$script:custodyId' AND VehicleId='$script:vehicleId' AND EmployeeId='$hrEmployee' AND IssuedAt=CONVERT(datetimeoffset,'$issuedAt') AND ReturnedAt=CONVERT(datetimeoffset,'$returnedAt') AND IssueOdometerKm=1000 AND ReturnOdometerKm=1000 AND IssueConditionPrivateReference='$issueReference' AND ReturnConditionPrivateReference='$returnReference' AND RecordedByUserId='$adminAccount' AND ReturnedToUserId='$adminAccount';") -eq '1') 'Custody SQL values or actors differ.'
    Assert ((Read-Sql "SELECT COUNT(*) FROM workflow.IdempotencyReceipts WHERE [Key] LIKE '$marker%';") -eq '3') 'Failed probes or retries committed extra receipts.'
    $query=@"
SELECT (SELECT r.Operation,r.[Key],r.RequestSha256,r.ProtectedResultReference,a.ProtectedChangesReference,a.TargetId,a.EmployeeScopeId
FROM workflow.IdempotencyReceipts r JOIN audit.AuditEvents a ON a.EventType=r.Operation AND a.ActorUserId=r.ActorUserId
WHERE r.ActorUserId='$adminAccount' AND r.[Key] IN('$marker-create','$marker-issue','$marker-return')
AND r.CompletedAt IS NOT NULL AND r.ResultCode='attendance-operation-v1'
AND a.TargetType='Attendance' AND a.TargetId IN('$script:vehicleId','$script:custodyId')
AND a.CorrelationId LIKE 'HR-V1-%' AND a.DeviceReference='$($admin.Id)'
ORDER BY r.Operation FOR JSON PATH,INCLUDE_NULL_VALUES);
"@
    $rows=@((Read-Sql $query) | ConvertFrom-Json -AsHashtable)
    Assert ($rows.Count -eq 3) 'Each business receipt must have one operation-linked audit.'
    $references=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach($row in $rows) {
        $suffix=switch -CaseSensitive ($row.Operation) {'vehicle.create' {'create'} 'vehicle.custody.issue' {'issue'} 'vehicle.custody.return' {'return'} default {throw 'Unapproved operation.'}}
        $id=if($suffix -ceq 'create'){$script:vehicleId}else{$script:custodyId}
        Assert ($row.Key -ceq "$marker-$suffix" -and $row.TargetId -eq $id) 'Operation/receipt resource binding differs.'
        Assert (($suffix -ceq 'issue' -and $row.EmployeeScopeId -eq $hrEmployee) -or ($suffix -cne 'issue' -and $null -eq $row.EmployeeScopeId)) 'Audit subject differs.'
        $actorN=([guid]$adminAccount).ToString('N')
        $resultText=Read-V1Protected $row.ProtectedResultReference "attendance:operation:${actorN}:$($row.Operation):result"
        $auditText=Read-V1Protected $row.ProtectedChangesReference "attendance:operation:${actorN}:$($row.Operation):audit"
        $result=$resultText|ConvertFrom-Json -AsHashtable -DateKind String; $audit=$auditText|ConvertFrom-Json -AsHashtable -DateKind String
        Assert ($result.ResourceId -eq $id -and $audit.ResourceId -eq $id -and $audit.operation -ceq $row.Operation -and $null -eq $audit.Before) 'Protected operation/resource differs.'
        Assert ($result.RequiresAll -eq $true -and @($result.Resources).Count -eq 0 -and $null -eq $result.ResourceScopes) 'Receipt scope differs.'
        $doc=[Text.Json.JsonDocument]::Parse($auditText,[Text.Json.JsonDocumentOptions]::new())
        try {
            $requestText=$doc.RootElement.GetProperty('request').GetRawText()
            $hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($requestText)))
            Assert ($hash -ceq $row.RequestSha256) 'Independent request digest differs from receipt.'
        } finally {$doc.Dispose()}
        Assert (($audit.After|ConvertTo-Json -Depth 20 -Compress) -ceq ($result.Result|ConvertTo-Json -Depth 20 -Compress)) 'Audit and protected replay result differ.'
        if($suffix -ceq 'create') {Assert-Vehicle $result.Result} else {Assert-Custody $result.Result ($suffix -ceq 'return')}
        foreach($serialized in @($row.ProtectedResultReference,$row.ProtectedChangesReference)) {
            $ref=$serialized | ConvertFrom-Json -AsHashtable
            Assert ($references.Add($ref.StorageKey)) 'Protected object unexpectedly shared.'
        }
    }
    $objects=@(Get-ChildItem -LiteralPath (Join-Path $root 'files') -Force)
    Assert ($objects.Count -eq 6 -and @($objects | Where-Object {$_.PSIsContainer -or !$references.Contains($_.Name)}).Count -eq 0) 'Expected exactly six protected business objects.'
}
