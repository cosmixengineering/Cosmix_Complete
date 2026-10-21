# Pure assertions over caller-supplied, already-decrypted evidence. No SQL, HTTP or filesystem IO.
# The caller retains responsibility for target identity and protected-reference/AES-GCM verification.
function Assert-V1EvidenceCondition([bool]$Condition,[string]$Code) {
    if (!$Condition) {throw [InvalidOperationException]::new("V1 evidence assertion: $Code")}
}

function Assert-V1EvidenceFields($Value,[string[]]$Names) {
    Assert-V1EvidenceCondition ($Value -is [Collections.IDictionary]) 'object-required'
    $keys=@($Value.Keys)
    Assert-V1EvidenceCondition ($keys.Count -eq $Names.Count) 'field-count'
    foreach($name in $Names) {
        Assert-V1EvidenceCondition (@($keys | Where-Object {$_ -ceq $name}).Count -eq 1) 'exact-field-case'
    }
}

function Assert-V1EvidenceJsonTree([Text.Json.JsonElement]$Element) {
    if($Element.ValueKind -eq [Text.Json.JsonValueKind]::Object) {
        $names=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach($property in $Element.EnumerateObject()) {
            Assert-V1EvidenceCondition ($names.Add($property.Name)) 'duplicate-or-case-alias'
            Assert-V1EvidenceJsonTree $property.Value
        }
    } elseif($Element.ValueKind -eq [Text.Json.JsonValueKind]::Array) {
        foreach($item in $Element.EnumerateArray()) {Assert-V1EvidenceJsonTree $item}
    }
}

function Assert-V1EvidenceGuid($Actual,[guid]$Expected) {
    $parsed=[guid]::Empty
    Assert-V1EvidenceCondition ($Actual -is [string] -and
        [guid]::TryParseExact($Actual,'D',[ref]$parsed) -and $parsed -eq $Expected -and $parsed -ne [guid]::Empty) 'identity'
}

function Assert-V1EvidenceInstant($Actual,[DateTimeOffset]$Expected) {
    $parsed=[DateTimeOffset]::MinValue
    Assert-V1EvidenceCondition ($Actual -is [string] -and
        [DateTimeOffset]::TryParse($Actual,[Globalization.CultureInfo]::InvariantCulture,
            [Globalization.DateTimeStyles]::None,[ref]$parsed) -and
        $parsed.Offset -eq [TimeSpan]::Zero -and $parsed -eq $Expected) 'utc-instant'
}

function Assert-V1PersistedVehicle($Value,[guid]$VehicleId) {
    Assert-V1EvidenceFields $Value @('Id','RegistrationNumber','VehicleType','MakeModel','AssetReference','IsActive')
    Assert-V1EvidenceGuid $Value['Id'] $VehicleId
    Assert-V1EvidenceCondition ($Value['RegistrationNumber'] -ceq 'TEST-HR-V1-20261020' -and
        $Value['VehicleType'] -ceq 'Motorcycle' -and $Value['MakeModel'] -ceq 'TEST ONLY custody acceptance' -and
        $Value['AssetReference'] -ceq 'TEST-HR-V1-20261020' -and
        $Value['IsActive'] -is [bool] -and $Value['IsActive']) 'vehicle-values'
}

