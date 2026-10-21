param([switch]$BuildSlotGranted, [string]$ReleaseDirectory)
$ErrorActionPreference='Stop'
if (!$BuildSlotGranted) { throw 'C05 compilation is held until the parent grants a build slot. Source guards may run separately.' }
if ((Get-PSDrive C).Free -lt 350MB) { throw 'C reserve below 350 MiB; C05 verification held.' }
if (!$ReleaseDirectory) { throw 'Supply the parent joint Release directory containing Cosmix.Domain/Application/Infrastructure.dll and their dependencies.' }
$c05Release=[IO.Path]::GetFullPath($ReleaseDirectory).TrimEnd('\')
if (!$c05Release.StartsWith('D:\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Parent Release inputs must be on D:.' }
if (!(Test-Path -LiteralPath $c05Release -PathType Container)) { throw 'Parent Release directory does not exist.' }
foreach ($c05Assembly in @('Cosmix.Domain.dll','Cosmix.Application.dll','Cosmix.Infrastructure.dll','Microsoft.EntityFrameworkCore.SqlServer.dll')) {
    if (!(Test-Path -LiteralPath (Join-Path $c05Release $c05Assembly) -PathType Leaf)) { throw "Missing joint Release dependency: $c05Assembly" }
    if ((Get-Item -LiteralPath (Join-Path $c05Release $c05Assembly)).Length -eq 0) { throw "Empty joint Release dependency: $c05Assembly" }
}
$c05Dotnet=(Get-Command dotnet -CommandType Application | Select-Object -First 1).Source
$c05DotnetRoot=Split-Path -Parent $c05Dotnet
$c05Sdk=Get-ChildItem -LiteralPath (Join-Path $c05DotnetRoot 'sdk') -Directory |
    Where-Object Name -Match '^10\.\d+\.\d+$' | Sort-Object { [Version]$_.Name } -Descending | Select-Object -First 1
if (!$c05Sdk) { throw 'Installed .NET 10 Roslyn compiler required; this runner does not install or restore tools.' }
$c05Compiler=Join-Path $c05Sdk.FullName 'Roslyn/bincore/csc.dll'
$c05References=@{}
foreach ($c05PackName in @('Microsoft.NETCore.App.Ref','Microsoft.AspNetCore.App.Ref')) {
    $c05Pack=Get-ChildItem -LiteralPath (Join-Path $c05DotnetRoot ('packs/'+$c05PackName)) -Directory |
        Where-Object Name -Match '^10\.\d+\.\d+$' | Sort-Object { [Version]$_.Name } -Descending | Select-Object -First 1
    if (!$c05Pack) { throw "Installed .NET 10 reference pack required: $c05PackName" }
    foreach ($c05Reference in Get-ChildItem -LiteralPath (Join-Path $c05Pack.FullName 'ref/net10.0') -Filter '*.dll' -File) {
        $c05References[$c05Reference.Name]=$c05Reference.FullName
    }
}
$c05Identity=[Guid]::NewGuid().ToString('N')
$c05Output=Join-Path 'D:\CodexTemp' ('C05-managed-checks-'+$c05Identity)
$null=New-Item -ItemType Directory -Path $c05Output
function Write-C05NewText([string]$Path,[string]$Value) {
    $bytes=[Text.Encoding]::UTF8.GetBytes($Value)
    $stream=[IO.File]::Open($Path,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try { $stream.Write($bytes,0,$bytes.Length); $stream.Flush($true) } finally { $stream.Dispose() }
}
$c05Environment=@{}
foreach ($c05Variable in @('TEMP','TMP','TMPDIR','DOTNET_CLI_HOME','DOTNET_CLI_TELEMETRY_OPTOUT','DOTNET_SKIP_FIRST_TIME_EXPERIENCE','DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE','DOTNET_GENERATE_ASPNET_CERTIFICATE','NUGET_HTTP_CACHE_PATH','NUGET_SCRATCH','NUGET_PACKAGES','MSBUILDDISABLENODEREUSE','DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER')) {
    $c05Environment[$c05Variable]=[Environment]::GetEnvironmentVariable($c05Variable,'Process')
}
try {
    $env:TEMP=$c05Output; $env:TMP=$c05Output; $env:TMPDIR=$c05Output; $env:DOTNET_CLI_HOME=$c05Output
    $env:DOTNET_CLI_TELEMETRY_OPTOUT='1'; $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
    $env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE='true'
    $env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
    $env:NUGET_HTTP_CACHE_PATH=Join-Path $c05Output 'nuget-http'
    $env:NUGET_SCRATCH=Join-Path $c05Output 'nuget-scratch'
    $env:NUGET_PACKAGES='D:\CodexTemp\C05-nuget-packages'
    $env:MSBUILDDISABLENODEREUSE='1'; $env:DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER='1'
    Write-Output "C05 exact output path: $c05Output"
    & (Join-Path $PSScriptRoot 'C05SourceChecks.ps1')
    $c05DependencyPaths=@{}
    foreach ($c05Dependency in Get-ChildItem -LiteralPath $c05Release -Filter '*.dll' -File) { $c05DependencyPaths[$c05Dependency.Name]=$c05Dependency.FullName }
    $c05DepsPath=Join-Path $c05Release 'Cosmix.Api.deps.json'
    if (!(Test-Path -LiteralPath $c05DepsPath -PathType Leaf)) { throw 'Joint API dependency manifest required to select Windows managed runtime assets.' }
    $c05DepsHash=(Get-FileHash -LiteralPath $c05DepsPath -Algorithm SHA256).Hash
    $c05Deps=Get-Content -LiteralPath $c05DepsPath -Raw | ConvertFrom-Json
    $c05Rid='win-'+[Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString().ToLowerInvariant()
    foreach ($c05RidChoice in @('win',$c05Rid)) {
        foreach ($c05Target in $c05Deps.targets.PSObject.Properties) {
            foreach ($c05Library in $c05Target.Value.PSObject.Properties) {
                foreach ($c05Asset in $c05Library.Value.runtimeTargets.PSObject.Properties) {
                    if ($c05Asset.Value.assetType -ne 'runtime' -or $c05Asset.Value.rid -ne $c05RidChoice) { continue }
                    $c05AssetPath=[IO.Path]::GetFullPath((Join-Path $c05Release $c05Asset.Name))
                    if (!$c05AssetPath.StartsWith($c05Release+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Runtime asset escapes Release directory.' }
                    if (!(Test-Path -LiteralPath $c05AssetPath -PathType Leaf)) { throw "Missing Release runtime asset: $c05AssetPath" }
                    $c05DependencyPaths[[IO.Path]::GetFileName($c05AssetPath)]=$c05AssetPath
                }
            }
        }
    }
    $c05Inputs=@(@{Path=$c05DepsPath;SHA256=$c05DepsHash})
    foreach ($c05DependencyPath in $c05DependencyPaths.Values | Sort-Object) {
        $c05Dependency=Get-Item -LiteralPath $c05DependencyPath
        $c05OriginalHash=(Get-FileHash -LiteralPath $c05Dependency.FullName -Algorithm SHA256).Hash
        $c05Copied=Join-Path $c05Output $c05Dependency.Name
        $c05Read=[IO.File]::OpenRead($c05Dependency.FullName)
        try {
            $c05Write=[IO.File]::Open($c05Copied,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
            try { $c05Read.CopyTo($c05Write); $c05Write.Flush($true) } finally { $c05Write.Dispose() }
        } finally { $c05Read.Dispose() }
        if ($c05OriginalHash -ne (Get-FileHash -LiteralPath $c05Copied -Algorithm SHA256).Hash) { throw 'Release dependency changed during copy; request a frozen Release directory.' }
        if (!$c05References.ContainsKey($c05Dependency.Name)) { $c05References[$c05Dependency.Name]=$c05Copied }
        $c05Inputs+=@{Path=$c05Dependency.FullName;SHA256=$c05OriginalHash}
    }
    Write-C05NewText (Join-Path $c05Output 'Release-inputs.json') ($c05Inputs | ConvertTo-Json -Depth 5)
    $c05Globals=Join-Path $c05Output 'C05.GlobalUsings.cs'
    Write-C05NewText $c05Globals @'
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;
'@
    $c05Runtime=@{runtimeOptions=@{tfm='net10.0';frameworks=@(@{name='Microsoft.NETCore.App';version='10.0.0'},@{name='Microsoft.AspNetCore.App';version='10.0.0'});rollForward='LatestPatch'}} | ConvertTo-Json -Depth 6
    $c05Results=@()
    foreach ($c05Check in @('Attribution','PolicyBoundary','Persistence','Notifications','Workflow')) {
        if ((Get-PSDrive C).Free -lt 350MB) { throw 'C reserve below 350 MiB; further C05 verification held.' }
        $c05Original=Join-Path $PSScriptRoot ('C05Verify'+$c05Check+'.cs')
        $c05Runfile=Join-Path $c05Output ('C05'+$c05Check+'.cs')
        $c05Text=[IO.File]::ReadAllText($c05Original)
        $c05Text=[regex]::Replace($c05Text,'(?m)^#:[^\r\n]*\r?\n','')
        Write-C05NewText $c05Runfile $c05Text
        $c05Program=Join-Path $c05Output ('C05'+$c05Check+'.dll')
        $c05Response=Join-Path $c05Output ($c05Check+'.rsp')
        $c05Arguments=@('/nostdlib+','/target:exe','/langversion:preview','/nullable:enable','/optimize+','/deterministic+','/warn:4',('/out:"'+$c05Program+'"'))
        foreach ($c05Reference in $c05References.Values | Sort-Object) { $c05Arguments+=('/reference:"'+$c05Reference+'"') }
        $c05Arguments+=('"'+$c05Globals+'"'); $c05Arguments+=('"'+$c05Runfile+'"')
        Write-C05NewText $c05Response ($c05Arguments -join "`r`n")
        & $c05Dotnet $c05Compiler /noconfig ('@'+$c05Response) 2>&1 |
            Tee-Object -FilePath (Join-Path $c05Output ($c05Check+'-compile.log'))
        if ($LASTEXITCODE -ne 0) { throw "C05 $c05Check standalone compilation failed; product was not rebuilt." }
        Write-C05NewText ([IO.Path]::ChangeExtension($c05Program,'.runtimeconfig.json')) $c05Runtime
        & $c05Dotnet $c05Program 2>&1 | Tee-Object -FilePath (Join-Path $c05Output ($c05Check+'.log'))
        if ($LASTEXITCODE -ne 0) { throw "C05 $c05Check failed." }
        $c05Results+=@{Check=$c05Check;State='passed';Source=$c05Original;SHA256=(Get-FileHash -LiteralPath $c05Original -Algorithm SHA256).Hash;Log=(Join-Path $c05Output ($c05Check+'.log'))}
    }
    foreach ($c05Input in $c05Inputs) {
        if ($c05Input.SHA256 -ne (Get-FileHash -LiteralPath $c05Input.Path -Algorithm SHA256).Hash) { throw 'Parent Release changed during checks; results require a frozen joint Release.' }
    }
    Write-C05NewText (Join-Path $c05Output 'Results.json') ($c05Results | ConvertTo-Json -Depth 5)
    Write-Output 'C05 source/attribution/policy/LINQ metadata/notification/workflow checks passed. No product rebuild, restore, SQL, host or persisted fixtures.'
} finally {
    foreach ($c05Variable in $c05Environment.Keys) { [Environment]::SetEnvironmentVariable($c05Variable,$c05Environment[$c05Variable],'Process') }
}
