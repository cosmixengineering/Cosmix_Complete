$ErrorActionPreference='Stop'
$path=Join-Path $PSScriptRoot 'Invoke-HrReportsR1.ps1'
$tokens=$null;$errors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile($path,[ref]$tokens,[ref]$errors)
if($errors.Count){throw ($errors|ForEach-Object Message|Out-String)}
$definition=@($ast.FindAll({param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -ceq 'Test-Route'},$true))
if($definition.Count -ne 1){throw 'Missing route guard.'}
. ([scriptblock]::Create($definition[0].Extent.Text))
$employee='35d19922-70a8-4c80-8578-e1029360e9fc'
$marker='TEST-HR-R1-20261020-export'
$reason='TEST ONLY R1 report acceptance'
$jobId='11111111-1111-1111-1111-111111111111'
$checks=0
function Check([bool]$condition,[string]$name){if(!$condition){throw $name};$script:checks++}
foreach($sample in @('[]','[{"Id":"11111111-1111-1111-1111-111111111111"}]')){
    $decoded=ConvertFrom-Json -InputObject $sample -NoEnumerate -AsHashtable
    Check ($decoded -is [array]) 'SQL JSON preserves empty and single-row arrays'
    $ids=@(foreach($item in $decoded){([guid]$item.Id).ToString('D')})
    Check ($ids.Count -eq $decoded.Count) 'SQL JSON creates no phantom null ID'
}
foreach($route in @(
    @('GET','/api/v1/me/permissions'),@('GET','/api/v1/auth/web/csrf'),
    @('POST','/api/v1/auth/web/logout'),@('POST','/api/v1/auth/web/local-testing/otp'),
    @('POST','/api/v1/auth/web/otp/verify'))){
    Check (Test-Route $route[0] $route[1] @{} '') ('allowed auth '+$route[1])
}
Check (Test-Route POST '/api/v1/auth/web/login' @{email='hr@example.invalid'} '') 'HR login allowed'
foreach($email in @('admin@example.invalid','superadmin@example.invalid','real@company.example','HR@example.invalid','')){
    Check (!(Test-Route POST '/api/v1/auth/web/login' @{email=$email} '')) 'other identity denied'
}
foreach($page in @(1,2)){
    Check (Test-Route GET "/api/v1/hr/reports/employee-master?employeeId=$employee&page=$page&pageSize=1" $null '') 'exact preview allowed'
}
foreach($route in @('/api/v1/hr/reports/employee-master',"/api/v1/hr/reports/employee-master?employeeId=$employee&page=1&pageSize=100",
    '/api/v1/hr/employees','/api/v1/hr/reports/audit',"/api/v1/hr/reports/employee-master?employeeId=$jobId&page=1&pageSize=1",
    "/api/v1/hr/reports/exports/$jobId/other",'/api/v1/hr/reports/exports/22222222-2222-2222-2222-222222222222')){
    Check (!(Test-Route GET $route $null '')) 'unapproved read denied'
}
foreach($format in @(1,2,3)){
    $body=@{format=$format;reason=$reason;filter=@{employeeId=$employee;page=1;pageSize=1}}
    $route=if($format -eq 3){'/api/v1/hr/reports/employee-master/export'}else{'/api/v1/hr/reports/employee-master/export-page'}
    $key=if($format -eq 3){$marker}else{''}
    Check (Test-Route POST $route $body $key) 'exact export allowed'
    foreach($field in @('employeeId','page','pageSize')){
        $copy=@{format=$format;reason=$reason;filter=@{employeeId=$employee;page=1;pageSize=1}}
        $copy.filter[$field]=if($field -eq 'employeeId'){$jobId}else{2}
        Check (!(Test-Route POST $route $copy $key)) 'changed filter denied'
    }
    Check (!(Test-Route DELETE $route $body $key)) 'wrong method denied'
}
Check (Test-Route GET "/api/v1/hr/reports/exports/$jobId" $null '') 'captured job status allowed'
Check (Test-Route GET "/api/v1/hr/reports/exports/$jobId/download" $null '') 'captured job download allowed'
$jobId=''
Check (!(Test-Route GET '/api/v1/hr/reports/exports/11111111-1111-1111-1111-111111111111' $null '')) 'no job before capture'
$text=[IO.File]::ReadAllText($path)
foreach($required in @("`$script:requests -le 60","Mode='Inspect'","Read-only packet changed; no writes allowed.",
    "R1 root exists; no reuse/reset.","Migration mismatch; no migration will run.",
    "Required current report grants absent; no provisioning.","Protected-Snapshot","Post-host SQL","Prior-Snapshot",
    "New-PrivateDirectory","CreateNoWindow=`$true","Environment.Clear()","hr-reports-r1",
    "AND State=0 AND Attempts=0","SourceLockReference","xl/worksheets/sheet2.xml",
    "metadata.totalCount -eq 1","Get-FileHash","Read-Protected",
    "Prior HR login challenges would change; stop before writes.",
    "Existing HR export found; no adoption or replay.",
    "AttendanceJobs=@{Enabled=`$false}",
    "state -ceq 'pending'","state -ceq 'completed'")){
    Check ($text.Contains($required)) ('retained runner safety contract '+$required)
}
Write-Output "PASS $checks R1 runner route/safety checks. No SQL, host, private-file or HTTP activity."
