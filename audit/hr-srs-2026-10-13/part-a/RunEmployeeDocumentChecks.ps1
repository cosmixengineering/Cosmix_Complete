param([Parameter(Mandatory = $true)][string]$BuildDirectory)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$a02BuildDirectory = [System.IO.Path]::GetFullPath($BuildDirectory)
if (-not (Test-Path -LiteralPath (Join-Path $a02BuildDirectory 'Cosmix.Infrastructure.dll') -PathType Leaf)) {
    throw 'Supply the coordinated Release output containing current Application, Domain, Infrastructure and dependencies.'
}
$a02CheckRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('Cosmix-A02-Checks-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $a02CheckRoot | Out-Null
$a02ProjectXml = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems><UseSharedCompilation>false</UseSharedCompilation>
    <NuGetAudit>false</NuGetAudit><RestoreSources>$(MSBuildProjectDirectory)</RestoreSources>
    <OutputPath>$(MSBuildProjectDirectory)/bin/$(HarnessName)/</OutputPath>
    <IntermediateOutputPath>$(MSBuildProjectDirectory)/obj/$(HarnessName)/</IntermediateOutputPath>
  </PropertyGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
    <Reference Include="$(CompiledAssemblyDirectory)/*.dll" />
    <Compile Include="$(CheckSourceDirectory)/$(HarnessName).cs" />
  </ItemGroup>
</Project>
'@
$a02ProjectPath = Join-Path $a02CheckRoot 'Checks.csproj'
[System.Xml.Linq.XDocument]::Parse($a02ProjectXml).Save($a02ProjectPath)
Write-Output "A02 TEMP harness: $a02CheckRoot"
dotnet restore $a02ProjectPath --source $a02CheckRoot --no-http-cache -p:NuGetAudit=false -v:minimal
if ($LASTEXITCODE -ne 0) { throw "A02 TEMP restore exited $LASTEXITCODE" }
foreach ($a02Harness in @('VerifyEmployeeDocuments', 'VerifyEmployeeDocumentService', 'VerifyEmployeeDocumentLifecycle', 'VerifyEmployeeDocumentGuard')) {
    dotnet run --project $a02ProjectPath -c Release --no-restore "-p:HarnessName=$a02Harness" "-p:CompiledAssemblyDirectory=$a02BuildDirectory" "-p:CheckSourceDirectory=$PSScriptRoot" -v:minimal
    if ($LASTEXITCODE -ne 0) { throw "$a02Harness exited $LASTEXITCODE" }
}
Write-Output 'A02 offline checks completed; no SQL connection, account, real provider or private employee file was used.'
