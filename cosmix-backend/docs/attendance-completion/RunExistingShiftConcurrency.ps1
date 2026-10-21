Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Huma-Acceptance-Common.ps1')
. (Join-Path $PSScriptRoot '../access-provisioning/LocalAcceptanceHttp.ps1')
$root = 'C:\Users\Huma.Khan\AppData\Local\Cosmix\LocalTesting'
$intent=Join-Path $root 'Attendance-Shift-Concurrency-20261013-B04-intent.json'
$receipt=Join-Path $root 'Attendance-Shift-Concurrency-20261013-B04-result.json'
if ((Test-Path -LiteralPath $intent) -or (Test-Path -LiteralPath $receipt)) { throw 'Prior concurrency attempt exists; reconcile rather than automatically rerun.' }
Assert-HumaPrivate $root
$fixture=Get-Content -LiteralPath (Join-Path $root 'Attendance-Shift-Acceptance-20261013-B03-result.json') -Raw | ConvertFrom-Json
$credentials=@(Get-Content -LiteralPath (Join-Path $root 'Test-Accounts.json') -Raw | ConvertFrom-Json)
$hr=$credentials | Where-Object Email -CEQ 'hr@example.invalid'
function Check([bool]$value,[string]$why) { if (-not $value) { throw $why } }
$a=New-LocalAcceptanceSession -BaseUri 'https://localhost:7481/' -Account $hr -SkipCertificateCheck
$b=New-LocalAcceptanceSession -BaseUri 'https://localhost:7481/' -Account $hr -SkipCertificateCheck
$current=Invoke-LocalAcceptanceRequest -Client $a -Method GET -Path ('/api/v1/hr/shifts/' + $fixture.ShiftId)
Check ($current.StatusCode -eq 200) 'Current source shift unavailable.'
Write-HumaNewJson $intent @{ Database='Cosmix_Testing'; Actor='Synthetic HR'; ShiftId=$fixture.ShiftId;
    ExpectedVersion=$current.Body.version; Requests='Two separate browser sessions and HTTP connections for each of two races';
    AtUtc=[DateTimeOffset]::UtcNow.ToString('O') }
function Http($session) {
    $handler=[Net.Http.HttpClientHandler]::new()
    $handler.CookieContainer=$session.WebSession.Cookies
    $handler.ServerCertificateCustomValidationCallback=[Net.Http.HttpClientHandler]::DangerousAcceptAnyServerCertificateValidator
    $client=[Net.Http.HttpClient]::new($handler)
    $client.Timeout=[TimeSpan]::FromSeconds(30)
    return $client
}
function Request($session,[string]$method,[string]$path,$body,[string]$key) {
    Assert-LocalAcceptanceUri ([uri]$session.BaseUri)
    $request=[Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::new($method),[uri]::new([uri]$session.BaseUri,$path))
    $request.Content=[Net.Http.StringContent]::new(($body | ConvertTo-Json -Depth 10 -Compress),[Text.Encoding]::UTF8,'application/json')
    $request.Headers.Add('X-CSRF-TOKEN',$session.CsrfToken)
    $request.Headers.Add('Idempotency-Key',$key)
    return $request
}
function Race($requestA,$requestB) {
    $httpA=Http $a; $httpB=Http $b
    try {
        $started=[DateTimeOffset]::UtcNow
        $taskA=$httpA.SendAsync($requestA)
        $firstIncomplete=-not $taskA.IsCompleted
        $taskB=$httpB.SendAsync($requestB)
        [Threading.Tasks.Task]::WhenAll([Threading.Tasks.Task[]]@($taskA,$taskB)).GetAwaiter().GetResult()
        Check $firstIncomplete 'Requests did not overlap; no concurrency acceptance claimed.'
        $responseA=$taskA.Result; $responseB=$taskB.Result
        try {
            $bodyA=$responseA.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
            $bodyB=$responseB.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
            return [pscustomobject]@{ A=[int]$responseA.StatusCode; B=[int]$responseB.StatusCode; BodyA=$bodyA; BodyB=$bodyB;
                Overlap=$true; Started=$started.ToString('O'); Finished=[DateTimeOffset]::UtcNow.ToString('O') }
        } finally { $responseA.Dispose();$responseB.Dispose() }
    } finally { $requestA.Dispose();$requestB.Dispose();$httpA.Dispose();$httpB.Dispose() }
}
$path='/api/v1/hr/shifts/' + $fixture.ShiftId
$renameA=@{name='TEST ONLY Race Winner A B04';expectedVersion=$current.Body.version;reason='Synthetic concurrent rename A, exact shared source version.'}
$renameB=@{name='TEST ONLY Race Winner B B04';expectedVersion=$current.Body.version;reason='Synthetic concurrent rename B, exact shared source version.'}
$rename=Race (Request $a 'PUT' $path $renameA 'huma-attendance-b04-race-a') (Request $b 'PUT' $path $renameB 'huma-attendance-b04-race-b')
Check ((@($rename.A,$rename.B) | Sort-Object) -join ',' -ceq '200,409') 'Expected exactly one rename winner and one stale-source conflict.'
$loser=if($rename.A -eq 409){$rename.BodyA}else{$rename.BodyB}
Check ($loser.title -ceq 'scheduling_version_conflict') 'Concurrent loser did not reject the stale source.'
$create=@{code='ATTHUMA13B04';name='TEST ONLY Same-Key Concurrent Site Shift B04';reason='Synthetic concurrent retry fixture, not a company schedule.';
    initialSchedule=@{effectiveFrom='2026-10-14';startTime='09:00:00';endTime='18:00:00';endDayOffset=0;scheduledMinutes=540;paidBreakMinutes=0;unpaidBreakMinutes=0;applicableWeekdays=127}}
$same=Race (Request $a 'POST' '/api/v1/hr/shifts' $create 'huma-attendance-b04-same-key') (Request $b 'POST' '/api/v1/hr/shifts' $create 'huma-attendance-b04-same-key')
Check ($same.A -eq 201 -and $same.B -eq 201 -and $same.BodyA.id -ceq $same.BodyB.id -and $same.BodyA.version -ceq $same.BodyB.version) 'Concurrent same-key retry did not return one exact identity.'
foreach ($session in @($a,$b)) {
    $logout=Invoke-LocalAcceptanceRequest -Client $session -Method POST -Path '/api/v1/auth/web/logout'
    Check ($logout.StatusCode -eq 204) 'Concurrency session logout failed.'
}
Write-HumaNewJson $receipt @{ Database='Cosmix_Testing'; OriginalShiftId=$fixture.ShiftId; CreatedShiftId=$same.BodyA.id;
    RenameStatus=@($rename.A,$rename.B); RenameOverlap=$rename.Overlap; RenameStarted=$rename.Started; RenameFinished=$rename.Finished;
    SameKeyStatus=@($same.A,$same.B); SameKeyOverlap=$same.Overlap; SameKeyStarted=$same.Started; SameKeyFinished=$same.Finished;
    SameCommittedIdentity=$true; Limits='HTTP overlap verified; independent SQL row/audit/receipt inspection required. No payroll/punch/face acceptance claimed.' }
$credentials=$null;$hr=$null;$a=$null;$b=$null
Write-Output 'PASS: actual overlapping HTTP writes through separate MFA sessions/connections: rename 200/409; concurrent same-key create 201/201 with one exact identity.'
