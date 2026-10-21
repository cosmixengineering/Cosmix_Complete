param([Parameter(Mandatory=$true)][string[]]$Sources)
$ErrorActionPreference = 'Stop'
$taskBackend = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../cosmix-backend'))
$taskWrapper = 'D:\CosmixPartAContinuation20261013\taxonomy-consumers'
New-Item -ItemType Directory -Path $taskWrapper -Force | Out-Null
$taskProject = @"
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><UseSharedCompilation>false</UseSharedCompilation><NuGetAudit>false</NuGetAudit></PropertyGroup><ItemGroup><FrameworkReference Include="Microsoft.AspNetCore.App"/><Reference Include="Cosmix.Application"><HintPath>$taskBackend\src\Cosmix.Application\bin\Release\net10.0\Cosmix.Application.dll</HintPath></Reference><Reference Include="Cosmix.Domain"><HintPath>$taskBackend\src\Cosmix.Domain\bin\Release\net10.0\Cosmix.Domain.dll</HintPath></Reference></ItemGroup></Project>
"@
[IO.File]::WriteAllText((Join-Path $taskWrapper 'Checks.csproj'), $taskProject)
foreach ($taskSource in $Sources) {
    $taskResolved = [IO.Path]::GetFullPath((Join-Path $taskBackend $taskSource))
    if (!$taskResolved.StartsWith($taskBackend + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Source outside backend' }
    $taskProgram = ([IO.File]::ReadAllLines($taskResolved) | Where-Object { ! $_.StartsWith('#:') }) -join "`n"
    $taskUsings = [regex]::Matches($taskProgram, '(?m)^using [A-Za-z0-9_.]+;\r?\n')
    $taskStart = $taskUsings[$taskUsings.Count-1].Index + $taskUsings[$taskUsings.Count-1].Length
    $taskTypes = [regex]::Match($taskProgram, '(?m)^(?:public |internal |sealed |abstract )*(?:class|record|struct|interface|enum)\s')
    if (!$taskTypes.Success) { throw 'Standalone class boundary missing' }
    $taskWrapped = $taskProgram.Substring(0,$taskStart) + "`ntry {`n" +
        $taskProgram.Substring($taskStart,$taskTypes.Index-$taskStart) +
        "`n} catch(Exception taskFailure) { Console.Error.WriteLine(taskFailure); Environment.ExitCode=1; }`n" +
        $taskProgram.Substring($taskTypes.Index)
    [IO.File]::WriteAllText((Join-Path $taskWrapper 'Program.cs'), $taskWrapped)
    Write-Output "Checking $taskSource"
    dotnet run --project (Join-Path $taskWrapper 'Checks.csproj') -c Release --property:RestoreSources="$taskBackend/src/Cosmix.Application" --property:NuGetAudit=false --property:UseSharedCompilation=false
    if ($LASTEXITCODE -ne 0) { throw "Consumer check failed: $taskSource (exit $LASTEXITCODE)" }
}
