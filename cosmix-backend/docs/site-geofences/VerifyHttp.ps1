$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$api = Join-Path $root 'src/Cosmix.Api'
$assembly = Join-Path $api 'bin/Release/net10.0/Cosmix.Api.dll'
if (-not (Test-Path -LiteralPath $assembly)) { throw 'Run the Release solution build first.' }
$output = Join-Path $root 'artifacts/site-geofences-http'
$null = New-Item -ItemType Directory -Path $output -Force
$profile = Join-Path $output 'profile'
$null = New-Item -ItemType Directory -Path $profile -Force
$listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
$listener.Start()
$port = $listener.LocalEndpoint.Port
$listener.Stop()
$url = "http://localhost:$port"
$environment = @{
    ASPNETCORE_ENVIRONMENT = 'Development'
    DOTNET_ENVIRONMENT = 'Development'
    COSMIX_LOCAL_TESTING_SETTINGS = ''
    LocalTesting__Enabled = 'false'
    ConnectionStrings__CosmixDatabase = 'Server=tcp:127.0.0.1,1;Initial Catalog=Cosmix_Geofence_NoDatabase;Integrated Security=true;Encrypt=true;Connect Timeout=1'
    MobileAuthentication__Enabled = 'false'
    WebAuthentication__Enabled = 'false'
    MobileAuthentication__Sms__Provider = ''
    Jwt__SigningKey = ''
    ASPNETCORE_HTTPS_PORT = ''
    ASPNETCORE_HTTPS_PORTS = ''
    AllowedHosts = 'localhost'
    LOCALAPPDATA = $profile
}
$process = $null
try {
    $process = Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList @(('"' + $assembly + '"'), '--urls', $url) `
        -WorkingDirectory $api -WindowStyle Hidden -PassThru -Environment $environment `
        -RedirectStandardOutput (Join-Path $output 'stdout.log') -RedirectStandardError (Join-Path $output 'stderr.log')
    $document = $null
    $startupError = ''
    $deadline = [DateTime]::UtcNow.AddSeconds(25)
    while ([DateTime]::UtcNow -lt $deadline) {
        if ($process.HasExited) { throw 'Verification host exited; inspect artifacts/site-geofences-http logs.' }
        try { $document = Invoke-RestMethod "$url/openapi/v1.json" -TimeoutSec 2; break }
        catch { $startupError = $_.Exception.Message; Start-Sleep -Milliseconds 200 }
    }
    if ($null -eq $document) { throw "Verification host did not publish OpenAPI in time: $startupError" }
    $base = '/api/v1/hr/sites/{siteId}/geofences'
    $operations = @(
        @('get', $base), @('get', "$base/versions"), @('get', "$base/versions/{versionId}"),
        @('get', "$base/history"), @('post', "$base/versions"), @('get', '/api/v1/me/site-attendance-configuration')
    )
    $checks = 0
    foreach ($pair in $operations) {
        $method, $path = $pair
        $operation = $document.paths.$path.$method
        if ($null -eq $operation -or -not $operation.summary -or -not $operation.description -or -not $operation.security) {
            throw "Missing documented operation: $method $path"
        }
        $checks++
        $endpoint = $path.Replace('{siteId}', '11111111-1111-1111-1111-111111111111').Replace('{versionId}', '22222222-2222-2222-2222-222222222222')
        $args = @{ Uri = "$url$endpoint"; Method = $method; SkipHttpErrorCheck = $true; TimeoutSec = 3 }
        if ($method -eq 'post') { $args.Body = '{}'; $args.ContentType = 'application/json' }
        $response = Invoke-WebRequest @args
        if ([int]$response.StatusCode -ne 401) { throw "Expected 401 for $method $path; got $($response.StatusCode)" }
        $checks++
    }
    if (-not $document.paths."$base/versions".post.responses.'201'.headers.Location) { throw 'Missing 201 Location metadata.' }
    $checks++
    if ($document.paths.'/api/v1/me/site-attendance-configuration'.get.parameters.Count -gt 0) {
        throw 'Self route unexpectedly accepts client identifiers or dates.'
    }
    $checks++
    $document | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath (Join-Path $output 'openapi.json')
    Write-Output "PASS $checks OpenAPI/anonymous HTTP checks covering all 6 geofence operations. Authentication disabled; no database acceptance claimed."
}
finally {
    if ($null -ne $process -and -not $process.HasExited) { Stop-Process -Id $process.Id }
}