function Assert-V1PersistedCustody($Value,[guid]$VehicleId,[guid]$CustodyId,
    [DateTimeOffset]$IssuedAt,[DateTimeOffset]$ReturnedAt,[bool]$Returned) {
    Assert-V1EvidenceFields $Value @('Id','VehicleId','RegistrationNumber','EmployeeId','EmployeeName',
        'IssuedAt','IssueOdometerKm','IssueConditionReference','ReturnedAt','ReturnOdometerKm','ReturnConditionReference','IsActive')
    Assert-V1EvidenceGuid $Value['Id'] $CustodyId
    Assert-V1EvidenceGuid $Value['VehicleId'] $VehicleId
    Assert-V1EvidenceGuid $Value['EmployeeId'] ([guid]'35d19922-70a8-4c80-8578-e1029360e9fc')
    Assert-V1EvidenceInstant $Value['IssuedAt'] $IssuedAt
    Assert-V1EvidenceCondition ($Value['RegistrationNumber'] -ceq 'TEST-HR-V1-20261020' -and
        $Value['EmployeeName'] -is [string] -and $Value['IssueOdometerKm'] -is [ValueType] -and
        $Value['IssueOdometerKm'] -eq 1000 -and
        $Value['IssueConditionReference'] -ceq 'TEST ONLY V1 issue condition; synthetic reference, no attachment' -and
        $Value['IsActive'] -is [bool] -and $Value['IsActive'] -eq (!$Returned)) 'custody-values'
    if($Returned) {
        Assert-V1EvidenceInstant $Value['ReturnedAt'] $ReturnedAt
        Assert-V1EvidenceCondition ($Value['ReturnOdometerKm'] -is [ValueType] -and $Value['ReturnOdometerKm'] -eq 1000 -and
            $Value['ReturnConditionReference'] -ceq 'TEST ONLY V1 return condition; synthetic reference, no attachment') 'return-values'
    } else {
        Assert-V1EvidenceCondition ($null -eq $Value['ReturnedAt'] -and $null -eq $Value['ReturnOdometerKm'] -and
            $null -eq $Value['ReturnConditionReference']) 'issue-has-return'
    }
}

