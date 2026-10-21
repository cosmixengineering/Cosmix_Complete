$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$api = Join-Path $root 'src/Cosmix.Api'
$assembly = Join-Path $api 'bin/Release/net10.0/Cosmix.Api.dll'
if (-not (Test-Path -LiteralPath $assembly)) { throw 'Build Release first.' }
$output = Join-Path $root 'artifacts/site-reports-http'
$profile = Join-Path $output 'profile'
$null = New-Item -ItemType Directory -Force -Path $profile
$listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
$listener.Start()
$port = $listener.LocalEndpoint.Port
$listener.Stop()
$url = "http://localhost:$port"
$environment = @{
    ASPNETCORE_ENVIRONMENT = 'Development'; DOTNET_ENVIRONMENT = 'Development'
    COSMIX_LOCAL_TESTING_SETTINGS = ''; LocalTesting__Enabled = 'false'
    ConnectionStrings__CosmixDatabase = 'Server=tcp:127.0.0.1,1;Initial Catalog=Cosmix_SiteReports_NoDatabase;Integrated Security=true;Encrypt=true;Connect Timeout=1'
    MobileAuthentication__Enabled = 'false'; WebAuthentication__Enabled = 'false'
    MobileAuthentication__Sms__Provider = ''; Jwt__SigningKey = ''
    AttendanceJobs__Enabled = 'false'; MissingCheckoutReminders__Enabled = 'false'
    AttendanceZones__Enabled = 'false'; AttendanceZones__RetentionEnabled = 'false'
    ZoneLocationStorage__Enabled = 'false'; FaceTemplateStorage__Enabled = 'false'
    ASPNETCORE_HTTPS_PORT = ''; ASPNETCORE_HTTPS_PORTS = ''
    AllowedHosts = 'localhost'; LOCALAPPDATA = $profile
}
$process = $null
try {
    $process = Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList @(('"' + $assembly + '"'), '--urls', $url) `
        -WorkingDirectory $api -WindowStyle Hidden -PassThru -Environment $environment `
        -RedirectStandardOutput (Join-Path $output 'stdout.log') -RedirectStandardError (Join-Path $output 'stderr.log')
    $document = $null
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    while ([DateTime]::UtcNow -lt $deadline) {
        if ($process.HasExited) { throw 'Verification host exited; inspect artifacts/site-reports-http logs.' }
        try { $document = Invoke-RestMethod "$url/openapi/v1.json" -TimeoutSec 2; break }
        catch { Start-Sleep -Milliseconds 200 }
    }
    if ($null -eq $document) { throw 'Verification host did not publish OpenAPI.' }
    $operations = @(
        @('post','/api/v1/site-reports'), @('get','/api/v1/site-reports/mine'),
        @('get','/api/v1/site-reports/mine/{id}'), @('get','/api/v1/site-reports/mine/{id}/evidence'),
        @('get','/api/v1/site-reports/queue'), @('get','/api/v1/site-reports/sites/{siteId}/{id}'),
        @('get','/api/v1/site-reports/sites/{siteId}/{id}/evidence'),
        @('post','/api/v1/site-reports/sites/{siteId}/{id}/decisions'),
        @('post','/api/v1/site-reports/mine/{id}/responses'),
        @('get','/api/v1/site-reports/mine/{id}/responses/{responseId}/evidence'),
        @('get','/api/v1/site-reports/sites/{siteId}/{id}/responses/{responseId}/evidence')
    )
    $checks = 0
    foreach ($pair in $operations) {
        $method,$path = $pair
        $op = $document.paths.$path.$method
        if (-not $op -or -not $op.summary -or -not $op.description -or -not $op.security) { throw "Missing documented protected route: $method $path" }
        $checks++
        $schemes = @($op.security | ForEach-Object { $_.PSObject.Properties.Name })
        if ($path -eq '/api/v1/site-reports') {
            if ('Bearer' -notin $schemes -or 'WebSession' -in $schemes) { throw 'Report submission must document mobile bearer only.' }
            $key = @($op.parameters | Where-Object { $_.name -eq 'Idempotency-Key' -and $_.required })
            if ($key.Count -ne 1 -or -not $op.responses.'201' -or -not $op.responses.'409') { throw 'Submission retry/result contract missing.' }
            $checks += 2
        } elseif ($path.Contains('/mine')) {
            if ('Bearer' -notin $schemes -or 'WebSession' -notin $schemes) { throw 'Own routes must document both supported clients.' }
            $checks++
        } else {
            if ('Bearer' -in $schemes -or 'WebSession' -notin $schemes) { throw 'Scoped queue/details must document web access only.' }
            $checks++
        }
        if ($method -eq 'post' -and $path -ne '/api/v1/site-reports') {
            $key = @($op.parameters | Where-Object { $_.name -eq 'Idempotency-Key' -and $_.required })
            if ($key.Count -ne 1 -or -not $op.responses.'200' -or -not $op.responses.'409') { throw 'Review/response retry/result contract missing.' }
            $checks++
        }
        $endpoint = $path.Replace('{siteId}','11111111-1111-1111-1111-111111111111').Replace('{id}','22222222-2222-2222-2222-222222222222').Replace('{responseId}','33333333-3333-3333-3333-333333333333')
        $request = @{ Uri = "$url$endpoint"; Method = $method; SkipHttpErrorCheck = $true; TimeoutSec = 3 }
        if ($method -eq 'post') { $request.Body = '{}'; $request.ContentType = 'application/json' }
        $response = Invoke-WebRequest @request
        if ([int]$response.StatusCode -ne 401) { throw "Expected anonymous 401 for $path; got $($response.StatusCode)" }
        $checks++
    }
    $schema = $document.components.schemas.SubmitSiteReportRequest
    if (-not $schema -or $schema.additionalProperties -ne $false) { throw 'Strict request schema missing.' }
    $checks++
    foreach ($forbidden in @('employeeId','registeredDeviceId','userAccountId','receivedAtUtc','privateEvidenceReference','reviewer')) {
        if ($schema.properties.$forbidden) { throw "Submission exposes client authority: $forbidden" }
        $checks++
    }
    foreach ($required in @('siteId','kind','notes')) {
        if ($required -notin $schema.required) { throw "Missing required submission input: $required" }
        $checks++
    }
    $summary = $document.components.schemas.SiteReportSummaryResponse
    if (-not $summary.properties.evidenceStatus -or -not $summary.properties.submissionStatus -or $summary.properties.privateEvidenceReference) { throw 'Received/unverified status or privacy contract missing.' }
    $checks++
    foreach ($entry in @(
        @{ Name='DecideSiteReportRequest'; Required=@('expectedReviewSequence','expectedResponseSequence','outcome','reason') },
        @{ Name='RespondToSiteReportRequest'; Required=@('expectedReviewSequence','expectedResponseSequence','returnedReviewId','writtenResponse') }
    )) {
        $schema = $document.components.schemas.($entry.Name)
        if (-not $schema -or $schema.additionalProperties -ne $false) { throw "Strict schema missing: $($entry.Name)" }
        $checks++
        foreach ($required in $entry.Required) {
            if ($required -notin $schema.required) { throw "Missing required input $required in $($entry.Name)" }
            $checks++
        }
        foreach ($forbidden in @('employeeId','userAccountId','reviewer','reviewerRole','reviewedAtUtc','submittedAtUtc','privateEvidenceReference','siteId','latitude','longitude')) {
            if ($schema.properties.$forbidden) { throw "Client authority/evidence field $forbidden in $($entry.Name)" }
            $checks++
        }
    }
    foreach ($schemaName in @('SiteReportSummaryResponse','SiteReportReviewResponse','SiteReportEmployeeResponse','SiteReportDetailResponse')) {
        $schema = $document.components.schemas.$schemaName
        if (-not $schema) { throw "Missing result schema $schemaName" }
        foreach ($forbidden in @('privateEvidenceReference','storageKey','encryptionKey','privateAttachmentReference')) {
            if ($schema.properties.$forbidden) { throw "Private storage field $forbidden exposed in $schemaName" }
            $checks++
        }
    }
    if (-not $document.components.schemas.SiteReportReviewResponse.properties.id -or
        -not $document.components.schemas.SiteReportSummaryResponse.properties.responseSequence -or
        -not $document.components.schemas.SiteReportDetailResponse.properties.responses) { throw 'Returned review identity or resubmission history missing.' }
    $checks++
    $document | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath (Join-Path $output 'openapi.json')
    Write-Output "PASS: $checks registered site-report OpenAPI/anonymous HTTP checks; no genuine login, SQL, permission grants or scanner calls."
}
finally {
    if ($null -ne $process -and -not $process.HasExited) { Stop-Process -Id $process.Id }
}
