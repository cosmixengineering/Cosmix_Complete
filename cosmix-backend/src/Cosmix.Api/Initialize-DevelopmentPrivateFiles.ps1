#Requires -Version 7.0
param()
$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'This development helper requires Windows.' }
$root = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Cosmix/Development/PrivateFiles'))
$objects = Join-Path $root 'objects'
$keys = Join-Path $root 'keys'
$sid = [Security.Principal.WindowsIdentity]::GetCurrent().User
for ($ancestor = [IO.DirectoryInfo]::new($root); $null -ne $ancestor; $ancestor = $ancestor.Parent) {
    if ($ancestor.Exists -and ($ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Private storage cannot be provisioned through a junction or symbolic link.' }
}
foreach ($path in @($root, $objects, $keys)) {
    if (Test-Path -LiteralPath $path) {
        $item = Get-Item -LiteralPath $path -Force
        if (-not $item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Unsafe private storage directory.' }
    } else { New-Item -ItemType Directory -Path $path | Out-Null }
    $acl = [Security.AccessControl.DirectorySecurity]::new()
    $acl.SetOwner($sid)
    $acl.SetAccessRuleProtection($true, $false)
    foreach ($identity in @($sid, [Security.Principal.SecurityIdentifier]::new('S-1-5-18'), [Security.Principal.SecurityIdentifier]::new('S-1-5-32-544'))) {
        $rule = [Security.AccessControl.FileSystemAccessRule]::new($identity, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow')
        $acl.AddAccessRule($rule)
    }
    Set-Acl -LiteralPath $path -AclObject $acl
}
$keyPath = Join-Path $keys 'development-v1.key'
if (-not (Test-Path -LiteralPath $keyPath)) {
    $key = [Security.Cryptography.RandomNumberGenerator]::GetBytes(32)
    try {
        $stream = [IO.FileStream]::new($keyPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
        try { $stream.Write($key); $stream.Flush($true) } finally { $stream.Dispose() }
    } finally { [Array]::Clear($key, 0, $key.Length) }
}
$keyInfo = Get-Item -LiteralPath $keyPath -Force
if ($keyInfo.Length -ne 32 -or ($keyInfo.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Existing key is invalid; it was not replaced.' }
$project = Join-Path $PSScriptRoot 'Cosmix.Api.csproj'
foreach ($setting in @(@('PrivateFiles:Directory', $objects), @('PrivateFiles:KeyFile', $keyPath), @('PrivateFiles:KeyReference', 'development-v1'))) {
    & dotnet user-secrets set $setting[0] $setting[1] --project $project | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not set local private storage configuration.' }
}
Write-Output 'Development private encrypted storage is configured outside Git. Existing encryption keys were preserved.'
Write-Output 'Scanner remains disabled until a real company-approved ClamAV endpoint is configured.'
