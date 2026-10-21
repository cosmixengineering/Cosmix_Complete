[CmdletBinding()]
param([string]$MigrationScriptPath)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-FreshSqlScope {
    $private = 'C:\Users\Huma.Khan\AppData\Local\Cosmix\AttendanceAcceptance\Huma20261013A01'
    return [pscustomobject]@{
        Instance = 'CosmixAttendanceHuma20261013A01'; Database = 'Cosmix_Testing'
        Owner = 'COSMIXRND\Huma.Khan'; Profile = 'HumaAttendanceIsolatedV1'
        Repository = 'C:\Users\Huma.Khan\Downloads\Cosmix_Erp_software\cosmix-backend'
        PrivateRoot = $private; DataFile = $private + '\sql\Cosmix_Testing.mdf'
        LogFile = $private + '\sql\Cosmix_Testing_log.ldf'
        Settings = $private + '\config\LocalTesting.json'
        DataProtectionDirectory = $private + '\keys\data-protection'
        ArtifactRoot = 'C:\Users\Huma.Khan\Downloads\Cosmix_Erp_software\cosmix-backend\artifacts\attendance-huma-20261013-a01'
    }
}

function Assert-FreshSqlAbsence([string[]]$Instances, [string[]]$ExistingPaths) {
    $scope = Get-FreshSqlScope
    if ($Instances -icontains $scope.Instance) { throw 'Proposed isolated instance already exists; stop without reuse.' }
    if ($ExistingPaths.Count -gt 0) { throw 'Proposed isolated path already exists; stop without overwrite.' }
}

