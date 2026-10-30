#requires -Version 7.4
<# Run locally on the IIS server. Creates a dedicated, STOPPED single site after
all preflight checks pass. No secrets, migrations, firewall or existing live site
are changed. WhatIf performs the same read-only server checks. #>
[CmdletBinding(SupportsShouldProcess, ConfirmImpact='Medium')]
param([Parameter(Mandatory)][string]$PackageDirectory,
    [ValidatePattern('^[A-Fa-f0-9]{40}$')][string]$CertificateThumbprint,
    [string]$SiteName='CosmixErp', [string]$AppPoolName='CosmixAppPool', [switch]$PackageOnly)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'IisDeployment.psm1') -Force
$plan = Get-CosmixIisPlan -PackageDirectory $PackageDirectory -SiteName $SiteName -AppPoolName $AppPoolName
$null = & (Join-Path $PSScriptRoot 'Test-IisPackage.ps1') -PackageDirectory $plan.PackageDirectory
if ($PackageOnly) {
    $plan | Add-Member -NotePropertyName Verification -NotePropertyValue 'Package only; IIS/certificate not checked' -PassThru
    return
}
if (-not $CertificateThumbprint) { throw 'Select the installed server certificate thumbprint before IIS setup.' }
if (-not $IsWindows) { throw 'IIS setup must run locally on the Windows server.' }
$principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Run elevated PowerShell 7.4+ on the IIS server.' }
if ($plan.Address -notin @([Net.NetworkInformation.NetworkInterface]::GetAllNetworkInterfaces() |
    ForEach-Object { $_.GetIPProperties().UnicastAddresses } | ForEach-Object { $_.Address.ToString() })) {
    throw 'The package IP is not assigned to this server.'
}
$certificate = Get-Item -LiteralPath ('Cert:\LocalMachine\My\'+$CertificateThumbprint)
Assert-CosmixCertificateIdentity -Certificate $certificate -Address $plan.Address
$chain = [Security.Cryptography.X509Certificates.X509Chain]::new($true)
try {
    $chain.ChainPolicy.RevocationMode = [Security.Cryptography.X509Certificates.X509RevocationMode]::Online
    $chain.ChainPolicy.UrlRetrievalTimeout = [TimeSpan]::FromSeconds(15)
    if (-not $chain.Build($certificate)) { throw 'Server certificate chain/revocation validation failed. Resolve trust with IT before binding.' }
} finally { $chain.Dispose() }
$administration = Join-Path $env:windir 'System32/inetsrv/Microsoft.Web.Administration.dll'
if (-not (Test-Path -LiteralPath $administration -PathType Leaf)) { throw 'IIS management components are not installed.' }
Add-Type -Path $administration
$manager = [Microsoft.Web.Administration.ServerManager]::new()
try {
    $globalModules = $manager.GetApplicationHostConfiguration().GetSection('system.webServer/globalModules').GetCollection()
    if ('AspNetCoreModuleV2' -notin @($globalModules | ForEach-Object { $_.GetAttributeValue('name') })) { throw '.NET Hosting Bundle / ASP.NET Core Module V2 is required.' }
    $dotnet = Join-Path $env:ProgramFiles 'dotnet/dotnet.exe'
    $runtimes = @(& $dotnet --list-runtimes)
    if ($LASTEXITCODE -ne 0 -or -not ($runtimes -match '^Microsoft.AspNetCore.App 10\.')) { throw '.NET 10 x64 ASP.NET Core runtime is required.' }
    $existing = $manager.Sites[$SiteName]
    $pool = $manager.ApplicationPools[$AppPoolName]
    foreach ($other in $manager.Sites) {
        if ($other.Name -eq $SiteName) { continue }
        foreach ($binding in $other.Bindings) {
            if ($binding.Protocol -eq 'https' -and $binding.EndPoint.Port -eq 443 -and
                $binding.EndPoint.Address.ToString() -in @($plan.Address,'0.0.0.0','::')) { throw 'Another IIS site owns a conflicting HTTPS binding.' }
        }
        if ($AppPoolName -in @($other.Applications | ForEach-Object ApplicationPoolName)) { throw 'The selected application pool is shared with another site.' }
    }
    if ($null -ne $existing) {
        if ($existing.State.ToString() -ne 'Stopped' -or $existing.Applications.Count -ne 1 -or
            $existing.Applications['/'].VirtualDirectories['/'].PhysicalPath -ne $plan.SitePath -or
            $existing.Applications['/'].ApplicationPoolName -ne $AppPoolName -or $existing.Bindings.Count -ne 1 -or
            $existing.Bindings[0].Protocol -ne 'https' -or $existing.Bindings[0].BindingInformation -ne $plan.Binding -or
            [Convert]::ToHexString($existing.Bindings[0].CertificateHash) -ne $CertificateThumbprint.ToUpperInvariant()) {
            throw 'Existing site differs or is running. Review deployment/rollback explicitly; this setup never overwrites it.'
        }
    } elseif ($null -ne $pool) { throw 'An existing unattached application pool is not assumed to belong to this release.' }
    if ($PSCmdlet.ShouldProcess("$SiteName at $($plan.Origin)", 'Provision dedicated stopped IIS site and read-only release access')) {
        if ($null -eq $pool) { $pool = $manager.ApplicationPools.Add($AppPoolName) }
        $pool.ManagedRuntimeVersion = ''
        $pool.ManagedPipelineMode = [Microsoft.Web.Administration.ManagedPipelineMode]::Integrated
        $pool.Enable32BitAppOnWin64 = $false
        $pool.StartMode = [Microsoft.Web.Administration.StartMode]::AlwaysRunning
        $pool.ProcessModel.IdentityType = [Microsoft.Web.Administration.ProcessModelIdentityType]::ApplicationPoolIdentity
        $pool.ProcessModel.IdleTimeout = [TimeSpan]::Zero
        $pool.ProcessModel.MaxProcesses = 1
        $pool.ProcessModel.LoadUserProfile = $true
        $pool.Failure.RapidFailProtection = $true
        if ($null -eq $existing) {
            $existing = $manager.Sites.Add($SiteName, 'https', $plan.Binding, $plan.SitePath)
            $existing.Bindings[0].CertificateHash = $certificate.GetCertHash()
            $existing.Bindings[0].CertificateStoreName = 'My'
            $existing.Bindings[0].SslFlags = [Microsoft.Web.Administration.SslFlags]::None
        }
        $existing.ServerAutoStart = $false
        $existing.Applications['/'].ApplicationPoolName = $AppPoolName
        $existing.Applications['/'].SetAttributeValue('preloadEnabled', $true)
        $manager.CommitChanges()
        $acl = Get-Acl -LiteralPath $plan.SitePath
        $rule = [Security.AccessControl.FileSystemAccessRule]::new(('IIS AppPool\'+$AppPoolName), 'ReadAndExecute', 'ContainerInherit,ObjectInherit', 'None', 'Allow')
        $acl.SetAccessRule($rule)
        Set-Acl -LiteralPath $plan.SitePath -AclObject $acl
        'Dedicated HTTPS site provisioned and left stopped. Complete private settings, storage ACLs, migrations and live health checks before activation.'
    }
} finally { $manager.Dispose() }
