param()
$ErrorActionPreference = 'Stop'
$private = 'C:\Users\Affan.Ahmed\AppData\Local\Cosmix\AccessAcceptance\20261009'
$settings = Join-Path $private 'settings.json'
$repository = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not (Test-Path -LiteralPath $settings)) { throw 'Approved private settings are missing.' }
if ([Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners().Port -contains 7249) {
    throw 'Acceptance port is occupied; do not stop an unrelated process.'
}
$oldSettings = $env:COSMIX_LOCAL_TESTING_SETTINGS
$oldEnvironment = $env:ASPNETCORE_ENVIRONMENT
$oldDotnetEnvironment = $env:DOTNET_ENVIRONMENT
try {
    $env:COSMIX_LOCAL_TESTING_SETTINGS = $settings
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:DOTNET_ENVIRONMENT = 'Development'
    $stamp = [Guid]::NewGuid().ToString('N')
    $stdout = Join-Path $private "host-$stamp.stdout.log"
    $stderr = Join-Path $private "host-$stamp.stderr.log"
    $assembly = Join-Path $repository 'src\Cosmix.Api\bin\Release\net10.0\Cosmix.Api.dll'
    $process = Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList @(('"' + $assembly + '"'), '--urls', 'https://localhost:7249') -WorkingDirectory (Join-Path $repository 'src\Cosmix.Api') -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru
    $metadata = [ordered]@{ ProcessId = $process.Id; StartedAtUtc = [DateTimeOffset]::UtcNow.ToString('O'); Stdout = $stdout; Stderr = $stderr; BaseUri = 'https://localhost:7249' }
    [IO.File]::WriteAllText((Join-Path $private 'host.json'), ($metadata | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
    Write-Host "Started owned HTTPS acceptance host PID $($process.Id) on localhost:7249; logs remain private."
} finally {
    $env:COSMIX_LOCAL_TESTING_SETTINGS = $oldSettings
    $env:ASPNETCORE_ENVIRONMENT = $oldEnvironment
    $env:DOTNET_ENVIRONMENT = $oldDotnetEnvironment
}
