$ErrorActionPreference = 'Stop'
$private = 'C:\Users\Affan.Ahmed\AppData\Local\Cosmix\AccessAcceptance\20261009'
$metadata = Get-Content -LiteralPath (Join-Path $private 'host.json') -Raw | ConvertFrom-Json
$ownedProcess = Get-CimInstance Win32_Process -Filter "ProcessId = $([int]$metadata.ProcessId)"
if ($null -eq $ownedProcess) { Write-Host 'Owned acceptance host is already stopped.'; return }
$expectedAssembly = Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) 'src\Cosmix.Api\bin\Release\net10.0\Cosmix.Api.dll'
if ($ownedProcess.Name -ne 'dotnet.exe' -or -not $ownedProcess.CommandLine.Contains($expectedAssembly) -or
    -not $ownedProcess.CommandLine.Contains('https://localhost:7249') -or
    [Math]::Abs(([DateTimeOffset]$ownedProcess.CreationDate - [DateTimeOffset]$metadata.StartedAtUtc).TotalSeconds) -gt 10) {
    throw 'PID identity changed; refusing to stop another process.'
}
Stop-Process -Id ([int]$metadata.ProcessId) -ErrorAction Stop
Write-Host "Stopped owned acceptance host PID $($metadata.ProcessId)."
