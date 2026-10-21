param([switch]$Initialize, [switch]$SetupOnly)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$private = Join-Path $env:LOCALAPPDATA 'Cosmix\LocalTesting'
$settings = Join-Path $private 'settings.json'

function New-SecretBytes([int]$length) {
    $bytes = New-Object byte[] $length
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
    return ,$bytes
}

if (-not (Test-Path -LiteralPath $settings)) {
    if (-not $Initialize) { throw 'Run with -Initialize once to provision the approved separate testing database.' }
    if ((Test-Path -LiteralPath $private) -and (Get-ChildItem -LiteralPath $private -Force | Select-Object -First 1)) {
        throw 'Private testing files already exist without settings.json. Inspect and recover them; keys must not be overwritten.'
    }
    [IO.Directory]::CreateDirectory($private) | Out-Null
    $sid = [Security.Principal.WindowsIdentity]::GetCurrent().User
    $acl = New-Object Security.AccessControl.DirectorySecurity
    $acl.SetOwner($sid)
    $acl.SetAccessRuleProtection($true, $false)
    $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($sid, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow'))
    Set-Acl -LiteralPath $private -AclObject $acl
    foreach ($name in @('keys','files','snapshots')) { [IO.Directory]::CreateDirectory((Join-Path $private $name)) | Out-Null }
    foreach ($name in @('snapshot','files')) { [IO.File]::WriteAllBytes((Join-Path $private "keys\$name.key"), (New-SecretBytes 32)) }
    $secretsPath = Join-Path $env:APPDATA 'Microsoft\UserSecrets\Cosmix-Backend-HR-Development\secrets.json'
    $original = Get-Content -LiteralPath $secretsPath -Raw | ConvertFrom-Json
    $connection = New-Object System.Data.Common.DbConnectionStringBuilder
    $connection.set_ConnectionString([string]$original.'ConnectionStrings:CosmixDatabase')
    $connection.set_Item('Initial Catalog', 'Cosmix_Testing')
    $connection.set_Item('Application Name', 'Cosmix Local Testing')
    $config = @{
        LocalTesting = @{ Enabled = $true; PrivateDirectory = $private }
        ConnectionStrings = @{ CosmixDatabase = $connection.get_ConnectionString() }
        Jwt = @{ Issuer = 'Cosmix.LocalTesting'; Audience = 'Cosmix.LocalTesting.Clients'; SigningKey = [Convert]::ToBase64String((New-SecretBytes 48)) }
        AllowedHosts = 'localhost'
        Cors = @{ AllowedOrigins = @('https://localhost:5173') }
        MobileAuthentication = @{
            Enabled = $true; OtpLifetimeSeconds = 300; ResendCooldownSeconds = 30; MaximumOtpAttempts = 5
            RateWindowSeconds = 60; RequestsPerPhone = 5; RequestsPerIp = 30; RequestsGlobal = 100
            VerificationAttemptsPerPhone = 15; VerificationAttemptsPerIp = 60; VerificationAttemptsGlobal = 200
            AccessTokenLifetimeSeconds = 300; RefreshTokenLifetimeSeconds = 3600; SessionLifetimeSeconds = 28800
            MinimumRequestDurationMilliseconds = 150; BusinessTimeZoneId = 'Asia/Karachi'
            Crypto = @{ KeyVersion = 'localtest1'; VerifierKeyBase64 = [Convert]::ToBase64String((New-SecretBytes 32)); EncryptionKeyBase64 = [Convert]::ToBase64String((New-SecretBytes 32)); MaxDeliveryLifetimeSeconds = 600 }
            Sms = @{ Provider = 'LocalTesting'; Endpoint = ''; BearerToken = '' }
        }
        WebAuthentication = @{
            Enabled = $true; MinimumPasswordLength = 14; MaximumPasswordFailures = 5; PasswordLockoutSeconds = 300
            PasswordAttemptsPerAccount = 10; PasswordAttemptsPerEmail = 10; PasswordAttemptsPerIp = 50; PasswordAttemptsGlobal = 200
        }
        PrivateFiles = @{ Directory = (Join-Path $private 'files'); KeyFile = (Join-Path $private 'keys\files.key'); KeyReference = 'local-test-files-v1' }
    }
    [IO.File]::WriteAllText($settings, ($config | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
}

$previousPath = $env:COSMIX_LOCAL_TESTING_SETTINGS
$previousEnvironment = $env:ASPNETCORE_ENVIRONMENT
$previousDotnetEnvironment = $env:DOTNET_ENVIRONMENT
try {
    $env:COSMIX_LOCAL_TESTING_SETTINGS = $settings
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:DOTNET_ENVIRONMENT = 'Development'
    Push-Location $root
    try {
        & dotnet build Cosmix.Backend.sln -c Release --verbosity minimal
        if ($LASTEXITCODE -ne 0) { throw 'Build failed. No testing setup was run.' }
        if ($Initialize) {
            & dotnet run --project src/Cosmix.Api -c Release --no-build --no-launch-profile -- setup-local-testing
            if ($LASTEXITCODE -ne 0) { throw 'Testing setup failed. Do not reset or delete the database; inspect the failure.' }
        }
        if (-not $SetupOnly) {
            Write-Host 'LOCAL TESTING ONLY: Cosmix_Testing; no real SMS. Default user-secrets configuration is unchanged.'
            & dotnet run --project src/Cosmix.Api -c Release --no-build --no-launch-profile --urls https://localhost:7240
            if ($LASTEXITCODE -ne 0) { throw 'Local testing API stopped with an error.' }
        }
    } finally { Pop-Location }
} finally {
    $env:COSMIX_LOCAL_TESTING_SETTINGS = $previousPath
    $env:ASPNETCORE_ENVIRONMENT = $previousEnvironment
    $env:DOTNET_ENVIRONMENT = $previousDotnetEnvironment
}
