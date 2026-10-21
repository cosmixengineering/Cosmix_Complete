param(
    [string]$ReleaseDirectory = 'D:/CosmixVerification/hr-backend-20261020/hr-vehicle-v1-build09/bin/Cosmix.Api/release',
    [switch]$ProfileChecks,
    [switch]$NativeDispatcher
)
$ErrorActionPreference = 'Stop'
if ($ProfileChecks -and $NativeDispatcher) { throw 'Select one native suite.' }
$release = [IO.Path]::GetFullPath($ReleaseDirectory).TrimEnd('\')
if (!$release.StartsWith('D:\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Frozen input must be on D.' }
$output = Join-Path 'D:/CosmixVerification/hr-backend-20261020' ('report-target-offline-' + [Guid]::NewGuid().ToString('N'))
$null = [IO.Directory]::CreateDirectory($output)
$dotnet = (Get-Command dotnet -CommandType Application | Select-Object -First 1).Source
$runtime = Split-Path -Parent $dotnet
$sdk = Get-ChildItem -LiteralPath (Join-Path $runtime 'sdk') -Directory |
    Where-Object Name -Match '^10\.\d+\.\d+$' | Sort-Object { [Version]$_.Name } -Descending | Select-Object -First 1
$references = @{}
foreach ($name in @('Microsoft.NETCore.App.Ref', 'Microsoft.AspNetCore.App.Ref')) {
    $pack = Get-ChildItem -LiteralPath (Join-Path $runtime ('packs/' + $name)) -Directory |
        Where-Object Name -Match '^10\.\d+\.\d+$' | Sort-Object { [Version]$_.Name } -Descending | Select-Object -First 1
    foreach ($dll in Get-ChildItem -LiteralPath (Join-Path $pack.FullName 'ref/net10.0') -Filter '*.dll') {
        $references[$dll.Name] = $dll.FullName
    }
}
$inputs = @()
foreach ($dll in Get-ChildItem -LiteralPath $release -Filter '*.dll') {
    $copy = Join-Path $output $dll.Name
    $hash = (Get-FileHash -LiteralPath $dll.FullName).Hash
    [IO.File]::Copy($dll.FullName, $copy, $false)
    if ((Get-FileHash -LiteralPath $copy).Hash -ne $hash) { throw 'Frozen input changed.' }
    $inputs += @{Path=$dll.FullName; SHA256=$hash}
    if (!$references.ContainsKey($dll.Name)) { $references[$dll.Name] = $copy }
}
# Use the platform runtime asset without changing any assembly metadata or authority.
$sqlClient = Join-Path $release 'runtimes/win/lib/net9.0/Microsoft.Data.SqlClient.dll'
if (Test-Path -LiteralPath $sqlClient) {
    $copy = Join-Path $output 'Microsoft.Data.SqlClient.dll'
    [IO.File]::Copy($sqlClient, $copy, $true)
    $references['Microsoft.Data.SqlClient.dll'] = $copy
    $inputs += @{Path=$sqlClient; SHA256=(Get-FileHash -LiteralPath $sqlClient).Hash}
}
$suite = if ($ProfileChecks) { 'VerifyHrReportTestingProfile' } else { 'VerifyHrReportAcceptanceJobRepository' }
$sources = @()
$ownedSources = if ($ProfileChecks -or $NativeDispatcher) { @((Join-Path $PSScriptRoot ($suite+'.cs'))) } else { @(
    (Join-Path $PSScriptRoot '../../src/Cosmix.Api/Development/HrReportTestingDispatcher.cs'),
    (Join-Path $PSScriptRoot 'VerifyHrReportAcceptanceJobRepository.cs')
) }
foreach ($source in $ownedSources) {
    $name = [IO.Path]::GetFileName($source)
    $copy = Join-Path $output $name
    [IO.File]::Copy($source, $copy, $false)
    $sources += $copy
    $inputs += @{Path=$source; SHA256=(Get-FileHash -LiteralPath $copy).Hash}
}
$global = Join-Path $output 'GlobalUsings.cs'
[IO.File]::WriteAllText($global, 'global using System; global using System.Collections.Generic; global using System.IO; global using System.Linq; global using System.Threading; global using System.Threading.Tasks;')
$assembly = Join-Path $output ($suite+'.dll')
$arguments = @('/nologo','/target:exe','/langversion:preview','/nullable:enable','/warnaserror+','/warnaserror-:1701',('/out:"'+$assembly+'"'))
$arguments += $references.Values | Sort-Object | ForEach-Object { '/reference:"'+$_+'"' }
$arguments += @($global) + $sources | ForEach-Object { '"'+$_+'"' }
$response = Join-Path $output 'compile.rsp'
[IO.File]::WriteAllLines($response, $arguments)
[IO.File]::WriteAllText((Join-Path $output 'inputs.json'), ($inputs | ConvertTo-Json -Depth 5))
& $dotnet (Join-Path $sdk.FullName 'Roslyn/bincore/csc.dll') ('@'+$response) 2>&1 |
    Tee-Object -FilePath (Join-Path $output 'compile.log')
if ($LASTEXITCODE -ne 0) { throw "Compile failed: $output" }
[IO.File]::WriteAllText((Join-Path $output ($suite+'.runtimeconfig.json')),
    '{"runtimeOptions":{"tfm":"net10.0","frameworks":[{"name":"Microsoft.NETCore.App","version":"10.0.0"},{"name":"Microsoft.AspNetCore.App","version":"10.0.0"}]}}')
& $dotnet $assembly 2>&1 | Tee-Object -FilePath (Join-Path $output 'checks.log')
$result = $LASTEXITCODE
foreach ($input in $inputs) {
    if ((Get-FileHash -LiteralPath $input.Path).Hash -ne $input.SHA256) { throw 'Input changed during verification.' }
}
Write-Output "Offline evidence: $output"
if ($result -ne 0) { throw "Offline checks failed: $result" }
