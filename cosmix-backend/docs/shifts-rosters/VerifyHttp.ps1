$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$api = Join-Path $root 'src/Cosmix.Api'
$assembly = Join-Path $api 'bin/Release/net10.0/Cosmix.Api.dll'
if (-not (Test-Path -LiteralPath $assembly)) { throw 'Run the Release solution build first.' }
$output = Join-Path $root 'artifacts/shifts-rosters-http'
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
    ConnectionStrings__CosmixDatabase = 'Server=tcp:127.0.0.1,1;Initial Catalog=Cosmix_Scheduling_NoDatabase;Integrated Security=true;Encrypt=true;Connect Timeout=1'
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
        if ($process.HasExited) { throw 'Verification host exited; inspect artifacts/shifts-rosters-http logs.' }
        try { $document = Invoke-RestMethod "$url/openapi/v1.json" -TimeoutSec 2; break }
        catch { $startupError = $_.Exception.Message; Start-Sleep -Milliseconds 200 }
    }
    if ($null -eq $document) { throw "Verification host did not publish OpenAPI in time: $startupError" }
    $shifts = '/api/v1/hr/shifts'
    $rosters = '/api/v1/hr/employees/{employeeId}/shift-assignments'
    $operations = @(
        @('get', $shifts), @('get', "$shifts/{id}"), @('post', $shifts),
        @('put', "$shifts/{id}"), @('post', "$shifts/{id}/status"), @('post', "$shifts/{id}/versions"),
        @('get', $rosters), @('post', $rosters), @('post', "$rosters/{assignmentId}/replace"),
        @('post', "$rosters/{assignmentId}/end"), @('get', '/api/v1/hr/employees/{employeeId}/roster'),
        @('get', '/api/v1/me/shift')
    )
    $checks = 0
    foreach ($pair in $operations) {
        $method, $path = $pair
        $operation = $document.paths.$path.$method
        if ($null -eq $operation -or -not $operation.summary -or -not $operation.description -or -not $operation.security) {
            throw "Missing documented secured operation: $method $path"
        }
        $checks++
        if ($method -in @('post', 'put')) {
            $key = @($operation.parameters | Where-Object { $_.name -eq 'Idempotency-Key' -and $_.in -eq 'header' })
            if ($key.Count -ne 1 -or -not $key[0].required) { throw "Missing required retry key: $method $path" }
            $checks++
        }
        $endpoint = $path.Replace('{id}', '11111111-1111-1111-1111-111111111111').Replace('{employeeId}', '22222222-2222-2222-2222-222222222222').Replace('{assignmentId}', '33333333-3333-3333-3333-333333333333')
        if ($path.EndsWith('/roster') -or $path.EndsWith('/me/shift')) { $endpoint += '?businessDate=2026-10-12' }
        $request = @{ Uri = "$url$endpoint"; Method = $method; SkipHttpErrorCheck = $true; TimeoutSec = 3 }
        if ($method -in @('post', 'put')) { $request.Body = '{}'; $request.ContentType = 'application/json' }
        $response = Invoke-WebRequest @request
        if ([int]$response.StatusCode -ne 401) { throw "Expected 401 for $method $path; got $($response.StatusCode)" }
        $checks++
    }
    foreach ($path in @($shifts, "$shifts/{id}/versions")) {
        if (-not $document.paths.$path.post.responses.'201'.headers.Location) { throw "Missing 201 Location metadata: $path" }
        $checks++
    }
    $selfParameters = @($document.paths.'/api/v1/me/shift'.get.parameters)
    if ($selfParameters.Count -ne 1 -or $selfParameters[0].name -ne 'businessDate' -or -not $selfParameters[0].required) {
        throw 'My Shift must accept only a required business date and no employee selector.'
    }
    $checks++
    $document | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath (Join-Path $output 'openapi.json')
    Write-Output "PASS $checks OpenAPI/anonymous HTTP checks covering all 12 scheduling operations. Authentication disabled; no database acceptance claimed."
}
finally {
    if ($null -ne $process -and -not $process.HasExited) { Stop-Process -Id $process.Id }
}
