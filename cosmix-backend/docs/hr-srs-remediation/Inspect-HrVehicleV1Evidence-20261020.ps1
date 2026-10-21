[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
# Post-stop diagnostic only: no HTTP, host, SQL mutations or private-file writes.
$runner = Join-Path $PSScriptRoot 'Invoke-HrVehicleV1.ps1'
$tokens = $null; $errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($runner, [ref]$tokens, [ref]$errors)
if ($errors.Count) { throw 'Runner parse failed.' }
foreach ($definition in $ast.FindAll({ param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst] }, $true)) {
    if ($definition.Name -cin @('Assert', 'Read-Sql', 'Assert-PrivateSource')) {
        . ([scriptblock]::Create($definition.Extent.Text))
    }
}
. (Join-Path $PSScriptRoot 'HrVehicleV1RunnerFunctions.ps1')
. (Join-Path $PSScriptRoot 'HrVehicleV1EvidenceAssertions.ps1')
$root = 'C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261020V1'
$pipe = 'np:\\.\pipe\LOCALDB#9F3001F7\tsql\query'
$marker = 'TEST-HR-V1-20261020'
Initialize-V1Manifest (Join-Path $PSScriptRoot 'HrVehicleV1Manifest.reviewed-20261020.json')
$binding = Read-Sql "SELECT CONCAT(CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')),'|',DB_NAME(),'|',ORIGINAL_LOGIN(),'|',SUSER_SNAME(d.owner_sid),'|',CONVERT(varchar(36),r.family_guid),'|',d.state_desc,'|',CONVERT(int,SERVERPROPERTY('ProcessID'))) FROM sys.databases d JOIN sys.database_recovery_status r ON r.database_id=d.database_id WHERE d.database_id=DB_ID();"
Assert ($binding -ieq 'Software-PC1\LOCALDB#9F3001F7|Cosmix_Testing|COSMIXRND\Huma.Khan|COSMIXRND\Huma.Khan|d9c34627-18a2-41cf-a804-821e39516303|ONLINE|25768') 'Diagnostic target differs.'
Assert ((Read-Sql "SELECT STRING_AGG(CONVERT(nvarchar(max),physical_name),'|') WITHIN GROUP(ORDER BY file_id) FROM sys.database_files;") -ieq 'C:\Users\Huma.Khan\Cosmix_Testing.mdf|C:\Users\Huma.Khan\Cosmix_Testing_log.ldf') 'Database files differ.'
Assert ((Read-Sql 'SELECT COUNT(*) FROM dbo.__EFMigrationsHistory;') -eq '25') 'Migration history differs.'
$vehicleId = 'c1f97788-0584-40aa-bec2-bce2c94e61bb'
$custodyId = 'b9f26448-7b5f-4c5b-bc8c-916f0fec4dd2'
$operatorSession = 'b96b1c57-b051-40bd-90f5-432831f4eb5c'
Assert ((Read-Sql "SELECT COUNT(*) FROM hr.Vehicles WHERE Id='$vehicleId' AND RegistrationNumber='$marker' AND VehicleType='Motorcycle' AND MakeModel='TEST ONLY custody acceptance' AND AssetReference='$marker' AND IsActive=1;") -eq '1') 'Vehicle state differs.'
Assert ((Read-Sql "SELECT COUNT(*) FROM hr.Vehicles WHERE RegistrationNumber='$marker';") -eq '1') 'Vehicle population differs.'
Assert ((Read-Sql "SELECT COUNT(*) FROM hr.VehicleCustodies WHERE VehicleId='$vehicleId' OR EmployeeId='$hrEmployee';") -eq '1') 'Custody population differs.'
Assert ((Read-Sql "SELECT COUNT(*) FROM hr.VehicleCustodies WHERE Id='$custodyId' AND VehicleId='$vehicleId' AND EmployeeId='$hrEmployee' AND IssuedAt=CONVERT(datetimeoffset,'$issuedAt') AND ReturnedAt=CONVERT(datetimeoffset,'$returnedAt') AND IssueOdometerKm=1000 AND ReturnOdometerKm=1000 AND IssueConditionPrivateReference='$issueReference' AND ReturnConditionPrivateReference='$returnReference' AND RecordedByUserId='$adminAccount' AND ReturnedToUserId='$adminAccount';") -eq '1') 'Custody state differs.'
Assert ((Read-Sql "SELECT COUNT(*) FROM workflow.IdempotencyReceipts WHERE [Key] LIKE '$marker%';") -eq '3') 'Receipt population differs.'
# A server trace is retained, not the request's arbitrary X-Correlation-ID header.
$query = @"
SELECT (SELECT r.Operation,r.[Key],r.ActorUserId,r.ResultCode,r.CompletedAt,r.RequestSha256,r.ProtectedResultReference,
a.ProtectedChangesReference,a.EventType,a.ActorUserId AS AuditActorUserId,a.TargetType,a.TargetId,a.EmployeeScopeId,a.DeviceReference,a.CorrelationId
FROM workflow.IdempotencyReceipts r JOIN audit.AuditEvents a ON a.EventType=r.Operation AND a.ActorUserId=r.ActorUserId
WHERE r.ActorUserId='$adminAccount' AND r.[Key] IN('$marker-create','$marker-issue','$marker-return')
AND r.CompletedAt IS NOT NULL AND r.ResultCode='attendance-operation-v1'
AND a.TargetType='Attendance' AND a.TargetId IN('$vehicleId','$custodyId')
AND a.DeviceReference='$operatorSession'
ORDER BY r.Operation FOR JSON PATH,INCLUDE_NULL_VALUES);
"@
$rows = @((Read-Sql $query) | ConvertFrom-Json -AsHashtable)
Assert ($rows.Count -eq 3) 'Expected three operation-linked audit records.'
$traces = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$references = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($row in $rows) {
    $suffix = switch -CaseSensitive ($row.Operation) {
        'vehicle.create' { 'create' }
        'vehicle.custody.issue' { 'issue' }
        'vehicle.custody.return' { 'return' }
        default { throw 'Unexpected operation.' }
    }
    $id = if ($suffix -ceq 'create') { $vehicleId } else { $custodyId }
    Assert ($row.Key -ceq "$marker-$suffix" -and $row.TargetId -eq $id) 'Resource binding differs.'
    Assert ($row.CorrelationId -cmatch '^[A-Z0-9]+:[0-9A-F]{8}$' -and $traces.Add($row.CorrelationId)) 'Expected distinct server traces.'
    Assert (($suffix -ceq 'issue' -and $row.EmployeeScopeId -eq $hrEmployee) -or ($suffix -cne 'issue' -and $null -eq $row.EmployeeScopeId)) 'Employee scope differs.'
    $actorN = ([guid]$adminAccount).ToString('N')
    $resultText = Read-V1Protected $row.ProtectedResultReference "attendance:operation:${actorN}:$($row.Operation):result"
    $auditText = Read-V1Protected $row.ProtectedChangesReference "attendance:operation:${actorN}:$($row.Operation):audit"
    $receipt = @{
        Operation=$row.Operation; Key=$row.Key; ActorUserId=$row.ActorUserId
        ResultCode=$row.ResultCode; CompletedAt=$row.CompletedAt; RequestSha256=$row.RequestSha256
    }
    $auditEvent = @{
        EventType=$row.EventType; ActorUserId=$row.AuditActorUserId; TargetType=$row.TargetType
        TargetId=$row.TargetId; EmployeeScopeId=$row.EmployeeScopeId
        DeviceReference=$row.DeviceReference; CorrelationId=$row.CorrelationId
    }
    $null = Assert-V1EvidenceRecord -ResultJson $resultText -AuditJson $auditText -Receipt $receipt `
        -AuditEvent $auditEvent -VehicleId $vehicleId -CustodyId $custodyId -AdminSessionId $operatorSession `
        -IssuedAt $issuedAt -ReturnedAt $returnedAt
    foreach ($serialized in @($row.ProtectedResultReference, $row.ProtectedChangesReference)) {
        $reference = $serialized | ConvertFrom-Json -AsHashtable
        Assert ($references.Add($reference.StorageKey)) 'Duplicate protected object.'
    }
    Write-Output ('PASS read-only SQL/audit/receipt/encrypted evidence: ' + $row.Operation)
}
$objects = @(Get-ChildItem -LiteralPath (Join-Path $root 'files') -Force)
Assert ($objects.Count -eq 6 -and @($objects | Where-Object { $_.PSIsContainer -or !$references.Contains($_.Name) }).Count -eq 0) 'Private object population differs.'
Write-Output 'Read-only diagnosis PASS: one vehicle, one returned custody, three linked receipts/audits, six authenticated encrypted objects. No live rerun or full V1 acceptance claim.'
