<#
Read-only, offline acceptance inventory. Does not execute dotnet, SQL, a server, scanner,
or provider; create files; read key/account contents; or output private settings values.
Observed means metadata only, never database or behavioral acceptance.
#>
[CmdletBinding()]
param([switch]$AsJson)

$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
$documentedLocalAppData = Join-Path $env:USERPROFILE 'AppData\Local'
$privateDirectory = Join-Path $documentedLocalAppData 'Cosmix\LocalTesting'
$settingsPath = Join-Path $privateDirectory 'settings.json'
$acceptanceDirectory = Join-Path $documentedLocalAppData 'Cosmix\AccessAcceptance\20261009'
$secretsPath = Join-Path $env:APPDATA 'Microsoft\UserSecrets\Cosmix-Backend-HR-Development\secrets.json'
$checks = [Collections.Generic.List[object]]::new()

function Add-Check([string]$Id, [string]$Status, [string]$Summary) {
    $checks.Add([pscustomobject]@{ Id = $Id; Status = $Status; Summary = $Summary })
}

function Has-NoLinkedAncestor($Item) {
    for ($cursor = $Item; $null -ne $cursor;) {
        if ($cursor.Attributes -band [IO.FileAttributes]::ReparsePoint) { return $false }
        if ($cursor -is [IO.DirectoryInfo]) { $cursor = $cursor.Parent }
        else { $cursor = $cursor.Directory }
    }
    return $true
}

function Read-Configuration([string]$Path) {
    # Only call with the fixed known configuration paths above. Suppress parser exceptions:
    # their diagnostic text may contain the input document, including credentials.
    try {
        $item = Get-Item -LiteralPath $Path -Force
        if ($item.PSIsContainer -or $item.Length -lt 1 -or $item.Length -gt 1048576 -or
            -not (Has-NoLinkedAncestor $item)) { return $null }
        return (Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json)
    } catch { return $null }
}

function Get-Setting($Configuration, [string]$Key) {
    if ($null -eq $Configuration) { return $null }
    $flat = $Configuration.PSObject.Properties[$Key]
    if ($null -ne $flat) { return $flat.Value }
    $value = $Configuration
    foreach ($part in $Key.Split(':')) {
        if ($null -eq $value) { return $null }
        $property = $value.PSObject.Properties[$part]
        if ($null -eq $property) { return $null }
        $value = $property.Value
    }
    return $value
}

function Is-True($Value) { return [string]$Value -ieq 'true' }

function Has-Text($Value) { return -not [string]::IsNullOrWhiteSpace([string]$Value) }

function Test-PrivateMetadata([string]$Path, [bool]$Directory, [long]$Length = -1) {
    try {
        $item = Get-Item -LiteralPath $Path -Force
        if ($item.PSIsContainer -ne $Directory -or ($Length -ge 0 -and $item.Length -ne $Length)) { return $false }
        if (-not (Has-NoLinkedAncestor $item)) { return $false }
        $acl = Get-Acl -LiteralPath $Path
        $descriptor = [Security.AccessControl.RawSecurityDescriptor]::new($acl.GetSecurityDescriptorBinaryForm(), 0)
        if ($null -eq $descriptor.DiscretionaryAcl) { return $false }
        $allowed = @([Security.Principal.WindowsIdentity]::GetCurrent().User.Value, 'S-1-5-18', 'S-1-5-32-544')
        if ($acl.GetOwner([Security.Principal.SecurityIdentifier]).Value -notin $allowed) { return $false }
        foreach ($rule in $acl.GetAccessRules($true, $true, [Security.Principal.SecurityIdentifier])) {
            if ($rule.AccessControlType -eq [Security.AccessControl.AccessControlType]::Allow -and
                $rule.IdentityReference.Value -notin $allowed) { return $false }
        }
        return $true
    } catch { return $false }
}

