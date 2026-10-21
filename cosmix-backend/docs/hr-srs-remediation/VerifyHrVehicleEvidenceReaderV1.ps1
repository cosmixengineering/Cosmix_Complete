[CmdletBinding()]
param(
    [string]$ReleaseDirectory='D:/CosmixVerification/hr-backend-20261020/hr-vehicle-v1-build09/bin/Cosmix.Api/release'
)
$ErrorActionPreference='Stop'
if($PSVersionTable.PSVersion -lt [version]'7.5'){throw 'PowerShell 7.5+ required.'}
. (Join-Path $PSScriptRoot 'HrVehicleV1EvidenceAssertions.ps1')
$checks=0
function Test([bool]$Condition,[string]$Name) {if(!$Condition){throw $Name};$script:checks++}
function Reject([scriptblock]$Action,[string]$Name) {
    $denied=$false
    try {$null=& $Action} catch [InvalidOperationException] {$denied=$true}
    Test $denied $Name
}
# Load only the frozen DTO/domain assemblies. No compilation, application startup or service resolution.
$release=[IO.Path]::GetFullPath($ReleaseDirectory)
if(!$release.StartsWith('D:\CosmixVerification\',[StringComparison]::OrdinalIgnoreCase)){throw 'Frozen verification output required.'}
$inputs=@{}
foreach($name in @('Cosmix.Domain.dll','Cosmix.Application.dll')) {
    $path=Join-Path $release $name
    $inputs[$path]=(Get-FileHash -LiteralPath $path).Hash
    $null=[Reflection.Assembly]::LoadFrom($path)
}
function Serialize($Value,[bool]$Web=$false) {
    $options=if($Web){[Text.Json.JsonSerializerOptions]::Web}else{[Text.Json.JsonSerializerOptions]::Default}
    return [Text.Json.JsonSerializer]::Serialize($Value,$Value.GetType(),$options)
}
function Map([Collections.IDictionary]$Values) {
    $map=[Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
    foreach($key in $Values.Keys){$map.Add($key,$Values[$key])}
    return ,$map
}
$vehicleId=[guid]'11111111-1111-1111-1111-111111111111'
$custodyId=[guid]'22222222-2222-2222-2222-222222222222'
$sessionId=[guid]'33333333-3333-3333-3333-333333333333'
$admin=[guid]'94a83b62-5b56-4450-9175-2a7a9e3261bf'
$employee=[guid]'35d19922-70a8-4c80-8578-e1029360e9fc'
$issued=[DateTimeOffset]'2026-10-20T07:00:00Z'
$returned=[DateTimeOffset]'2026-10-20T07:05:00Z'
$marker='TEST-HR-V1-20261020'
$issueRef='TEST ONLY V1 issue condition; synthetic reference, no attachment'
$returnRef='TEST ONLY V1 return condition; synthetic reference, no attachment'
$vehicle=[Cosmix.Application.HR.VehicleFuel.VehicleResponse]::new($vehicleId,$marker,'Motorcycle','TEST ONLY custody acceptance',$marker,$true)
$issuedDto=[Cosmix.Application.HR.VehicleFuel.VehicleCustodyResponse].GetConstructors()[0].Invoke([object[]]@(
    $custodyId,$vehicleId,$marker,$employee,'TEST ONLY',$issued,[decimal]1000,$issueRef,$null,$null,$null,$true))
$returnedDto=[Cosmix.Application.HR.VehicleFuel.VehicleCustodyResponse]::new(
    $custodyId,$vehicleId,$marker,$employee,'TEST ONLY',$issued,1000,$issueRef,$returned,1000,$returnRef,$false)
$create=[Cosmix.Application.HR.VehicleFuel.CreateVehicleRequest]::new($marker,'Motorcycle','TEST ONLY custody acceptance',$marker)
$issue=[Cosmix.Application.HR.VehicleFuel.IssueCustodyRequest]::new($vehicleId,$employee,$issued,1000,$issueRef)
$return=[Cosmix.Application.HR.VehicleFuel.ReturnCustodyRequest]::new($returned,1000,$returnRef)
$resources=[Cosmix.Application.Authentication.Access.AccessResource[]]@()
$receiptDefinition=[Cosmix.Application.Attendance.Common.AttendanceOperation].GetNestedType('AttendanceReceipt`1',[Reflection.BindingFlags]::NonPublic)
Test ($null -ne $receiptDefinition) 'Actual persisted receipt type missing.'
$cases=@(
    @{Suffix='create';Dto=$vehicle;Payload=$create;Id=$vehicleId;Subject=$null;Permission='attendance.vehicle.manage';Reason="Create vehicle $marker"},
    @{Suffix='issue';Dto=$issuedDto;Payload=$issue;Id=$custodyId;Subject=$employee;Permission='attendance.vehicle.custody.manage';Reason="Issue custody of vehicle $vehicleId to employee $employee"},
    @{Suffix='return';Dto=$returnedDto;Payload=$return;Id=$custodyId;Subject=$null;Permission='attendance.vehicle.custody.manage';Reason="Return custody $custodyId"}
)
foreach($case in $cases) {
    $persisted=Serialize $case.Dto
    $web=Serialize $case.Dto $true
    $pascal=ConvertFrom-Json -InputObject $persisted -AsHashtable -DateKind String
    $camel=ConvertFrom-Json -InputObject $web -AsHashtable -DateKind String
    Test ($pascal.Contains('Id') -and !$pascal.Contains('id') -and $null -eq $pascal.id) 'Persisted PascalCase must remain case-distinct.'
    Test ($camel.Contains('id') -and !$camel.Contains('Id') -and $null -eq $camel.Id) 'HTTP camelCase must remain case-distinct.'
    if($case.Suffix -ceq 'create') {
        Assert-V1PersistedVehicle $pascal $vehicleId;$checks++
        Reject {Assert-V1PersistedVehicle $camel $vehicleId} 'HTTP vehicle accepted as persisted vehicle.'
    } else {
        Assert-V1PersistedCustody $pascal $vehicleId $custodyId $issued $returned ($case.Suffix -ceq 'return');$checks++
        Reject {Assert-V1PersistedCustody $camel $vehicleId $custodyId $issued $returned ($case.Suffix -ceq 'return')} 'HTTP custody accepted as persisted custody.'
    }
    $operation=if($case.Suffix -ceq 'create'){'vehicle.create'}else{'vehicle.custody.'+$case.Suffix}
    $request=Map ([ordered]@{permission=$case.Permission;employeeId=$case.Subject;DeviceId=$null;payload=$case.Payload;reason=$case.Reason})
    $requestJson=Serialize $request
    $digest=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($requestJson)))
    $receiptType=$receiptDefinition.MakeGenericType([type[]]@($case.Dto.GetType()))
    $wrapped=[Activator]::CreateInstance($receiptType,[object[]]@($case.Id,$case.Dto,$resources,$true,$null))
    $resultJson=Serialize $wrapped
    $auditJson=Serialize (Map ([ordered]@{operation=$operation;request=$request;ResourceId=$case.Id;Before=$null;After=$case.Dto;resources=$resources;resourceScopes=$null}))
    $clientHeader='HR-V1-synthetic-client-header'
    $serverTrace='SERVER-TRACE:00000001'
    $event=[Cosmix.Domain.Common.Audit.AuditEvent]::new($operation,$admin.ToString(),'ADMIN','Attendance',$case.Id.ToString(),
        $returned,$serverTrace,$case.Subject,$null,'127.0.0.1',$sessionId.ToString())
    $metadata=@{EventType=$event.EventType;ActorUserId=$event.ActorUserId;TargetType=$event.TargetType;TargetId=$event.TargetId;
        EmployeeScopeId=if($null -ne $case.Subject){$case.Subject.ToString()}else{$null};DeviceReference=$event.DeviceReference;CorrelationId=$event.CorrelationId}
    $receipt=@{Operation=$operation;Key="$marker-$($case.Suffix)";ActorUserId=$admin.ToString();ResultCode='attendance-operation-v1';CompletedAt=$returned;RequestSha256=$digest}
    $arguments=@{ResultJson=$resultJson;AuditJson=$auditJson;Receipt=$receipt;AuditEvent=$metadata;VehicleId=$vehicleId;
        CustodyId=$custodyId;AdminSessionId=$sessionId;IssuedAt=$issued;ReturnedAt=$returned}
    $proof=Assert-V1EvidenceRecord @arguments
    Test ($proof.Passed -and $proof.ServerCorrelationId -ceq $serverTrace -and $proof.ServerCorrelationId -cne $clientHeader -and
        $proof.RequestSha256 -ceq $digest -and $proof.ResourceId -eq $case.Id) 'Actual server trace/digest/resource must be preserved.'
    foreach($badReceipt in @(
        @{RequestSha256=('0'*64)},@{ActorUserId=$employee.ToString()},@{Key="$marker-other"},@{CompletedAt=$null},@{ResultCode='other'}
    )) {
        $argsCopy=$arguments.Clone();$argsCopy.Receipt=$receipt.Clone()
        foreach($key in $badReceipt.Keys){$argsCopy.Receipt[$key]=$badReceipt[$key]}
        Reject {Assert-V1EvidenceRecord @argsCopy} 'Receipt metadata/digest change accepted.'
    }
    foreach($badEvent in @(
        @{ActorUserId=$employee.ToString()},@{DeviceReference=$vehicleId.ToString()},@{TargetId=$sessionId.ToString()},
        @{EventType='vehicle.other'},@{TargetType='Other'},@{CorrelationId=''},@{CorrelationId="bad`ntrace"}
    )) {
        $argsCopy=$arguments.Clone();$argsCopy.AuditEvent=$metadata.Clone()
        foreach($key in $badEvent.Keys){$argsCopy.AuditEvent[$key]=$badEvent[$key]}
        Reject {Assert-V1EvidenceRecord @argsCopy} 'Audit metadata change accepted.'
    }
    $argsCopy=$arguments.Clone();$argsCopy.ResultJson=Serialize $wrapped $true
    Reject {Assert-V1EvidenceRecord @argsCopy} 'Web-serialized receipt accepted.'
    $argsCopy=$arguments.Clone();$argsCopy.AuditJson=$auditJson.Replace('"reason":','"Reason":')
    Reject {Assert-V1EvidenceRecord @argsCopy} 'Case-changed request accepted.'
    $argsCopy=$arguments.Clone();$argsCopy.AuditJson=$auditJson.Replace('"reason":','"reason":"tampered","reason":')
    Reject {Assert-V1EvidenceRecord @argsCopy} 'Duplicate request property accepted.'
    $argsCopy=$arguments.Clone();$argsCopy.AuditJson=$auditJson.Replace($case.Reason,'changed reason')
    Reject {Assert-V1EvidenceRecord @argsCopy} 'Changed raw request digest accepted.'
    $argsCopy=$arguments.Clone();$argsCopy.ResultJson=$resultJson.Replace('"IsActive":','"isActive":')
    Reject {Assert-V1EvidenceRecord @argsCopy} 'Case-changed persisted DTO accepted.'
    $argsCopy=$arguments.Clone();$argsCopy.ResultJson=$resultJson.Replace('"RequiresAll":true','"RequiresAll":false')
    Reject {Assert-V1EvidenceRecord @argsCopy} 'Changed receipt scope accepted.'
    $argsCopy=$arguments.Clone();$argsCopy.AuditEvent=$metadata.Clone();$argsCopy.AuditEvent.CorrelationId='OTHER-SERVER:00000002'
    $proof=Assert-V1EvidenceRecord @argsCopy
    Test ($proof.ServerCorrelationId -ceq 'OTHER-SERVER:00000002') 'Trace must be preserved, never fabricated or prefix-normalized.'
}
foreach($path in $inputs.Keys) {Test ((Get-FileHash -LiteralPath $path).Hash -ceq $inputs[$path]) 'Frozen assembly changed.'}
Write-Output "PASS $checks offline persisted evidence casing/digest/actor/session/resource/scope/server-trace checks. Actual build09 DTOs and System.Text.Json; no SQL, HTTP, host, private operational IO or product build."
