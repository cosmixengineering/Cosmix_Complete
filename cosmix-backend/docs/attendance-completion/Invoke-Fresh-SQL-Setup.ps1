[CmdletBinding()]
param(
    [switch]$Execute,
    [string]$ApprovedPacketSha256,
    [string]$SqlApproval,
    [ValidateSet('SetupOnly')][string]$AfterSeed = 'SetupOnly'
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Prepare-Fresh-SQL-Packet.ps1')
$script:FreshExecutionContext = $null
$script:FreshNativeDirectoryType = $null

function Stop-FreshExecutor([string]$Stage) {
    throw ('Fresh setup refused: ' + $Stage + '; values suppressed.')
}

function Assert-FreshExecutorIntent([bool]$ExecutionRequested, [string]$ApprovedHash,
    [string]$CurrentHash, [string]$Operator, [string]$Checkout) {
    if (-not $ExecutionRequested) { Stop-FreshExecutor 'ExecutionFlag' }
    if ($ApprovedHash -cnotmatch '^[A-F0-9]{64}$' -or $CurrentHash -cnotmatch '^[A-F0-9]{64}$' -or
        $ApprovedHash -cne $CurrentHash) { Stop-FreshExecutor 'ApprovedPacketSha256' }
    $scope = Get-FreshSqlScope
    if ($Operator -ine $scope.Owner) { Stop-FreshExecutor 'Operator' }
    if ($Checkout -ine $scope.Repository) { Stop-FreshExecutor 'Checkout' }
    # Matching bytes is a drift guard, never proof of a human's SQL approval.
}

function Assert-FreshExecutorAbsence([string[]]$Instances, [string[]]$ExistingPaths) {
    try { Assert-FreshSqlAbsence $Instances $ExistingPaths }
    catch { Stop-FreshExecutor 'TargetAbsence' }
}

function Resolve-FreshExecutorPipe([Collections.IDictionary]$Metadata) {
    $scope = Get-FreshSqlScope
    if ($null -eq $Metadata -or $Metadata.Count -ne 4 -or
        $Metadata['Name'] -cne $scope.Instance -or $Metadata['Owner'] -ine $scope.Owner -or
        $Metadata['State'] -cne 'Running' -or
        $Metadata['InstancePipeName'] -inotmatch '^\\\\\.\\pipe\\LOCALDB#[A-F0-9]{8}\\tsql\\query$' -or
        $Metadata['InstancePipeName'] -imatch 'LOCALDB#842DD4FB') { Stop-FreshExecutor 'NewInstanceMetadata' }
    return 'np:' + $Metadata['InstancePipeName']
}

function Assert-FreshExecutorSqlIdentity([string]$Pipe, [string]$ActualServer,
    [string]$Operator, [string]$Catalog) {
    if ($Pipe -inotmatch '^np:\\\\\.\\pipe\\LOCALDB#([A-F0-9]{8})\\tsql\\query$') { Stop-FreshExecutor 'SqlIdentity' }
    $id = $Matches[1]
    if ($id -ieq '842DD4FB' -or $ActualServer -inotmatch '^[A-Z0-9][A-Z0-9._-]*\\LOCALDB#[A-F0-9]{8}$' -or
        -not $ActualServer.EndsWith(('\LOCALDB#' + $id), [StringComparison]::OrdinalIgnoreCase) -or
        $Operator -ine (Get-FreshSqlScope).Owner -or $Catalog -cnotin @('master','Cosmix_Testing')) {
        Stop-FreshExecutor 'SqlIdentity'
    }
}

function Get-FreshExecutorSqlArtifact {
    return Join-Path (Get-FreshSqlScope).Repository 'docs\sql\20261012151917_AddAttendanceFinancialAttestationsAndLegalReviewEvidence.fresh-isolated.sql'
}

function Get-FreshExecutorSqlcmdArguments([string]$Pipe, [string]$SqlPath) {
    if ($Pipe -inotmatch '^np:\\\\\.\\pipe\\LOCALDB#[A-F0-9]{8}\\tsql\\query$' -or
        $Pipe -imatch 'LOCALDB#842DD4FB') { Stop-FreshExecutor 'SqlIdentity' }
    if ($SqlPath -ine (Get-FreshExecutorSqlArtifact)) { Stop-FreshExecutor 'SqlArtifactPath' }
    return @('-S', $Pipe, '-d', 'Cosmix_Testing', '-E', '-C', '-b', '-X1', '-r1', '-t', '60', '-i', $SqlPath)
}

function Assert-FreshExecutorSqlText([string]$Sql) {
    # Conservative extra routing barrier; the reviewed immutable SQL hash remains mandatory.
    if ([string]::IsNullOrWhiteSpace($Sql) -or $Sql.Length -gt 16777216 -or
        $Sql -match '(?m)^\s*[:!]' -or $Sql.Contains('$(') -or
        $Sql -match '(?i)\bUSE\s+|\b(?:CREATE|ALTER|DROP)\s+DATABASE\b|\b(?:OPENROWSET|OPENDATASOURCE|OPENQUERY|LINKEDSERVER)\b') {
        Stop-FreshExecutor 'SqlRouting'
    }
}

function Assert-FreshExecutorHashes([object[]]$Expected, [object[]]$Actual) {
    if ($null -eq $Expected -or $Expected.Count -eq 0 -or $null -eq $Actual -or $Expected.Count -ne $Actual.Count) {
        Stop-FreshExecutor 'InputDrift'
    }
    $seen = @{}
    foreach ($entry in $Expected) {
        if ([string]::IsNullOrWhiteSpace($entry.Path) -or $seen.ContainsKey($entry.Path) -or
            $entry.Sha256 -cnotmatch '^[A-F0-9]{64}$') { Stop-FreshExecutor 'InputDrift' }
        $seen.Add($entry.Path, $entry.Sha256)
    }
    foreach ($entry in $Actual) {
        if (-not $seen.ContainsKey($entry.Path) -or $entry.Sha256 -cne $seen[$entry.Path]) { Stop-FreshExecutor 'InputDrift' }
        $seen.Remove($entry.Path)
    }
    if ($seen.Count -ne 0) { Stop-FreshExecutor 'InputDrift' }
}

function Get-FreshExecutorDigest([string]$Text) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($Text)))).Replace('-', '') }
    finally { $sha.Dispose() }
}