function Assert-FreshNoLinks([string]$Path) {
    for ($cursor = [IO.Path]::GetFullPath($Path); $cursor; $cursor = [IO.Path]::GetDirectoryName($cursor)) {
        if ((Test-Path -LiteralPath $cursor) -and
            ((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw 'Linked paths cannot be part of the isolated setup.'
        }
        if ([IO.Path]::GetDirectoryName($cursor) -eq $cursor) { break }
    }
}

function Get-FreshDatabaseCreationSql {
    # Parameters come from independently verified metadata of the NEW named instance.
    # This helper returns a review artifact; it has no SQL execution path.
    return @'
IF @ApprovedServer IS NULL OR @ApprovedOwner IS NULL
   OR CONVERT(nvarchar(128), SERVERPROPERTY('ServerName')) <> @ApprovedServer
   OR ORIGINAL_LOGIN() <> @ApprovedOwner OR DB_NAME() <> N'master'
    THROW 51000, 'Fresh isolated creation identity mismatch.', 1;
IF DB_ID(N'Cosmix_Testing') IS NOT NULL
    THROW 51001, 'Catalog already exists; preserve it and stop.', 1;
IF EXISTS (SELECT 1 FROM sys.master_files WHERE physical_name IN (
    N'C:\Users\Huma.Khan\AppData\Local\Cosmix\AttendanceAcceptance\Huma20261013A01\sql\Cosmix_Testing.mdf',
    N'C:\Users\Huma.Khan\AppData\Local\Cosmix\AttendanceAcceptance\Huma20261013A01\sql\Cosmix_Testing_log.ldf'))
    THROW 51002, 'Physical file is already attached; stop.', 1;
CREATE DATABASE [Cosmix_Testing]
ON PRIMARY (NAME=N'Cosmix_Testing', FILENAME=N'C:\Users\Huma.Khan\AppData\Local\Cosmix\AttendanceAcceptance\Huma20261013A01\sql\Cosmix_Testing.mdf')
LOG ON (NAME=N'Cosmix_Testing_log', FILENAME=N'C:\Users\Huma.Khan\AppData\Local\Cosmix\AttendanceAcceptance\Huma20261013A01\sql\Cosmix_Testing_log.ldf');
'@
}

function Get-FreshSqlPacket([string]$ScriptPath) {
    $scope = Get-FreshSqlScope
    if ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd('\') -ine $scope.Repository) { throw 'Wrong checkout.' }
    if ([Security.Principal.WindowsIdentity]::GetCurrent().Name -ine $scope.Owner) { throw 'Wrong Windows operator.' }
    $instances = @(& SqlLocalDB info | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    if ($LASTEXITCODE -ne 0) { throw 'LocalDB metadata inventory failed; no setup permitted.' }
    $paths = @($scope.PrivateRoot, $scope.DataFile, $scope.LogFile, $scope.ArtifactRoot)
    foreach ($path in $paths) { Assert-FreshNoLinks $path }
    $existing = @($paths | Where-Object { Test-Path -LiteralPath $_ })
    Assert-FreshSqlAbsence $instances $existing
    $migrationRoot = Join-Path $scope.Repository 'src\Cosmix.Infrastructure\Database\Migrations'
    $designers = @(Get-ChildItem -LiteralPath $migrationRoot -Filter '*.Designer.cs' | Sort-Object Name)
    $final = '20261012151917_AddAttendanceFinancialAttestationsAndLegalReviewEvidence'
    if ($designers.Count -ne 21 -or $designers[-1].BaseName -cne ($final + '.Designer')) { throw 'Migration chain changed; review required.' }
    $hashes = @($designers | ForEach-Object {
        foreach ($name in @($_.Name, $_.Name.Replace('.Designer.cs','.cs'))) {
            $file = Join-Path $migrationRoot $name
            [pscustomobject]@{ Path = 'src/Cosmix.Infrastructure/Database/Migrations/' + $name; Sha256 = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash }
        }
    })
    $snapshot = Join-Path $migrationRoot 'CosmixDbContextModelSnapshot.cs'
    $hashes += [pscustomobject]@{ Path = 'src/Cosmix.Infrastructure/Database/Migrations/CosmixDbContextModelSnapshot.cs'; Sha256 = (Get-FileHash -LiteralPath $snapshot -Algorithm SHA256).Hash }
    $sqlHash = $null
    if ($ScriptPath) {
        $file = [IO.Path]::GetFullPath($ScriptPath)
        $allowed = [IO.Path]::GetFullPath((Join-Path $scope.Repository ('docs\sql\' + $final + '.fresh-isolated.sql')))
        if ($file -ine $allowed) { throw 'Only the separately reviewed fresh full-chain artifact is allowed.' }
        Assert-FreshNoLinks $file
        $item = Get-Item -LiteralPath $file -Force
        if ($item.PSIsContainer -or $item.Length -le 0 -or $item.Length -gt 16777216) { throw 'Invalid migration artifact.' }
        $sqlHash = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash
    }
    return [pscustomobject]@{
        Mode = 'ReadOnlyPreparation'; Scope = $scope; ExistingInstancesUntouched = $instances
        NewInstanceAbsent = $true; NewPathsAbsent = $true; MigrationCount = $designers.Count
        FinalMigration = $final; SourceHashes = $hashes; FullChainSqlSha256 = $sqlHash
        CreationCommandsForReviewOnly = @('SqlLocalDB create CosmixAttendanceHuma20261013A01 17.0', 'SqlLocalDB start CosmixAttendanceHuma20261013A01')
        CreationSqlForReviewOnly = Get-FreshDatabaseCreationSql
        Routing = 'Capture ONLY that new instance pipe; verify SQL server/operator; creation uses master on that new instance only. Migrations, seeding and tests use Cosmix_Testing on that exact pipe with no alias fallback.'
        InitialFixtureBudget = 'Exactly the existing three synthetic .invalid authentication fixtures. Attendance fixtures require a bounded reviewed manifest. No real SMS/face service.'
        KeyringAndWorkers = 'Explicit isolated Data Protection directory. Only local OTP worker retained by fresh profile; all OTP/audit writes must be budgeted.'
        Cleanup = 'Stop the identity-verified owned host and restore temporary process environment; retain the disposable DB/files. No DROP, instance deletion, recursive deletion or reset.'
        ReadyForSqlExecution = $false
        MissingBeforeSqlApproval = @('Actual executable provisioning/configuration/secret-generation adapter review', 'Owned host/fixture manifest and binary hashes', 'Explicit human approval; actual new pipe/server/file checks after staged creation')
        SqlConnectionsOpened = 0; InstanceWrites = 0; DatabaseWrites = 0; PrivateWrites = 0
    }
}

if ($MyInvocation.InvocationName -ne '.') {
    Get-FreshSqlPacket $MigrationScriptPath | ConvertTo-Json -Depth 7
}
