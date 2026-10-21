# Dot-source only; loading defines functions without SQL, HTTP or private-file reads.
Set-StrictMode -Version Latest

function Assert-Huma([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Get-HumaPath([string]$Path, [string]$Root, [switch]$File) {
    $full = [IO.Path]::GetFullPath($Path)
    $rootFull = [IO.Path]::GetFullPath($Root).TrimEnd('\')
    Assert-Huma ($full.Equals($rootFull, [StringComparison]::OrdinalIgnoreCase) -or
        $full.StartsWith($rootFull + '\', [StringComparison]::OrdinalIgnoreCase)) 'Path is outside its reviewed root.'
    for ($cursor = $full; $cursor; $cursor = [IO.Path]::GetDirectoryName($cursor)) {
        if (Test-Path -LiteralPath $cursor) {
            Assert-Huma (-not ((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) 'Linked paths are forbidden.'
        }
        if ([IO.Path]::GetDirectoryName($cursor) -eq $cursor) { break }
    }
    if ($File) {
        $item = Get-Item -LiteralPath $full -Force
        Assert-Huma (-not $item.PSIsContainer -and $item.Length -gt 0 -and $item.Length -le 16777216) 'Input must be a bounded regular file.'
    }
    return $full
}

function Assert-HumaPrivate([string]$Path) {
    $item = Get-Item -LiteralPath $Path -Force
    $acl = Get-Acl -LiteralPath $item.FullName
    $descriptor = [Security.AccessControl.RawSecurityDescriptor]::new($acl.GetSecurityDescriptorBinaryForm(), 0)
    Assert-Huma ($null -ne $descriptor.DiscretionaryAcl) 'Private path has a null ACL.'
    $allowed = @([Security.Principal.WindowsIdentity]::GetCurrent().User.Value, 'S-1-5-18', 'S-1-5-32-544')
    Assert-Huma ($acl.GetOwner([Security.Principal.SecurityIdentifier]).Value -in $allowed) 'Unexpected private path owner.'
    foreach ($rule in $acl.GetAccessRules($true, $true, [Security.Principal.SecurityIdentifier])) {
        Assert-Huma ($rule.AccessControlType -ne 'Allow' -or $rule.IdentityReference.Value -in $allowed) 'Private path allows an unapproved principal.'
    }
}

function Get-HumaHash([byte[]]$Bytes) {
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($Bytes))
}

function Assert-HumaHash([string]$Path, [string]$Hash) {
    Assert-Huma ($Hash -cmatch '^[A-F0-9]{64}$') 'Missing canonical SHA256.'
    Assert-Huma ((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -ceq $Hash) 'Reviewed artifact hash changed.'
}

function Read-HumaContext([string]$ManifestPath, [string]$ApprovedManifestSha256, [string]$SourceServer) {
    Assert-Huma ($PSVersionTable.PSVersion.Major -ge 7 -and $IsWindows) 'Windows PowerShell 7 or later is required.'
    $repo = 'C:\Users\Huma.Khan\Downloads\Cosmix_Erp_software\cosmix-backend'
    Assert-Huma ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd('\') -ieq $repo) 'Wrong checkout.'
    Assert-Huma ([Security.Principal.WindowsIdentity]::GetCurrent().Name -ieq 'COSMIXRND\Huma.Khan') 'Wrong Windows operator.'
    $path = Get-HumaPath $ManifestPath (Join-Path $repo 'docs\attendance-completion') -File
    $bytes = [IO.File]::ReadAllBytes($path)
    Assert-Huma ((Get-HumaHash $bytes) -ceq $ApprovedManifestSha256.ToUpperInvariant()) 'Exact explicit manifest SHA256 required.'
    try { $m = [Text.Encoding]::UTF8.GetString($bytes).TrimStart([char]0xFEFF) | ConvertFrom-Json -AsHashtable -Depth 50 }
    catch { throw 'Acceptance manifest is invalid; contents suppressed.' }
    Assert-Huma ($m.SchemaVersion -eq 1 -and $m.Profile -ceq 'Huma-existing-localdb-v1' -and
        $m.Mode -ceq 'ExistingDatabase' -and $m.Database -ceq 'Cosmix_Testing') 'Only the existing Cosmix_Testing profile is supported; fresh fixtures/keys need separate approval and tooling.'
    Assert-Huma ($SourceServer -ceq 'np:\\.\pipe\LOCALDB#842DD4FB\tsql\query' -and $m.SourceServer -ceq $SourceServer -and
        $m.ActualServerName -ceq 'Software-PC1\LOCALDB#842DD4FB' -and $m.Owner -ceq 'COSMIXRND\Huma.Khan') 'Only the observed explicit local pipe/server/owner is allowed. Re-review any changed pipe.'
    Assert-Huma ($m.ExistingPrivateRoot -ceq 'C:\Users\Huma.Khan\AppData\Local\Cosmix\LocalTesting' -and
        $m.ArtifactRoot -ceq ($repo + '\artifacts\attendance-huma-20261012-a01') -and
        $m.RunId -cmatch '^HUMA20261012[A-Z0-9]{8,24}$') 'Unreviewed private/artifact root or run ID.'
    Assert-Huma ($m.WorkerEnabled -eq $false -and $m.FreshFixtures -eq $false -and $m.RotateKeys -eq $false) 'Workers, fresh fixtures and key rotation are outside this profile.'
    $required = @(
        'docs/attendance-completion/Huma-Acceptance-Common.ps1',
        'docs/attendance-completion/Huma-Acceptance-Preflight.ps1',
        'docs/attendance-completion/Huma-Acceptance-Upgrade.ps1',
        'docs/attendance-completion/Huma-Acceptance-Auth.ps1',
        'docs/attendance-completion/Huma-Acceptance-Setup.md',
        'docs/access-provisioning/LocalAcceptanceHttp.ps1',
        'src/Cosmix.Api/Development/LocalTesting.cs',
        'src/Cosmix.Api/Development/LocalTestingSetup.cs'
    )
    Assert-Huma ($m.ArtifactHashes -is [Collections.IDictionary]) 'ArtifactHashes map is required.'
    foreach ($relative in $required) { Assert-Huma ($m.ArtifactHashes.Contains($relative)) 'Required tooling/guard hash is missing.' }
    foreach ($relative in $m.ArtifactHashes.Keys) {
        Assert-Huma ($relative -cmatch '^(docs/(attendance-completion|access-provisioning)/[A-Za-z0-9._-]+|src/[A-Za-z0-9._/-]+)$' -and
            -not $relative.Contains('..') -and -not $relative.Contains('//')) 'Artifact path is outside reviewed source/document inputs.'
        $artifact = Get-HumaPath (Join-Path $repo $relative) $repo -File
        Assert-HumaHash $artifact ([string]$m.ArtifactHashes[$relative])
    }
    return [pscustomobject]@{ Manifest = $m; ManifestBytes = $bytes; ManifestPath = $path;
        ManifestSha256 = $ApprovedManifestSha256.ToUpperInvariant(); Repository = $repo; SourceServer = $SourceServer }
}

function Assert-HumaMigrationTarget($Context) {
    $m = $Context.Manifest
    $folder = Join-Path $Context.Repository 'src\Cosmix.Infrastructure\Database\Migrations'
    $local = @(Get-ChildItem -LiteralPath $folder -Filter '*.Designer.cs' | Sort-Object Name | ForEach-Object { $_.Name.Replace('.Designer.cs', '') })
    Assert-Huma ($local.Count -gt 12 -and $local[11] -ceq '20261008074438_AddEmployeeChangeJournal') 'Local migration chain does not contain the observed baseline.'
    Assert-Huma ($m.TargetMigrationCount -eq $local.Count -and $m.TargetMigration -ceq $local[-1]) 'Exact final root migration target is unresolved or source changed.'
    Assert-Huma ($m.BaselineSchemaSha256 -cmatch '^[A-F0-9]{64}$' -and $m.TargetSchemaSha256 -cmatch '^[A-F0-9]{64}$' -and
        @($m.BaselineTableCounts).Count -gt 0 -and @($m.TargetTableCounts).Count -gt 0) 'Exact baseline and reviewed target schema/data expectations are required.'
    foreach ($stage in @('BaselineHistory', 'TargetHistory')) {
        $rows = @($m[$stage])
        $count = if ($stage -ceq 'BaselineHistory') { 12 } else { $local.Count }
        Assert-Huma ($rows.Count -eq $count) 'Manifest must enumerate the complete migration history, including ProductVersion.'
        for ($i = 0; $i -lt $count; $i++) {
            Assert-Huma ($rows[$i].MigrationId -ceq $local[$i] -and $rows[$i].ProductVersion -cmatch '^\d+\.\d+\.\d+([-.A-Za-z0-9]+)?$') 'Migration history is not the exact ordered local chain.'
            foreach ($suffix in @('.cs', '.Designer.cs')) {
                $relative = 'src/Cosmix.Infrastructure/Database/Migrations/' + $local[$i] + $suffix
                Assert-Huma ($m.ArtifactHashes.Contains($relative)) 'Migration source/designer hash is missing.'
            }
        }
    }
    for ($i = 0; $i -lt 12; $i++) {
        Assert-Huma ($m.BaselineHistory[$i].ProductVersion -ceq $m.TargetHistory[$i].ProductVersion) 'Target history must preserve all applied baseline ProductVersion values.'
    }
    Assert-Huma ($m.ArtifactHashes.Contains('src/Cosmix.Infrastructure/Database/Migrations/CosmixDbContextModelSnapshot.cs')) 'Model snapshot hash is missing.'
    Assert-Huma ($m.MigrationScript -cmatch '^docs/attendance-completion/[A-Za-z0-9_-]+\.sql$' -and
        $m.ArtifactHashes.Contains($m.MigrationScript)) 'Root-reviewed pending SQL artifact and its hash are required.'
}

function New-HumaSqlConnectionString([string]$SourceServer) {
    Assert-Huma ($SourceServer -ceq 'np:\\.\pipe\LOCALDB#842DD4FB\tsql\query') 'Only the reviewed existing test pipe is allowed.'
    $builder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new()
    # Explicit CLR setters avoid PowerShell's dictionary adaptation of this builder.
    $builder.set_DataSource($SourceServer)
    $builder.set_InitialCatalog('Cosmix_Testing')
    $builder.set_IntegratedSecurity($true)
    $builder.set_ConnectTimeout(15)
    $builder.set_Encrypt($true)
    $builder.set_TrustServerCertificate($true)
    $builder.set_Pooling($false)
    $builder.set_ApplicationName('Cosmix Huma guarded acceptance')
    return $builder.get_ConnectionString()
}
function Open-HumaSql($Context) {
    $connection = [System.Data.SqlClient.SqlConnection]::new((New-HumaSqlConnectionString $Context.SourceServer))
    try { $connection.Open(); return $connection }
    catch { $connection.Dispose(); throw 'Could not connect to the explicitly reviewed existing local pipe. No alias fallback or initialization.' }
}

function Read-HumaRows($Connection, [string]$Sql, $Transaction = $null) {
    $command = $Connection.CreateCommand()
    $command.CommandText = $Sql
    $command.CommandTimeout = 60
    $command.Transaction = $Transaction
    try {
        $reader = $command.ExecuteReader()
        try {
            $rows = [Collections.Generic.List[object]]::new()
            while ($reader.Read()) {
                $row = [ordered]@{}
                for ($i = 0; $i -lt $reader.FieldCount; $i++) {
                    $row[$reader.GetName($i)] = if ($reader.IsDBNull($i)) { $null } else { $reader.GetValue($i) }
                }
                $rows.Add([pscustomobject]$row)
            }
            return $rows.ToArray()
        } finally { $reader.Dispose() }
    } catch { throw 'Read-only SQL inspection failed; SQL/provider details suppressed.' }
    finally { $command.Dispose() }
}

function Assert-HumaSqlIdentity($Connection, $Context, $Transaction = $null) {
    $rows = @(Read-HumaRows $Connection @'
SELECT CONVERT(nvarchar(128), SERVERPROPERTY('ServerName')) AS ActualServerName,
       DB_NAME() AS DatabaseName, SUSER_SNAME(d.owner_sid) AS OwnerName,
       ORIGINAL_LOGIN() AS OperatorName, d.state_desc AS StateName, d.is_read_only AS ReadOnly,
       HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','VIEW DEFINITION') AS CanViewDefinition,
       (SELECT COUNT_BIG(*) FROM sys.synonyms) AS SynonymCount,
       (SELECT COUNT_BIG(*) FROM sys.sql_expression_dependencies
        WHERE referenced_server_name IS NOT NULL OR referenced_database_name IS NOT NULL) AS ExternalDependencyCount
FROM sys.databases d WHERE d.database_id = DB_ID();
'@ $Transaction)
    Assert-Huma ($rows.Count -eq 1 -and $rows[0].ActualServerName -ceq $Context.Manifest.ActualServerName -and
        $rows[0].DatabaseName -ceq 'Cosmix_Testing' -and $rows[0].OwnerName -ieq $Context.Manifest.Owner -and
        $rows[0].OperatorName -ieq $Context.Manifest.Owner -and $rows[0].StateName -ceq 'ONLINE' -and -not $rows[0].ReadOnly -and
        $rows[0].CanViewDefinition -eq 1 -and $rows[0].SynonymCount -eq 0 -and
        $rows[0].ExternalDependencyCount -eq 0) 'Actual SQL identity/state/metadata visibility or indirect-target guard failed.'
    return $rows[0]
}

function Get-HumaInventory($Connection, $Context, $Transaction = $null) {
    $identity = Assert-HumaSqlIdentity $Connection $Context $Transaction
    $history = @(Read-HumaRows $Connection 'SELECT MigrationId, ProductVersion FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;' $Transaction)
    # Only IDs/state/rowversions and a synthetic-match flag leave SQL; no HR details or credentials.
    $accounts = @(Read-HumaRows $Connection @'
SELECT CONVERT(varchar(36),a.Id) AS AccountId, CONVERT(varchar(36),a.EmployeeId) AS EmployeeId,
       CONVERT(varchar(18),a.RowVersion,1) AS RowVersion,
       CASE WHEN (a.LoginName COLLATE Latin1_General_100_BIN2 = N'superadmin@example.invalid' AND e.FullLegalName COLLATE Latin1_General_100_BIN2 = N'TEST ONLY SuperAdmin' AND e.ApprovedLoginPhoneNumber = '+12025550101') THEN 'SuperAdmin'
            WHEN (a.LoginName COLLATE Latin1_General_100_BIN2 = N'admin@example.invalid' AND e.FullLegalName COLLATE Latin1_General_100_BIN2 = N'TEST ONLY Admin' AND e.ApprovedLoginPhoneNumber = '+12025550102') THEN 'Admin'
            WHEN (a.LoginName COLLATE Latin1_General_100_BIN2 = N'hr@example.invalid' AND e.FullLegalName COLLATE Latin1_General_100_BIN2 = N'TEST ONLY HR' AND e.ApprovedLoginPhoneNumber = '+12025550103') THEN 'HR'
            ELSE 'UNCONFIRMED' END AS SyntheticRole
FROM auth.UserAccounts a LEFT JOIN hr.Employees e ON e.Id=a.EmployeeId ORDER BY a.Id;
'@ $Transaction)
    $employees = @(Read-HumaRows $Connection @'
SELECT CONVERT(varchar(36),Id) AS EmployeeId, Status, OnboardingStatus,
       CONVERT(varchar(18),RowVersion,1) AS RowVersion
FROM hr.Employees ORDER BY Id;
'@ $Transaction)
    $tables = @(Read-HumaRows $Connection @'
SELECT s.name AS SchemaName,t.name AS TableName FROM sys.tables t
JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE t.is_ms_shipped=0 ORDER BY s.name,t.name;
'@ $Transaction)
    $counts = @($tables | ForEach-Object {
        $table = '[' + $_.SchemaName.Replace(']', ']]') + '].[' + $_.TableName.Replace(']', ']]') + ']'
        $row = @(Read-HumaRows $Connection ("SELECT COUNT_BIG(*) AS RowCount FROM $table;") $Transaction)[0]
        [pscustomobject]@{ Table = $_.SchemaName + '.' + $_.TableName; RowCount = [long]$row.RowCount }
    })
    # Deterministic catalog fingerprint detects partial/unrecorded schema changes before retry.
    $catalog = @(Read-HumaRows $Connection @'
SELECT (SELECT s.name AS [Schema],o.name AS [Name],o.type AS [Type],
        OBJECT_DEFINITION(o.object_id) AS [Definition],
        (SELECT c.name,c.column_id,c.system_type_id,c.user_type_id,c.max_length,c.precision,c.scale,c.is_nullable,c.is_identity,c.is_computed,c.collation_name
         FROM sys.columns c WHERE c.object_id=o.object_id ORDER BY c.column_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS [Columns],
        (SELECT i.name,i.type,i.is_unique,i.is_disabled,i.filter_definition,
                (SELECT ic.column_id,ic.key_ordinal,ic.is_descending_key,ic.is_included_column FROM sys.index_columns ic
                 WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id ORDER BY ic.index_column_id FOR JSON PATH) AS [Columns]
         FROM sys.indexes i WHERE i.object_id=o.object_id ORDER BY i.index_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS [Indexes],
        (SELECT f.name,OBJECT_SCHEMA_NAME(f.referenced_object_id) AS ReferencedSchema,OBJECT_NAME(f.referenced_object_id) AS ReferencedTable,f.delete_referential_action,f.update_referential_action,f.is_disabled,f.is_not_trusted,
                (SELECT fc.parent_column_id,fc.referenced_column_id FROM sys.foreign_key_columns fc
                 WHERE fc.constraint_object_id=f.object_id ORDER BY fc.constraint_column_id FOR JSON PATH) AS [Columns]
         FROM sys.foreign_keys f WHERE f.parent_object_id=o.object_id ORDER BY f.name FOR JSON PATH) AS [ForeignKeys]
 FROM sys.objects o JOIN sys.schemas s ON s.schema_id=o.schema_id WHERE o.is_ms_shipped=0
 ORDER BY s.name,o.name,o.type FOR JSON PATH,INCLUDE_NULL_VALUES) AS Catalog;
'@ $Transaction)
    $lifecycle = @(Read-HumaRows $Connection @'
SELECT COUNT_BIG(*) AS PendingCount FROM hr.EmployeeStatusChanges c JOIN hr.Employees e ON e.Id=c.EmployeeId WHERE c.NewStatus<>e.Status;
'@ $Transaction)[0].PendingCount
    return [pscustomobject]@{ Identity = $identity; History = $history; Accounts = $accounts; Employees = $employees;
        TableCounts = $counts; DraftEmployeeCount = @($employees | Where-Object Status -eq 0).Count;
        SchemaSha256 = Get-HumaHash ([Text.Encoding]::UTF8.GetBytes([string]$catalog[0].Catalog)); LifecycleMismatchCount = $lifecycle }
}

function Assert-HumaEqual($Actual, $Expected, [string]$Message) {
    $a = ConvertTo-Json -InputObject $Actual -Depth 40 -Compress
    $e = ConvertTo-Json -InputObject $Expected -Depth 40 -Compress
    Assert-Huma ($a -ceq $e) $Message
}

function Assert-HumaState($Inventory, $Context, [ValidateSet('Baseline','Target')][string]$Stage) {
    $m = $Context.Manifest
    Assert-Huma ($Inventory.Accounts.Count -eq 3 -and $Inventory.Employees.Count -eq 11 -and
        $Inventory.DraftEmployeeCount -eq 8 -and $Inventory.LifecycleMismatchCount -eq 0) 'Observed counts/lifecycle guard changed; stop for review.'
    Assert-Huma ((@($Inventory.Accounts.SyntheticRole | Sort-Object) -join '|') -ceq 'Admin|HR|SuperAdmin') 'Exactly the three known synthetic account/employee triples are required.'
    Assert-HumaEqual @($Inventory.History) @($m[$Stage + 'History']) 'Applied migration history/ProductVersion differs; no automatic resume.'
    Assert-HumaEqual @($Inventory.Accounts) @($m.AllowedAccounts) 'Exact allowed account IDs, employee links or rowversions changed.'
    Assert-HumaEqual @($Inventory.Employees) @($m.AllowedEmployees) 'Exact employee ID/state/rowversion inventory changed.'
    Assert-HumaEqual @($Inventory.TableCounts) @($m[$Stage + 'TableCounts']) 'Unexpected tables or data counts; review before continuing.'
    Assert-Huma ($Inventory.SchemaSha256 -ceq $m[$Stage + 'SchemaSha256']) 'Schema fingerprint differs, including possible partial migration state.'
}

function Assert-HumaWriteApproval($Context, [ValidateSet('Upgrade','Auth')][string]$Action) {
    $m = $Context.Manifest
    Assert-Huma ($m.Approvals[$Action] -eq $true -and $m.Approvals.ExistingDataOwnership -eq $true -and
        $m.Approvals.PrivateArtifactSetup -eq $true -and $m.Approvals.RetainedKeyring -eq $true -and
        $m.ApprovalReference -cmatch '^[A-Za-z0-9][A-Za-z0-9 ._:/-]{7,199}$') 'Human action, existing-data ownership, private artifact setup and retained-keyring approval are unresolved.'
    Assert-Huma ($m.Approvals.ExclusiveSqlWindow -eq $true) 'Explicit human confirmation that other hosts/writers are stopped is required before any write phase.'
    Assert-Huma (@($m.OwnershipApprovals).Count -eq 11) 'Ownership approval must cover every existing employee, including all eight additional records.'
    $approved = @($m.OwnershipApprovals | ForEach-Object {
        Assert-Huma ($_.SyntheticConfirmed -eq $true -and $_.ApprovalReference -cmatch '^[A-Za-z0-9][A-Za-z0-9 ._:/-]{7,199}$') 'An employee lacks human synthetic ownership approval.'
        ([guid]$_.EmployeeId).ToString('D')
    } | Sort-Object)
    Assert-HumaEqual $approved @($m.AllowedEmployees.EmployeeId | Sort-Object) 'Ownership approvals do not cover the exact employee set.'
    $root = Get-HumaPath $m.ArtifactRoot $Context.Repository
    Assert-Huma ((Get-Item -LiteralPath $root -Force).PSIsContainer) 'Root must provision the exact private artifact directory first.'
    Assert-HumaPrivate $root
}

function Write-HumaNewJson([string]$Path, $Value) {
    $stream = [IO.File]::Open($Path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try {
        $bytes = [Text.Encoding]::UTF8.GetBytes((ConvertTo-Json -InputObject $Value -Depth 50))
        $stream.Write($bytes, 0, $bytes.Length); $stream.Flush($true)
    } finally { $stream.Dispose() }
}

function Start-HumaAttempt($Context, [ValidateSet('upgrade','auth')][string]$Stage) {
    $root = $Context.Manifest.ArtifactRoot
    $null = Get-HumaPath $root $Context.Repository
    Assert-HumaPrivate $root
    # One marker per stage per approved artifact root, even when a caller changes RunId.
    $marker = Get-HumaPath (Join-Path $root ($Stage + '-attempt.json')) $root
    Assert-Huma (-not (Test-Path -LiteralPath $marker)) 'Stage already attempted; root must reconcile retained state. No rerun/resume switch.'
    $run = Get-HumaPath (Join-Path $root ($Context.Manifest.RunId + '-' + $Stage)) $root
    Assert-Huma (-not (Test-Path -LiteralPath $run)) 'Run directory already exists; preserve and reconcile it.'
    Write-HumaNewJson $marker @{ RunId = $Context.Manifest.RunId; ManifestSha256 = $Context.ManifestSha256;
        Stage = $Stage; AtUtc = [DateTimeOffset]::UtcNow.ToString('O') }
    $null = [IO.Directory]::CreateDirectory($run)
    Assert-HumaPrivate $run
    $stream = [IO.File]::Open((Join-Path $run 'approved-manifest.json'), [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try { $stream.Write($Context.ManifestBytes, 0, $Context.ManifestBytes.Length); $stream.Flush($true) } finally { $stream.Dispose() }
    return $run
}
