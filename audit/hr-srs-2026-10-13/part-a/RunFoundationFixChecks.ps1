param([string] $BackendRoot = (Join-Path (Split-Path $PSScriptRoot -Parent | Split-Path -Parent | Split-Path -Parent) 'cosmix-backend'))
$ErrorActionPreference = 'Stop'
$BackendRoot = (Resolve-Path -LiteralPath $BackendRoot).Path
$verificationRoot = Join-Path $env:TEMP ('cosmix-foundation-fixes-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $verificationRoot | Out-Null
$applicationProject = [System.Security.SecurityElement]::Escape((Join-Path $BackendRoot 'src/Cosmix.Application/Cosmix.Application.csproj'))
$controllerSource = [System.Security.SecurityElement]::Escape((Join-Path $BackendRoot 'src/Cosmix.Api/Controllers/HR/EmployeeBankController.cs'))
$verificationSource = [System.Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'VerifyFoundationFixes.cs'))
$project = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup>
  <ItemGroup><ProjectReference Include="$applicationProject"/><FrameworkReference Include="Microsoft.AspNetCore.App"/><Compile Include="$verificationSource"/><Compile Include="$controllerSource"/></ItemGroup>
</Project>
"@
$projectPath = Join-Path $verificationRoot 'Checks.csproj'
Set-Content -LiteralPath $projectPath -Value $project -Encoding utf8
Write-Output "Standalone verification files: $verificationRoot"
& dotnet run --project $projectPath -c Release --artifacts-path (Join-Path $verificationRoot 'Artifacts')
exit $LASTEXITCODE
