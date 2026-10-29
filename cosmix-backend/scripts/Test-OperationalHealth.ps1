#requires -Version 7.0
[CmdletBinding()]
param([Parameter(Mandatory)][uri]$Origin, [switch]$LocalDrill)
$ErrorActionPreference = 'Stop'
if ($Origin.UserInfo -or $Origin.Query -or $Origin.Fragment -or $Origin.AbsolutePath -ne '/' -or
    ($Origin.Scheme -ne 'https' -and -not ($LocalDrill -and $Origin.IsLoopback -and $Origin.Scheme -eq 'http'))) {
    throw 'An HTTPS origin is required; HTTP is restricted to explicit loopback drills.'
}
$handler = [Net.Http.HttpClientHandler]::new()
$handler.AllowAutoRedirect = $false
$client = [Net.Http.HttpClient]::new($handler)
$client.Timeout = [TimeSpan]::FromSeconds(10)
try {
    $checks = foreach ($name in @('live','ready','workers','payroll-ready')) {
        $ok = $false
        try {
            $response = $client.GetAsync([uri]::new($Origin, '/health/'+$name)).GetAwaiter().GetResult()
            try { $ok = [int]$response.StatusCode -eq 200 } finally { $response.Dispose() }
        } catch { }
        [pscustomobject]@{check=$name;healthy=$ok}
    }
    [pscustomobject]@{observedAtUtc=[DateTimeOffset]::UtcNow.ToString('o');checks=@($checks)} | ConvertTo-Json -Depth 4
    if (@($checks | Where-Object {-not $_.healthy}).Count) { exit 1 }
} finally { $client.Dispose() }
