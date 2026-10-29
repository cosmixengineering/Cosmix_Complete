[CmdletBinding()]
param([string]$ReportPath = 'TestResults/dependency-audit.json')
$ErrorActionPreference = 'Stop'
$project = Join-Path (Split-Path -Parent $PSScriptRoot) 'tests/Cosmix.Tests/Cosmix.Tests.csproj'
$report = & dotnet list $project package --vulnerable --include-transitive --format json --output-version 1
if ($LASTEXITCODE -ne 0) { throw 'Dependency audit could not complete.' }
$parsed = ($report -join "`n") | ConvertFrom-Json
if ($parsed.version -ne 1 -or $null -eq $parsed.projects -or @($parsed.projects).Count -eq 0 -or ($null -ne $parsed.problems -and @($parsed.problems).Count -gt 0)) {
    throw 'Dependency audit returned incomplete results.'
}
$destination = [IO.Path]::GetFullPath($ReportPath)
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destination) | Out-Null
($report -join "`n") | Set-Content -LiteralPath $destination -Encoding utf8
$blocking = @(
    foreach ($entry in $parsed.projects) {
        foreach ($framework in $entry.frameworks) {
            foreach ($package in @($framework.topLevelPackages) + @($framework.transitivePackages)) {
                foreach ($vulnerability in $package.vulnerabilities) {
                    if ($vulnerability.severity -in @('High','Critical')) { $package.id }
                }
            }
        }
    }
)
if ($blocking.Count -gt 0) { throw "High/critical dependency advisories found. Review the retained audit report ($($blocking.Count) affected package entries)." }
Write-Output 'Dependency audit completed with no high/critical advisories.'
