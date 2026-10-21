[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$pipe='np:\\.\pipe\LOCALDB#9F3001F7\tsql\query'
$owner='COSMIXRND\Huma.Khan'
function Require([bool]$value,[string]$message){if(!$value){throw $message}}
function Read-Sql([string]$sql){
    Require ($sql.TrimStart().StartsWith('SELECT ',[StringComparison]::OrdinalIgnoreCase)) 'Only reviewed SELECT statements allowed.'
    $connection=[System.Data.SqlClient.SqlConnection]::new("Server=$pipe;Database=Cosmix_Testing;Integrated Security=True;Encrypt=False;Pooling=False;Connect Timeout=5;Application Name=Cosmix.HrFixtures.ReadOnly")
    try {
        $connection.Open()
        $command=$connection.CreateCommand()
        try {
            $command.CommandTimeout=20
            $command.CommandText=$sql
            $reader=$command.ExecuteReader()
            try {
                $rows=[Collections.Generic.List[string]]::new()
                while($reader.Read()){
                    Require ($reader.FieldCount -eq 1 -and !$reader.IsDBNull(0)) 'Unexpected read-only SQL result.'
                    $rows.Add([string]$reader.GetValue(0))
                }
                return ($rows -join "`n")
            } finally {$reader.Dispose()}
        } finally {$command.Dispose()}
    } finally {$connection.Dispose()}
}
Require ([Security.Principal.WindowsIdentity]::GetCurrent().Name -ieq $owner) 'Windows operator differs.'
$identity=Read-Sql "SELECT CONCAT(CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')),'|',DB_NAME(),'|',SUSER_SNAME(d.owner_sid),'|',ORIGINAL_LOGIN(),'|',CONVERT(nvarchar(36),r.family_guid),'|',d.state_desc,'|',CONVERT(int,SERVERPROPERTY('ProcessID'))) FROM sys.databases d JOIN sys.database_recovery_status r ON r.database_id=d.database_id WHERE d.database_id=DB_ID();"
Require ($identity -ceq 'Software-PC1\LOCALDB#9F3001F7|Cosmix_Testing|COSMIXRND\Huma.Khan|COSMIXRND\Huma.Khan|D9C34627-18A2-41CF-A804-821E39516303|ONLINE|25768') 'SQL target differs; no fallback or restart.'
$files=Read-Sql "SELECT STRING_AGG(CONVERT(nvarchar(max),physical_name),'|') WITHIN GROUP(ORDER BY file_id) FROM sys.database_files;"
Require ($files -ieq 'C:\Users\Huma.Khan\Cosmix_Testing.mdf|C:\Users\Huma.Khan\Cosmix_Testing_log.ldf') 'Database files differ.'
$expected=@(Get-ChildItem -LiteralPath (Join-Path $repo 'src/Cosmix.Infrastructure/Database/Migrations') -Filter '*.cs' -File |
    Where-Object Name -Match '^\d{14}_[^.]+\.cs$' | Sort-Object Name | ForEach-Object BaseName)
$applied=(Read-Sql 'SELECT MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;') -split '\r?\n'
Require ($expected.Count -eq 25 -and ($expected -join '|') -ceq ($applied -join '|')) 'Schema baseline differs; no migrations allowed.'
$accounts=ConvertFrom-Json -InputObject (Read-Sql @"
SELECT COALESCE((SELECT LoginName,Id,EmployeeId,Status,WebAccessEnabled,SecurityVersion
FROM auth.UserAccounts WHERE LoginName IN('hr@example.invalid','admin@example.invalid','superadmin@example.invalid')
ORDER BY LoginName FOR JSON PATH),N'[]');
"@) -NoEnumerate
Require ($accounts.Count -eq 3 -and @($accounts | Where-Object {$_.Status -ne 3 -or !$_.WebAccessEnabled}).Count -eq 0) 'Synthetic accounts unavailable.'
Require (($accounts | Where-Object LoginName -eq 'hr@example.invalid').Id -eq '8475187f-bf3b-4f9c-a3ed-4bac32253005') 'HR identity differs.'
Require (($accounts | Where-Object LoginName -eq 'admin@example.invalid').Id -eq '94a83b62-5b56-4450-9175-2a7a9e3261bf') 'Admin identity differs.'
$required=@('attendance.leaves.benefits.review','attendance.vehicle.policy.manage',
    'attendance.deployments.read','attendance.deployments.request','attendance.deployments.decide')
$catalog=@((Read-Sql "SELECT Code FROM auth.Permissions WHERE Code IN('attendance.leaves.benefits.review','attendance.vehicle.policy.manage','attendance.deployments.read','attendance.deployments.request','attendance.deployments.decide') ORDER BY Code;") -split '\r?\n' | Where-Object {$_})
$grants=ConvertFrom-Json -InputObject (Read-Sql @"
SELECT COALESCE((SELECT a.LoginName,r.Code AS RoleCode,p.Code AS Permission,s.Kind AS Scope
FROM auth.UserAccounts a JOIN auth.UserRoleAssignments u ON u.UserAccountId=a.Id
JOIN auth.Roles r ON r.Id=u.RoleId JOIN auth.RolePermissions rp ON rp.RoleId=r.Id
JOIN auth.Permissions p ON p.Id=rp.PermissionId JOIN auth.UserAccessScopes s ON s.UserRoleAssignmentId=u.Id
WHERE a.LoginName IN('hr@example.invalid','admin@example.invalid','superadmin@example.invalid')
AND r.IsActive=1 AND u.EffectiveFrom<=SYSUTCDATETIME()
AND (u.EffectiveTo IS NULL OR u.EffectiveTo>SYSUTCDATETIME())
AND (u.RevokedAt IS NULL OR u.RevokedAt>SYSUTCDATETIME())
AND rp.RevokedAt IS NULL AND rp.GrantedAt<=SYSUTCDATETIME()
AND p.Code IN('attendance.leaves.benefits.review','attendance.vehicle.policy.manage',
'attendance.deployments.read','attendance.deployments.request','attendance.deployments.decide')
ORDER BY a.LoginName,p.Code FOR JSON PATH),N'[]');
"@) -NoEnumerate
$counts=[ordered]@{}
foreach($table in @('hr.LeaveRequests','hr.LeaveLedgerEntries','hr.LeaveRecoveryCommitments',
    'hr.Policies','hr.PolicyVersions','hr.EmployeeAssignments','hr.Vehicles','hr.VehicleCustodies',
    'hr.VehicleTrips','hr.VehicleMileageEntries','hr.VehicleServiceRecords','hr.FuelClaims',
    'hr.OutstationDeployments','hr.DeploymentHomeBreakRequests','hr.AllowanceClaims','hr.AllowanceClaimSubmissions')){
    Require ((Read-Sql "SELECT COUNT(*) FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE s.name+'.'+t.name='$table';") -eq '1') "Required table missing: $table"
    $counts[$table]=[int](Read-Sql "SELECT COUNT(*) FROM $table;")
}
[pscustomobject]@{
    Target=$identity
    AppliedMigrations=$applied.Count
    SyntheticAccounts=$accounts.Count
    MissingCatalogPermissions=@($required|Where-Object {$_ -cnotin $catalog})
    ExistingRelevantGrants=@($grants)
    SourceRowCounts=$counts
    Port7481Available=(@(Get-NetTCPConnection -LocalPort 7481 -State Listen -ErrorAction SilentlyContinue).Count -eq 0)
    WritesPerformed=$false
    Meaning='Prerequisite inventory only; not authenticated HR business acceptance.'
} | ConvertTo-Json -Depth 6