function New-FreshExecutorConfiguration([string]$Pipe, [string]$ActualServer, [Collections.IDictionary]$ExplicitValues) {
    Assert-FreshExecutorSqlIdentity $Pipe $ActualServer (Get-FreshSqlScope).Owner 'Cosmix_Testing'
    $names = @('JwtSigningKey','OtpVerifier','OtpEncryption','FilesKey','SnapshotKey')
    if ($null -eq $ExplicitValues -or $ExplicitValues.Count -ne $names.Count) { Stop-FreshExecutor 'ExplicitFreshCrypto' }
    $seen = @{}
    foreach ($name in $names) {
        $value = $ExplicitValues[$name]
        if ($value -isnot [string] -or $value.Length -ne 44) { Stop-FreshExecutor 'ExplicitFreshCrypto' }
        try { $bytes = [Convert]::FromBase64String($value) }
        catch { Stop-FreshExecutor 'ExplicitFreshCrypto' }
        if ($bytes.Length -ne 32 -or [Convert]::ToBase64String($bytes) -cne $value -or $seen.ContainsKey($value)) {
            Stop-FreshExecutor 'ExplicitFreshCrypto'
        }
        $seen.Add($value, $true)
    }
    # In-memory template only: explicit values are not evidence of fresh production entropy.
    # FilesKey/SnapshotKey bytes belong in CreateNew private files, never in this JSON.
    $scope = Get-FreshSqlScope
    return [ordered]@{
        ConnectionStrings = [ordered]@{
            CosmixDatabase = 'Data Source=' + $Pipe + ';Initial Catalog=Cosmix_Testing;Integrated Security=True;Encrypt=True;Trust Server Certificate=True;Pooling=False;Connect Timeout=15;Connect Retry Count=0;Application Name=Cosmix.HumaAttendanceIsolatedV1;'
        }
        LocalTesting = [ordered]@{
            Enabled = $true; Profile = $scope.Profile; PrivateDirectory = $scope.PrivateRoot
            Isolation = [ordered]@{
                InstanceName = $scope.Instance; Owner = $scope.Owner; PrivateRoot = $scope.PrivateRoot
                Pipe = $Pipe; ActualServerName = $ActualServer; DataProtectionDirectory = $scope.DataProtectionDirectory
            }
        }
        Jwt = [ordered]@{ Issuer = 'Cosmix.HumaAttendanceIsolatedV1'; Audience = 'Cosmix.HumaAttendanceIsolatedV1'; SigningKey = $ExplicitValues['JwtSigningKey'] }
        MobileAuthentication = [ordered]@{
            Enabled = $true; BusinessTimeZoneId = 'Pakistan Standard Time'
            OtpLifetimeSeconds = 300; ResendCooldownSeconds = 30; MaximumOtpAttempts = 5
            RateWindowSeconds = 300; RequestsPerPhone = 5; RequestsPerIp = 20; RequestsGlobal = 100
            VerificationAttemptsPerPhone = 10; VerificationAttemptsPerIp = 40; VerificationAttemptsGlobal = 200
            AccessTokenLifetimeSeconds = 300; RefreshTokenLifetimeSeconds = 3600; SessionLifetimeSeconds = 86400
            MinimumRequestDurationMilliseconds = 100
            Crypto = [ordered]@{
                KeyVersion = 'huma-20261013-a01'; VerifierKeyBase64 = $ExplicitValues['OtpVerifier']
                EncryptionKeyBase64 = $ExplicitValues['OtpEncryption']; MaxDeliveryLifetimeSeconds = 300
            }
            Sms = [ordered]@{ Provider = 'LocalTesting'; Endpoint = ''; BearerToken = ''; TimeoutSeconds = 10 }
        }
        WebAuthentication = [ordered]@{
            Enabled = $true; MinimumPasswordLength = 12; MaximumPasswordFailures = 5; PasswordLockoutSeconds = 300
            PasswordAttemptsPerAccount = 10; PasswordAttemptsPerEmail = 10; PasswordAttemptsPerIp = 40; PasswordAttemptsGlobal = 200
        }
        PrivateFiles = [ordered]@{ Directory = $scope.PrivateRoot + '\files'; KeyFile = $scope.PrivateRoot + '\keys\files.key'; KeyReference = 'huma-20261013-a01-files'; MaxSizeBytes = 10485760 }
        SelfHostedFace = [ordered]@{ Enabled = $false; SilentFace = [ordered]@{ Enabled = $false } }
        FaceCapture = [ordered]@{ Enabled = $false }
        FaceTemplateStorage = [ordered]@{ Enabled = $false }
        FaceStorageRetention = [ordered]@{ Enabled = $false }
        FaceStorageRetentionJournal = [ordered]@{ Enabled = $false }
    }
}

function Get-FreshExecutorStages {
    @(
        [pscustomobject]@{ Stage = 'ConfigurationProviderIsolation'; Implemented = $true; Operation = 'EmptyBuilder contract/source/binary guards and cleared child environment; setup-local-testing only.' }
        [pscustomobject]@{ Stage = 'PrivateRootAclAndManifest'; Implemented = $true; Operation = 'Absence/reparse recheck, atomic protected ACL directory/CreateNew file creation, retained private stage records.' }
        [pscustomobject]@{ Stage = 'NewInstanceCreation'; Implemented = $true; Operation = 'Create/start ONLY CosmixAttendanceHuma20261013A01 17.0; system directory originally absent.' }
        [pscustomobject]@{ Stage = 'NewInstanceMetadata'; Implemented = $true; Operation = 'After create: parse exact new instance info, ADO new-pipe master parameterized operator query; capture server and system files.' }
        [pscustomobject]@{ Stage = 'CatalogCreation'; Implemented = $true; Operation = 'Guarded parameterized master CREATE with private MDF/LDF; reverify target files/owner immediately.' }
        [pscustomobject]@{ Stage = 'FullChainMigration'; Implemented = $true; Operation = 'Rehash before installed sqlcmd exact new pipe/Cosmix_Testing/-E/-C/-b/-X1. No directives or target switching.' }
        [pscustomobject]@{ Stage = 'SchemaAndEmptyBusinessData'; Implemented = $true; Operation = 'Exact history/product versions/table inventory; every business table COUNT_BIG zero, exact owner/server/files, no triggers/synonyms/external dependencies.' }
        [pscustomobject]@{ Stage = 'FreshPrivateMaterial'; Implemented = $true; Operation = 'Five independent fresh crypto values; CreateNew protected settings/files.key/snapshot.key; isolated empty DP directory.' }
        [pscustomobject]@{ Stage = 'SetupOnlySeedAndRetention'; Implemented = $true; Operation = 'Existing fresh seed branch as bounded child, exact table/auth/private evidence budget; retain all state and records. No host start/resume/cleanup.' }
    )
}

function Assert-FreshExecutorReady([string]$HostSource) {
    foreach ($binding in @('WebApplication.CreateEmptyBuilder', 'ApplicationName = typeof(LocalTesting).Assembly',
        'EnvironmentName = Environments.Development', 'builder.WebHost.UseKestrel()', 'COSMIX_FRESH_TESTING_HOST',
        'Fresh local-testing requires its isolated configuration provider path.')) {
        if (-not $HostSource.Contains($binding)) { Stop-FreshExecutor 'ConfigurationProviderIsolation' }
    }
}

function Get-FreshManagedSystemDirectory {
    return 'C:\Users\Huma.Khan\AppData\Local\Microsoft\Microsoft SQL Server Local DB\Instances\CosmixAttendanceHuma20261013A01'
}

function ConvertFrom-FreshLocalDbInfo([string]$Text) {
    if ($Text.Length -gt 8192) { Stop-FreshExecutor 'NewInstanceMetadata' }
    $fields = @{}
    foreach ($line in ($Text -split '\r?\n')) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        if ($line -notmatch '^([^:]+):\s*(.*)$') { Stop-FreshExecutor 'NewInstanceMetadata' }
        $name = $Matches[1].Trim(); $value = $Matches[2].Trim()
        if ($fields.ContainsKey($name)) { Stop-FreshExecutor 'NewInstanceMetadata' }
        $fields.Add($name, $value)
    }
    foreach ($name in @('Name','Version','Shared name','Owner','Auto-create','State','Last start time','Instance pipe name')) {
        if (-not $fields.ContainsKey($name)) { Stop-FreshExecutor 'NewInstanceMetadata' }
    }
    if ($fields.Count -ne 8 -or $fields['Version'] -notmatch '^17\.0\.\d+\.\d+$' -or
        $fields['Shared name'] -ne '' -or $fields['Auto-create'] -cne 'No') { Stop-FreshExecutor 'NewInstanceMetadata' }
    $reportedPipe = $fields['Instance pipe name']
    if ($reportedPipe.StartsWith('np:', [StringComparison]::OrdinalIgnoreCase)) { $reportedPipe = $reportedPipe.Substring(3) }
    $pipe = Resolve-FreshExecutorPipe @{
        Name = $fields['Name']; Owner = $fields['Owner']; State = $fields['State']; InstancePipeName = $reportedPipe
    }
    return [pscustomobject]@{ Pipe = $pipe; Version = $fields['Version']; Owner = $fields['Owner']; Name = $fields['Name'] }
}

function New-FreshChildEnvironment {
    $profile = 'C:\Users\Huma.Khan'
    return [ordered]@{
        SystemRoot = 'C:\Windows'; WINDIR = 'C:\Windows'; COMSPEC = 'C:\Windows\System32\cmd.exe'
        PATH = 'C:\Windows\System32'; USERPROFILE = $profile; LOCALAPPDATA = $profile + '\AppData\Local'
        APPDATA = $profile + '\AppData\Roaming'; TEMP = (Get-FreshSqlScope).PrivateRoot + '\temp'; TMP = (Get-FreshSqlScope).PrivateRoot + '\temp'
        DOTNET_ROOT = 'C:\Program Files\dotnet'; DOTNET_MULTILEVEL_LOOKUP = '0'; DOTNET_CLI_TELEMETRY_OPTOUT = '1'
        COSMIX_FRESH_TESTING_HOST = 'HumaAttendanceIsolatedV1'; COSMIX_LOCAL_TESTING_SETTINGS = (Get-FreshSqlScope).Settings
    }
}

