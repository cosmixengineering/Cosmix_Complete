[CmdletBinding()]
param(
    [string]$ConfigurationFile,
    [switch]$CheckLocalArtifacts,
    [switch]$CheckProviderConnection,
    [string]$BuildDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$faceRepositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$faceTemporaryRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('Cosmix-Face-Setup-' + [Guid]::NewGuid().ToString('N'))

try {
    New-Item -ItemType Directory -Path $faceTemporaryRoot | Out-Null
    if (-not $BuildDirectory) {
        $BuildDirectory = Join-Path $faceTemporaryRoot 'ReleaseBuild'
        dotnet build (Join-Path $faceRepositoryRoot 'Cosmix.Backend.sln') -c Release --no-restore -m:1 -p:UseSharedCompilation=false "-p:OutDir=$BuildDirectory/" -v:minimal
        if ($LASTEXITCODE -ne 0) { throw 'build_failed' }
    }
    $BuildDirectory = [System.IO.Path]::GetFullPath($BuildDirectory)
    if (-not (Test-Path -LiteralPath (Join-Path $BuildDirectory 'Cosmix.Infrastructure.dll') -PathType Leaf)) { throw 'build_missing' }

    # A package-free TEMP runner references compiled assemblies; it cannot launch the API.
    $faceProject = [System.Xml.Linq.XDocument]::Parse(@'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <NuGetAudit>false</NuGetAudit>
    <UseSharedCompilation>false</UseSharedCompilation>
  </PropertyGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
</Project>
'@)
    $faceItems = $faceProject.Root.Element('ItemGroup')
    foreach ($faceAssembly in Get-ChildItem -LiteralPath $BuildDirectory -Filter '*.dll' -File) {
        $faceReference = [System.Xml.Linq.XElement]::new([System.Xml.Linq.XName]'Reference')
        $faceReference.SetAttributeValue('Include', $faceAssembly.FullName)
        $faceItems.Add($faceReference)
    }
    $faceNativeDirectory = Join-Path $BuildDirectory 'runtimes/win-x64/native'
    if (Test-Path -LiteralPath $faceNativeDirectory -PathType Container) {
        foreach ($faceNative in Get-ChildItem -LiteralPath $faceNativeDirectory -Filter '*.dll' -File) {
            $faceContent = [System.Xml.Linq.XElement]::new([System.Xml.Linq.XName]'Content')
            $faceContent.SetAttributeValue('Include', $faceNative.FullName)
            $faceContent.Add([System.Xml.Linq.XElement]::new([System.Xml.Linq.XName]'Link', $faceNative.Name))
            $faceContent.Add([System.Xml.Linq.XElement]::new([System.Xml.Linq.XName]'CopyToOutputDirectory', 'PreserveNewest'))
            $faceItems.Add($faceContent)
        }
    }
    $faceProjectPath = Join-Path $faceTemporaryRoot 'Setup.csproj'
    $faceProject.Save($faceProjectPath)
    [System.IO.File]::WriteAllText((Join-Path $faceTemporaryRoot 'Program.cs'), @'
using System.Text.Json;
using System.Runtime.InteropServices;
using Cosmix.Infrastructure.FaceVerification;
using Microsoft.Extensions.Configuration;
try
{
    var file = args.Length > 0 && args[0].Length > 0 ? args[0] : null;
    var connect = args.Length > 2 && args[2] == "provider-health";
    // Keep validation and the probe bound to the same file; deny replacement or writes.
    FileStream? fileLease = null;
    try
    {
        if (file is not null) fileLease = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read);
    }
    catch (Exception)
    {
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            Issues = new[] { new FaceConfigurationIssue("ConfigurationFile", "private_configuration_file_invalid") },
            DisabledSections = Array.Empty<string>(), LocalArtifacts = "not_checked", Passed = false,
            ProviderConnection = "not_checked", RealPersonAcceptance = "not_run"
        }));
        return 2;
    }
    using var heldFileLease = fileLease;
    if (connect || args.Length > 1 && args[1] == "local-artifacts")
    {
        // This package-free runner has no package-native asset map. Resolve only
        // the codec staged from the selected build, never a machine-wide fallback.
        NativeLibrary.SetDllImportResolver(typeof(OpenCvSharp.Mat).Assembly,
            (name, assembly, searchPath) => name == "OpenCvSharpExtern"
                ? NativeLibrary.Load(Path.Combine(AppContext.BaseDirectory, "OpenCvSharpExtern.dll"))
                : IntPtr.Zero);
    }
    var report = SelfHostedFaceConfigurationReadiness.EvaluateFile(file,
        connect || args.Length > 1 && args[1] == "local-artifacts");
    var providerConnection = "not_checked";
    if (connect && report.Passed && file is not null)
    {
        try
        {
            // Reuse the actual adapter's private-destination and pinned TLS validation.
            var configuration = new ConfigurationBuilder().AddJsonFile(file).Build();
            using var configurationLease = configuration as IDisposable;
            var options = configuration.GetSection(CompreFaceOptions.SectionName).Get<CompreFaceOptions>()!;
            // Standalone diagnostics reuse the internal transport, as adapter harnesses do.
            var factory = typeof(CompreFaceClient).GetMethod("CreateTransport",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                ?? throw new InvalidOperationException();
            using var transport = (HttpMessageHandler)factory.Invoke(null, [options])!;
            using var client = new HttpClient(transport, disposeHandler: false)
            {
                Timeout = TimeSpan.FromSeconds(15),
                MaxResponseContentBufferSize = 1024
            };
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            using var response = await client.GetAsync(new Uri(options.BaseUri!, "healthz"), deadline.Token);
            if (response.StatusCode != System.Net.HttpStatusCode.OK ||
                (await response.Content.ReadAsStringAsync(deadline.Token)).Trim() != "ok")
                throw new InvalidOperationException();
            providerConnection = "reachable_with_adapter_tls_validation";
        }
        catch (Exception)
        {
            providerConnection = "failed";
            report = new FaceConfigurationReport(
                [.. report.Issues, new("SelfHostedFace:CompreFace", "provider_health_unavailable")],
                report.DisabledSections, report.LocalArtifacts);
        }
    }
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        report.Issues, report.DisabledSections, report.LocalArtifacts, report.Passed,
        ProviderConnection = providerConnection, RealPersonAcceptance = "not_run"
    }, new JsonSerializerOptions { WriteIndented = true }));
    return report.Passed ? 0 : 2;
}
catch (Exception)
{
    Console.WriteLine("face_setup_runner_failed");
    return 3;
}
'@)
    dotnet restore $faceProjectPath --source $faceTemporaryRoot --packages (Join-Path $faceTemporaryRoot 'packages') --no-http-cache -p:NuGetAudit=false -v:quiet
    if ($LASTEXITCODE -ne 0) { throw 'restore_failed' }
    dotnet build $faceProjectPath -c Release --no-restore -v:quiet
    if ($LASTEXITCODE -ne 0) { throw 'runner_build_failed' }
    $faceMode = if ($CheckLocalArtifacts) { 'local-artifacts' } else { 'configuration' }
    $faceInputFile = if ($ConfigurationFile) { [System.IO.Path]::GetFullPath($ConfigurationFile) } else { '' }
    $faceConnection = if ($CheckProviderConnection) { 'provider-health' } else { 'no-network' }
    dotnet (Join-Path $faceTemporaryRoot 'bin/Release/net10.0/Setup.dll') $faceInputFile $faceMode $faceConnection
    exit $LASTEXITCODE
}
catch {
    Write-Output 'face_setup_runner_failed'
    exit 3
}
