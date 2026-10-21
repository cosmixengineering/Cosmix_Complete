$ErrorActionPreference='Stop'
$tokens=$null;$errors=$null
$path=Join-Path $PSScriptRoot 'Invoke-HrBusinessC03.ps1'
$ast=[Management.Automation.Language.Parser]::ParseFile($path,[ref]$tokens,[ref]$errors)
if($errors.Count){throw 'Runner syntax failed.'}
foreach($name in @('Assert','Begin-Request','Assert-Snapshot')) {
    $definition=@($ast.FindAll({param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst]},$true)|Where-Object Name -EQ $name)
    if($definition.Count -ne 1){throw 'Unique helper required.'}
    . ([scriptblock]::Create($definition[0].Extent.Text))
}
$script:requests=0;$script:departmentId=[guid]::Empty.ToString()
$departmentCode='TEST-C03-20261019';$checks=1
$fake=[pscustomobject]@{Sent=0;Last=$null}
$fake|Add-Member ScriptMethod SendAsync {param($message) $this.Sent++;$this.Last=$message;return [Threading.Tasks.Task]::CompletedTask}
function Reject([scriptblock]$action) {
    $before=$fake.Sent;$denied=$false
    try{& $action}catch{$denied=$true}
    if(!$denied -or $fake.Sent -ne $before){throw 'Unapproved request reached transport.'}
    $script:checks++
}
function Allowed([string]$method,[string]$path,$body) {
    $before=$fake.Sent
    $pending=Begin-Request $fake $method $path $body 'synthetic-csrf' 'TEST-C03-20261019-test'
    if($fake.Sent -ne $before+1 -or $fake.Last.Headers.GetValues('X-CSRF-TOKEN') -cne 'synthetic-csrf'){throw 'Approved request/header changed.'}
    $pending.Request.Dispose();$script:checks++
}
foreach($method in @('GET','POST','PUT','PATCH','DELETE')) {
    foreach($route in @('/api/v1/hr/employees','/api/v1/auth/web/refresh','https://remote.invalid/api/v1/hr/departments')) {
        Reject {Begin-Request $fake $method $route @{}}
    }
}
Reject {Begin-Request $fake POST '/api/v1/auth/web/login' @{email='superadmin@example.invalid'}}
Reject {Begin-Request $fake POST '/api/v1/hr/departments' @{code='real';name='Real department'}}
Reject {Begin-Request $fake POST '/api/v1/hr/departments' @{code=$departmentCode;name='Real department'}}
Reject {Begin-Request $fake POST '/api/v1/hr/departments' @{code=$departmentCode}}
Reject {Begin-Request $fake GET '/api/v1/leaves/admin/queue?pageSize=500' $null}
$id='c66d047c-06db-4cb5-807d-0b74b8940ec2'
Reject {Begin-Request $fake PUT ('/api/v1/hr/departments/'+$id) @{name='TEST ONLY C03 before create'}}
foreach($email in @('hr@example.invalid','admin@example.invalid')) {Allowed POST '/api/v1/auth/web/login' @{email=$email;password='synthetic'}}
Allowed POST '/api/v1/hr/departments' @{code=$departmentCode;name='TEST ONLY C03 concurrency department'}
Allowed POST '/api/v1/hr/departments' @{code=$departmentCode;name='TEST ONLY C03 changed payload'}
Allowed GET '/api/v1/leaves/admin/queue?pendingOnly=true&from=2026-01-01&to=2026-12-31&pageSize=5' $null
$script:departmentId=$id
Allowed PUT ('/api/v1/hr/departments/'+$id) @{name='TEST ONLY C03 winner A';expectedVersion='AAAAAAAAAAA=';reason='Synthetic concurrent update A'}
Allowed GET ('/api/v1/hr/departments/'+$id) $null
Allowed GET ('/api/v1/hr/departments/'+$id+'/history?pageSize=10') $null
Allowed POST ('/api/v1/hr/departments/'+$id+'/status') @{isActive=$false;expectedVersion='AAAAAAAAAAA=';reason='Synthetic test complete; retain inactive evidence'}
Reject {Begin-Request $fake PUT '/api/v1/hr/departments/00000000-0000-0000-0000-000000000001' @{name='TEST ONLY C03 wrong ID'}}
Reject {Begin-Request $fake DELETE ('/api/v1/hr/departments/'+$id) $null}
Reject {Begin-Request $fake GET ('/api/v1/hr/departments/'+$id+'/history?pageSize=500') $null}
$script:requests=160
Reject {Begin-Request $fake GET '/api/v1/me/permissions' $null}
Assert-Snapshot @{a='same'} @{a='same'} 'synthetic';$checks++
Reject {Assert-Snapshot @{a='same'} @{a='changed'} 'synthetic'}
Reject {Assert-Snapshot @{a='same'} @{a='same';b='extra'} 'synthetic'}
Write-Output "PASS $checks C03 runner parser/allowlist/request-budget/preservation checks; no HTTP, SQL or private writes."