function Assert-V1EvidenceRecord {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$ResultJson,
        [Parameter(Mandatory)][string]$AuditJson,
        [Parameter(Mandatory)][Collections.IDictionary]$Receipt,
        [Parameter(Mandatory)][Collections.IDictionary]$AuditEvent,
        [Parameter(Mandatory)][guid]$VehicleId,
        [Parameter(Mandatory)][guid]$CustodyId,
        [Parameter(Mandatory)][guid]$AdminSessionId,
        [Parameter(Mandatory)][DateTimeOffset]$IssuedAt,
        [Parameter(Mandatory)][DateTimeOffset]$ReturnedAt
    )
    Assert-V1EvidenceCondition ($VehicleId -ne [guid]::Empty -and $CustodyId -ne [guid]::Empty -and
        $VehicleId -ne $CustodyId -and $AdminSessionId -ne [guid]::Empty -and
        $IssuedAt -lt $ReturnedAt -and $IssuedAt.Offset -eq [TimeSpan]::Zero -and $ReturnedAt.Offset -eq [TimeSpan]::Zero) 'expected-bindings'
    Assert-V1EvidenceFields $Receipt @('Operation','Key','ActorUserId','ResultCode','CompletedAt','RequestSha256')
    Assert-V1EvidenceFields $AuditEvent @('EventType','ActorUserId','TargetType','TargetId','EmployeeScopeId','DeviceReference','CorrelationId')
    $operation=$Receipt['Operation']
    $suffix=switch -CaseSensitive ($operation) {
        'vehicle.create' {'create'}
        'vehicle.custody.issue' {'issue'}
        'vehicle.custody.return' {'return'}
        default {throw [InvalidOperationException]::new('V1 evidence assertion: operation')}
    }
    $resourceId=if($suffix -ceq 'create'){$VehicleId}else{$CustodyId}
    $admin=[guid]'94a83b62-5b56-4450-9175-2a7a9e3261bf'
    Assert-V1EvidenceGuid $Receipt['ActorUserId'] $admin
    Assert-V1EvidenceGuid $AuditEvent['ActorUserId'] $admin
    Assert-V1EvidenceGuid $AuditEvent['DeviceReference'] $AdminSessionId
    Assert-V1EvidenceGuid $AuditEvent['TargetId'] $resourceId
    Assert-V1EvidenceCondition ($Receipt['Key'] -ceq "TEST-HR-V1-20261020-$suffix" -and
        $Receipt['ResultCode'] -ceq 'attendance-operation-v1' -and $null -ne $Receipt['CompletedAt'] -and
        $Receipt['RequestSha256'] -cmatch '^[A-F0-9]{64}$' -and $AuditEvent['EventType'] -ceq $operation -and
        $AuditEvent['TargetType'] -ceq 'Attendance') 'metadata'
    $trace=$AuditEvent['CorrelationId']
    Assert-V1EvidenceCondition ($trace -is [string] -and $trace.Length -ge 1 -and $trace.Length -le 100 -and
        ![string]::IsNullOrWhiteSpace($trace) -and @($trace.ToCharArray() | Where-Object {[char]::IsControl($_)}).Count -eq 0) 'server-trace'
    if($suffix -ceq 'issue') {
        Assert-V1EvidenceGuid $AuditEvent['EmployeeScopeId'] ([guid]'35d19922-70a8-4c80-8578-e1029360e9fc')
    } else {Assert-V1EvidenceCondition ($null -eq $AuditEvent['EmployeeScopeId']) 'audit-subject'}
    $resultDoc=$null;$auditDoc=$null
    try {
        $resultDoc=[Text.Json.JsonDocument]::Parse($ResultJson,[Text.Json.JsonDocumentOptions]::new())
        $auditDoc=[Text.Json.JsonDocument]::Parse($AuditJson,[Text.Json.JsonDocumentOptions]::new())
        Assert-V1EvidenceJsonTree $resultDoc.RootElement
        Assert-V1EvidenceJsonTree $auditDoc.RootElement
        $result=ConvertFrom-Json -InputObject $ResultJson -AsHashtable -DateKind String
        $audit=ConvertFrom-Json -InputObject $AuditJson -AsHashtable -DateKind String
        Assert-V1EvidenceFields $result @('ResourceId','Result','Resources','RequiresAll','ResourceScopes')
        Assert-V1EvidenceFields $audit @('operation','request','ResourceId','Before','After','resources','resourceScopes')
        Assert-V1EvidenceGuid $result['ResourceId'] $resourceId
        Assert-V1EvidenceGuid $audit['ResourceId'] $resourceId
        Assert-V1EvidenceCondition ($audit['operation'] -ceq $operation -and $null -eq $audit['Before'] -and
            $result['RequiresAll'] -is [bool] -and $result['RequiresAll'] -and
            $result['Resources'] -is [array] -and $result['Resources'].Count -eq 0 -and
            $audit['resources'] -is [array] -and $audit['resources'].Count -eq 0 -and
            $null -eq $result['ResourceScopes'] -and $null -eq $audit['resourceScopes']) 'wrapper-scope'
        $request=$audit['request']
        Assert-V1EvidenceFields $request @('permission','employeeId','DeviceId','payload','reason')
        $permission=if($suffix -ceq 'create'){'attendance.vehicle.manage'}else{'attendance.vehicle.custody.manage'}
        Assert-V1EvidenceCondition ($request['permission'] -ceq $permission -and $null -eq $request['DeviceId']) 'request-authority'
        if($suffix -ceq 'issue') {
            Assert-V1EvidenceGuid $request['employeeId'] ([guid]'35d19922-70a8-4c80-8578-e1029360e9fc')
        } else {Assert-V1EvidenceCondition ($null -eq $request['employeeId']) 'request-subject'}
        # Hash the original System.Text.Json bytes; never reserialize through PowerShell.
        $requestText=$auditDoc.RootElement.GetProperty('request').GetRawText()
        $digest=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($requestText)))
        Assert-V1EvidenceCondition ($digest -ceq $Receipt['RequestSha256']) 'request-digest'
        Assert-V1EvidenceCondition ($auditDoc.RootElement.GetProperty('After').GetRawText() -ceq
            $resultDoc.RootElement.GetProperty('Result').GetRawText()) 'audit-result-equality'
        if($suffix -ceq 'create') {Assert-V1PersistedVehicle $result['Result'] $VehicleId}
        else {Assert-V1PersistedCustody $result['Result'] $VehicleId $CustodyId $IssuedAt $ReturnedAt ($suffix -ceq 'return')}
        [pscustomobject]@{Operation=$operation;ResourceId=$resourceId;ServerCorrelationId=$trace;RequestSha256=$digest;Passed=$true}
    } finally {
        if($null -ne $resultDoc){$resultDoc.Dispose()}
        if($null -ne $auditDoc){$auditDoc.Dispose()}
    }
}
