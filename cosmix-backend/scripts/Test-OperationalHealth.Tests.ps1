#requires -Version 7.4
# Isolated HTTP doubles exercise monitor semantics, never a company service.
$ErrorActionPreference = 'Stop'
$checks = 0
$executable = (Get-Process -Id $PID).Path
$probe = Join-Path $PSScriptRoot 'Test-OperationalHealth.ps1'
foreach ($case in @(
    @{Name='healthy';Status='200 OK';Type='text/plain; charset=utf-8';Body='Healthy';Expected=0},
    @{Name='SPA fallback';Status='200 OK';Type='text/html';Body='<html>Healthy</html>';Expected=1},
    @{Name='degraded';Status='200 OK';Type='text/plain';Body='Degraded';Expected=1},
    @{Name='wrong body';Status='200 OK';Type='text/plain';Body='OK';Expected=1},
    @{Name='unavailable';Status='503 Service Unavailable';Type='text/plain';Body='Healthy';Expected=1},
    @{Name='oversized response';Status='200 OK';Type='text/plain';Body=('x'*2048);Expected=1})) {
    $job = Start-Job -ArgumentList $case -ScriptBlock {
        param($Case)
        $server = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0)
        $server.Start()
        try {
            $server.LocalEndpoint.Port
            for ($i=0; $i -lt 4; $i++) {
                $client = $server.AcceptTcpClient()
                try {
                    $stream = $client.GetStream()
                    $stream.ReadTimeout=5000
                    $buffer = [byte[]]::new(8192)
                    $null = $stream.Read($buffer,0,$buffer.Length)
                    $body=[Text.Encoding]::UTF8.GetBytes($Case.Body)
                    $header=[Text.Encoding]::ASCII.GetBytes("HTTP/1.1 $($Case.Status)`r`nContent-Type: $($Case.Type)`r`nContent-Length: $($body.Length)`r`nConnection: close`r`n`r`n")
                    $stream.Write($header); $stream.Write($body)
                } finally { $client.Dispose() }
            }
        } finally { $server.Stop() }
    }
    try {
        $port = $null
        $deadline = [DateTime]::UtcNow.AddSeconds(15)
        while (-not $port -and [DateTime]::UtcNow -lt $deadline) {
            $port = @(Receive-Job $job -Keep | Where-Object { $_ -is [int] } | Select-Object -First 1)[0]
            if (-not $port) { Start-Sleep -Milliseconds 100 }
        }
        if (-not $port) { throw 'Local health double failed to start.' }
        $raw = & $executable -NoProfile -File $probe -Origin "http://127.0.0.1:$port" -LocalDrill
        $code = $LASTEXITCODE
        $report = ($raw -join "`n") | ConvertFrom-Json
        if ($code -ne $case.Expected -or $report.checks.Count -ne 4 -or
            @($report.checks | Where-Object { $_.healthy -ne ($case.Expected -eq 0) }).Count -ne 0) {
            throw "Health probe incorrectly classified $($case.Name)."
        }
        $checks++
    } finally { Stop-Job $job -ErrorAction SilentlyContinue; Remove-Job $job -Force }
}
[pscustomobject]@{Passed=$checks;Failed=0;CompanyServiceTested=$false}
