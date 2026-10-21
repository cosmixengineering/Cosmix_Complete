param([string]$BackendPath = (Join-Path $PSScriptRoot '../../../cosmix-backend'))
$ErrorActionPreference = 'Stop'
$consumerBackendRoot = (Resolve-Path -LiteralPath $BackendPath).Path
$consumerApplicationProject = Join-Path $consumerBackendRoot 'src/Cosmix.Application/Cosmix.Application.csproj'
$consumerCheckRoot = Join-Path $env:TEMP ('cosmix-taxonomy-hr-checks-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $consumerCheckRoot | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'taxonomy.HrConsumerChecks.cs') -Destination (Join-Path $consumerCheckRoot 'Program.cs')
$consumerProjectXmlPath = [System.Security.SecurityElement]::Escape($consumerApplicationProject)
$consumerProjectText = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup>
  <ItemGroup><ProjectReference Include="$consumerProjectXmlPath" /></ItemGroup>
</Project>
"@
$consumerProjectPath = Join-Path $consumerCheckRoot 'Checks.csproj'
Set-Content -LiteralPath $consumerProjectPath -Value $consumerProjectText -Encoding utf8
& dotnet run --project $consumerProjectPath --configuration Release --artifacts-path (Join-Path $consumerCheckRoot 'artifacts')
if ($LASTEXITCODE -ne 0) { throw "HR consumer checks failed with exit code $LASTEXITCODE. Artifacts: $consumerCheckRoot" }
Write-Output "Synthetic HR consumer verification artifacts: $consumerCheckRoot"
