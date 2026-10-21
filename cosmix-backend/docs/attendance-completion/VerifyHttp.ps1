$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$api = Join-Path $root 'src/Cosmix.Api'
$assembly = Join-Path $api 'bin/Release/net10.0/Cosmix.Api.dll'
$output = Join-Path $root 'artifacts/attendance-completion-http'
$profile = Join-Path $output 'profile'
$null = New-Item -ItemType Directory -Force -Path $profile
$listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
$listener.Start(); $port = $listener.LocalEndpoint.Port; $listener.Stop()
$url = "http://localhost:$port"
# Allow the complete anonymous contract sweep, then deliberately exhaust this explicit
# isolated-host quota. More registered operations must not exhaust it halfway through the sweep.
$probeQuota=256
$environment = @{
    ASPNETCORE_ENVIRONMENT='Development'; DOTNET_ENVIRONMENT='Development'; COSMIX_LOCAL_TESTING_SETTINGS=''; LocalTesting__Enabled='false'
    ConnectionStrings__CosmixDatabase='Server=tcp:127.0.0.1,1;Initial Catalog=Cosmix_Attendance_NoDatabase;Integrated Security=true;Encrypt=true;Connect Timeout=1'
    MobileAuthentication__Enabled='false'; WebAuthentication__Enabled='false'; AttendanceJobs__Enabled='false'; FaceTemplateStorage__Enabled='false'
    MobileAuthentication__Sms__Provider=''; Jwt__SigningKey=''; ASPNETCORE_HTTPS_PORT=''; ASPNETCORE_HTTPS_PORTS=''; AllowedHosts='localhost'; LOCALAPPDATA=$profile
    SelfHostedFace__Enabled='false'; FaceCapture__Enabled='false'
    AttendanceTraffic__CapturePerMinute="$probeQuota"; AttendanceTraffic__FacePerMinute="$probeQuota"; AttendanceTraffic__ReportsPerMinute="$probeQuota"
    AttendanceTraffic__ZoneSamplesPerMinute='3'; AttendanceZones__Enabled='false'; AttendanceZones__RetentionEnabled='false'; ZoneLocationStorage__Enabled='false'; MissingCheckoutReminders__Enabled='false'
}
$process=$null; $checks=0
try {
    $process=Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList @(('"'+$assembly+'"'),'--urls',$url) -WorkingDirectory $api `
        -WindowStyle Hidden -PassThru -Environment $environment -RedirectStandardOutput (Join-Path $output 'stdout.log') -RedirectStandardError (Join-Path $output 'stderr.log')
    $document=$null; $deadline=[DateTime]::UtcNow.AddSeconds(30)
    while([DateTime]::UtcNow -lt $deadline) {
        if($process.HasExited) {throw 'Owned HTTP host exited; inspect ignored logs.'}
        try {$document=Invoke-RestMethod "$url/openapi/v1.json" -TimeoutSec 2; break} catch {Start-Sleep -Milliseconds 200}
    }
    if($null -eq $document) {throw 'No OpenAPI document from owned host.'}
    $paths=@($document.paths.PSObject.Properties | Where-Object {
        $_.Name -like '/api/v1/me/attendance/*' -or $_.Name -like '/api/v1/attendance/*' -or $_.Name -like '/api/v1/me/overtime/*' -or
        $_.Name -like '/api/v1/hr/employees/*/attendance-carry-forward*' -or $_.Name -like '/api/v1/hr/attendance-policies*' -or
        $_.Name -like '/api/v1/hr/employees/*/face-enrollments*' -or $_.Name -like '/api/v1/me/notifications*' -or $_.Name -like '/api/v1/site-reports*' -or
        $_.Name -like '/api/v1/leaves/*' -or $_.Name -like '/api/v1/hr/leave-settings/*' -or $_.Name -like '/api/v1/hr/company-settings*'
    })
    $routeCount=0
    foreach($entry in $paths) {
        $path=$entry.Name
        foreach($operation in $entry.Value.PSObject.Properties | Where-Object {$_.Name -in @('get','post','put','delete')}) {
            $method=$operation.Name; $op=$operation.Value; $routeCount++
            if(-not $op.security) {throw "Unprotected OpenAPI operation $method $path"}; $checks++
            $schemes=@($op.security | ForEach-Object {$_.PSObject.Properties.Name})
            $mobileOnly=($path -like '/api/v1/attendance/face/challenges*') -or ($path -like '/api/v1/me/attendance/zones/monitoring-windows*') -or $path -in @('/api/v1/me/attendance/check-in','/api/v1/me/attendance/check-out') -or ($path -eq '/api/v1/site-reports' -and $method -eq 'post')
            $self=($path -like '/api/v1/me/*') -or ($path -like '/api/v1/site-reports/mine*') -or ($path -like '/api/v1/leaves/mine*') -or $path -eq '/api/v1/attendance/face/capability'
            if($mobileOnly) {if('Bearer' -notin $schemes -or 'WebSession' -in $schemes){throw "Incorrect mobile-only security: $path"}}
            elseif($self) {if('Bearer' -notin $schemes -or 'WebSession' -notin $schemes){throw "Missing supported self client: $path"}}
            else {if('Bearer' -in $schemes -or 'WebSession' -notin $schemes){throw "Incorrect management security: $path"}}
            $checks++
            $endpoint=$path -replace '\{year\}','2026' -replace '\{month\}','9' -replace '\{[^}]+\}','11111111-1111-1111-1111-111111111111'
            $request=@{Uri="$url$endpoint";Method=$method;SkipHttpErrorCheck=$true;TimeoutSec=3}
            if($method -ne 'get') {$request.Body='{}';$request.ContentType='application/json'}
            $response=Invoke-WebRequest @request
            if([int]$response.StatusCode -ne 401){throw "Expected anonymous 401: $method $path got $($response.StatusCode)"}; $checks++
        }
    }
    if($routeCount -ne 166){throw "Expected 166 attendance/policy/leave/company/evidence operations; found $routeCount"}; $checks++
    foreach($schemaName in @('CreateAttendancePolicyRequest','PreviewAttendancePolicyRequest','PublishAttendancePolicyRequest',
        'AssignAttendancePolicyRequest','EndAttendancePolicyAssignmentRequest','ReplaceAttendancePolicyAssignmentRequest',
        'SubmitLeaveRequest','DecideLeaveRequest','RequestLeaveCancellation','DecideLeaveCancellation','PublishLeavePolicyRequest','CreateLeaveTypeRequest',
        'SaveAttendancePayrollInputsRequest','SaveCompanyProfileRequest','PublishCompanySiteJurisdictionRequest',
        'CreateAttendancePayrollAttestationRequest','RecordLegalReviewRequest',
        'UploadVerificationFaceCaptureRequest','UploadEnrollmentFaceCaptureRequest')) {
        $schema=$document.components.schemas.$schemaName
        if(-not $schema -or $schema.additionalProperties -ne $false){throw "Missing strict policy/leave envelope: $schemaName"}; $checks++
        foreach($field in @('actorUserId','approvedBy','employeeSalary','financialReady','payrollReady','faceVerified')) {
            if($schema.properties.$field){throw "Client authority or salary input in $schemaName.$field"}; $checks++
        }
    }
    if($document.components.schemas.SubmitLeaveRequest.properties.employeeId){throw 'Leave intake allows a client employee identity'}; $checks++
    foreach($field in @('inputProvenance','payrollReady')) {
        if(-not $document.components.schemas.AttendancePolicyResult.properties.$field){throw "Missing explicit daily provenance/readiness: $field"}; $checks++
    }
    foreach($field in @('annualDays','sickDays','casualDays','minimumServiceMonths','standardDayMinutes')) {
        $fieldSchema=$document.components.schemas.LeavePolicySettings.properties.$field
        if($fieldSchema.type -ne 'integer' -or $fieldSchema.pattern){throw "Leave settings advertise rejected quoted integers: $field"}; $checks++
    }
    foreach($schemaName in @('PreviewAttendancePolicyRequest','PublishAttendancePolicyRequest','AssignAttendancePolicyRequest','EndAttendancePolicyAssignmentRequest','ReplaceAttendancePolicyAssignmentRequest','SaveAttendancePayrollInputsRequest')) {
        $fieldSchema=$document.components.schemas.$schemaName.properties.expectedRevision
        if($fieldSchema.type -ne 'integer' -or $fieldSchema.pattern){throw "Publication revision advertises rejected numeric strings: $schemaName"}; $checks++
    }
    foreach($schemaName in @('SubmitPunchRequest','SubmitSiteReportRequest','DecideSiteReportRequest','RespondToSiteReportRequest','CreateCorrectionRequest','SubmitCarryForwardRequest','FinalizeMonthLockRequest','SubmitOvertimeRequest','DecideOvertimeRequest','SetOvertimeEligibilityRequest','ZoneSampleRequest','ConfigureZoneMonitoringRequest','StartZoneMonitoringRequest')) {
        $schema=$document.components.schemas.$schemaName
        if(-not $schema -or $schema.additionalProperties -ne $false){throw "Strict schema missing: $schemaName"}; $checks++
        foreach($field in @('employeeId','registeredDeviceId','userAccountId','receivedAtUtc','accepted','livenessPassed')) {
            if($schema.properties.$field){throw "Client authority field $schemaName.$field"}; $checks++
        }
    }
    $correctionRequest=$document.components.schemas.CreateCorrectionRequest
    if(-not $correctionRequest.properties.expectedSourceFingerprint){throw 'Correction source fingerprint missing from request contract'}; $checks++
    if('expectedSourceFingerprint' -in @($correctionRequest.required)){throw 'Legacy correction replay must not be blocked by JSON presence validation'}; $checks++
    $correctionSource=$document.components.schemas.CorrectionSourceResponse
    foreach($field in @('sourceFingerprint','sessionRevisions')) {
        if(-not $correctionSource.properties.$field){throw "Correction source evidence contract missing $field"}; $checks++
    }
    if($correctionSource.properties.originalResourceScopes){throw 'Private correction authorization paths exposed publicly'}; $checks++
    $faceCapability=$document.components.schemas.FaceCapabilityResponse
    foreach($field in @('enrollmentCompatible','checkInFaceReady','availabilityKind')) {
        if(-not $faceCapability.properties.$field){throw "Face readiness contract missing $field"}; $checks++
    }
    foreach($schemaName in @('FaceEnrollmentAttemptPage','CorrectionQueueResponse','CarryForwardRequestPage','OvertimePage','ZoneEventPage','AttendanceExceptionQueueResponse','AttendancePunchAuditPage','AttendanceSessionAuditPage')) {
        $schema=$document.components.schemas.$schemaName
        if(-not $schema.properties.items -or -not $schema.properties.nextCursor){throw "Missing bounded continuation contract: $schemaName"}; $checks++
        foreach($field in @('totalCount','lastScannedId','privateReference','originalResourceScopes')) {
            if($schema.properties.$field){throw "Private queue metadata exposed: $schemaName.$field"}; $checks++
        }
    }
    $enrollmentAttempt=$document.components.schemas.FaceEnrollmentAttemptResponse
    foreach($field in @('finalizationProof','protectedResultReference','template','providerEvidenceToken','actorRoleCode','resources')) {
        if($enrollmentAttempt.properties.$field){throw "Private enrollment attempt data exposed: $field"}; $checks++
    }
    foreach($schemaName in @('AttendanceDashboardResponse','AttendanceFactualBatchResponse')) {
        $schema=$document.components.schemas.$schemaName
        if(-not $schema.properties.metadata){throw "Missing factual report metadata: $schemaName"}; $checks++
    }
    foreach($field in @('generatedAtUtc','timeZone','from','to','employeeIds','lockStatus','coverage','policyStatus','dataKind')) {
        if(-not $document.components.schemas.AttendanceFactualReadMetadata.properties.$field){throw "Missing factual read provenance: $field"}; $checks++
    }
    foreach($schemaName in @('FactualAttendanceTotals','AttendanceExceptionDetail','AttendanceExceptionListItem','AttendancePunchAuditItem','AttendanceSessionAuditItem','PunchResponse','WorkSessionResponse')) {
        $schema=$document.components.schemas.$schemaName
        if(-not $schema){throw "Missing protected read contract: $schemaName"}; $checks++
        foreach($field in @('salary','bankAccount','privateReference','latitude','longitude','template','faceProof','payableMinutes')) {
            if($schema.properties.$field){throw "Sensitive or unsupported field exposed: $schemaName.$field"}; $checks++
        }
    }
    $correctionDecision=$document.components.schemas.DecideCorrectionRequest
    if(-not $correctionDecision -or $correctionDecision.additionalProperties -ne $false){throw 'Strict correction decision schema missing'}; $checks++
    foreach($field in @('actorUserId','actorRoleCode','employeeId','approvedBy','payableMinutes','faceVerified')) {
        if($correctionDecision.properties.$field){throw "Client authority in correction decision contract: $field"}; $checks++
    }
    foreach($schemaName in @('SubmitCarryForwardRequest','FinalizeMonthLockRequest')) {
        $schema=$document.components.schemas.$schemaName
        foreach($field in @('actorUserId','approvedBy','approvalRequestId','payableMinutes','salary','reopen')) {
            if($schema.properties.$field){throw "Client authority in $schemaName.$field"}; $checks++
        }
    }
    foreach($requiredField in @('expectedSourceFingerprint','expectedPriorAdjustmentId')) {
        if($requiredField -notin @($document.components.schemas.SubmitCarryForwardRequest.required)){throw "Missing required carry-forward stale-proof field $requiredField"}; $checks++
    }
    if('expectedVersion' -notin @($document.components.schemas.FinalizeMonthLockRequest.required)){throw 'Month-lock expectedVersion must be explicit, including null'}; $checks++
    foreach($requiredField in @('isEnabled','expectedRevision','expectedTimeline','reason')) {
        if($requiredField -notin @($document.components.schemas.SetOvertimeEligibilityRequest.required)){throw "Missing explicit OT eligibility field $requiredField"}; $checks++
    }
    if(@($document.paths.PSObject.Properties.Name | Where-Object {$_ -like '*month-locks*reopen*'}).Count){throw 'Forbidden month reopening operation exposed'}; $checks++
    $hiringRouteCount=0
    foreach($entry in $document.paths.PSObject.Properties | Where-Object {
        $_.Name -like '/api/v1/hr/employees*' -and $_.Name -notlike '*face-enrollments*' -and $_.Name -notlike '*attendance-carry-forward*'
    }) {
        foreach($operation in $entry.Value.PSObject.Properties | Where-Object {$_.Name -in @('get','post','put','delete')}) {
            if(-not $operation.Value.security){throw "Employee operation lost authorization metadata: $($entry.Name)"}; $checks++
            $endpoint=$entry.Name -replace '\{[^}]+\}','11111111-1111-1111-1111-111111111111'
            $request=@{Uri="$url$endpoint";Method=$operation.Name;SkipHttpErrorCheck=$true;TimeoutSec=3}
            if($operation.Name -ne 'get'){$request.Body='{}';$request.ContentType='application/json'}
            $response=Invoke-WebRequest @request
            if([int]$response.StatusCode -ne 401){throw "Employee anonymous authorization regression: $($entry.Name)"}; $checks++
            $hiringRouteCount++
        }
    }
    if($hiringRouteCount -lt 30){throw 'Expected existing employee/hiring route set missing'}; $checks++
    $limited=$false
    for($i=0;$i -le $probeQuota;$i++) {
        $response=Invoke-WebRequest "$url/api/v1/me/attendance/state" -SkipHttpErrorCheck -TimeoutSec 3
        if([int]$response.StatusCode -eq 429) {
            $body=if($response.Content -is [byte[]]){[Text.Encoding]::UTF8.GetString($response.Content)}else{[string]$response.Content}
            $problem=$body | ConvertFrom-Json
            if($problem.code -ne 'attendance_rate_limited' -or -not $problem.traceId){throw 'Missing rate-limit ProblemDetails'}
            $limited=$true;break
        }
        if([int]$response.StatusCode -ne 401){throw 'Unexpected anonymous limiter probe status'}
    }
    if(-not $limited){throw 'Attendance traffic limit did not reject excess requests'}; $checks++
    $caseResponse=Invoke-WebRequest "$url/API/V1/ME/ATTENDANCE/STATE" -SkipHttpErrorCheck -TimeoutSec 3
    if([int]$caseResponse.StatusCode -ne 429){throw 'Route casing bypassed attendance traffic control'}; $checks++
    $carryCaseResponse=Invoke-WebRequest "$url/API/V1/HR/EMPLOYEES/11111111-1111-1111-1111-111111111111/ATTENDANCE-CARRY-FORWARD/SOURCES" -SkipHttpErrorCheck -TimeoutSec 3
    if([int]$carryCaseResponse.StatusCode -ne 429){throw 'HR carry-forward route bypassed shared attendance traffic control'}; $checks++
    $otCaseResponse=Invoke-WebRequest "$url/API/V1/ME/OVERTIME/REQUESTS" -SkipHttpErrorCheck -TimeoutSec 3
    if([int]$otCaseResponse.StatusCode -ne 429){throw 'Self overtime route bypassed shared attendance traffic control'}; $checks++
    $zoneSampleUri="$url/API/V1/ME/ATTENDANCE/ZONES/MONITORING-WINDOWS/11111111-1111-1111-1111-111111111111/SAMPLES"
    $zoneLimited=$false
    for($i=0;$i -lt 4;$i++) {
        $response=Invoke-WebRequest $zoneSampleUri -Method Post -Body '{}' -ContentType 'application/json' -SkipHttpErrorCheck -TimeoutSec 3
        if([int]$response.StatusCode -eq 429){$zoneLimited=$true;break}
        if([int]$response.StatusCode -ne 401){throw 'Unexpected anonymous zone-sample quota response'}
    }
    if(-not $zoneLimited){throw 'Zone sample technical quota or casing normalization failed'}; $checks++
    $document | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath (Join-Path $output 'openapi.json')
    "PASS $checks HTTP/OpenAPI/traffic checks across $routeCount new operations and $hiringRouteCount existing employee operations. Anonymous only; no SQL/auth/provider acceptance."
} finally {if($null -ne $process -and -not $process.HasExited){Stop-Process -Id $process.Id}}
