param([Parameter(Mandatory=$true)][string]$FrozenVerificationDirectory)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath('D:/CosmixVerification').TrimEnd('\') + '\'
$frozen = [IO.Path]::GetFullPath($FrozenVerificationDirectory).TrimEnd('\')
if (!$frozen.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) { throw 'Frozen verification inputs must be under D:/CosmixVerification' }
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$output = Join-Path $root ('benefit-authority-scoped-' + [Guid]::NewGuid().ToString('N'))
$null = [IO.Directory]::CreateDirectory($output)
$dotnet = (Get-Command dotnet -CommandType Application | Select-Object -First 1).Source
$sdk = Get-ChildItem -LiteralPath (Join-Path (Split-Path -Parent $dotnet) 'sdk') -Directory |
    Where-Object Name -Match '^10\.\d+\.\d+$' | Sort-Object { [Version]$_.Name } -Descending | Select-Object -First 1
if (!$sdk) { throw 'Installed .NET 10 compiler required' }
$manifestPath = Join-Path $frozen 'inputs.json'
$baseline = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
foreach ($item in $baseline) {
    if ((Get-FileHash -LiteralPath $item.Path).Hash -ne $item.SHA256) { throw 'Frozen Release input changed' }
    if ([IO.Path]::GetExtension($item.Path) -eq '.dll') {
        $copy = Join-Path $frozen ([IO.Path]::GetFileName($item.Path))
        if ((Get-FileHash -LiteralPath $copy).Hash -ne $item.SHA256) { throw 'Frozen verification copy changed' }
    }
}
$inputs = @(@{ Path=$manifestPath; SHA256=(Get-FileHash -LiteralPath $manifestPath).Hash })
foreach ($dll in Get-ChildItem -LiteralPath $frozen -Filter '*.dll' -File) {
    $hash = (Get-FileHash -LiteralPath $dll.FullName).Hash
    $copy = Join-Path $output $dll.Name
    [IO.File]::Copy($dll.FullName, $copy, $false)
    if ((Get-FileHash -LiteralPath $copy).Hash -ne $hash) { throw 'Frozen assembly changed during copy' }
    $inputs += @{ Path=$dll.FullName; SHA256=$hash }
}
$sources = @(
    'src/Cosmix.Application/HR/Leaves/LeaveBenefitService.cs',
    'src/Cosmix.Application/HR/Leaves/LeaveBenefitCorrectionService.cs',
    'src/Cosmix.Application/HR/Leaves/LeaveBenefitResolutionService.cs',
    'src/Cosmix.Application/HR/Leaves/LeaveRoleCodes.cs',
    'docs/hr-srs-remediation/VerifyLeaveBenefitWorkflow.cs'
)
$copies = @()
foreach ($source in $sources) {
    $path = Join-Path $repo $source
    $hash = (Get-FileHash -LiteralPath $path).Hash
    $content = [IO.File]::ReadAllText($path)
    if ((Get-FileHash -LiteralPath $path).Hash -ne $hash) { throw 'Source changed during copy' }
    $copy = Join-Path $output ([IO.Path]::GetFileName($path))
    [IO.File]::WriteAllText($copy, [regex]::Replace($content, '(?m)^#:(?:project|property) .+\r?\n?', ''))
    $copies += $copy
    $inputs += @{ Path=$path; SHA256=$hash }
}
$global = Join-Path $output 'GlobalUsings.cs'
[IO.File]::Copy((Join-Path $frozen 'GlobalUsings.cs'), $global, $false)
$responsePath = Join-Path $frozen 'VerifyLeaveBenefitWorkflow.rsp'
$references = Get-Content -LiteralPath $responsePath | Where-Object { $_.StartsWith('/reference:') }
$inputs += @{ Path=$responsePath; SHA256=(Get-FileHash -LiteralPath $responsePath).Hash }
$assembly = Join-Path $output 'VerifyLeaveBenefitAuthorityScoped.dll'
# Only service partials replace baseline types; shared authorization/evidence execute frozen IL.
$arguments = @('/nologo','/target:exe','/langversion:preview','/nullable:enable',
    '/warnaserror+','/warnaserror-:1701','/nowarn:0436',('/out:"'+$assembly+'"'))
$arguments += $references
$arguments += @($global) + $copies | ForEach-Object { '"'+$_+'"' }
$response = Join-Path $output 'compile.rsp'
[IO.File]::WriteAllLines($response, $arguments)
[IO.File]::WriteAllText((Join-Path $output 'inputs.json'), ($inputs | ConvertTo-Json -Depth 8))
& $dotnet (Join-Path $sdk.FullName 'Roslyn/bincore/csc.dll') ('@'+$response) 2>&1 |
    Tee-Object -FilePath (Join-Path $output 'compile.log')
if ($LASTEXITCODE -ne 0) { throw "Scoped compilation failed; $output" }
[IO.File]::Copy((Join-Path $frozen 'VerifyLeaveBenefitWorkflow.runtimeconfig.json'),
    (Join-Path $output 'VerifyLeaveBenefitAuthorityScoped.runtimeconfig.json'), $false)
& $dotnet $assembly 2>&1 | Tee-Object -FilePath (Join-Path $output 'verification.log')
$result = $LASTEXITCODE
foreach ($item in $inputs + $baseline) {
    if ((Get-FileHash -LiteralPath $item.Path).Hash -ne $item.SHA256) { throw 'Verification input changed' }
}
if ($result -ne 0) { throw "Scoped verification failed; $output" }
Write-Output "Scoped GREEN output: $output. Service source overlay only; product Release build, SQL and host not run."
