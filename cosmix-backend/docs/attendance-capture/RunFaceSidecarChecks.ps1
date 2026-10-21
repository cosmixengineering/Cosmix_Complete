$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$faceRepositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$faceTemporaryRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('Cosmix-Face-Sidecar-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $faceTemporaryRoot | Out-Null
$faceHarnessNames = @(
    'VerifySelfHostedFaceProvider',
    'VerifyFaceStorageLifecycle',
    'VerifyFaceCaptureProtection',
    'VerifyFaceReadiness',
    'VerifyEnrollmentJournal',
    'VerifyCaptureService',
    'VerifyFaceCaptureSelection'
)

# This temporary project has no packages or project references and cannot start the API.
$faceProjectXml = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <NuGetAudit>false</NuGetAudit>
    <RestoreSources>$(MSBuildProjectDirectory)</RestoreSources>
    <UseSharedCompilation>false</UseSharedCompilation>
    <OutputPath>$(MSBuildProjectDirectory)/bin/$(HarnessName)/</OutputPath>
    <IntermediateOutputPath>$(MSBuildProjectDirectory)/obj/$(HarnessName)/</IntermediateOutputPath>
  </PropertyGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
    <Reference Include="$(MSBuildProjectDirectory)/ReleaseBuild/*.dll" />
    <Compile Include="$(MSBuildProjectDirectory)/$(HarnessName).cs" />
    <Content Include="$(FaceRuntimeDirectory)/win-x64/native/*.dll">
      <Link>%(Filename)%(Extension)</Link>
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </Content>
  </ItemGroup>
</Project>
'@
$faceProjectPath = Join-Path $faceTemporaryRoot 'Checks.csproj'
[System.Xml.Linq.XDocument]::Parse($faceProjectXml).Save($faceProjectPath)
foreach ($faceHarnessName in $faceHarnessNames) {
    $faceSourcePath = Join-Path $PSScriptRoot "$faceHarnessName.cs"
    $faceSourceLines = [System.IO.File]::ReadAllLines($faceSourcePath) | Where-Object { -not $_.StartsWith('#:') }
    [System.IO.File]::WriteAllLines((Join-Path $faceTemporaryRoot "$faceHarnessName.cs"), [string[]]$faceSourceLines)
}

Write-Output "Face verification TEMP root: $faceTemporaryRoot"
Push-Location $faceRepositoryRoot
try {
    dotnet build Cosmix.Backend.sln -c Release --no-restore -m:1 -p:UseSharedCompilation=false -p:CopyLocalLockFileAssemblies=true "-p:OutDir=$faceTemporaryRoot/ReleaseBuild/" -v:minimal
    if ($LASTEXITCODE -ne 0) { throw "Release build exited $LASTEXITCODE" }

    # Native binaries come from the already installed, locked Windows runtime packages.
    $faceRuntimeDirectory = Join-Path $faceRepositoryRoot 'src/Cosmix.Api/bin/Release/net10.0/runtimes'
    $faceNativePath = Join-Path $faceRuntimeDirectory 'win-x64/native/OpenCvSharpExtern.dll'
    if (-not (Test-Path -LiteralPath $faceNativePath -PathType Leaf)) { throw 'Existing Windows OpenCV runtime is missing' }

    $facePreviousPackages = [Environment]::GetEnvironmentVariable('NUGET_PACKAGES', 'Process')
    $facePreviousHttpCache = [Environment]::GetEnvironmentVariable('NUGET_HTTP_CACHE_PATH', 'Process')
    $facePreviousPluginCache = [Environment]::GetEnvironmentVariable('NUGET_PLUGINS_CACHE_PATH', 'Process')
    try {
        $env:NUGET_PACKAGES = Join-Path $faceTemporaryRoot 'packages'
        $env:NUGET_HTTP_CACHE_PATH = Join-Path $faceTemporaryRoot 'http-cache'
        $env:NUGET_PLUGINS_CACHE_PATH = Join-Path $faceTemporaryRoot 'plugin-cache'
        dotnet restore $faceProjectPath --source $faceTemporaryRoot --packages $env:NUGET_PACKAGES --no-http-cache -p:NuGetAudit=false "-p:FaceRuntimeDirectory=$faceRuntimeDirectory" -v:minimal
        if ($LASTEXITCODE -ne 0) { throw "TEMP harness restore exited $LASTEXITCODE" }
        foreach ($faceHarnessName in $faceHarnessNames) {
            Write-Output "Executing: $faceHarnessName"
            dotnet run --project $faceProjectPath -c Release --no-restore "-p:HarnessName=$faceHarnessName" "-p:FaceRuntimeDirectory=$faceRuntimeDirectory" -v:minimal
            $faceExitCode = $LASTEXITCODE
            Write-Output "Exit: $faceHarnessName=$faceExitCode"
            if ($faceExitCode -ne 0) { throw "$faceHarnessName exited $faceExitCode" }
        }
    }
    finally {
        [Environment]::SetEnvironmentVariable('NUGET_PACKAGES', $facePreviousPackages, 'Process')
        [Environment]::SetEnvironmentVariable('NUGET_HTTP_CACHE_PATH', $facePreviousHttpCache, 'Process')
        [Environment]::SetEnvironmentVariable('NUGET_PLUGINS_CACHE_PATH', $facePreviousPluginCache, 'Process')
    }
}
finally { Pop-Location }

Write-Output 'Completed existing offline checks only; no provider/model/device/SQL acceptance.'
