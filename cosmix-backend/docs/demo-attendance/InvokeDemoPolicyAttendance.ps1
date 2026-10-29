$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot '../access-provisioning/LocalAcceptanceHttp.ps1')
$root=Join-Path $env:LOCALAPPDATA 'Cosmix\LocalTesting\DemoAttendancePolicy20261028'
$fixture=Get-Content -LiteralPath (Join-Path $root 'fixture.json') -Raw|ConvertFrom-Json
$accounts=Get-Content -LiteralPath (Join-Path $env:LOCALAPPDATA 'Cosmix\LocalTesting\Test-Accounts.json') -Raw|ConvertFrom-Json
$admin=$null;$hr=$null;$script:checks=0;$script:requests=0
function Check($ok,[string]$name){if(!$ok){throw $name};$script:checks++}
function Request($client,[string]$method,[string]$path,$body=$null,$key=$null){
 $script:requests++;if($script:requests -gt 50){throw 'Bounded request limit'}
 $args=@{Client=$client;Method=$method;Path=$path};if($null -ne $body){$args.Body=$body};if($key){$args.Headers=@{'Idempotency-Key'=$key}}
 Invoke-LocalAcceptanceRequest @args
}
function Ok($r,[string]$step){if($r.StatusCode -ne 200){$title=if($r.Body){$r.Body.title}else{'no_problem_title'};throw "$step HTTP $($r.StatusCode): $title"};$script:checks++;return $r.Body}
function Save([string]$name,$value){$value|ConvertTo-Json -Depth 30|Set-Content -LiteralPath (Join-Path $root ($name+'.json'))}
try {
 $admin=New-LocalAcceptanceSession -BaseUri 'https://localhost:7240/' -Account ($accounts|Where-Object Email -eq 'admin@example.invalid') -SkipCertificateCheck
 $hr=New-LocalAcceptanceSession -BaseUri 'https://localhost:7240/' -Account ($accounts|Where-Object Email -eq 'hr@example.invalid') -SkipCertificateCheck
 $id=[string]$fixture.EmployeeId;$base='/api/v1/attendance/employees/'+$id
 $permissions=Ok (Request $hr GET '/api/v1/me/permissions') 'HR permissions'
 foreach($p in @('attendance.corrections.decide','attendance.summaries.read','attendance.summaries.calculate','attendance.policies.configure')){Check (@($permissions|Where-Object {$_.permission -eq $p -and $_.scopeKind -eq 7}).Count -gt 0) ('Missing existing HR permission '+$p)}
 $valid=Ok (Request $hr POST '/api/v1/hr/attendance-policies/validate' @{rules=$fixture.Rules}) 'Policy validation'
 Check ($valid.isValid -and !$valid.published) 'Explicit rules validation failed';Save 'rules-validation' $valid
 $invalidRules=$fixture.Rules|ConvertTo-Json -Depth 15|ConvertFrom-Json
 $invalidRules.arrival.halfDayAfterShiftStartSeconds=100
 $invalid=Ok (Request $hr POST '/api/v1/hr/attendance-policies/validate' @{rules=$invalidRules}) 'Invalid thresholds validation'
 Check (!$invalid.isValid -and @($invalid.errors).Count -gt 0) 'Invalid thresholds accepted';Save 'invalid-rules-denial' $invalid
 $policy=Ok (Request $hr GET ('/api/v1/hr/attendance-policies/'+$fixture.PolicyId)) 'Policy detail'
 $preview=Request $hr POST ('/api/v1/hr/attendance-policies/'+$fixture.PolicyId+'/publication-preview') @{expectedVersion=$policy.version;expectedRevision=$policy.revision;effectiveFrom='2026-10-28';effectiveTo='2026-10-29';rules=$fixture.Rules}
 Check ($preview.StatusCode -eq 200 -and !$preview.Body.canPublish -and @($preview.Body.errors|Where-Object code -eq 'policy_future_date_required').Count -gt 0) 'Expected specific future-date publication rejection';Save 'nonfuture-publication-denial' $preview
 $source=Ok (Request $admin GET ($base+'/correction-requests/sources?businessDate=2026-10-28')) 'Source preview'
 Check ($source.historicalRoster.status -eq 'scheduled' -and $source.historicalRoster.holiday.status -eq 'configured' -and $source.historicalRoster.holiday.calendarId -eq $fixture.HolidayCalendarId -and @($source.historicalRoster.holiday.holidays).Count -eq 0 -and !$source.periodLocked) 'Policy demo roster/calendar mismatch';Save 'source' $source
 $path=Join-Path $root 'submitted.json'
 if(Test-Path -LiteralPath $path){$submitted=Get-Content -LiteralPath $path -Raw|ConvertFrom-Json}else{
  $proposal=@{businessDate='2026-10-28';kind=4;rawPunchId=$null;expectedAssessmentId=$source.latestAssessmentId;expectedSummaryId=$source.latestSummaryId;expectedSourceFingerprint=$source.sourceFingerprint;requestedTimeUtc='2026-10-28T04:00:00Z';requestedEndUtc='2026-10-28T12:00:00Z';requestedStatus=$null;reason='TEST ONLY demo #33: synthetic eight-hour duty for configured-policy integration verification.';writtenEvidence='TEST ONLY: isolated demo employee, site and dated schema-3 test policy. Synthetic duty on 28 October 2026 09:00-17:00 Asia/Karachi. No physical attendance, location, camera, face or raw punches asserted. Holiday calendar contains no test holiday for this date. No salary payment, financial posting or legal approval authorized. Distinct HR review required.'}
  Save 'proposal' $proposal
  $submitted=Ok (Request $admin POST ($base+'/correction-requests') $proposal 'DEMO-POLICY-20261028-SUBMIT') 'Submit'
  Check ($submitted.state -eq 'Pending' -and !$submitted.changesAttendance) 'Submission applied attendance prematurely';Save 'submitted' $submitted
 }
 $path=Join-Path $root 'approved.json'
 if(Test-Path -LiteralPath $path){$approved=Get-Content -LiteralPath $path -Raw|ConvertFrom-Json}else{
  $review=Ok (Request $hr GET ($base+'/corrections/'+$submitted.id)) 'HR evidence read'
  $body=@{expectedVersion=$review.request.version;decision=1;reason='TEST ONLY: approve explicitly synthetic eight-hour demo duty after reviewing evidence and configured demo schedule. No real biometric/payroll/legal approval asserted.'}
  Save 'decision-body' $body
  $denied=Request $admin POST ($base+'/corrections/'+$submitted.id+'/decide') $body 'DEMO-POLICY-20261028-SELF-DENIAL'
  Check ($denied.StatusCode -eq 403 -and $denied.Body.title -eq 'correction_requester_cannot_decide') 'Self-review was not denied';Save 'self-review-denial' $denied
  $approved=Ok (Request $hr POST ($base+'/corrections/'+$submitted.id+'/decide') $body 'DEMO-POLICY-20261028-APPROVE') 'Independent HR approval';Save 'approved' $approved
 }
 $body=Get-Content -LiteralPath (Join-Path $root 'decision-body.json') -Raw|ConvertFrom-Json
 $retry=Ok (Request $hr POST ($base+'/corrections/'+$submitted.id+'/decide') $body 'DEMO-POLICY-20261028-APPROVE') 'Exact approval replay'
 Check ($retry.decisionId -eq $approved.decisionId -and $retry.appliedSessionRevisionId -eq $approved.appliedSessionRevisionId -and $retry.appliedSummaryRevisionId -eq $approved.appliedSummaryRevisionId) 'Replay changed attendance';Save 'replay' $retry
 $daily=Ok (Request $hr GET ($base+'/daily?businessDate=2026-10-28')) 'Calculated daily read';Save 'daily' $daily
 Check ($daily.policy.status -eq 'calculated' -and $daily.policy.dayStatus -eq 2 -and $daily.policy.policyVersionId -eq $fixture.PolicyVersionId -and $daily.facts.workedMinutes -eq 480 -and $daily.policy.payableMinutes -eq 480 -and $daily.policy.lateMinutes -eq 0 -and $daily.policy.overtimeMinutes -eq 0 -and !$daily.policy.payrollReady) 'Expected Present/480/0/0 nonfinancial calculation not returned'
 Check ($daily.latestSavedRevision.id -eq $approved.appliedSummaryRevisionId -and $daily.latestSavedRevision.status -eq 'Present' -and $daily.latestSavedRevision.payableMinutes -eq 480) 'Persisted calculation differs from API'
 $stale=Request $hr POST ($base+'/daily/recalculate') @{businessDate='2026-10-28';expectedRevision=0;reason='TEST ONLY stale revision rejection check'} 'DEMO-POLICY-20261028-STALE'
 Check ($stale.StatusCode -eq 409 -and $stale.Body.title -eq 'attendance_summary_revision_conflict') 'Stale revision accepted';Save 'stale-denial' $stale
 $calendar=Ok (Request $hr GET ($base+'/calendar?from=2026-10-01&to=2026-10-31')) 'Full month';Save 'calendar' $calendar
 Check (@($calendar).Count -eq 31 -and @($calendar|Where-Object {$_.businessDate -eq '2026-10-28' -and $_.policy.dayStatus -eq 2 -and $_.latestSavedRevision.id -eq $approved.appliedSummaryRevisionId}).Count -eq 1) 'Full month lost calculated date'
 $prior=@($calendar|Where-Object businessDate -eq '2026-10-27')[0]
 Check ($prior.latestSavedRevision.id -eq '35629bd1-7641-486e-9932-81df6662cedd' -and $prior.facts.workedMinutes -eq 480 -and $prior.latestSavedRevision.policyVersionId -eq $null) 'Prior Oct27 history changed'
 $history=Ok (Request $hr GET ($base+'/daily/history?businessDate=2026-10-28')) 'History';Check ($history.totalCount -eq 1) 'Duplicate summary';Save 'history' $history
 $result=[pscustomobject]@{Employee=33;BusinessDate='2026-10-28';HttpChecks=$script:checks;BusinessRequests=$script:requests;DayStatus='Present';WorkedMinutes=480;PayableMinutes=480;LateMinutes=0;OvertimeMinutes=0;PayrollReady=$false;SummaryId=$approved.appliedSummaryRevisionId;SessionId=$approved.appliedSessionRevisionId;PolicyVersionId=$fixture.PolicyVersionId;PriorDayPreserved=$true;RealBiometricsTested=$false}
 Save 'result' $result;$result|ConvertTo-Json
}finally{
 foreach($client in @($admin,$hr)){if($client){try{$r=Invoke-LocalAcceptanceRequest -Client $client -Method POST -Path '/api/v1/auth/web/logout';Write-Output ('Owned test session logout HTTP '+$r.StatusCode)}finally{if($client.WebSession -is [IDisposable]){$client.WebSession.Dispose()}}}}
}
