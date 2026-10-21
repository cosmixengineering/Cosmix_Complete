$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$api = Join-Path $root 'src/Cosmix.Api'
$assembly = Join-Path $api 'bin/Release/net10.0/Cosmix.Api.dll'
if (-not (Test-Path -LiteralPath $assembly)) { throw 'Run the Release solution build first.' }
$output = Join-Path $root 'artifacts/site-rosters-http'
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
    ConnectionStrings__CosmixDatabase = 'Server=tcp:127.0.0.1,1;Initial Catalog=Cosmix_SiteRoster_NoDatabase;Integrated Security=true;Encrypt=true;Connect Timeout=1'
    MobileAuthentication__Enabled = 'false'
    WebAuthentication__Enabled = 'false'
    MobileAuthentication__Sms__Provider = ''
    Jwt__SigningKey = ''
    ASPNETCORE_HTTPS_PORT = ''
    ASPNETCORE_HTTPS_PORTS = ''
    AllowedHosts = 'localhost'
    LOCALAPPDATA = $profile
}

# No credentials, database fixtures or permission grants are used. Only this script's host is stopped.
$process = $null
try {
    $process = Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList @(('"' + $assembly + '"'), '--urls', $url) `
        -WorkingDirectory $api -WindowStyle Hidden -PassThru -Environment $environment `
        -RedirectStandardOutput (Join-Path $output 'stdout.log') -RedirectStandardError (Join-Path $output 'stderr.log')
    $document = $null
    $startupError = ''
    $deadline = [DateTime]::UtcNow.AddSeconds(25)
    while ([DateTime]::UtcNow -lt $deadline) {
        if ($process.HasExited) { throw 'Verification host exited; inspect artifacts/site-rosters-http logs.' }
        try { $document = Invoke-RestMethod "$url/openapi/v1.json" -TimeoutSec 2; break }
        catch { $startupError = $_.Exception.Message; Start-Sleep -Milliseconds 200 }
    }
    if ($null -eq $document) { throw "Verification host did not publish OpenAPI in time: $startupError" }
    $base = '/api/v1/hr/sites/{siteId}'
    $assignments = "$base/shift-assignments"
    $temporary = "$base/temporary-shift-changes"
    $operations = @(
        @('get', $assignments), @('post', $assignments),
        @('post', "$assignments/{assignmentId}/replace"), @('post', "$assignments/{assignmentId}/end"),
        @('get', "$temporary/preview"), @('post', $temporary),
        @('get', "$temporary/{changeId}/cancel/preview"), @('post', "$temporary/{changeId}/cancel"),
        @('get', "$temporary/{changeId}/supersede/preview"), @('post', "$temporary/{changeId}/supersede")
    )
    $checks = 0
    foreach ($pair in $operations) {
        $method, $path = $pair
        $operation = $document.paths.$path.$method
        if ($null -eq $operation -or -not $operation.summary -or -not $operation.description -or -not $operation.security) {
            throw "Missing documented secured site operation: $method $path"
        }
        $checks++
        if ($method -eq 'post') {
            $key = @($operation.parameters | Where-Object { $_.name -eq 'Idempotency-Key' -and $_.in -eq 'header' })
            if ($key.Count -ne 1 -or -not $key[0].required) { throw "Missing required retry key: $method $path" }
            $checks++
            if (-not $operation.responses.'409' -or -not $operation.responses.'503') {
                throw "Missing conflict/schema-unavailable contracts: $method $path"
            }
            $checks++
        }
        $endpoint = $path.Replace('{siteId}', '11111111-1111-1111-1111-111111111111').Replace('{assignmentId}', '22222222-2222-2222-2222-222222222222').Replace('{changeId}', '44444444-4444-4444-4444-444444444444')
        if ($path.EndsWith('/preview')) {
            $endpoint += '?businessDate=2026-10-12&replacementShiftVersionId=33333333-3333-3333-3333-333333333333'
        }
        $request = @{ Uri = "$url$endpoint"; Method = $method; SkipHttpErrorCheck = $true; TimeoutSec = 3 }
        if ($method -eq 'post') { $request.Body = '{}'; $request.ContentType = 'application/json' }
        $response = Invoke-WebRequest @request
        if ([int]$response.StatusCode -ne 401) { throw "Expected 401 for $method $path; got $($response.StatusCode)" }
        $checks++
    }

    $preview = $document.paths."$temporary/preview".get
    foreach ($name in @('businessDate', 'replacementShiftVersionId')) {
        $parameter = @($preview.parameters | Where-Object { $_.name -eq $name -and $_.in -eq 'query' })
        if ($parameter.Count -ne 1 -or -not $parameter[0].required) { throw "Missing required preview query: $name" }
        $checks++
    }

    # Validate public schemas without assuming provider-specific handling of constructor validation attributes.
    $requestSchemas = @{
        CreateSiteRosterRequest = @('shiftId', 'effectiveFrom', 'expectedTimeline', 'expectedShiftVersion', 'reason')
        EndSiteRosterRequest = @('effectiveTo', 'expectedTimeline', 'reason')
        CreateSiteTemporaryChangeRequest = @('businessDate', 'replacementShiftVersionId', 'expectedTimeline', 'expectedStaff', 'writtenEvidence', 'reason')
        CancelSiteTemporaryChangeRequest = @('expectedTimeline', 'expectedStaff', 'writtenEvidence', 'reason')
        SupersedeSiteTemporaryChangeRequest = @('replacementShiftVersionId', 'expectedTimeline', 'expectedStaff', 'writtenEvidence', 'reason')
    }
    foreach ($entry in $requestSchemas.GetEnumerator()) {
        $schema = $document.components.schemas.($entry.Key)
        if ($null -eq $schema) { throw "Missing request schema: $($entry.Key)" }
        foreach ($field in $entry.Value) {
            if ($null -eq $schema.properties.$field -or $field -notin $schema.required) {
                throw "Missing required property: $($entry.Key).$field"
            }
            $checks++
        }
        if ($schema.additionalProperties -ne $false -or $schema.properties.employeeId -or
            $schema.properties.employeeIds -or $schema.properties.approvalRequestId -or $schema.properties.approvedBy) {
            throw "Site request unexpectedly accepts employee selection/approval authority: $($entry.Key)"
        }
        $checks++
    }
    foreach ($name in @('GrantRolePermissionsRequest', 'AssignAccessRoleRequest')) {
        $schema = $document.components.schemas.$name
        if ($null -eq $schema.properties.reason -or 'reason' -notin $schema.required) {
            throw "Access configuration must document its required reason: $name"
        }
        $checks++
    }
    foreach ($pair in @(
        @('SiteRosterHistoryResponse', @('siteId', 'timeline', 'assignments', 'temporaryChanges')),
        @('SiteTemporaryPreviewResponse', @('siteId', 'businessDate', 'timeline', 'staffToken', 'originalShiftVersionId', 'replacementShiftVersionId', 'affectedEmployeeIds')),
        @('EffectiveRosterResponse', @('employeeId', 'businessDate', 'siteId', 'shiftVersionId', 'explanation', 'holiday')),
        @('RosterResolutionExplanation', @('source', 'siteAssignmentId', 'membershipRecordId', 'temporaryChangeId', 'originalShiftVersionId', 'rule')),
        @('RosterHolidayContextResponse', @('calendarId', 'calendarRevision', 'holidays', 'isInformational', 'doesNotCancelDuty'))
    )) {
        $schemaName, $fields = $pair
        $schema = $document.components.schemas.$schemaName
        foreach ($field in $fields) {
            if ($null -eq $schema.properties.$field) { throw "Missing public response property: $schemaName.$field" }
            $checks++
        }
    }
    $holiday = $document.components.schemas.RosterHolidayContextResponse
    $holidayItem = $document.components.schemas.RosterHolidayItem
    if ($holiday.properties.governmentNotificationReference -or $holidayItem.properties.notificationPrivateReference) {
        throw 'Employee holiday response exposes private notification evidence.'
    }
    $checks++

    foreach ($path in @('/api/v1/me/shift', '/api/v1/hr/employees/{employeeId}/roster')) {
        $operation = $document.paths.$path.get
        if (-not $operation.security -or -not $operation.responses.'200' -or $operation.description -notmatch '(?i)site') {
            throw "Effective roster endpoint does not document site resolution: $path"
        }
        $checks++
        $endpoint = $path.Replace('{employeeId}', '22222222-2222-2222-2222-222222222222') + '?businessDate=2026-10-12'
        $response = Invoke-WebRequest "$url$endpoint" -SkipHttpErrorCheck -TimeoutSec 3
        if ([int]$response.StatusCode -ne 401) { throw "Expected 401 for effective site roster: $path" }
        $checks++
    }
    $selfParameters = @($document.paths.'/api/v1/me/shift'.get.parameters)
    if ($selfParameters.Count -ne 1 -or $selfParameters[0].name -ne 'businessDate' -or -not $selfParameters[0].required) {
        throw 'My Shift must accept only the required business date and no employee selector.'
    }
    $checks++

    # A retired legacy write still requires authentication. This host cannot verify authenticated 409.
    $individual = '/api/v1/hr/employees/{employeeId}/shift-assignments'
    foreach ($path in @($individual, "$individual/{assignmentId}/replace", "$individual/{assignmentId}/end")) {
        $operation = $document.paths.$path.post
        $successResponses = @($operation.responses.PSObject.Properties | Where-Object { $_.Name -match '^2[0-9][0-9]$' })
        if (-not $operation.security -or -not $operation.responses.'409' -or $successResponses.Count -gt 0) {
            throw "Retired individual write still promises success: $path"
        }
        $checks++
        $endpoint = $path.Replace('{employeeId}', '22222222-2222-2222-2222-222222222222').Replace('{assignmentId}', '33333333-3333-3333-3333-333333333333')
        $response = Invoke-WebRequest "$url$endpoint" -Method Post -Body '{}' -ContentType 'application/json' -SkipHttpErrorCheck -TimeoutSec 3
        if ([int]$response.StatusCode -ne 401) { throw "Expected 401 for retired individual write: $path" }
        $checks++
    }
    $document | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath (Join-Path $output 'openapi.json')
    Write-Output "PASS $checks OpenAPI/anonymous HTTP checks: ten site operations, access reason schemas, two effective site roster reads and three retired individual writes. No authenticated, SQL, schema-readiness or permission-grant acceptance claimed."
}
finally {
    if ($null -ne $process -and -not $process.HasExited) { Stop-Process -Id $process.Id }
}
