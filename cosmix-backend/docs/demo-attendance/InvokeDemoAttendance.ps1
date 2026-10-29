$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot '../access-provisioning/LocalAcceptanceHttp.ps1')
$root=Join-Path $env:LOCALAPPDATA 'Cosmix\LocalTesting\DemoAttendance20261028'
$fixture=Get-Content -LiteralPath (Join-Path $root 'fixture.json') -Raw|ConvertFrom-Json
$accounts=Get-Content -LiteralPath (Join-Path $env:LOCALAPPDATA 'Cosmix\LocalTesting\Test-Accounts.json') -Raw|ConvertFrom-Json
$admin=$null; $hr=$null
try {
$admin=New-LocalAcceptanceSession -BaseUri 'https://localhost:7240/' -Account ($accounts|Where-Object Email -eq 'admin@example.invalid') -SkipCertificateCheck
$hr=New-LocalAcceptanceSession -BaseUri 'https://localhost:7240/' -Account ($accounts|Where-Object Email -eq 'hr@example.invalid') -SkipCertificateCheck
function Need($Condition,[string]$Message){if(!$Condition){throw $Message}}
function SaveResult([string]$Name,$Value){ConvertTo-Json -InputObject $Value -Depth 30|Set-Content -LiteralPath (Join-Path $root ($Name+'.json'))}
function RequireOk($Response,[string]$Step){if($Response.StatusCode -ne 200){$title=if($Response.Body){$Response.Body.title}else{'no_problem_title'};throw "$Step HTTP $($Response.StatusCode): $title"};return $Response.Body}
foreach($pair in @(@($admin,'Admin'),@($hr,'HR'))){
 $permissions=RequireOk (Invoke-LocalAcceptanceRequest -Client $pair[0] -Method GET -Path '/api/v1/me/permissions') 'Permissions'
 $needed=if($pair[1] -eq 'Admin'){@('attendance.corrections.submit','attendance.corrections.decide')}else{@('attendance.corrections.review','attendance.corrections.decide','attendance.summaries.read')}
 foreach($permission in $needed){Need (@($permissions|Where-Object { $_.permission -eq $permission -and $_.scopeKind -eq 7 }).Count -gt 0) ('Existing permission missing: '+$pair[1]+' '+$permission)}
 SaveResult ($pair[1]+'-attendance-permissions') @($permissions|Where-Object permission -match '^attendance\.(corrections|summaries)\.')
}
$id=[string]$fixture.EmployeeId
$base='/api/v1/attendance/employees/'+$id
$source=RequireOk (Invoke-LocalAcceptanceRequest -Client $admin -Method GET -Path ($base+'/correction-requests/sources?businessDate=2026-10-27')) 'Source preview'
SaveResult 'source-preview' $source
Need ($source.historicalRoster.status -eq 'scheduled' -and $source.historicalRoster.siteId -eq $fixture.SiteId -and $source.historicalRoster.shiftVersionId -eq $fixture.ShiftVersionId -and !$source.periodLocked) 'Demo schedule/target/lock mismatch'
$requestPath=Join-Path $root 'submitted-correction.json'
if(Test-Path -LiteralPath $requestPath){$submitted=Get-Content -LiteralPath $requestPath -Raw|ConvertFrom-Json}else{
 $proposal=@{businessDate='2026-10-27';kind=4;rawPunchId=$null;expectedAssessmentId=$source.latestAssessmentId;expectedSummaryId=$source.latestSummaryId;expectedSourceFingerprint=$source.sourceFingerprint;requestedTimeUtc='2026-10-27T04:00:00Z';requestedEndUtc='2026-10-27T12:00:00Z';requestedStatus=$null;reason='TEST ONLY DEMO ATTENDANCE: synthetic manual-duty integration test; no physical attendance claim.';writtenEvidence='User-authorized isolated TEST ONLY demo employee and site. Synthetic duty window 27 October 2026 09:00-17:00 Asia/Karachi. No raw Check-In, Check-Out, registered device, biometric or location evidence exists or is asserted. Request the existing authorized HR reviewer to approve this explicitly synthetic manual-duty demonstration only. This is not real hiring approval, payroll eligibility, payment or legal evidence.'}
 SaveResult 'proposal' $proposal
 $submitted=RequireOk (Invoke-LocalAcceptanceRequest -Client $admin -Method POST -Path ($base+'/correction-requests') -Headers @{'Idempotency-Key'='DEMO-ATT-20261028-submit-v1'} -Body $proposal) 'Submit'
 SaveResult 'submitted-correction' $submitted
 Need ($submitted.state -eq 'Pending' -and !$submitted.changesAttendance) 'Submission unexpectedly applied attendance'
}
$decisionPath=Join-Path $root 'approved-correction.json'
if(Test-Path -LiteralPath $decisionPath){$approved=Get-Content -LiteralPath $decisionPath -Raw|ConvertFrom-Json}else{
 $body=@{expectedVersion=$submitted.version;decision=1;reason='TEST ONLY: reviewed explicit synthetic duty evidence for the isolated demo. Approve manual duty for integration testing only; no genuine biometric/punch attendance, hiring, salary, payment or legal approval is asserted.'}
 $denied=Invoke-LocalAcceptanceRequest -Client $admin -Method POST -Path ($base+'/corrections/'+$submitted.id+'/decide') -Headers @{'Idempotency-Key'='DEMO-ATT-20261028-deny-self-v1'} -Body $body
 SaveResult 'requester-self-review-denial' $denied
 Need ($denied.StatusCode -eq 403 -and $denied.Body.title -eq 'correction_requester_cannot_decide') 'Requester self-review guard did not deny as expected'
 $reviewed=RequireOk (Invoke-LocalAcceptanceRequest -Client $hr -Method GET -Path ($base+'/corrections/'+$submitted.id)) 'HR evidence read'
 SaveResult 'hr-reviewed-evidence' $reviewed
 Need ($reviewed.writtenEvidence -like '*synthetic*') 'Synthetic evidence label missing'
 $body.expectedVersion=$reviewed.request.version
 $approved=RequireOk (Invoke-LocalAcceptanceRequest -Client $hr -Method POST -Path ($base+'/corrections/'+$submitted.id+'/decide') -Headers @{'Idempotency-Key'='DEMO-ATT-20261028-approve-v1'} -Body $body) 'HR decision'
 SaveResult 'approved-correction' $approved
}
Need ($approved.request.state -eq 'Approved' -and $approved.request.changesAttendance -and $approved.appliedSessionRevisionId -and $approved.appliedSummaryRevisionId -and $approved.request.interpretation -eq 'hr_approved_manual_duty') 'Applied approval provenance missing'
$daily=RequireOk (Invoke-LocalAcceptanceRequest -Client $hr -Method GET -Path ($base+'/daily?businessDate=2026-10-27')) 'Daily read'
$calendar=RequireOk (Invoke-LocalAcceptanceRequest -Client $hr -Method GET -Path ($base+'/calendar?from=2026-10-27&to=2026-10-28')) 'Calendar read'
$history=RequireOk (Invoke-LocalAcceptanceRequest -Client $hr -Method GET -Path ($base+'/daily/history?businessDate=2026-10-27')) 'Summary history'
SaveResult 'daily' $daily; SaveResult 'calendar' $calendar;SaveResult 'summary-history' $history
Need ($daily.facts.workedMinutes -eq 480 -and $daily.latestSavedRevision.id -eq $approved.appliedSummaryRevisionId) 'Daily persisted summary mismatch'
# Calendar endpoints are inclusive, unlike correction queue ranges.
Need (@($calendar).Count -eq 2 -and $calendar[0].businessDate -eq '2026-10-27' -and $calendar[0].latestSavedRevision.id -eq $approved.appliedSummaryRevisionId -and $calendar[0].facts.workedMinutes -eq 480 -and $calendar[1].businessDate -eq '2026-10-28' -and $null -eq $calendar[1].latestSavedRevision) 'Calendar persisted summary mismatch'
Need ($history.totalCount -eq 1 -and $history.items[0].id -eq $approved.appliedSummaryRevisionId) 'Summary history mismatch'
Need ($null -eq $daily.latestSavedRevision.payableMinutes -and $null -eq $daily.latestSavedRevision.lateMinutes -and $null -eq $daily.latestSavedRevision.overtimeMinutes -and !$daily.policy.payrollReady) 'Factual-only boundary mismatch'
$result=[pscustomobject]@{Level='Authenticated HTTP + SQL-backed correction, daily and calendar';EmployeeNumber=$fixture.EmployeeNumber;EmployeeId=$id;BusinessDate='2026-10-27';Minutes=480;CorrectionId=$submitted.id;SessionId=$approved.appliedSessionRevisionId;SummaryId=$approved.appliedSummaryRevisionId;SelfReviewDenied=$true;Interpretation=$approved.request.interpretation;PolicyStatus=$daily.policy.status;HiringAcceptance=$false;GenuineBiometricAttendance=$false}
SaveResult 'http-result' $result
$result|ConvertTo-Json

} finally {
 foreach($ownedClient in @($admin,$hr)){if($ownedClient -and $ownedClient.WebSession -is [IDisposable]){$ownedClient.WebSession.Dispose()}}
}
