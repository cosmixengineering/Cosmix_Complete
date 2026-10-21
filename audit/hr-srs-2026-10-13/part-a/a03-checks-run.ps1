param([string]$RepoRoot = 'C:\Users\Huma.Khan\Downloads\Cosmix_Erp_software\cosmix-backend')
$ErrorActionPreference = 'Stop'
$a03Temp = Join-Path $env:TEMP ('cosmix-a03-verification-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $a03Temp -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'a03-checks.cs') -Destination (Join-Path $a03Temp 'Program.cs')
$a03Application = [System.Security.SecurityElement]::Escape((Join-Path $RepoRoot 'src/Cosmix.Application/Cosmix.Application.csproj'))
$a03Infrastructure = [System.Security.SecurityElement]::Escape((Join-Path $RepoRoot 'src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj'))
$a03Project = '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><ProjectReference Include="' + $a03Application + '" /><ProjectReference Include="' + $a03Infrastructure + '" /></ItemGroup></Project>'
Set-Content -LiteralPath (Join-Path $a03Temp 'Checks.csproj') -Value $a03Project
$a03Output = Join-Path $a03Temp 'out'
dotnet run --project (Join-Path $a03Temp 'Checks.csproj') -c Release "-p:OutDir=$a03Output\"
exit $LASTEXITCODE
