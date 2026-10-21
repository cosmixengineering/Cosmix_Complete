param([string]$ReleaseDirectory = 'src/Cosmix.Api/bin/Release/net10.0')
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$release = if ([IO.Path]::IsPathRooted($ReleaseDirectory)) { [IO.Path]::GetFullPath($ReleaseDirectory) }
    else { [IO.Path]::GetFullPath((Join-Path $repo $ReleaseDirectory)) }
$dotnet = (Get-Command dotnet -CommandType Application | Select-Object -First 1).Source
$runtime = Split-Path -Parent $dotnet
$sdk = Get-ChildItem -LiteralPath (Join-Path $runtime 'sdk') -Directory |
    Where-Object Name -Match '^10\.\d+\.\d+$' | Sort-Object { [Version]$_.Name } -Descending | Select-Object -First 1
if (!$sdk) { throw 'Installed .NET 10 compiler required' }
$output = Join-Path $PSScriptRoot ('artifacts/g3-scoped-' + [Guid]::NewGuid().ToString('N'))
$null = [IO.Directory]::CreateDirectory($output)
$references = @{}
foreach ($name in @('Microsoft.NETCore.App.Ref','Microsoft.AspNetCore.App.Ref')) {
    $pack = Get-ChildItem -LiteralPath (Join-Path $runtime ('packs/'+$name)) -Directory |
        Where-Object Name -Match '^10\.\d+\.\d+$' | Sort-Object { [Version]$_.Name } -Descending | Select-Object -First 1
    if (!$pack) { throw "Missing reference pack $name" }
    foreach ($dll in Get-ChildItem -LiteralPath (Join-Path $pack.FullName 'ref/net10.0') -Filter '*.dll') {
        $references[$dll.Name] = $dll.FullName
    }
}
$inputs = @()
$assets = @{}
foreach ($dll in Get-ChildItem -LiteralPath $release -Filter '*.dll') { $assets[$dll.Name] = $dll.FullName }
$depsPath = Join-Path $release 'Cosmix.Api.deps.json'
$deps = Get-Content -LiteralPath $depsPath -Raw | ConvertFrom-Json
$inputs += @{ Path=$depsPath; SHA256=(Get-FileHash -LiteralPath $depsPath).Hash }
$rid = 'win-'+[Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString().ToLowerInvariant()
foreach ($choice in @('win',$rid)) {
    foreach ($target in $deps.targets.PSObject.Properties) {
        foreach ($library in $target.Value.PSObject.Properties) {
            foreach ($asset in $library.Value.runtimeTargets.PSObject.Properties) {
                if ($asset.Value.assetType -ne 'runtime' -or $asset.Value.rid -ne $choice) { continue }
                $path = [IO.Path]::GetFullPath((Join-Path $release $asset.Name))
                if (!$path.StartsWith($release+'\',[StringComparison]::OrdinalIgnoreCase) -or !(Test-Path -LiteralPath $path -PathType Leaf)) {
                    throw 'Runtime asset unavailable or outside baseline directory'
                }
                $assets[[IO.Path]::GetFileName($path)] = $path
            }
        }
    }
}
foreach ($path in $assets.Values | Sort-Object) {
    $name = [IO.Path]::GetFileName($path)
    $copy = Join-Path $output $name
    $hash = (Get-FileHash -LiteralPath $path).Hash
    [IO.File]::Copy($path,$copy,$false)
    if ((Get-FileHash -LiteralPath $copy).Hash -ne $hash) { throw 'Baseline assembly changed during copy' }
    $inputs += @{ Path=$path; SHA256=$hash }
    if (!$references.ContainsKey($name)) { $references[$name] = $copy }
}
# Compile only this scope into standalone executables. Shared products and bin/obj are read-only.
$products = @(Get-ChildItem -LiteralPath (Join-Path $repo 'src/Cosmix.Application/HR/VehicleFuel') -Filter '*.cs' |
    Where-Object Name -NE 'VehicleFuelRegistration.cs' | Select-Object -ExpandProperty FullName)
$products += Join-Path $repo 'src/Cosmix.Infrastructure/Repositories/HR/VehicleFuelRepository.cs'
$products += Join-Path $repo 'src/Cosmix.Api/Controllers/HR/VehicleFuelControllers.cs'
$copies = @()
foreach ($source in $products) {
    $copy = Join-Path $output ([IO.Path]::GetFileName($source))
    [IO.File]::Copy($source,$copy,$false)
    $copies += $copy
    $inputs += @{ Path=$source; SHA256=(Get-FileHash -LiteralPath $copy).Hash }
}
$global = Join-Path $output 'GlobalUsings.cs'
[IO.File]::WriteAllText($global, 'global using System; global using System.Collections.Generic; global using System.IO; global using System.Linq; global using System.Threading; global using System.Threading.Tasks; global using Microsoft.AspNetCore.Http;')
foreach ($name in @('VerifyVehicleEntitlements','VerifyVehicleRepository')) {
    $source = Join-Path $PSScriptRoot ($name+'.cs')
    $copy = Join-Path $output ($name+'.cs')
    [IO.File]::Copy($source,$copy,$false)
    $inputs += @{ Path=$source; SHA256=(Get-FileHash -LiteralPath $copy).Hash }
    $assembly = Join-Path $output ($name+'.dll')
    # CS0436 is expected: the copied scope replaces the same types in baseline assemblies.
    $arguments = @('/nologo','/target:exe','/langversion:preview','/nullable:enable','/warnaserror+',
        '/warnaserror-:1701','/nowarn:0436',('/out:"'+$assembly+'"'))
    $arguments += $references.Values | Sort-Object | ForEach-Object { '/reference:"'+$_+'"' }
    $arguments += @($global,$copy) + $copies | ForEach-Object { '"'+$_+'"' }
    $response = Join-Path $output ($name+'.rsp')
    [IO.File]::WriteAllLines($response,$arguments)
    & $dotnet (Join-Path $sdk.FullName 'Roslyn/bincore/csc.dll') ('@'+$response) 2>&1 |
        Tee-Object -FilePath (Join-Path $output ($name+'-compile.log'))
    if ($LASTEXITCODE -ne 0) { throw "$name scoped compilation failed; $output" }
    [IO.File]::WriteAllText((Join-Path $output ($name+'.runtimeconfig.json')),
        '{"runtimeOptions":{"tfm":"net10.0","frameworks":[{"name":"Microsoft.NETCore.App","version":"10.0.0"},{"name":"Microsoft.AspNetCore.App","version":"10.0.0"}]}}')
    & $dotnet $assembly 2>&1 | Tee-Object -FilePath (Join-Path $output ($name+'.log'))
    if ($LASTEXITCODE -ne 0) { throw "$name failed; $output" }
}
foreach ($input in $inputs) {
    if ((Get-FileHash -LiteralPath $input.Path).Hash -ne $input.SHA256) { throw 'Scoped verification input changed' }
}
[IO.File]::WriteAllText((Join-Path $output 'inputs.json'),($inputs | ConvertTo-Json -Depth 6))
Write-Output "G3 scoped verification passed: $output. No product build, SQL connection or host."