try {
    if ([IO.Path]::GetFullPath($env:LOCALAPPDATA).TrimEnd('\') -ine $documentedLocalAppData.TrimEnd('\')) {
        Add-Check 'localappdata-scope' 'Review' 'LOCALAPPDATA differs from the documented user-profile path; only the documented profile location is inspected, without searching other profiles or sandbox packages.'
    } else { Add-Check 'localappdata-scope' 'Observed' 'LOCALAPPDATA matches the documented user-profile location.' }
    $dotnet = Get-Command dotnet.exe -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    $sdkPresent = $false
    if ($null -ne $dotnet) {
        $sdkPath = Join-Path (Split-Path -Parent $dotnet.Source) 'sdk'
        $sdkPresent = @(Get-ChildItem -LiteralPath $sdkPath -Directory -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -match '^10\.0\.[0-9]+$' }).Count -gt 0
    }
    if ($sdkPresent) { Add-Check 'dotnet-sdk' 'Observed' '.NET 10 SDK directory and dotnet executable are present; neither was executed.' }
    else { Add-Check 'dotnet-sdk' 'Blocked' '.NET 10 SDK metadata was not found beside the dotnet executable on PATH.' }
    $localDb = Get-Command SqlLocalDB.exe -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -ne $localDb) { Add-Check 'localdb-tool' 'Observed' 'SqlLocalDB executable is present; no instance was queried or started.' }
    else { Add-Check 'localdb-tool' 'Review' 'SqlLocalDB executable was not found on PATH; instance availability is unknown.' }
    $assemblies = @('Cosmix.Api', 'Cosmix.Application', 'Cosmix.Domain', 'Cosmix.Infrastructure')
    $missingAssemblies = @($assemblies | Where-Object {
        -not (Test-Path -LiteralPath (Join-Path $repository "src\Cosmix.Api\bin\Release\net10.0\$_.dll") -PathType Leaf)
    })
    if ($missingAssemblies.Count -eq 0) { Add-Check 'release-assemblies' 'Observed' 'Four Release assemblies exist; freshness and build success are not established by this check.' }
    else { Add-Check 'release-assemblies' 'Review' 'One or more expected Release assemblies are absent.' }
    $missingAssets = @($assemblies | Where-Object {
        -not (Test-Path -LiteralPath (Join-Path $repository "src\$_\obj\project.assets.json") -PathType Leaf)
    })
    if ($missingAssets.Count -eq 0) { Add-Check 'restore-assets' 'Observed' 'Four restore asset manifests exist; no restore or package download was attempted.' }
    else { Add-Check 'restore-assets' 'Review' 'One or more restore asset manifests are absent; dependency resolution is unverified.' }

    $settings = Read-Configuration $settingsPath
    $secrets = $null
    if ($null -ne $settings) { $secrets = Read-Configuration $secretsPath }
    if ($null -eq $settings) {
        Add-Check 'testing-settings' 'Blocked' 'Known private testing settings are absent, unreadable, oversized or invalid; no values are printed.'
    } else {
        Add-Check 'testing-settings' 'Observed' 'Known private testing settings parsed in memory; no values are printed.'
        if ((Is-True (Get-Setting $settings 'LocalTesting:Enabled')) -and
            (Is-True (Get-Setting $settings 'WebAuthentication:Enabled')) -and
            (Is-True (Get-Setting $settings 'MobileAuthentication:Enabled'))) {
            Add-Check 'testing-auth-switches' 'Observed' 'Testing, web and mobile authentication switches are enabled in the testing overlay.'
        } else { Add-Check 'testing-auth-switches' 'Blocked' 'Required testing/authentication switches are missing or disabled in the testing overlay.' }
        try {
            # The parser only interprets text; it cannot open a database connection.
            $connection = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
            $connection.set_ConnectionString([string](Get-Setting $settings 'ConnectionStrings:CosmixDatabase'))
            $namedInstance = $connection.get_DataSource() -ieq '(localdb)\MSSQLLocalDB'
            $localPipe = $connection.get_DataSource().StartsWith('np:\\.\pipe\LOCALDB#', [StringComparison]::OrdinalIgnoreCase)
            if ($connection.get_InitialCatalog() -ceq 'Cosmix_Testing' -and $connection.get_IntegratedSecurity() -and
                -not (Has-Text $connection.get_UserID()) -and -not (Has-Text $connection.get_Password()) -and ($namedInstance -or $localPipe)) {
                Add-Check 'testing-connection-policy' 'Observed' 'Connection text names exact Cosmix_Testing with Windows authentication and the allowed local LocalDB form.'
                if ($localPipe) { Add-Check 'local-instance-resolution' 'Review' 'A local named pipe is configured; its current instance mapping and liveness were not queried.' }
                else { Add-Check 'local-instance-resolution' 'NotRun' 'The configured MSSQLLocalDB instance was not queried; existence and liveness are unknown.' }
            } else { Add-Check 'testing-connection-policy' 'Blocked' 'Connection text does not meet the exact local synthetic catalog policy.' }
        } catch { Add-Check 'testing-connection-policy' 'Blocked' 'Connection text could not be safely parsed; parser details are suppressed.' }
        if ((Get-Setting $settings 'MobileAuthentication:Sms:Provider') -ceq 'LocalTesting' -and
            -not (Has-Text (Get-Setting $settings 'MobileAuthentication:Sms:Endpoint')) -and
            -not (Has-Text (Get-Setting $settings 'MobileAuthentication:Sms:BearerToken'))) {
            Add-Check 'testing-sms' 'Observed' 'The testing overlay selects the in-memory SMS adapter with no external endpoint/token.'
        } else { Add-Check 'testing-sms' 'Blocked' 'The testing overlay must explicitly select LocalTesting without external SMS endpoint/token.' }
        $expectedObjects = Join-Path $privateDirectory 'files'
        $expectedKey = Join-Path $privateDirectory 'keys\files.key'
        if ((Get-Setting $settings 'LocalTesting:PrivateDirectory') -ieq $privateDirectory -and
            (Get-Setting $settings 'PrivateFiles:Directory') -ieq $expectedObjects -and
            (Get-Setting $settings 'PrivateFiles:KeyFile') -ieq $expectedKey) {
            Add-Check 'testing-storage-paths' 'Observed' 'Testing storage uses the documented private object directory and separate key path; acceptance must use fresh storage.'
        } else { Add-Check 'testing-storage-paths' 'Review' 'Testing private paths differ from the documented layout; no alternate path was inspected.' }
    }
    $privateMetadata = (Test-PrivateMetadata $privateDirectory $true) -and
        (Test-PrivateMetadata $settingsPath $false) -and
        (Test-PrivateMetadata (Join-Path $privateDirectory 'files') $true) -and
        (Test-PrivateMetadata (Join-Path $privateDirectory 'keys') $true) -and
        (Test-PrivateMetadata (Join-Path $privateDirectory 'keys\files.key') $false 32)
    if ($privateMetadata) { Add-Check 'testing-storage-metadata' 'Observed' 'Known testing paths have restricted ACL metadata, no linked ancestors and a 32-byte file-key length; key bytes were not read.' }
    else { Add-Check 'testing-storage-metadata' 'Review' 'One or more known private path, ACL, link or key-length metadata checks did not pass; no files were changed.' }
    if (Test-Path -LiteralPath (Join-Path $privateDirectory 'Test-Accounts.json') -PathType Leaf) {
        Add-Check 'account-file' 'Observed' 'The synthetic account file exists; its contents and database identities were not read.'
    } else { Add-Check 'account-file' 'Blocked' 'The documented synthetic account file was not found.' }
    if (Test-Path -LiteralPath $acceptanceDirectory) {
        Add-Check 'acceptance-directory' 'Review' 'The proposed acceptance directory already exists; do not reuse or overwrite it without reviewing isolation.'
    } else { Add-Check 'acceptance-directory' 'Observed' 'The proposed acceptance directory does not exist; it was not created.' }

    # The testing JSON is added last by LocalTesting.Configure. For this narrow scanner
    # inventory, inspect only presence/shape across that overlay, environment and user secrets.
    function Get-ScannerSetting([string]$Name) {
        $key = 'EmployeeFileScanner:' + $Name
        $value = Get-Setting $settings $key
        if ($null -ne $value) { return $value }
        $value = [Environment]::GetEnvironmentVariable('EmployeeFileScanner__' + $Name)
        if ($null -ne $value) { return $value }
        return (Get-Setting $secrets $key)
    }
    if ($null -eq $settings) {
        Add-Check 'scanner-config' 'Review' 'Effective scanner configuration cannot be established without the testing overlay; no scanner endpoint was contacted.'
    } elseif (Is-True (Get-ScannerSetting 'Enabled')) {
        $address = $null
        $hostIsAddress = [Net.IPAddress]::TryParse([string](Get-ScannerSetting 'Host'), [ref]$address)
        if ($hostIsAddress -and [Net.IPAddress]::IsLoopback($address)) {
            Add-Check 'scanner-config' 'Review' 'Scanner settings enable a loopback IP endpoint; reachability, engine/signature freshness and detection are untested.'
        } else { Add-Check 'scanner-config' 'Review' 'Scanner enabled configuration needs endpoint/options review; no endpoint value was printed or contacted.' }
    } else { Add-Check 'scanner-config' 'Review' 'No enabled scanner was observed in testing overlay, scanner environment fields or readable user secrets; fail-closed document scanning is expected.' }
    Add-Check 'scanner-acceptance' 'NotRun' 'Clean sample, standard malware-test detection, timeout and unavailable-scanner acceptance require a separately authorized local scanner run.'
    Add-Check 'real-sms-acceptance' 'NotRun' 'Carrier SMS delivery/provider policy is unverified and outside the local synthetic test envelope.'
    Add-Check 'database-and-schema' 'NotRun' 'No SQL connection or schema query occurred. Site-roster and amendment model/schema compatibility must be established before starting LocalTesting.'
    Add-Check 'authority' 'NotRun' 'Fixed Admin/SuperAdmin web role configuration is confirmed; no account or role grants were inspected, changed or accepted against SQL.'
    Add-Check 'authenticated-acceptance' 'NotRun' 'No host/login/MFA/CSRF/session revocation/concurrency/rollback/encrypted-disk request was executed.'
    Add-Check 'approval' 'NotRun' 'Fresh authorization is needed for the exact synthetic SQL/private-storage/grant/backup/localhost process envelope in the acceptance plan.'
} catch {
    # Never interpolate exceptions: configuration/path diagnostics can disclose values.
    Add-Check 'inventory-error' 'Blocked' 'The offline inventory could not complete; error details were suppressed to protect private values.'
} finally {
    $settings = $null
    $secrets = $null
    $connection = $null
}

if ($AsJson) {
    [pscustomobject]@{ Mode = 'OfflineReadOnly'; BehavioralAcceptanceEstablished = $false; Checks = @($checks.ToArray()) } |
        ConvertTo-Json -Depth 4
} else {
    $checks | Format-Table Id, Status, Summary -Wrap -AutoSize
    'Observed means file/configuration metadata only. No SQL, server or behavioral acceptance was executed.'
}
