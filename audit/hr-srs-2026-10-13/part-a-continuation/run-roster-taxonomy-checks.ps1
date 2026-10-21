param(
    [Parameter(Mandatory = $true)][string]$ArtifactsPath,
    [string]$BackendPath = (Join-Path $PSScriptRoot '../../../cosmix-backend')
)
$ErrorActionPreference = 'Stop'
$rosterBackend = (Resolve-Path -LiteralPath $BackendPath).Path
$rosterOutput = [IO.Path]::GetFullPath($ArtifactsPath)
if ($rosterOutput.StartsWith($rosterBackend + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Use a separate build-output directory, not the backend checkout.'
}
if (Test-Path -LiteralPath $rosterOutput) { throw 'Choose a fresh output directory; prior evidence is retained.' }
$rosterReferences = @(
    'src/Cosmix.Application/bin/Release/net10.0/Cosmix.Application.dll',
    'src/Cosmix.Domain/bin/Release/net10.0/Cosmix.Domain.dll'
) | ForEach-Object {
    $rosterAssembly = (Resolve-Path -LiteralPath (Join-Path $rosterBackend $_)).Path
    '<Reference Include="' + [IO.Path]::GetFileNameWithoutExtension($rosterAssembly) + '"><HintPath>' +
        [System.Security.SecurityElement]::Escape($rosterAssembly) + '</HintPath></Reference>'
}
New-Item -ItemType Directory -Path $rosterOutput | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'VerifyRosterTaxonomy.cs') -Destination (Join-Path $rosterOutput 'Program.cs')
$rosterProject = '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup>' + ($rosterReferences -join '') + '</ItemGroup></Project>'
Set-Content -LiteralPath (Join-Path $rosterOutput 'Checks.csproj') -Value $rosterProject -Encoding utf8
& dotnet run --project (Join-Path $rosterOutput 'Checks.csproj') --configuration Release 2>&1 |
    Tee-Object -FilePath (Join-Path $rosterOutput 'result.log')
if ($LASTEXITCODE -ne 0) { throw "Roster taxonomy checks failed: $rosterOutput" }
Write-Output 'These are standalone source checks against the already-built Release assemblies; no SQL, HTTP, provider or employee mutations.'
