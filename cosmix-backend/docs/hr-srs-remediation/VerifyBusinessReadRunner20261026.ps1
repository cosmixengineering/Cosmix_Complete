$ErrorActionPreference='Stop'
$path=Join-Path $PSScriptRoot 'Invoke-HrBusinessRead20261026.ps1'
$tokens=$null;$errors=$null
$ast=[System.Management.Automation.Language.Parser]::ParseFile($path,[ref]$tokens,[ref]$errors)
if($errors.Count){throw 'Runner has parser errors.'}
# Load only selected function definitions, never the script's preflight or execution body.
foreach($name in @('Assert','Begin-Request','Assert-Snapshot','Assert-PrivateSource')) {
    $definition=$ast.FindAll({param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst]},$true)|Where-Object Name -EQ $name
    if(@($definition).Count -ne 1){throw 'Missing unique helper.'}
    . ([scriptblock]::Create($definition.Extent.Text))
}
$script:requests=0;$checks=1
$fake=[pscustomobject]@{Sent=0;Last=$null}
$fake|Add-Member ScriptMethod SendAsync {param($message) $this.Sent++;$this.Last=$message;return [Threading.Tasks.Task]::CompletedTask}
function Reject([scriptblock]$action) {
    $before=$fake.Sent
    $denied=$false
    try{& $action}catch{$denied=$true}
    if(!$denied -or $fake.Sent -ne $before){throw 'Unsafe request reached transport.'}
    $script:checks++
}
foreach($method in @('GET','POST','PUT','DELETE','PATCH')) {
    foreach($route in @('/api/v1/hr/departments','/api/v1/auth/web/refresh','/api/v1/auth/web/logout-all','https://remote.invalid/api/v1/me/permissions')) {
        Reject {Begin-Request $fake $method $route @{}}
    }
}
Reject {Begin-Request $fake POST '/api/v1/auth/web/login' @{email='superadmin@example.invalid';password='synthetic'}}
foreach($email in @('hr@example.invalid','admin@example.invalid')) {
    $pending=Begin-Request $fake POST '/api/v1/auth/web/login' @{email=$email;password='synthetic-not-a-credential'} 'synthetic-csrf'
    if($fake.Last.Headers.GetValues('X-CSRF-TOKEN') -cne 'synthetic-csrf'){throw 'Missing CSRF header.'}
    $body=$fake.Last.Content.ReadAsStringAsync().GetAwaiter().GetResult()|ConvertFrom-Json
    if($body.email -cne $email){throw 'Wrong account body.'}
    $pending.Request.Dispose();$checks+=2
}
$script:requests=80
Reject {Begin-Request $fake GET '/api/v1/me/permissions' $null}
Assert-Snapshot @{a='hash'} @{a='hash'} 'synthetic';$checks++
Reject {Assert-Snapshot @{a='hash'} @{a='changed'} 'synthetic'}
Reject {Assert-Snapshot @{a='hash'} @{a='hash';b='extra'} 'synthetic'}
# Empty retained stdout is valid evidence; empty credentials/settings are never valid.
function Get-Item { param([string]$LiteralPath) [pscustomobject]@{PSIsContainer=$false;Length=0;Attributes=[IO.FileAttributes]::Archive} }
function Get-Acl {
    param([string]$LiteralPath)
    $acl=[Security.AccessControl.FileSecurity]::new()
    $acl.SetOwner([Security.Principal.WindowsIdentity]::GetCurrent().User)
    return $acl
}
try {
    Reject {Assert-PrivateSource 'C:\synthetic-empty.log'}
    Assert-PrivateSource 'C:\synthetic-empty.log' $true; $checks++
} finally {
    Remove-Item Function:Get-Item,Function:Get-Acl
}
Write-Output "PASS $checks runner parser/allowlist/budget/snapshot checks; synthetic transport, no host, SQL or private files."
