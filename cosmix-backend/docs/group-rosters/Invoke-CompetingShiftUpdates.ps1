[CmdletBinding()]
param(
    [Parameter(Mandatory)][object]$AdminClient,
    [Parameter(Mandatory)][string]$FixturePath,
    [Parameter(Mandatory)][string]$MainEvidencePath,
    [Parameter(Mandatory)][string]$EvidencePath
)

# Explicit invocation only. Root supplies a genuine current MFA session and owns execution.
# Renames one existing synthetic shift. No new account, grant, fixture, SQL or automatic retry.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot '../access-provisioning/LocalAcceptanceHttp.ps1')
Assert-LocalAcceptanceUri ([uri]$AdminClient.BaseUri)
if ($AdminClient.CsrfHeaderName -cne 'X-CSRF-TOKEN' -or [string]::IsNullOrWhiteSpace($AdminClient.CsrfToken)) {
    throw 'Require the current authenticated browser CSRF context.'
}
$privateRoot = 'C:\Users\Affan.Ahmed\AppData\Local\Cosmix\AccessAcceptance\20261009'
function PrivatePath([string]$Path) {
    if (-not [IO.Path]::IsPathFullyQualified($Path)) { throw 'Use absolute private acceptance paths.' }
    $full = [IO.Path]::GetFullPath($Path)
    if (-not $full.StartsWith($privateRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Path is outside the approved private root.' }
    for ($cursor = (Get-Item -LiteralPath ([IO.Path]::GetDirectoryName($full)) -Force); $null -ne $cursor; $cursor = $cursor.Parent) {
        if ($cursor.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked evidence paths are not permitted.' }
    }
    if (Test-Path -LiteralPath $full) {
        $file = Get-Item -LiteralPath $full -Force
        if ($file.PSIsContainer -or ($file.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Require regular private evidence files.' }
    }
    return $full
}
$FixturePath = PrivatePath $FixturePath
$MainEvidencePath = PrivatePath $MainEvidencePath
$EvidencePath = PrivatePath $EvidencePath
if (Test-Path -LiteralPath $EvidencePath) { throw 'Competing-update evidence already exists. Preserve it; never blindly rerun writes.' }
$fixture = Get-Content -LiteralPath $FixturePath -Raw | ConvertFrom-Json -Depth 30
$main = Get-Content -LiteralPath $MainEvidencePath -Raw | ConvertFrom-Json -Depth 80
if ($fixture.Database -cne 'Cosmix_Testing' -or -not $fixture.Committed -or
    $fixture.RunId -cnotmatch '^ACC[A-Z0-9]{6,18}$' -or $main.runId -cne $fixture.RunId -or
    $main.database -cne 'Cosmix_Testing' -or -not $main.completed -or @($main.created.shiftIds).Count -ne 3) {
    throw 'Require the exact committed synthetic fixture and completed main scheduling evidence.'
}
$runId = [string]$fixture.RunId
$shiftId = ([guid]$main.created.shiftIds[0]).ToString()
if ($shiftId -eq [guid]::Empty.ToString()) { throw 'Missing original synthetic base shift.' }
$path = "/api/v1/hr/shifts/$shiftId"
$checks = 0
function Check([bool]$Condition, [string]$Name) {
    if (-not $Condition) { throw "Competing update acceptance failed: $Name. Inspect retained private evidence." }
    $script:checks++
}
function Json($Value) { ConvertTo-Json -InputObject $Value -Depth 80 -Compress }
function Save {
    $script:state.checks = $script:checks
    $script:state | ConvertTo-Json -Depth 80 | Set-Content -LiteralPath $EvidencePath -Encoding utf8
}

$probe = Invoke-LocalAcceptanceRequest -Client $AdminClient -Method GET -Path '/api/v1/auth/web/local-testing'
Check ($probe.StatusCode -eq 200 -and $probe.Body.enabled -eq $true -and $probe.Body.database -ceq 'Cosmix_Testing') 'Host confirms isolated local testing mode'
$beforeResponse = Invoke-LocalAcceptanceRequest -Client $AdminClient -Method GET -Path $path
Check ($beforeResponse.StatusCode -eq 200) 'Current authenticated Admin can read the existing shift'
$before = $beforeResponse.Body
Check ($before.id -eq $shiftId -and $before.code -ceq "$runId.BASE" -and $before.isActive -and @($before.versions).Count -eq 2) 'Target is exactly the retained main-run synthetic base shift'
$attempts = @(
    [ordered]@{ key = "$runId.competing.first"; request = [ordered]@{ name = "SYNTHETIC $runId competing FIRST";
        expectedVersion = $before.version; reason = "SYNTHETIC $runId first competing writer" } },
    [ordered]@{ key = "$runId.competing.second"; request = [ordered]@{ name = "SYNTHETIC $runId competing SECOND";
        expectedVersion = $before.version; reason = "SYNTHETIC $runId second competing writer" } }
)
Check ($attempts[0].key -cne $attempts[1].key -and $attempts[0].request.name -cne $attempts[1].request.name -and
    $attempts[0].request.expectedVersion -ceq $attempts[1].request.expectedVersion) 'Two distinct updates compete for the same current rowversion'
$state = [ordered]@{ database = 'Cosmix_Testing'; runId = $runId; shiftId = $shiftId; actorAccountId = $fixture.AdminAccountId;
    method = 'PUT'; path = $path; before = $before; attempts = $attempts; responses = @(); after = $null;
    requestsDispatchedBeforeAwait = $false; completed = $false; checks = $checks }
# Reserve evidence before either HTTP mutation; an uncertain run can be reconciled from both keys.
$reservation = [IO.FileStream]::new($EvidencePath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
$reservation.Dispose()
Save

$handler = [Net.Http.HttpClientHandler]::new()
$handler.CookieContainer = $AdminClient.WebSession.Cookies
$handler.UseCookies = $true
$handler.AllowAutoRedirect = $false
$handler.MaxConnectionsPerServer = 2
if ($AdminClient.SkipCertificateCheck) {
    # The URL is restricted to the already verified HTTPS localhost application.
    $handler.ServerCertificateCustomValidationCallback = [Net.Http.HttpClientHandler]::DangerousAcceptAnyServerCertificateValidator
}
$http = [Net.Http.HttpClient]::new($handler)
$http.Timeout = [TimeSpan]::FromSeconds(45)
$requests = @(); $responses = @()
try {
    foreach ($attempt in $attempts) {
        $request = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::Put, ([uri]::new([uri]$AdminClient.BaseUri, $path)))
        $request.Content = [Net.Http.StringContent]::new((Json $attempt.request), [Text.Encoding]::UTF8, 'application/json')
        [void]$request.Headers.TryAddWithoutValidation('Idempotency-Key', [string]$attempt.key)
        [void]$request.Headers.TryAddWithoutValidation('X-CSRF-TOKEN', [string]$AdminClient.CsrfToken)
        $requests += $request
    }
    # Both real network requests start before waiting for either response. No sequential retry.
    $firstPending = $http.SendAsync($requests[0])
    $secondPending = $http.SendAsync($requests[1])
    $state.requestsDispatchedBeforeAwait = $true
    Save
    [void][Threading.Tasks.Task]::WhenAll([Threading.Tasks.Task[]]@($firstPending, $secondPending)).GetAwaiter().GetResult()
    $responses = @($firstPending.GetAwaiter().GetResult(), $secondPending.GetAwaiter().GetResult())
    for ($index = 0; $index -lt $responses.Count; $index++) {
        $content = $responses[$index].Content.ReadAsStringAsync().GetAwaiter().GetResult()
        $body = $null
        if (-not [string]::IsNullOrWhiteSpace($content)) {
            try { $body = ConvertFrom-Json -InputObject $content -Depth 80 }
            catch { throw 'Competing response was not JSON; response text suppressed.' }
        }
        # Store only synthetic request/result data. Never save response headers, clients or cookies.
        $state.responses += [ordered]@{ key = $attempts[$index].key; status = [int]$responses[$index].StatusCode; body = $body }
    }
    Save
}
finally {
    foreach ($response in $responses) { $response.Dispose() }
    foreach ($request in $requests) { $request.Dispose() }
    $http.Dispose()
}
$successful = @($state.responses | Where-Object status -eq 200)
$conflicted = @($state.responses | Where-Object status -eq 409)
Check ($successful.Count -eq 1 -and $conflicted.Count -eq 1) 'Exactly one concurrent update succeeds and the other returns conflict'
$winner = $successful[0]
$loser = $conflicted[0]
$winnerAttempt = @($attempts | Where-Object key -ceq $winner.key)[0]
$loserAttempt = @($attempts | Where-Object key -ceq $loser.key)[0]
Check ($winner.body.id -eq $shiftId -and $winner.body.name -ceq $winnerAttempt.request.name -and
    $winner.body.version -cne $before.version) 'Success result identifies its writer and a new stored rowversion'
$afterResponse = Invoke-LocalAcceptanceRequest -Client $AdminClient -Method GET -Path $path
Check ($afterResponse.StatusCode -eq 200) 'Read persisted state after both writes complete'
$state.after = $afterResponse.Body
Save
Check ($state.after.name -ceq $winnerAttempt.request.name -and $state.after.name -cne $loserAttempt.request.name -and
    $state.after.version -ceq $winner.body.version) 'Only the winning name and version persisted'
Check ($state.after.code -ceq $before.code -and $state.after.isActive -eq $before.isActive -and
    (Json $state.after.versions) -ceq (Json $before.versions)) 'Competing metadata writes preserve immutable schedule history'
$state.winnerKey = $winner.key
$state.loserKey = $loser.key
$state.completed = $true
Save
Write-Output "PASS Competing Shift Updates: $checks HTTP checks; exactly one 200 and one 409, persisted winner verified. Private evidence retained."
