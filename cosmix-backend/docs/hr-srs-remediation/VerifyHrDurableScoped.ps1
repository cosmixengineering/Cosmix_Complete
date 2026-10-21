param(
    [string]$ReleaseDirectory = 'D:/CosmixVerification/hr-backend-20261019/hr-production-build01/bin/Cosmix.Api/release',
    [string[]]$Suites = @('VerifyHrDurableExportSurface','VerifyHrDurableExports')
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$release = [IO.Path]::GetFullPath($ReleaseDirectory).TrimEnd('\')
if (!$release.StartsWith('D:\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Frozen Release must be on D' }
$output = Join-Path 'D:/CosmixVerification/hr-backend-20261019' ('hr-export-scoped-' + [Guid]::NewGuid().ToString('N'))
$null = [IO.Directory]::CreateDirectory($output)
$dotnet = (Get-Command dotnet -CommandType Application | Select-Object -First 1).Source
$runtime = Split-Path -Parent $dotnet
$sdk = Get-ChildItem -LiteralPath (Join-Path $runtime 'sdk') -Directory |
    Where-Object Name -Match '^10\.\d+\.\d+$' | Sort-Object { [Version]$_.Name } -Descending | Select-Object -First 1
$references = @{}
foreach ($name in @('Microsoft.NETCore.App.Ref','Microsoft.AspNetCore.App.Ref')) {
    $pack = Get-ChildItem -LiteralPath (Join-Path $runtime ('packs/' + $name)) -Directory |
        Where-Object Name -Match '^10\.\d+\.\d+$' | Sort-Object { [Version]$_.Name } -Descending | Select-Object -First 1
    foreach ($dll in Get-ChildItem -LiteralPath (Join-Path $pack.FullName 'ref/net10.0') -Filter '*.dll') { $references[$dll.Name] = $dll.FullName }
}
$inputs = @()
foreach ($dll in Get-ChildItem -LiteralPath $release -Filter '*.dll') {
    $hash = (Get-FileHash -LiteralPath $dll.FullName).Hash
    $copy = Join-Path $output $dll.Name
    [IO.File]::Copy($dll.FullName, $copy, $false)
    if ((Get-FileHash -LiteralPath $copy).Hash -ne $hash) { throw 'Frozen input changed during copy' }
    $inputs += @{Path=$dll.FullName; SHA256=$hash}
    if (!$references.ContainsKey($dll.Name)) { $references[$dll.Name] = $copy }
}
$depsPath = Join-Path $release 'Cosmix.Api.deps.json'
$deps = Get-Content -LiteralPath $depsPath -Raw | ConvertFrom-Json
$inputs += @{Path=$depsPath; SHA256=(Get-FileHash -LiteralPath $depsPath).Hash}
$rid = 'win-' + [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString().ToLowerInvariant()
foreach ($choice in @('win', $rid)) {
    foreach ($target in $deps.targets.PSObject.Properties) {
        foreach ($library in $target.Value.PSObject.Properties) {
            foreach ($asset in $library.Value.runtimeTargets.PSObject.Properties) {
                if ($asset.Value.assetType -ne 'runtime' -or $asset.Value.rid -ne $choice) { continue }
                $source = [IO.Path]::GetFullPath((Join-Path $release $asset.Name))
                if (!$source.StartsWith($release + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Runtime asset outside frozen root' }
                $hash = (Get-FileHash -LiteralPath $source).Hash
                $name = [IO.Path]::GetFileName($source)
                $copy = Join-Path $output $name
                [IO.File]::Copy($source, $copy, $true)
                if ((Get-FileHash -LiteralPath $copy).Hash -ne $hash) { throw 'Runtime input changed during copy' }
                $references[$name] = $copy
                $inputs += @{Path=$source; SHA256=$hash}
            }
        }
    }
}
# The existing HR OT projector calls an Application-internal evidence reader. Add
# friend metadata ONLY to the copied DLL so the unchanged internal IL can execute
# from this standalone overlay. Never patch the frozen input or product binaries.
$cecil = Join-Path $env:USERPROFILE '.nuget/packages/mono.cecil/0.11.3/lib/netstandard2.0/Mono.Cecil.dll'
Add-Type -LiteralPath $cecil
$copiedApplication = Join-Path $output 'Cosmix.Application.dll'
$resolver = [Mono.Cecil.DefaultAssemblyResolver]::new()
$resolver.AddSearchDirectory($output)
$resolver.AddSearchDirectory((Split-Path -Parent ([object].Assembly.Location)))
$reader = [Mono.Cecil.ReaderParameters]::new()
$reader.AssemblyResolver = $resolver
$definition = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($copiedApplication, $reader)
$ctor = [Runtime.CompilerServices.InternalsVisibleToAttribute].GetConstructor(@([string]))
$import = $definition.MainModule.ImportReference($ctor)
foreach ($name in $Suites) {
    $attribute = [Mono.Cecil.CustomAttribute]::new($import)
    $attribute.ConstructorArguments.Add([Mono.Cecil.CustomAttributeArgument]::new($definition.MainModule.TypeSystem.String, $name))
    $definition.CustomAttributes.Add($attribute)
}
$friendCopy = Join-Path $output 'Application.friend.dll'
$definition.Write($friendCopy)
$definition.Dispose()
[IO.File]::Copy($friendCopy, $copiedApplication, $true)
$products = @(Get-ChildItem -LiteralPath (Join-Path $repo 'src/Cosmix.Application/HR/Reports') -Filter '*.cs' | Select-Object -ExpandProperty FullName)
$products += Get-ChildItem -LiteralPath (Join-Path $repo 'src/Cosmix.Infrastructure/Repositories/HR') -Filter 'HrReport*.cs' | Select-Object -ExpandProperty FullName
# Existing partial is a read-only compile input; it is outside this worker's edit ownership.
$products += Join-Path $repo 'src/Cosmix.Infrastructure/Repositories/HR/HrAttendanceReportResultsRepository.cs'
$products += Join-Path $repo 'src/Cosmix.Api/Controllers/HR/HrReportsController.cs'
# Parent-owned frozen preparation contract/processor are read-only inputs until the coordinated build.
$products += Join-Path $repo 'src/Cosmix.Application/Common/BackgroundJobs/DurableJobContracts.cs'
$products += Join-Path $repo 'src/Cosmix.Application/Common/BackgroundJobs/DurableJobProcessor.cs'
$copies = @()
foreach ($source in $products) {
    $copy = Join-Path $output ([IO.Path]::GetFileName($source))
    [IO.File]::Copy($source,$copy,$false)
    $copies += $copy
    $inputs += @{Path=$source; SHA256=(Get-FileHash -LiteralPath $copy).Hash}
}
$global = Join-Path $output 'GlobalUsings.cs'
[IO.File]::WriteAllText((Join-Path $output 'inputs.json'), ($inputs | ConvertTo-Json -Depth 5))
[IO.File]::WriteAllText($global, 'global using System; global using System.Collections.Generic; global using System.IO; global using System.Linq; global using System.Threading; global using System.Threading.Tasks; global using Microsoft.AspNetCore.Http;')
foreach ($name in $Suites) {
    $source = Join-Path $PSScriptRoot ($name+'.cs')
    $copy = Join-Path $output ($name+'.cs')
    $text = [regex]::Replace([IO.File]::ReadAllText($source), '(?m)^#:(?:project|property) .+\r?\n?', '')
    [IO.File]::WriteAllText($copy, $text)
    $inputs += @{Path=$source; SHA256=(Get-FileHash -LiteralPath $source).Hash}
    $assembly = Join-Path $output ($name+'.dll')
    $arguments = @('/nologo','/target:exe','/langversion:preview','/nullable:enable','/warnaserror+','/warnaserror-:1701','/nowarn:0436',('/out:"'+$assembly+'"'))
    $arguments += $references.Values | Sort-Object | ForEach-Object { '/reference:"'+$_+'"' }
    $arguments += @($global,$copy) + $copies | ForEach-Object { '"'+$_+'"' }
    $response = Join-Path $output ($name+'.rsp')
    [IO.File]::WriteAllLines($response,$arguments)
    & $dotnet (Join-Path $sdk.FullName 'Roslyn/bincore/csc.dll') ('@'+$response) 2>&1 | Tee-Object -FilePath (Join-Path $output ($name+'-compile.log'))
    if ($LASTEXITCODE -ne 0) { throw "Scoped compile failed: $name; $output" }
    [IO.File]::WriteAllText((Join-Path $output ($name+'.runtimeconfig.json')), '{"runtimeOptions":{"tfm":"net10.0","frameworks":[{"name":"Microsoft.NETCore.App","version":"10.0.0"},{"name":"Microsoft.AspNetCore.App","version":"10.0.0"}]}}')
    & $dotnet $assembly 2>&1 | Tee-Object -FilePath (Join-Path $output ($name+'.log'))
    if ($LASTEXITCODE -ne 0) { throw "Scoped suite failed: $name; $output" }
}
foreach ($input in $inputs) { if ((Get-FileHash -LiteralPath $input.Path).Hash -ne $input.SHA256) { throw 'Scoped input changed during verification' } }
[IO.File]::WriteAllText((Join-Path $output 'inputs.json'), ($inputs | ConvertTo-Json -Depth 5))
Write-Output "PASS frozen-DLL owned-source overlay: $output. No product build, SQL, host or provider calls."