function Assert-FreshOwnedPath([string]$Path, [switch]$AllowRoot) {
    $root = (Get-FreshSqlScope).PrivateRoot
    if ([string]::IsNullOrWhiteSpace($Path) -or -not [IO.Path]::IsPathRooted($Path) -or $Path.Substring(2).Contains(':')) {
        Stop-FreshExecutor 'PrivatePathScope'
    }
    $full = [IO.Path]::GetFullPath($Path).TrimEnd('\')
    if ($full -ine $Path.TrimEnd('\') -or
        (-not $full.StartsWith($root + '\', [StringComparison]::OrdinalIgnoreCase) -and -not ($AllowRoot -and $full -ieq $root))) {
        Stop-FreshExecutor 'PrivatePathScope'
    }
}

function Get-FreshFixtureBudget {
    $repo = (Get-FreshSqlScope).Repository
    $owner = [IO.File]::ReadAllText((Join-Path $repo 'src\Cosmix.Application\Authentication\Bootstrap\OwnerBootstrapService.cs'))
    $seed = [IO.File]::ReadAllText((Join-Path $repo 'src\Cosmix.Api\Development\LocalTestingSetup.cs'))
    $permissionMatch = [regex]::Match($owner, '(?s)FixedPermissionCodes.*?new\[\]\s*\{(.*?)\}\);')
    $hrMatch = [regex]::Match($seed, '(?s)var hrCodes = new HashSet<string>\(StringComparer.Ordinal\)\s*\{(.*?)\};')
    if (-not $permissionMatch.Success -or -not $hrMatch.Success) { Stop-FreshExecutor 'FixtureSourceContract' }
    $permissions = @([regex]::Matches($permissionMatch.Groups[1].Value, '"([a-z][a-z.]+)"') | ForEach-Object { $_.Groups[1].Value })
    $hr = @([regex]::Matches($hrMatch.Groups[1].Value, '"([a-z][a-z.]+)"') | ForEach-Object { $_.Groups[1].Value })
    if ($permissions.Count -ne 39 -or $hr.Count -ne 14 -or @($hr | Where-Object { $_ -cnotin $permissions }).Count -ne 0) {
        Stop-FreshExecutor 'FixtureSourceContract'
    }
    foreach ($operation in @('CreateEmployee(0, false)', 'AddEmployeeAccount(1,', 'AddEmployeeAccount(2,',
        'Role.CreateSystem(SystemRoleKind.SuperAdmin', 'Role.CreateSystem(SystemRoleKind.Admin', 'new Role("HR"',
        'new AuditEvent("development.fixture.seeded"', 'FileMode.CreateNew', 'FreshLocalTestingDatabase.VerifyAsync',
        'InEmptyDatabaseTransactionAsync')) {
        if (-not $seed.Contains($operation)) { Stop-FreshExecutor 'FixtureSourceContract' }
    }
    return [pscustomobject]@{
        Tables = [ordered]@{
            'hr.Employees' = 3; 'hr.EmployeeSubmissions' = 3; 'hr.EmployeeOnboardingDecisions' = 3
            'auth.UserAccounts' = 3; 'auth.Roles' = 3; 'auth.Permissions' = $permissions.Count
            'auth.RolePermissions' = 2 * $permissions.Count + $hr.Count
            'auth.UserRoleAssignments' = 3; 'auth.UserAccessScopes' = 3; 'auth.LoginPhoneBindings' = 3; 'audit.AuditEvents' = 1
        }
        PermissionCodes = $permissions; HrPermissionCodes = $hr
        PrivateObjects = [ordered]@{ BootstrapSnapshots = 1; EncryptedSubmissionFiles = 2; CredentialsFiles = 1; DataProtectionKeysDuringSetup = 0 }
        Source = 'Existing LocalTestingSetup body: one owner, two AddEmployeeAccount calls; each Provision adds one role assignment/scope/binding; two fixed roles get all enumerated permissions, HR gets explicit subset; one audit event.'
    }
}

function ConvertTo-FreshWindowsArgument([string]$Value) {
    # CommandLineToArgvW quoting; never shell-evaluate arguments.
    return '"' + [regex]::Replace([regex]::Replace($Value, '(\\*)"', '$1$1\"'), '(\\+)$', '$1$1') + '"'
}

function Assert-FreshChildInvocation([string]$Kind, [string[]]$Arguments, [string]$Pipe, [string]$ApiAssembly) {
    $scope = Get-FreshSqlScope
    $joined = $Arguments -join "`n"
    if ($Kind -ceq 'LocalDb') {
        $allowed = @('info', ('info' + "`n" + $scope.Instance), ('start' + "`n" + $scope.Instance), ('create' + "`n" + $scope.Instance + "`n17.0"))
        if ($joined -cnotin $allowed) { Stop-FreshExecutor 'ChildArguments' }
    } elseif ($Kind -ceq 'Dotnet') {
        $expected = Join-Path $scope.Repository 'src\Cosmix.Api\bin\Release\net10.0\Cosmix.Api.dll'
        if ($ApiAssembly -cne $expected -or $Arguments.Count -ne 2 -or $Arguments[0] -cne $expected -or $Arguments[1] -cne 'setup-local-testing') { Stop-FreshExecutor 'ChildArguments' }
    } elseif ($Kind -ceq 'Sqlcmd') {
        if ($joined -cne ((Get-FreshExecutorSqlcmdArguments $Pipe (Get-FreshExecutorSqlArtifact)) -join "`n")) { Stop-FreshExecutor 'ChildArguments' }
    } else { Stop-FreshExecutor 'ChildExecutable' }
}

function Assert-FreshApiBinary([byte[]]$Bytes) {
    $names = [Text.Encoding]::UTF8.GetString($Bytes)
    foreach ($name in @('CreateEmptyBuilder','UseKestrel')) {
        if (-not $names.Contains($name)) { Stop-FreshExecutor 'ApiReleaseBuildRequired' }
    }
    # User-string heap UTF16 starts can have either parity; do not assume even alignment.
    if (-not $names.Replace([string][char]0,'').Contains('Fresh local-testing requires its isolated configuration provider path.')) { Stop-FreshExecutor 'ApiReleaseBuildRequired' }
}

function Assert-FreshExecutionContext($Context) {
    if ($null -eq $Context -or $null -eq $script:FreshExecutionContext -or
        -not [object]::ReferenceEquals($Context, $script:FreshExecutionContext) -or -not $Context.Authorized) {
        Stop-FreshExecutor 'ExecutionContext'
    }
}

function Get-FreshPrivateAcl([bool]$Directory) {
    $sid = [Security.Principal.WindowsIdentity]::GetCurrent().User
    if ($Directory) { $acl = [Security.AccessControl.DirectorySecurity]::new() }
    else { $acl = [Security.AccessControl.FileSecurity]::new() }
    $acl.SetOwner($sid); $acl.SetAccessRuleProtection($true, $false)
    foreach ($identity in @($sid.Value, 'S-1-5-18', 'S-1-5-32-544')) {
        $inherit = [Security.AccessControl.InheritanceFlags]::None
        if ($Directory) { $inherit = [Security.AccessControl.InheritanceFlags]'ContainerInherit, ObjectInherit' }
        $rule = [Security.AccessControl.FileSystemAccessRule]::new([Security.Principal.SecurityIdentifier]::new($identity),
            [Security.AccessControl.FileSystemRights]::FullControl, $inherit,
            [Security.AccessControl.PropagationFlags]::None, [Security.AccessControl.AccessControlType]::Allow)
        $acl.AddAccessRule($rule)
    }
    return $acl
}

function Assert-FreshPrivateAcl([string]$Path) {
    Assert-FreshNoLinks $Path
    $item = Get-Item -LiteralPath $Path -Force
    $acl = Get-Acl -LiteralPath $Path
    $user = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    if ($acl.GetOwner([Security.Principal.SecurityIdentifier]).Value -cne $user) { Stop-FreshExecutor 'PrivateAcl' }
    $allowed = @($user,'S-1-5-18','S-1-5-32-544'); $seen = @{}
    foreach ($rule in $acl.GetAccessRules($true, $true, [Security.Principal.SecurityIdentifier])) {
        if ($rule.IdentityReference.Value -cnotin $allowed -or $rule.AccessControlType -ne 'Allow' -or
            $rule.FileSystemRights -ne [Security.AccessControl.FileSystemRights]::FullControl) { Stop-FreshExecutor 'PrivateAcl' }
        $seen[$rule.IdentityReference.Value] = $true
    }
    if ($seen.Count -ne 3) { Stop-FreshExecutor 'PrivateAcl' }
}

function Get-FreshNativeDirectoryType {
    if ($null -eq $script:FreshNativeDirectoryType) {
        # In-memory P/Invoke declaration: no compiler, emitted file or dependency install.
        $name = [Reflection.AssemblyName]::new('Cosmix.FreshSetup.NativeDirectory')
        $assembly = [AppDomain]::CurrentDomain.DefineDynamicAssembly($name, [Reflection.Emit.AssemblyBuilderAccess]::Run)
        $module = $assembly.DefineDynamicModule('Native')
        $builder = $module.DefineType('CosmixFreshDirectory', [Reflection.TypeAttributes]'Public, Abstract, Sealed')
        $method = $builder.DefinePInvokeMethod('CreateDirectory', 'kernel32.dll', 'CreateDirectoryW',
            [Reflection.MethodAttributes]'Public, Static, PinvokeImpl', [Reflection.CallingConventions]::Standard,
            [bool], [type[]]@([string],[IntPtr]), [Runtime.InteropServices.CallingConvention]::Winapi, [Runtime.InteropServices.CharSet]::Unicode)
        $method.SetImplementationFlags($method.GetMethodImplementationFlags() -bor [Reflection.MethodImplAttributes]::PreserveSig)
        $script:FreshNativeDirectoryType = $builder.CreateType()
    }
    return $script:FreshNativeDirectoryType
}

function New-FreshPrivateDirectory($Context, [string]$Path, [switch]$Parent) {
    Assert-FreshExecutionContext $Context
    $scope = Get-FreshSqlScope
    if ($Parent) {
        if ($Path -cnotin @('C:\Users\Huma.Khan\AppData\Local\Cosmix', 'C:\Users\Huma.Khan\AppData\Local\Cosmix\AttendanceAcceptance')) { Stop-FreshExecutor 'PrivatePathScope' }
    } else { Assert-FreshOwnedPath $Path -AllowRoot }
    Assert-FreshNoLinks $Path
    if (Test-Path -LiteralPath $Path) { Stop-FreshExecutor 'TargetAbsence' }
    if (-not (Test-Path -LiteralPath ([IO.Path]::GetDirectoryName($Path)) -PathType Container)) { Stop-FreshExecutor 'PrivateParent' }
    # CreateDirectoryW fails on an existing directory and applies the protected ACL atomically.
    $native = Get-FreshNativeDirectoryType
    $descriptor = (Get-FreshPrivateAcl $true).GetSecurityDescriptorBinaryForm()
    $descriptorPointer = [Runtime.InteropServices.Marshal]::AllocHGlobal($descriptor.Length)
    $attributesPointer = [Runtime.InteropServices.Marshal]::AllocHGlobal(24)
    try {
        [Runtime.InteropServices.Marshal]::Copy($descriptor,0,$descriptorPointer,$descriptor.Length)
        [Runtime.InteropServices.Marshal]::Copy(([byte[]]::new(24)),0,$attributesPointer,24)
        [Runtime.InteropServices.Marshal]::WriteInt32($attributesPointer,0,24)
        [Runtime.InteropServices.Marshal]::WriteIntPtr($attributesPointer,8,$descriptorPointer)
        if (-not $native::CreateDirectory($Path,$attributesPointer)) { Stop-FreshExecutor 'PrivateDirectoryCreation' }
    } finally {
        [Runtime.InteropServices.Marshal]::FreeHGlobal($attributesPointer)
        [Runtime.InteropServices.Marshal]::FreeHGlobal($descriptorPointer)
    }
    Assert-FreshPrivateAcl $Path
}

function Write-FreshPrivateBytes($Context, [string]$Path, [byte[]]$Bytes) {
    Assert-FreshExecutionContext $Context
    Assert-FreshOwnedPath $Path
    Assert-FreshNoLinks $Path
    if (Test-Path -LiteralPath $Path) { Stop-FreshExecutor 'TargetAbsence' }
    Assert-FreshPrivateAcl ([IO.Path]::GetDirectoryName($Path))
    if ($null -eq $Bytes -or $Bytes.Length -eq 0 -or $Bytes.Length -gt 4194304) { Stop-FreshExecutor 'PrivateWriteBound' }
    $file = [IO.FileStream]::new($Path, [IO.FileMode]::CreateNew, [Security.AccessControl.FileSystemRights]::Write,
        [IO.FileShare]::None, 4096, [IO.FileOptions]::WriteThrough, (Get-FreshPrivateAcl $false))
    try { $file.Write($Bytes, 0, $Bytes.Length); $file.Flush($true) }
    finally { $file.Dispose() }
    Assert-FreshPrivateAcl $Path
    $saved = [IO.File]::ReadAllBytes($Path)
    try {
        if ($saved.Length -ne $Bytes.Length) { Stop-FreshExecutor 'PrivateWriteReadback' }
        $difference = 0
        for ($i = 0; $i -lt $saved.Length; $i++) { $difference = $difference -bor ($saved[$i] -bxor $Bytes[$i]) }
        if ($difference -ne 0) { Stop-FreshExecutor 'PrivateWriteReadback' }
    } finally { [Array]::Clear($saved,0,$saved.Length) }
}

function Write-FreshStageRecord($Context, [string]$Stage, [string]$Status, $Evidence) {
    Assert-FreshExecutionContext $Context
    $Context.RecordNumber++
    $record = [ordered]@{ Stage = $Stage; Status = $Status; PacketSha256 = $Context.Review.PacketSha256; Utc = [DateTime]::UtcNow.ToString('o'); Evidence = $Evidence }
    $path = (Get-FreshSqlScope).PrivateRoot + ('\manifest\{0:D3}-{1}-{2}.json' -f $Context.RecordNumber, $Stage, $Status)
    Write-FreshPrivateBytes $Context $path ([Text.Encoding]::UTF8.GetBytes(($record | ConvertTo-Json -Depth 16)))
}

function New-FreshCryptoValues($Context) {
    Assert-FreshExecutionContext $Context
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    $values = @{}
    try {
        foreach ($name in @('JwtSigningKey','OtpVerifier','OtpEncryption','FilesKey','SnapshotKey')) {
            $bytes = [byte[]]::new(32)
            try { $rng.GetBytes($bytes); $values.Add($name, [Convert]::ToBase64String($bytes)) }
            finally { [Array]::Clear($bytes, 0, $bytes.Length) }
        }
    } finally { $rng.Dispose() }
    return $values
}

function Invoke-FreshChild($Context, [string]$Executable, [string[]]$Arguments, [switch]$CaptureMetadata) {
    Assert-FreshExecutionContext $Context
    $tools = $Context.Review.Packet.Tools
    if ($Executable -cnotin @($tools.LocalDb,$tools.Sqlcmd,$tools.Dotnet)) { Stop-FreshExecutor 'ChildExecutable' }
    $kind = 'LocalDb'
    if ($Executable -ceq $tools.Sqlcmd) { $kind = 'Sqlcmd' }
    if ($Executable -ceq $tools.Dotnet) { $kind = 'Dotnet' }
    Assert-FreshChildInvocation $kind $Arguments $Context.Pipe $Context.Review.Packet.ApiAssembly
    if ($CaptureMetadata -and ($kind -cne 'LocalDb' -or $Arguments[0] -cne 'info')) { Stop-FreshExecutor 'ChildArguments' }
    $info = [Diagnostics.ProcessStartInfo]::new()
    $info.FileName = $Executable; $info.Arguments = (@($Arguments | ForEach-Object { ConvertTo-FreshWindowsArgument $_ }) -join ' ')
    $info.UseShellExecute = $false; $info.CreateNoWindow = $true
    $info.WorkingDirectory = $Context.Review.Packet.SourceWorkingApiContentDir
    $info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true
    $info.EnvironmentVariables.Clear()
    foreach ($entry in (New-FreshChildEnvironment).GetEnumerator()) { $info.EnvironmentVariables.Add($entry.Key, $entry.Value) }
    $process = [Diagnostics.Process]::new(); $process.StartInfo = $info
    $outBuffer = [byte[]]::new(4096); $errBuffer = [byte[]]::new(4096)
    $capture = [IO.MemoryStream]::new(); $total = 0
    try {
        if (-not $process.Start()) { Stop-FreshExecutor 'ChildStart' }
        $outStream = $process.StandardOutput.BaseStream; $errStream = $process.StandardError.BaseStream
        $outRead = $outStream.ReadAsync($outBuffer, 0, $outBuffer.Length)
        $errRead = $errStream.ReadAsync($errBuffer, 0, $errBuffer.Length)
        $timer = [Diagnostics.Stopwatch]::StartNew()
        while ($null -ne $outRead -or $null -ne $errRead -or -not $process.HasExited) {
            if ($timer.Elapsed.TotalSeconds -gt 180) { Stop-FreshExecutor 'ChildTimeout' }
            if ($null -ne $outRead -and $outRead.IsCompleted) {
                $count = $outRead.GetAwaiter().GetResult(); $total += $count
                if ($CaptureMetadata -and $count -gt 0) { $capture.Write($outBuffer, 0, $count) }
                if ($count -eq 0) { $outRead = $null } else { $outRead = $outStream.ReadAsync($outBuffer, 0, $outBuffer.Length) }
            }
            if ($null -ne $errRead -and $errRead.IsCompleted) {
                $count = $errRead.GetAwaiter().GetResult(); $total += $count
                if ($count -eq 0) { $errRead = $null } else { $errRead = $errStream.ReadAsync($errBuffer, 0, $errBuffer.Length) }
            }
            if ($total -gt 65536 -or $capture.Length -gt 8192) { Stop-FreshExecutor 'ChildOutputBound' }
            [Threading.Thread]::Sleep(20)
        }
        if ($process.ExitCode -ne 0) { Stop-FreshExecutor 'ChildExitCode' }
        if ($CaptureMetadata) { return [Text.Encoding]::UTF8.GetString($capture.ToArray()) }
    } finally {
        # Only the Process object just started here may be terminated. No instance/host stop commands.
        try { if ($process.Id -gt 0 -and -not $process.HasExited) { $process.Kill(); [void]$process.WaitForExit(5000) } } catch { }
        $process.Dispose(); $capture.Dispose()
        [Array]::Clear($outBuffer,0,$outBuffer.Length); [Array]::Clear($errBuffer,0,$errBuffer.Length)
        $info.EnvironmentVariables.Clear()
    }
}

function New-FreshSqlConnection($Context, [string]$Pipe, [string]$Catalog) {
    Assert-FreshExecutionContext $Context
    if ($Pipe -cne $Context.Pipe -or $Catalog -cnotin @('master','Cosmix_Testing')) { Stop-FreshExecutor 'SqlIdentity' }
    # The pipe was captured only from the successfully created NEW instance.
    if ($Pipe -inotmatch '^np:\\\\\.\\pipe\\LOCALDB#[A-F0-9]{8}\\tsql\\query$' -or $Pipe -imatch 'LOCALDB#842DD4FB') { Stop-FreshExecutor 'SqlIdentity' }
    $connection = [System.Data.SqlClient.SqlConnection]::new('Data Source=' + $Pipe + ';Initial Catalog=' + $Catalog + ';Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Pooling=False;Connect Timeout=15;ConnectRetryCount=0;Application Name=Cosmix.FreshSetup;')
    try { $connection.Open(); return $connection } catch { $connection.Dispose(); Stop-FreshExecutor 'SqlConnection' }
}

function Invoke-FreshSqlRead($Context, $Connection, [string]$Sql, [Collections.IDictionary]$Parameters) {
    Assert-FreshExecutionContext $Context
    if ($Connection.DataSource -cne $Context.Pipe -or $Connection.Database -cnotin @('master','Cosmix_Testing') -or $Connection.State -ne 'Open') { Stop-FreshExecutor 'SqlIdentity' }
    $command = $Connection.CreateCommand(); $command.CommandText = $Sql; $command.CommandTimeout = 30
    try {
        if ($null -ne $Parameters) {
            foreach ($entry in $Parameters.GetEnumerator()) {
                $parameter = $command.Parameters.Add($entry.Key, [Data.SqlDbType]::NVarChar, 256); $parameter.Value = $entry.Value
            }
        }
        $reader = $command.ExecuteReader()
        try {
            $rows = [Collections.Generic.List[object]]::new()
            while ($reader.Read()) {
                if ($rows.Count -ge 512) { Stop-FreshExecutor 'SqlResultBound' }
                $row = @{}
                for ($i = 0; $i -lt $reader.FieldCount; $i++) { $row.Add($reader.GetName($i), $reader.GetValue($i)) }
                $rows.Add($row)
            }
            return $rows.ToArray()
        } finally { $reader.Dispose() }
    } finally { $command.Dispose() }
}

function Assert-FreshPhysicalFileMetadata([object[]]$Files) {
    $scope = Get-FreshSqlScope
    if ($Files.Count -ne 2 -or $Files[0].Path -ine $scope.DataFile -or $Files[0].FileType -ne 0 -or
        $Files[1].Path -ine $scope.LogFile -or $Files[1].FileType -ne 1) { Stop-FreshExecutor 'PhysicalFiles' }
}

function Assert-FreshBusinessRowCount([string]$Table, [long]$Count, [Collections.IDictionary]$Budget, [bool]$AfterSeed) {
    $expected = 0
    if ($AfterSeed -and $Budget.Contains($Table)) { $expected = $Budget[$Table] }
    if ($Count -ne $expected) { Stop-FreshExecutor 'BusinessRowBudget' }
}

function Assert-FreshLiveDatabase($Context, $Connection, [string]$Catalog) {
    $scope = Get-FreshSqlScope
    $rows = @(Invoke-FreshSqlRead $Context $Connection @'
SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) AS ServerName,
       DB_NAME() AS Catalog, ORIGINAL_LOGIN() AS OperatorName, SUSER_SNAME(d.owner_sid) AS OwnerName,
       d.is_read_only AS ReadOnly,
       (SELECT COUNT(*) FROM sys.synonyms) AS Synonyms,
       (SELECT COUNT(*) FROM sys.sql_expression_dependencies WHERE referenced_server_name IS NOT NULL OR referenced_database_name IS NOT NULL) AS ExternalDependencies,
       (SELECT COUNT(*) FROM sys.triggers WHERE is_ms_shipped=0) AS UserTriggers
FROM sys.databases d WHERE d.database_id=DB_ID() AND ORIGINAL_LOGIN()=@ApprovedOwner AND DB_NAME()=@Catalog;
'@ @{ '@ApprovedOwner' = $scope.Owner; '@Catalog' = $Catalog })
    if ($rows.Count -ne 1) { Stop-FreshExecutor 'SqlIdentity' }
    $row = $rows[0]
    Assert-FreshExecutorSqlIdentity $Context.Pipe $row.ServerName $row.OperatorName $row.Catalog
    if ($Context.Server -and $row.ServerName -ine $Context.Server) { Stop-FreshExecutor 'SqlIdentity' }
    if ($row.ReadOnly) { Stop-FreshExecutor 'SqlIdentity' }
    if ($Catalog -ceq 'Cosmix_Testing') {
        if ($row.OwnerName -ine $scope.Owner) { Stop-FreshExecutor 'SqlIdentity' }
        if ($row.Synonyms -ne 0 -or $row.ExternalDependencies -ne 0 -or $row.UserTriggers -ne 0) { Stop-FreshExecutor 'IndirectDependencies' }
        $files = @(Invoke-FreshSqlRead $Context $Connection 'SELECT physical_name AS Path, type AS FileType FROM sys.database_files ORDER BY file_id;' @{})
        Assert-FreshPhysicalFileMetadata $files
        Assert-FreshPrivateAcl $scope.DataFile; Assert-FreshPrivateAcl $scope.LogFile
    }
    return $row.ServerName
}

function Get-FreshSchemaContract {
    $sql = [IO.File]::ReadAllText((Get-FreshExecutorSqlArtifact))
    Assert-FreshExecutorSqlText $sql
    $tables = @([regex]::Matches($sql, '(?m)^\s*CREATE TABLE \[(?:(?<schema>[^\]]+)\]\.\[)?(?<name>[^\]]+)\]') | ForEach-Object {
        $schema = $_.Groups['schema'].Value; if (-not $schema) { $schema = 'dbo' }
        $schema + '.' + $_.Groups['name'].Value
    } | Sort-Object -Unique)
    $history = @([regex]::Matches($sql, "VALUES \(N'(\d{14}_[^']+)', N'([^']+)'\)") | ForEach-Object {
        [pscustomobject]@{ MigrationId = $_.Groups[1].Value; ProductVersion = $_.Groups[2].Value }
    })
    if ($history.Count -ne 21 -or $tables.Count -lt 126 -or $history[-1].MigrationId -cne '20261012151917_AddAttendanceFinancialAttestationsAndLegalReviewEvidence') { Stop-FreshExecutor 'SchemaContract' }
    return [pscustomobject]@{ Tables = $tables; History = $history }
}

function Assert-FreshSchemaAndData($Context, $Connection, [switch]$AfterSeed, [switch]$BeforeMigration) {
    [void](Assert-FreshLiveDatabase $Context $Connection 'Cosmix_Testing')
    $tables = @(Invoke-FreshSqlRead $Context $Connection 'SELECT s.name AS SchemaName,t.name AS TableName FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE t.is_ms_shipped=0 ORDER BY s.name,t.name;' @{})
    if ($BeforeMigration) {
        if ($tables.Count -ne 0) { Stop-FreshExecutor 'CatalogNotEmpty' }
        return
    }
    $contract = $Context.Review.Packet.SchemaContract
    $names = @($tables | ForEach-Object { $_.SchemaName + '.' + $_.TableName } | Sort-Object)
    if (($names -join '|') -cne ($contract.Tables -join '|')) { Stop-FreshExecutor 'SchemaInventory' }
    $history = @(Invoke-FreshSqlRead $Context $Connection 'SELECT MigrationId,ProductVersion FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;' @{})
    if ($history.Count -ne $contract.History.Count) { Stop-FreshExecutor 'MigrationHistory' }
    for ($i = 0; $i -lt $history.Count; $i++) {
        if ($history[$i].MigrationId -cne $contract.History[$i].MigrationId -or $history[$i].ProductVersion -cne $contract.History[$i].ProductVersion) { Stop-FreshExecutor 'MigrationHistory' }
    }
    $budget = $Context.Review.Packet.FixtureBudget.Tables
    foreach ($table in $tables) {
        $name = $table.SchemaName + '.' + $table.TableName
        if ($name -ceq 'dbo.__EFMigrationsHistory') { continue }
        $quoted = '[' + $table.SchemaName.Replace(']',']]') + '].[' + $table.TableName.Replace(']',']]') + ']'
        $count = @(Invoke-FreshSqlRead $Context $Connection ('SELECT COUNT_BIG(*) AS RowCount FROM ' + $quoted + ';') @{})
        if ($count.Count -ne 1) { Stop-FreshExecutor 'BusinessRowBudget' }
        Assert-FreshBusinessRowCount $name $count[0].RowCount $budget ([bool]$AfterSeed)
    }
    if ($AfterSeed) {
        $accounts = @(Invoke-FreshSqlRead $Context $Connection @'
SELECT a.LoginName,e.FullLegalName,e.ApprovedLoginPhoneNumber,
       CASE WHEN a.PasswordHash IS NOT NULL AND a.PasswordHashFormat=N'Microsoft.AspNetCore.Identity.PasswordHasherV3' AND a.WebAccessEnabled=1 AND a.MfaRequired=1 THEN 1 ELSE 0 END AS CredentialReady
FROM auth.UserAccounts a JOIN hr.Employees e ON e.Id=a.EmployeeId ORDER BY a.LoginName;
'@ @{})
        $expected = @(
            @('admin@example.invalid','TEST ONLY Admin','+12025550102'),
            @('hr@example.invalid','TEST ONLY HR','+12025550103'),
            @('superadmin@example.invalid','TEST ONLY SuperAdmin','+12025550101')
        )
        if ($accounts.Count -ne 3) { Stop-FreshExecutor 'FixtureIdentity' }
        for ($i = 0; $i -lt 3; $i++) {
            if ($accounts[$i].LoginName -cne $expected[$i][0] -or $accounts[$i].FullLegalName -cne $expected[$i][1] -or
                $accounts[$i].ApprovedLoginPhoneNumber -cne $expected[$i][2] -or $accounts[$i].CredentialReady -ne 1) { Stop-FreshExecutor 'FixtureIdentity' }
        }
        $permissions = @(Invoke-FreshSqlRead $Context $Connection 'SELECT Code FROM auth.Permissions ORDER BY Code;' @{})
        if ((@($permissions | ForEach-Object { $_.Code }) -join '|') -cne (@($Context.Review.Packet.FixtureBudget.PermissionCodes | Sort-Object) -join '|')) { Stop-FreshExecutor 'FixturePermissions' }
    }
}

function Assert-FreshInputSnapshot($Context) {
    Assert-FreshExecutionContext $Context
    Assert-FreshExecutorHashes $Context.Review.Packet.InputHashes @(Get-FreshExecutorInputHashes)
    if ((Get-FileHash -LiteralPath (Get-FreshExecutorSqlArtifact) -Algorithm SHA256).Hash -cne $Context.Review.Packet.FullChainSqlSha256) { Stop-FreshExecutor 'ReviewedSqlDrift' }
}

function Assert-FreshSeedPrivateObjects($Context) {
    Assert-FreshExecutionContext $Context
    $root = (Get-FreshSqlScope).PrivateRoot
    foreach ($entry in @(@('snapshots',1,'^[a-f0-9]{32}\.bootstrap\.enc$'), @('files',2,'^[a-f0-9]{32}\.enc$'), @('keys\data-protection',0,''))) {
        $items = @(Get-ChildItem -LiteralPath ($root + '\' + $entry[0]) -Force)
        if ($items.Count -ne $entry[1]) { Stop-FreshExecutor 'PrivateObjectBudget' }
        foreach ($item in $items) {
            if ($item.PSIsContainer -or $item.Name -cnotmatch $entry[2] -or $item.Length -lt 32) { Stop-FreshExecutor 'PrivateObjectBudget' }
            Assert-FreshPrivateAcl $item.FullName
        }
    }
    $credentials = $root + '\Test-Accounts.json'
    Assert-FreshPrivateAcl $credentials
    if ((Get-Item -LiteralPath $credentials).Length -gt 8192) { Stop-FreshExecutor 'PrivateObjectBudget' }
    $accounts = @([IO.File]::ReadAllText($credentials) | ConvertFrom-Json)
    try {
        if ($accounts.Count -ne 3) { Stop-FreshExecutor 'PrivateObjectBudget' }
        $emails = @($accounts | ForEach-Object { $_.Email } | Sort-Object)
        if (($emails -join '|') -cne 'admin@example.invalid|hr@example.invalid|superadmin@example.invalid') { Stop-FreshExecutor 'PrivateObjectBudget' }
        $passwords = @{}
        foreach ($account in $accounts) {
            if ($account.Password -isnot [string] -or $account.Password -cnotmatch '^[A-Za-z0-9+/]{32}$' -or $passwords.ContainsKey($account.Password)) { Stop-FreshExecutor 'PrivateObjectBudget' }
            $passwords.Add($account.Password,$true)
        }
    } finally { $accounts = $null; $passwords = $null }
}

function Invoke-FreshExecutorExecution($Context) {
    Assert-FreshExecutionContext $Context
    $scope = Get-FreshSqlScope; $tools = $Context.Review.Packet.Tools
    $stage = 'PrivateRootAclAndManifest'; $canRecord = $false; $connection = $null; $values = $null
    try {
        Assert-FreshInputSnapshot $Context
        # Repeat original absence immediately before the first write, not only at review time.
        $absence = Get-FreshSqlPacket (Get-FreshExecutorSqlArtifact)
        $managed = Get-FreshManagedSystemDirectory; Assert-FreshNoLinks $managed
        Assert-FreshExecutorAbsence @() @($managed | Where-Object { Test-Path -LiteralPath $_ })
        foreach ($parent in $Context.Review.Packet.NewPrivateParents) { New-FreshPrivateDirectory $Context $parent -Parent }
        New-FreshPrivateDirectory $Context $scope.PrivateRoot
        foreach ($relative in @('manifest','results','sql','config','keys','keys\data-protection','snapshots','files','metadata','receipts','exports','temp')) {
            New-FreshPrivateDirectory $Context ($scope.PrivateRoot + '\' + $relative)
        }
        $canRecord = $true
        Write-FreshStageRecord $Context $stage 'Complete' @{ ReviewedPacket = $Context.Review.Packet; CreatedParents = $Context.Review.Packet.NewPrivateParents }

        $stage = 'NewInstanceCreation'; Assert-FreshInputSnapshot $Context
        $names = Invoke-FreshChild $Context $tools.LocalDb @('info') -CaptureMetadata
        $instances = @($names -split '\r?\n' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
        Assert-FreshExecutorAbsence $instances @($managed | Where-Object { Test-Path -LiteralPath $_ })
        Assert-FreshNoLinks $managed
        Write-FreshStageRecord $Context $stage 'Started' @{}
        Invoke-FreshChild $Context $tools.LocalDb @('create',$scope.Instance,'17.0')
        Invoke-FreshChild $Context $tools.LocalDb @('start',$scope.Instance)
        Write-FreshStageRecord $Context $stage 'Complete' @{}

        $stage = 'NewInstanceMetadata'
        $info = Invoke-FreshChild $Context $tools.LocalDb @('info',$scope.Instance) -CaptureMetadata
        $metadata = ConvertFrom-FreshLocalDbInfo $info; $info = $null; $Context.Pipe = $metadata.Pipe
        $connection = New-FreshSqlConnection $Context $Context.Pipe 'master'
        $Context.Server = Assert-FreshLiveDatabase $Context $connection 'master'
        $systemFiles = @(Invoke-FreshSqlRead $Context $connection 'SELECT database_id AS DatabaseId,physical_name AS Path FROM sys.master_files WHERE database_id IN (1,2,3,4) ORDER BY database_id,file_id;' @{})
        if ($systemFiles.Count -lt 6) { Stop-FreshExecutor 'SystemPhysicalFiles' }
        foreach ($file in $systemFiles) {
            if (-not $file.Path.StartsWith($managed + '\',[StringComparison]::OrdinalIgnoreCase)) { Stop-FreshExecutor 'SystemPhysicalFiles' }
            Assert-FreshNoLinks $file.Path
        }
        Write-FreshStageRecord $Context $stage 'Complete' @{ Pipe = $Context.Pipe; ActualServer = $Context.Server; LocalDb = $metadata; SystemFiles = $systemFiles }

        $stage = 'CatalogCreation'; Assert-FreshInputSnapshot $Context
        foreach ($path in @($scope.DataFile,$scope.LogFile)) { Assert-FreshNoLinks $path; if (Test-Path -LiteralPath $path) { Stop-FreshExecutor 'TargetAbsence' } }
        [void](Assert-FreshLiveDatabase $Context $connection 'master')
        Write-FreshStageRecord $Context $stage 'Started' @{}
        $command = $connection.CreateCommand(); $command.CommandText = $Context.Review.Packet.CreationSqlForReviewOnly; $command.CommandTimeout = 60
        try {
            $p = $command.Parameters.Add('@ApprovedServer',[Data.SqlDbType]::NVarChar,128); $p.Value = $Context.Server
            $p = $command.Parameters.Add('@ApprovedOwner',[Data.SqlDbType]::NVarChar,128); $p.Value = $scope.Owner
            [void]$command.ExecuteNonQuery()
        } finally { $command.Dispose() }
        $connection.Dispose(); $connection = New-FreshSqlConnection $Context $Context.Pipe 'Cosmix_Testing'
        Assert-FreshSchemaAndData $Context $connection -BeforeMigration
        Write-FreshStageRecord $Context $stage 'Complete' @{ DataFile = $scope.DataFile; LogFile = $scope.LogFile }

        $stage = 'FullChainMigration'; Assert-FreshInputSnapshot $Context
        Assert-FreshSchemaAndData $Context $connection -BeforeMigration
        Write-FreshStageRecord $Context $stage 'Started' @{}
        Invoke-FreshChild $Context $tools.Sqlcmd (Get-FreshExecutorSqlcmdArguments $Context.Pipe (Get-FreshExecutorSqlArtifact))
        Write-FreshStageRecord $Context $stage 'Complete' @{}

        $stage = 'SchemaAndEmptyBusinessData'
        Assert-FreshSchemaAndData $Context $connection
        Write-FreshStageRecord $Context $stage 'Complete' @{}

        $stage = 'FreshPrivateMaterial'; Assert-FreshInputSnapshot $Context
        $values = New-FreshCryptoValues $Context
        $config = New-FreshExecutorConfiguration $Context.Pipe $Context.Server $values
        foreach ($key in @(@('FilesKey','files.key'),@('SnapshotKey','snapshot.key'))) {
            $bytes = [Convert]::FromBase64String($values[$key[0]])
            try { Write-FreshPrivateBytes $Context ($scope.PrivateRoot + '\keys\' + $key[1]) $bytes }
            finally { [Array]::Clear($bytes,0,$bytes.Length) }
        }
        $bytes = [Text.Encoding]::UTF8.GetBytes(($config | ConvertTo-Json -Depth 12))
        try { Write-FreshPrivateBytes $Context $scope.Settings $bytes }
        finally { [Array]::Clear($bytes,0,$bytes.Length); $values.Clear(); $values = $null; $config = $null }
        Write-FreshStageRecord $Context $stage 'Complete' @{ Settings = $scope.Settings; DataProtectionDirectory = $scope.DataProtectionDirectory; KeyReferences = @('huma-20261013-a01-files','local-testing-v1') }

        $stage = 'SetupOnlySeedAndRetention'; Assert-FreshInputSnapshot $Context
        Assert-FreshSchemaAndData $Context $connection
        if (Test-Path -LiteralPath ($scope.PrivateRoot + '\Test-Accounts.json')) { Stop-FreshExecutor 'TargetAbsence' }
        Write-FreshStageRecord $Context $stage 'Started' @{}
        Invoke-FreshChild $Context $tools.Dotnet @($Context.Review.Packet.ApiAssembly,'setup-local-testing')
        Assert-FreshSchemaAndData $Context $connection -AfterSeed
        Assert-FreshSeedPrivateObjects $Context
        Assert-FreshInputSnapshot $Context
        Write-FreshStageRecord $Context $stage 'Complete' @{ Budget = $Context.Review.Packet.FixtureBudget; AfterSeed = 'SetupOnly'; HostStarted = $false }
        return [pscustomobject]@{ Mode = 'SetupOnly'; SetupVerified = $true; StateRetained = $true; PacketSha256 = $Context.Review.PacketSha256; HostStarted = $false }
    } catch {
        if ($canRecord) {
            try { Write-FreshStageRecord $Context $stage 'Failed' @{ ErrorCode = 'StageFailed'; DetailsSuppressed = $true; StateRetained = $true } } catch { }
        }
        Stop-FreshExecutor $stage
    } finally {
        if ($null -ne $connection) { $connection.Dispose() }
        if ($null -ne $values) { $values.Clear() }
    }
}

function Get-FreshExecutorInputHashes {
    $scope = Get-FreshSqlScope
    $paths = [Collections.Generic.List[string]]::new()
    foreach ($project in @('Cosmix.Api','Cosmix.Application','Cosmix.Domain','Cosmix.Infrastructure')) {
        $root = Join-Path $scope.Repository ('src\' + $project)
        Assert-FreshNoLinks $root
        # Traverse explicitly so junctions cannot escape the checkout before inspection.
        $pending = [Collections.Generic.Stack[string]]::new(); $pending.Push($root)
        while ($pending.Count -gt 0) {
            $directory = $pending.Pop()
            foreach ($item in Get-ChildItem -LiteralPath $directory -Force) {
                if ($item.PSIsContainer -and $item.Name -cin @('bin','obj','.vs')) { continue }
                Assert-FreshNoLinks $item.FullName
                if ($item.PSIsContainer) { $pending.Push($item.FullName) }
                elseif ($item.Extension -cin @('.cs','.csproj') -or $item.Name -ceq 'packages.lock.json') { $paths.Add($item.FullName) }
            }
        }
    }
    foreach ($relative in @('Directory.Build.props','global.json',
        'docs\attendance-completion\Prepare-Fresh-SQL-Packet.ps1',
        'docs\attendance-completion\Invoke-Fresh-SQL-Setup.ps1',
        'docs\attendance-completion\VerifyFreshSqlSetupExecutor.ps1',
        'docs\attendance-completion\Fresh-Executor-Review.md')) { $paths.Add((Join-Path $scope.Repository $relative)) }
    $binaryRoot = Join-Path $scope.Repository 'src\Cosmix.Api\bin\Release\net10.0'
    Assert-FreshNoLinks $binaryRoot
    if (-not (Test-Path -LiteralPath (Join-Path $binaryRoot 'Cosmix.Api.dll'))) { Stop-FreshExecutor 'AssemblyInventory' }
    foreach ($item in Get-ChildItem -LiteralPath $binaryRoot -File -Recurse) {
        Assert-FreshNoLinks $item.FullName
        if ($item.Extension -ieq '.dll' -or $item.Name -match '\.(deps|runtimeconfig)\.json$') { $paths.Add($item.FullName) }
    }
    foreach ($name in @('sqlcmd','SqlLocalDB','dotnet')) {
        $commands = @(Get-Command $name -CommandType Application -ErrorAction Stop)
        if ($commands.Count -ne 1) { Stop-FreshExecutor 'ToolInventory' }
        $paths.Add($commands[0].Source)
    }
    foreach ($file in @('C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe',
        'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Data.dll',
        'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\mscorlib.dll')) { $paths.Add($file) }
    foreach ($file in @($paths | Sort-Object -Unique)) {
        Assert-FreshNoLinks $file
        [pscustomobject]@{ Path = $file; Sha256 = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash }
    }
}

function Get-FreshExecutorReview {
    $packet = Get-FreshSqlPacket (Get-FreshExecutorSqlArtifact)
    if ($packet.FullChainSqlSha256 -cne 'E9FB2FBBF59C96C72C7C6F1506652EFA431545B0BD93C90987AD94822D63AB03') { Stop-FreshExecutor 'ReviewedSqlDrift' }
    Assert-FreshExecutorSqlText ([IO.File]::ReadAllText((Get-FreshExecutorSqlArtifact)))
    $hostSource = [IO.File]::ReadAllText((Join-Path $packet.Scope.Repository 'src\Cosmix.Api\Development\LocalTesting.cs'))
    Assert-FreshExecutorReady $hostSource
    $programSource = [IO.File]::ReadAllText((Join-Path $packet.Scope.Repository 'src\Cosmix.Api\Program.cs'))
    if (-not $programSource.Contains('LocalTesting.CreateBuilder(setupLocalTesting, args)')) { Stop-FreshExecutor 'ConfigurationProviderIsolation' }
    $apiAssembly = Join-Path $packet.Scope.Repository 'src\Cosmix.Api\bin\Release\net10.0\Cosmix.Api.dll'
    Assert-FreshNoLinks $apiAssembly
    $assemblyBytes = [IO.File]::ReadAllBytes($apiAssembly)
    # A stale Release build without the new factory must be rejected before provisioning.
    Assert-FreshApiBinary $assemblyBytes
    $assemblyBytes = $null
    $managed = Get-FreshManagedSystemDirectory
    Assert-FreshNoLinks $managed
    Assert-FreshExecutorAbsence @() @($managed | Where-Object { Test-Path -LiteralPath $_ })
    $parents = @('C:\Users\Huma.Khan\AppData\Local\Cosmix', 'C:\Users\Huma.Khan\AppData\Local\Cosmix\AttendanceAcceptance')
    foreach ($parent in $parents) { Assert-FreshNoLinks $parent }
    $newParents = @($parents | Where-Object { -not (Test-Path -LiteralPath $_) })
    $tools = [ordered]@{
        LocalDb = (Get-Command SqlLocalDB -CommandType Application -ErrorAction Stop).Source
        Sqlcmd = (Get-Command sqlcmd -CommandType Application -ErrorAction Stop).Source
        Dotnet = (Get-Command dotnet -CommandType Application -ErrorAction Stop).Source
    }
    $manifest = [ordered]@{
        Version = 1; Mode = 'REVIEW-ONLY'; Scope = $packet.Scope
        FullChainSqlSha256 = $packet.FullChainSqlSha256; MigrationSourceHashes = $packet.SourceHashes
        InputHashes = @(Get-FreshExecutorInputHashes)
        CreationSqlForReviewOnly = $packet.CreationSqlForReviewOnly
        Stages = @(Get-FreshExecutorStages); AfterSeed = 'SetupOnly'
        SourceWorkingApiContentDir = Join-Path $packet.Scope.Repository 'src\Cosmix.Api'
        ApiAssembly = $apiAssembly; Tools = $tools; ExecutionRuntime = '64-bit Windows PowerShell 5.1, -NoProfile; built-in Framework System.Data.SqlClient'
        ManagedSystemDirectory = $managed; ManagedSystemDirectoryAbsent = $true; NewPrivateParents = $newParents
        ChildEnvironment = New-FreshChildEnvironment; SchemaContract = Get-FreshSchemaContract
        ManagedSystemDirectoryScope = '%LOCALAPPDATA%\Microsoft\Microsoft SQL Server Local DB\Instances\CosmixAttendanceHuma20261013A01; LocalDB-managed master/model/tempdb MDF/LDF files, resolved and recorded only from new-instance metadata after creation. No existing instance changes.'
        PrivateWritesContract = 'CreateNew only; no inherited ACL; only COSMIXRND\Huma.Khan, SYSTEM and local Administrators; reparse/collision refuses; retain manifest/results, no resume or cleanup.'
        FixtureBudget = Get-FreshFixtureBudget
        Approval = 'Requires later explicit human SQL approval. ApprovedPacketSha256 binds exact review bytes only and grants no permission.'
        PreparedForSqlApproval = $true; ReadyForSqlExecution = $false; PendingExternalDecision = 'Explicit human SQL/instance/private material/fixture approval of this exact packet; none exists.'
        SqlConnectionsOpened = 0; InstanceWrites = 0; DatabaseWrites = 0; PrivateWrites = 0
    }
    $canonical = $manifest | ConvertTo-Json -Depth 12 -Compress
    return [pscustomobject]@{ PacketSha256 = Get-FreshExecutorDigest $canonical; Packet = $manifest }
}

function Invoke-FreshExecutorEntry([bool]$ExecutionRequested, [string]$ApprovedHash, [string]$ApprovalStatement) {
    if (-not $ExecutionRequested) { return Get-FreshExecutorReview }
    if ($ApprovedHash -cnotmatch '^[A-F0-9]{64}$') { Stop-FreshExecutor 'ApprovedPacketSha256' }
    if ($ApprovalStatement -cne 'APPROVE NEW CosmixAttendanceHuma20261013A01 Cosmix_Testing ONLY') { Stop-FreshExecutor 'ExplicitSqlApproval' }
    if ($PSVersionTable.PSEdition -cne 'Desktop' -or $PSVersionTable.PSVersion.Major -ne 5 -or -not [Environment]::Is64BitProcess) { Stop-FreshExecutor 'WindowsPowerShell51Required' }
    $review = Get-FreshExecutorReview
    $scope = Get-FreshSqlScope
    $operator = [Security.Principal.WindowsIdentity]::GetCurrent().Name
    $checkout = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd('\')
    Assert-FreshExecutorIntent $true $ApprovedHash $review.PacketSha256 $operator $checkout
    $context = [pscustomobject]@{ Authorized = $true; Review = $review; Pipe = ''; Server = ''; RecordNumber = 0 }
    $script:FreshExecutionContext = $context
    $locks = [Collections.Generic.List[IO.FileStream]]::new()
    try {
        # Hold read handles across the write stages: reviewed files cannot be replaced/rebuilt mid-run.
        foreach ($file in @($review.Packet.InputHashes.Path) + @(Get-FreshExecutorSqlArtifact)) {
            $locks.Add([IO.FileStream]::new($file,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read))
        }
        Assert-FreshInputSnapshot $context
        Invoke-FreshExecutorExecution $context
    } finally {
        foreach ($file in $locks) { $file.Dispose() }
        $context.Authorized = $false; $script:FreshExecutionContext = $null
    }
}

if ($MyInvocation.InvocationName -ne '.') {
    try { Invoke-FreshExecutorEntry ([bool]$Execute) $ApprovedPacketSha256 $SqlApproval | ConvertTo-Json -Depth 18 }
    catch {
        # Suppress dependency exceptions/paths/config contents; no detailed private record exists yet.
        $message = $_.Exception.Message
        if ($message -cnotmatch '^Fresh setup refused: [A-Za-z0-9]+; values suppressed\.$') {
            $message = 'Fresh setup refused: ReadOnlyPreflight; values suppressed.'
        }
        throw $message
    }
}
