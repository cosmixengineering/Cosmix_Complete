param(
    [Parameter(Mandatory=$true)][string]$ReleaseDirectory,
    [Parameter(Mandatory=$true)][string[]]$Sources
)
$ErrorActionPreference = 'Stop'
if ((Get-PSDrive C).Free -lt 350MB) { throw 'C reserve below 350MB' }
$release = [IO.Path]::GetFullPath($ReleaseDirectory).TrimEnd('\')
if (!$release.StartsWith('D:\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Release inputs must be on D' }
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd('\')
$dotnet = (Get-Command dotnet -CommandType Application | Select-Object -First 1).Source
$runtimeRoot = Split-Path -Parent $dotnet
$sdk = Get-ChildItem -LiteralPath (Join-Path $runtimeRoot 'sdk') -Directory |
    Where-Object Name -Match '^10\.\d+\.\d+$' | Sort-Object { [Version]$_.Name } -Descending | Select-Object -First 1
if (!$sdk) { throw 'Installed .NET 10 compiler required' }
$references = @{}
foreach ($name in @('Microsoft.NETCore.App.Ref', 'Microsoft.AspNetCore.App.Ref')) {
    $pack = Get-ChildItem -LiteralPath (Join-Path $runtimeRoot ('packs/'+$name)) -Directory |
        Where-Object Name -Match '^10\.\d+\.\d+$' | Sort-Object { [Version]$_.Name } -Descending | Select-Object -First 1
    if (!$pack) { throw "Missing reference pack $name" }
    foreach ($dll in Get-ChildItem -LiteralPath (Join-Path $pack.FullName 'ref/net10.0') -Filter '*.dll' -File) {
        $references[$dll.Name] = $dll.FullName
    }
}
$output = Join-Path 'D:\CosmixVerification\part-c-parent-20261013' ('managed-'+[Guid]::NewGuid().ToString('N'))
$null = [IO.Directory]::CreateDirectory($output)
function New-Text([string]$Path, [string]$Text) {
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes($Text)
    $file = [IO.File]::Open($Path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try { $file.Write($bytes,0,$bytes.Length); $file.Flush($true) } finally { $file.Dispose() }
}
$assets = @{}
foreach ($dll in Get-ChildItem -LiteralPath $release -File -Filter '*.dll') { $assets[$dll.Name] = $dll.FullName }
$depsPath = Join-Path $release 'Cosmix.Api.deps.json'
$deps = Get-Content -LiteralPath $depsPath -Raw | ConvertFrom-Json
$rid = 'win-'+[Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString().ToLowerInvariant()
foreach ($choice in @('win',$rid)) {
    foreach ($target in $deps.targets.PSObject.Properties) {
        foreach ($library in $target.Value.PSObject.Properties) {
            foreach ($asset in $library.Value.runtimeTargets.PSObject.Properties) {
                if ($asset.Value.assetType -ne 'runtime' -or $asset.Value.rid -ne $choice) { continue }
                $path = [IO.Path]::GetFullPath((Join-Path $release $asset.Name))
                if (!$path.StartsWith($release+'\',[StringComparison]::OrdinalIgnoreCase) -or !(Test-Path -LiteralPath $path -PathType Leaf)) {
                    throw 'Release runtime asset unavailable or outside target'
                }
                $assets[[IO.Path]::GetFileName($path)] = $path
            }
        }
    }
}
$inputs = @(@{Path=$depsPath; SHA256=(Get-FileHash -LiteralPath $depsPath).Hash})
foreach ($path in $assets.Values | Sort-Object) {
    $hash = (Get-FileHash -LiteralPath $path).Hash
    $copied = Join-Path $output ([IO.Path]::GetFileName($path))
    [IO.File]::Copy($path, $copied, $false)
    if ((Get-FileHash -LiteralPath $copied).Hash -ne $hash) { throw 'Release input changed during copy' }
    if (!$references.ContainsKey([IO.Path]::GetFileName($path))) { $references[[IO.Path]::GetFileName($path)] = $copied }
    $inputs += @{Path=$path; SHA256=$hash}
}
New-Text (Join-Path $output 'inputs.json') ($inputs | ConvertTo-Json -Depth 8)
New-Text (Join-Path $output 'GlobalUsings.cs') @'
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;
'@
foreach ($source in $Sources) {
    $path = [IO.Path]::GetFullPath((Join-Path $repo $source))
    if (!$path.StartsWith($repo+'\',[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetExtension($path) -ne '.cs') { throw 'Source outside checkout' }
    $name = [IO.Path]::GetFileNameWithoutExtension($path)
    $sourceHash = (Get-FileHash -LiteralPath $path).Hash
    $text = [regex]::Replace([IO.File]::ReadAllText($path), '(?m)^#:(?:project|property) .+\r?\n?', '')
    $run = Join-Path $output ($name+'.cs')
    New-Text $run $text
    $assembly = Join-Path $output ($name+'.dll')
    # Retained packages can target older framework references; report their unification warning without masking source warnings.
    $args = @('/nologo','/target:exe','/langversion:preview','/nullable:enable','/warnaserror+','/warnaserror-:1701',('/out:"'+$assembly+'"'))
    $args += $references.Values | Sort-Object | ForEach-Object { '/reference:"'+$_+'"' }
    $args += '"'+(Join-Path $output 'GlobalUsings.cs')+'"'; $args += '"'+$run+'"'
    $rsp = Join-Path $output ($name+'.rsp')
    New-Text $rsp ($args -join "`n")
    & $dotnet (Join-Path $sdk.FullName 'Roslyn/bincore/csc.dll') ('@'+$rsp) 2>&1 |
        Tee-Object -FilePath (Join-Path $output ($name+'-compile.log'))
    if ($LASTEXITCODE -ne 0) { throw "$name standalone compilation failed" }
    New-Text (Join-Path $output ($name+'.runtimeconfig.json')) '{"runtimeOptions":{"tfm":"net10.0","frameworks":[{"name":"Microsoft.NETCore.App","version":"10.0.0"},{"name":"Microsoft.AspNetCore.App","version":"10.0.0"}]}}'
    & $dotnet $assembly 2>&1 | Tee-Object -FilePath (Join-Path $output ($name+'.log'))
    if ($LASTEXITCODE -ne 0) { throw "$name failed; output $output" }
    if ((Get-FileHash -LiteralPath $path).Hash -ne $sourceHash) { throw 'Check source changed during verification' }
}
foreach ($input in $inputs) { if ((Get-FileHash -LiteralPath $input.Path).Hash -ne $input.SHA256) { throw 'Release inputs changed during verification' } }
Write-Output "Verified suites from frozen Release; exact output $output. No product rebuild or implicit host/SQL approval."
