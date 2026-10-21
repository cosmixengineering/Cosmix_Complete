$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSHOME 'Modules/Microsoft.PowerShell.Security/Microsoft.PowerShell.Security.psd1')
$repo = Split-Path -Parent $PSScriptRoot
$cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object {
    $_.Subject -eq 'CN=localhost' -and $_.HasPrivateKey -and $_.NotAfter -gt (Get-Date) -and
    ($_.Extensions | Where-Object { $_.Oid.Value -eq '1.3.6.1.4.1.311.84.1.1' })
} | Sort-Object NotAfter -Descending | Select-Object -First 1
if (!$cert -or !(Test-Path "Cert:\CurrentUser\Root\$($cert.Thumbprint)")) {
    throw 'A trusted ASP.NET localhost certificate is required. Run dotnet dev-certs https --trust, then npm run dev. No trust settings are changed by this script.'
}
$folder = Join-Path ([IO.Path]::GetTempPath()) ('cosmix-https-' + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $folder
$acl = New-Object System.Security.AccessControl.DirectorySecurity
$acl.SetAccessRuleProtection($true, $false)
$identity = [Security.Principal.WindowsIdentity]::GetCurrent().User
$acl.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule($identity, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow')))
Set-Acl -LiteralPath $folder -AclObject $acl
$bytes = New-Object byte[] 32
$rng = [Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($bytes)
$rng.Dispose()
$password = [Convert]::ToBase64String($bytes)
$pfx = Join-Path $folder 'localhost.pfx'
$previousPfx = $env:COSMIX_DEV_HTTPS_PFX
$previousPassword = $env:COSMIX_DEV_HTTPS_PASSWORD
try {
    [IO.File]::WriteAllBytes($pfx, $cert.Export([Security.Cryptography.X509Certificates.X509ContentType]::Pfx, $password))
    $env:COSMIX_DEV_HTTPS_PFX = $pfx
    $env:COSMIX_DEV_HTTPS_PASSWORD = $password
    Push-Location $repo
    try { & node (Join-Path $repo 'node_modules/vite/bin/vite.js') @args }
    finally { Pop-Location }
} finally {
    $env:COSMIX_DEV_HTTPS_PFX = $previousPfx
    $env:COSMIX_DEV_HTTPS_PASSWORD = $previousPassword
    $resolved = [IO.Path]::GetFullPath($folder)
    $temp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if ($resolved.StartsWith($temp, [StringComparison]::OrdinalIgnoreCase) -and (Split-Path $resolved -Leaf) -like 'cosmix-https-*') {
        Remove-Item -LiteralPath $pfx -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $resolved -Force -ErrorAction SilentlyContinue
    }
}
