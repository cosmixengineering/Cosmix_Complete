param([string]$BackendRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')))
$ErrorActionPreference = 'Stop'
$c05Required = @(
    'src/Cosmix.Application/Attendance/Waiting/WaitingContracts.cs',
    'src/Cosmix.Application/Attendance/Waiting/WaitingAttribution.cs',
    'src/Cosmix.Application/Attendance/Waiting/WaitingSources.cs',
    'src/Cosmix.Application/Attendance/Waiting/WaitingWorkflowService.cs',
    'src/Cosmix.Application/Attendance/Waiting/WaitingPerformanceEffectReader.cs',
    'src/Cosmix.Infrastructure/Repositories/Attendance/WaitingWorkflowRepository.cs',
    'src/Cosmix.Api/Controllers/Attendance/WaitingControllers.cs')
foreach ($c05Relative in $c05Required) {
    $c05File = Join-Path $BackendRoot $c05Relative
    if (!(Test-Path -LiteralPath $c05File) -or (Get-Item -LiteralPath $c05File).Length -eq 0) {
        throw "C05 missing implementation: $c05File"
    }
}
$c05Service = [IO.File]::ReadAllText((Join-Path $BackendRoot $c05Required[3]))
$c05Review = [IO.File]::ReadAllText((Join-Path $BackendRoot 'src/Cosmix.Application/Attendance/Waiting/WaitingReviews.cs'))
$c05Reader = [IO.File]::ReadAllText((Join-Path $BackendRoot $c05Required[4]))
$c05Repository = [IO.File]::ReadAllText((Join-Path $BackendRoot $c05Required[5]))
if ($c05Service -match 'new WaitingTimeEvidence' -or $c05Review -notmatch 'new WaitingTimeEvidence' -or $c05Review -notmatch 'ApprovalState.Approved') {
    throw 'Verified evidence must have a real approved review path.'
}
if ($c05Reader -match 'operation\.(ReadAsync|WriteAsync)' -or $c05Reader -notmatch 'RequireTransaction') {
    throw 'Consumer reader must use the existing caller transaction.'
}
if ($c05Repository -notmatch 'AsNoTracking' -or $c05Repository -notmatch 'Take\(' -or
    $c05Repository -notmatch 'AuditEvents' -or $c05Repository -notmatch 'ApprovalRequests') {
    throw 'Real bounded protected persistence required.'
}
Write-Output 'C05 source presence/boundary guards passed. This is not build or behavioral acceptance.'
