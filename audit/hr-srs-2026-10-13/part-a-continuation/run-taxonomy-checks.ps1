param([string]$BackendPath = (Join-Path $PSScriptRoot '../../../cosmix-backend'))
$ErrorActionPreference = 'Stop'
$taxonomyBackendRoot = (Resolve-Path -LiteralPath $BackendPath).Path
$taxonomyApplicationProject = Join-Path $taxonomyBackendRoot 'src/Cosmix.Application/Cosmix.Application.csproj'
if (-not (Test-Path -LiteralPath $taxonomyApplicationProject)) { throw 'The real backend Application project was not found.' }
$taxonomyCheckRoot = Join-Path $env:TEMP ('cosmix-taxonomy-checks-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taxonomyCheckRoot | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'taxonomy.Program.cs') -Destination (Join-Path $taxonomyCheckRoot 'Program.cs')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'taxonomy.ServiceChecks.cs') -Destination (Join-Path $taxonomyCheckRoot 'ServiceChecks.cs')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'taxonomy.HttpChecks.cs') -Destination (Join-Path $taxonomyCheckRoot 'HttpChecks.cs')
$taxonomyProjectXmlPath = [System.Security.SecurityElement]::Escape($taxonomyApplicationProject)
$taxonomyControllerXmlPath = [System.Security.SecurityElement]::Escape((Join-Path $taxonomyBackendRoot 'src/Cosmix.Api/Controllers/HR/StaffTaxonomyController.cs'))
$taxonomyFilterXmlPath = [System.Security.SecurityElement]::Escape((Join-Path $taxonomyBackendRoot 'src/Cosmix.Api/Controllers/HR/StrictStaffTaxonomyJsonAttribute.cs'))
$taxonomyProjectText = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup>
  <ItemGroup><Compile Include="*.cs" /><ProjectReference Include="$taxonomyProjectXmlPath" /></ItemGroup>
  <ItemGroup><FrameworkReference Include="Microsoft.AspNetCore.App" />
    <Compile Include="$taxonomyControllerXmlPath" Link="StaffTaxonomyController.cs" />
    <Compile Include="$taxonomyFilterXmlPath" Link="StrictStaffTaxonomyJsonAttribute.cs" />
  </ItemGroup>
</Project>
"@
$taxonomyProjectPath = Join-Path $taxonomyCheckRoot 'Checks.csproj'
Set-Content -LiteralPath $taxonomyProjectPath -Value $taxonomyProjectText -Encoding utf8
$taxonomyArtifactsPath = Join-Path $taxonomyCheckRoot 'artifacts'
# Actual feature/domain code with synthetic collaborators only. No SQL, provider, private employee file or account use.
& dotnet run --project $taxonomyProjectPath --configuration Release --artifacts-path $taxonomyArtifactsPath
if ($LASTEXITCODE -ne 0) { throw "Taxonomy checks failed with exit code $LASTEXITCODE. Artifacts: $taxonomyCheckRoot" }
Write-Output "Synthetic taxonomy verification artifacts: $taxonomyCheckRoot"
