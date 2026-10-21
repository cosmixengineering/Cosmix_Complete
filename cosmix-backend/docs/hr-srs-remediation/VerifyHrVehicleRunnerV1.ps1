[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$count=0
function Test([bool]$condition,[string]$name) {if(!$condition){throw $name};$script:count++}
$runnerPath=Join-Path $PSScriptRoot 'Invoke-HrVehicleV1.ps1'
$helperPath=Join-Path $PSScriptRoot 'HrVehicleV1RunnerFunctions.ps1'
foreach($path in @($runnerPath,$helperPath)) {
    $tokens=$null;$errors=$null
    $ast=[Management.Automation.Language.Parser]::ParseFile($path,[ref]$tokens,[ref]$errors)
    Test ($errors.Count -eq 0) ('Parser: '+[IO.Path]::GetFileName($path))
    foreach($definition in $ast.FindAll({param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst]},$true)) {
        if($path -eq $helperPath -or $definition.Name -cin @('Assert','Begin-Request','Finish-Request','Request','Assert-Snapshot')) {
            . ([scriptblock]::Create($definition.Extent.Text))
        }
    }
    if($path -eq $runnerPath) {$runnerAst=$ast}
}
# Only extracted definitions execute; this verifier never invokes the runner's operational top level.
$runnerText=$runnerAst.Extent.Text
Test ($runnerText.Contains("[string]`$Mode = 'Inspect'")) 'Default must remain Inspect'
Test ($runnerText.IndexOf("if(`$Mode -eq 'Inspect') {return}") -lt $runnerText.IndexOf('    New-PrivateDirectory $root')) 'Inspect mutation gate'
Test ($runnerText.Contains('$ApprovedPacketSha256 -ceq $packetHash')) 'Exact packet gate'
Test ($runnerText.Contains("Huma20261019C04'") -and $runnerText.Contains('Post-host C01-C04 evidence')) 'C04 preservation'
Test (!$runnerText.Contains('Invoke-HrBusinessC04.ps1')) 'Do not invoke C04'
Test (!$runnerText.Contains('dotnet build') -and !$runnerText.Contains('Database.Migrate')) 'No product build/migrate'
$script:fixtureText=@'
{"profile":"HumaHrVehicle20261020V1","bindingSource":"parent-read-only-inspection-20261020","adminAccountId":"94a83b62-5b56-4450-9175-2a7a9e3261bf","adminEmployeeId":"7c505086-87ea-4fd2-980e-823ecccbe36a","hrAccountId":"8475187f-bf3b-4f9c-a3ed-4bac32253005","hrEmployeeId":"35d19922-70a8-4c80-8578-e1029360e9fc","issuedAt":"2026-10-20T00:00:00Z","returnedAt":"2026-10-20T00:01:00Z"}
'@
function Get-Content {param($LiteralPath,[switch]$Raw) if($LiteralPath -cne 'synthetic-memory-manifest'){throw 'No filesystem reads in manifest test.'};return $script:fixtureText}
Initialize-V1Manifest 'synthetic-memory-manifest'
Test ($issuedAt -is [string] -and $hrEmployee -ceq '35d19922-70a8-4c80-8578-e1029360e9fc') 'Manifest parsed exact bindings and strings'
$good=$script:fixtureText
foreach($bad in @(
    $good.Replace('35d19922-70a8-4c80-8578-e1029360e9fc','35d19922-70a8-4c80-8578-e1029360e9fd'),
    $good.Replace('"issuedAt":"2026-10-20T00:00:00Z"','"issuedAt":null'),
    $good.Replace('00:01:00Z','00:00:00Z'),
    ($good.TrimEnd().Substring(0,$good.TrimEnd().Length-1)+',"issuedAt":"2026-10-20T00:00:00Z"}'))) {
    $script:fixtureText=$bad;$rejected=$false
    try{Initialize-V1Manifest 'synthetic-memory-manifest'}catch{$rejected=$true}
    Test $rejected 'Changed/duplicate/undated manifest rejected'
}
$script:fixtureText=$good;Initialize-V1Manifest 'synthetic-memory-manifest'
$marker='TEST-HR-V1-20261020'
$base=[uri]'https://localhost:7481/'
$script:requests=0;$script:businessRequests=0
$script:vehicleId=[guid]::Empty.ToString();$script:custodyId=[guid]::Empty.ToString()
$httpResults=[Collections.Generic.List[object]]::new()
$fake=[pscustomobject]@{Sent=0;Last=$null}
$fake | Add-Member ScriptMethod SendAsync {
    param($message)
    $this.Sent++;$this.Last=$message
    $message.RequestUri=[uri]::new([uri]'https://localhost:7481/',$message.RequestUri)
    $response=[Net.Http.HttpResponseMessage]::new([Net.HttpStatusCode]::OK)
    $response.Content=[Net.Http.StringContent]::new('[]')
    return [Threading.Tasks.Task]::FromResult($response)
}
function Reject([scriptblock]$action) {
    $before=$fake.Sent;$rejected=$false
    try{& $action}catch{$rejected=$true}
    Test ($rejected -and $fake.Sent -eq $before) 'Disallowed request reached transport'
}
function Allow([string]$method,[string]$path,$body=$null,[string]$key='') {
    $before=$fake.Sent
    $pending=Begin-Request $fake $method $path $body 'synthetic-csrf' $key
    Test ($fake.Sent -eq $before+1 -and $fake.Last.Headers.GetValues('X-CSRF-TOKEN') -ceq 'synthetic-csrf') 'Allowed request/CSRF changed'
    $result=Finish-Request $pending
    Test ($result.Value -is [array] -and $result.Value.Count -eq 0) 'Empty JSON array lost'
}
foreach($method in @('GET','POST','PUT','DELETE','PATCH')) {
    foreach($path in @('/api/v1/hr/departments','/api/v1/hr/vehicles/policies','/api/v1/me/vehicles/fuel-claims','/api/v1/auth/web/refresh','https://foreign.invalid/api/v1/hr/vehicles')) {
        Reject {Begin-Request $fake $method $path @{}}
    }
}
$create=@{registrationNumber=$marker;vehicleType='Motorcycle';makeModel='TEST ONLY custody acceptance';assetReference=$marker}
Allow POST '/api/v1/hr/vehicles' $create "$marker-create"
foreach($mutation in @(@{registrationNumber='OTHER'},@{vehicleType='Car'},@{assetReference='OTHER'},@{makeModel='TEST ONLY custody changed payload'},@{extra='x'})) {
    $body=$create.Clone();foreach($k in $mutation.Keys){$body[$k]=$mutation[$k]}
    Reject {Begin-Request $fake POST '/api/v1/hr/vehicles' $body 'csrf' "$marker-create"}
}
Reject {Begin-Request $fake POST '/api/v1/hr/vehicles' $create 'csrf' "$marker-other"}
Reject {Begin-Request $fake GET "/api/v1/hr/vehicles/custody/history?employeeId=$adminEmployee" $null}
Allow GET "/api/v1/hr/vehicles/custody/history?employeeId=$hrEmployee"
$script:vehicleId='11111111-1111-1111-1111-111111111111'
$script:custodyId='22222222-2222-2222-2222-222222222222'
$issue=@{vehicleId=$script:vehicleId;employeeId=$hrEmployee;issuedAt=$issuedAt;issueOdometerKm=1000;issueConditionReference=$issueReference}
Allow POST '/api/v1/hr/vehicles/custody/issue' $issue "$marker-issue"
foreach($mutation in @(@{vehicleId=$script:custodyId},@{employeeId=$adminEmployee},@{issueOdometerKm=1001},@{issuedAt=$returnedAt},@{extra='x'})) {
    $body=$issue.Clone();foreach($k in $mutation.Keys){$body[$k]=$mutation[$k]}
    Reject {Begin-Request $fake POST '/api/v1/hr/vehicles/custody/issue' $body 'csrf' "$marker-issue"}
}
$return=@{returnedAt=$returnedAt;returnOdometerKm=1000;returnConditionReference=$returnReference}
Allow POST "/api/v1/hr/vehicles/custody/$script:custodyId/return" $return "$marker-return"
Reject {Begin-Request $fake POST "/api/v1/hr/vehicles/custody/$script:vehicleId/return" $return 'csrf' "$marker-return"}
$script:businessRequests=18
Reject {Begin-Request $fake GET '/api/v1/hr/vehicles' $null}
$script:requests=100
Reject {Begin-Request $fake GET '/api/v1/me/permissions' $null}
Assert-Snapshot @{a='same'} @{a='same'} 'synthetic';$count++
Reject {Assert-Snapshot @{a='same'} @{a='changed'} 'synthetic'}
Reject {Assert-Snapshot @{a='same'} @{a='same';b='extra'} 'synthetic'}
foreach($case in @(@{Body='{"title":"idempotency_key_reused","password":"SENTINEL"}';Code='idempotency_key_reused'},@{Body='{"title":"SENTINEL","cookie":"SENTINEL"}';Code=$null})) {
    $response=[Net.Http.HttpResponseMessage]::new([Net.HttpStatusCode]::Conflict)
    $response.Content=[Net.Http.StringContent]::new($case.Body)
    $request=[Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::Post,'https://localhost:7481/api/v1/hr/vehicles')
    $null=Finish-Request @{Task=[Threading.Tasks.Task]::FromResult($response);Request=$request}
    Test ($httpResults[-1].ErrorCode -ceq $case.Code -and ($httpResults|ConvertTo-Json) -notmatch 'SENTINEL|password|cookie') 'Evidence leaked response data'
}
# Drive the actual business sequence through queued in-memory HTTP responses.
$vehicleResult=@{id=$script:vehicleId;registrationNumber=$marker;vehicleType='Motorcycle';makeModel='TEST ONLY custody acceptance';assetReference=$marker;isActive=$true}
$custodyResult=@{id=$script:custodyId;vehicleId=$script:vehicleId;employeeId=$hrEmployee;registrationNumber=$marker;issuedAt=$issuedAt;issueOdometerKm=1000;issueConditionReference=$issueReference;returnedAt=$null;returnOdometerKm=$null;returnConditionReference=$null;isActive=$true}
$returnResult=$custodyResult.Clone();$returnResult.returnedAt=$returnedAt;$returnResult.returnOdometerKm=1000;$returnResult.returnConditionReference=$returnReference;$returnResult.isActive=$false
$script:queue=[Collections.Generic.Queue[object]]::new()
foreach($item in @(
    @{Status=200;Body='[]'},@{Status=200;Body='[]'},@{Status=400;Body='{}'},
    @{Status=200;Body=($vehicleResult|ConvertTo-Json -Compress)},@{Status=200;Body=($vehicleResult|ConvertTo-Json -Compress)},
    @{Status=409;Body='{"title":"idempotency_key_reused"}'},@{Status=200;Body='[]'},
    @{Status=200;Body=($custodyResult|ConvertTo-Json -Compress)},@{Status=200;Body=($custodyResult|ConvertTo-Json -Compress)},
    @{Status=409;Body='{"title":"vehicle_already_in_custody"}'},
    @{Status=200;Body=(ConvertTo-Json -InputObject @($custodyResult) -Compress)},@{Status=200;Body='[]'},
    @{Status=200;Body=($returnResult|ConvertTo-Json -Compress)},@{Status=200;Body=($returnResult|ConvertTo-Json -Compress)},
    @{Status=409;Body='{"title":"custody_already_returned"}'},
    @{Status=200;Body=(ConvertTo-Json -InputObject @($returnResult) -Compress)},
    @{Status=200;Body=(ConvertTo-Json -InputObject @($returnResult) -Compress)},
    @{Status=200;Body=(ConvertTo-Json -InputObject @($vehicleResult) -Compress)}
)) {$script:queue.Enqueue($item)}
$fake | Add-Member -Force ScriptMethod SendAsync {
    param($message)
    $this.Sent++;$this.Last=$message
    $message.RequestUri=[uri]::new([uri]'https://localhost:7481/',$message.RequestUri)
    $item=$script:queue.Dequeue()
    $response=[Net.Http.HttpResponseMessage]::new([Net.HttpStatusCode]$item.Status)
    $response.Content=[Net.Http.StringContent]::new($item.Body)
    return [Threading.Tasks.Task]::FromResult($response)
}
function New-Client {param($cookies) return $fake}
function Assert-V1Bindings {$script:count++}
function Read-Sql {param($query) if($query -notlike 'SELECT COUNT(*) FROM hr.VehicleCustodies WHERE VehicleId=*'){throw 'Unexpected synthetic query.'};return '0'}
function Check {param($name,$condition) Test $condition $name}
$script:requests=0;$script:businessRequests=0;$script:vehicleId=[guid]::Empty.ToString();$script:custodyId=[guid]::Empty.ToString()
$session=@{Client=$fake;Cookies='synthetic';Csrf='synthetic'}
Invoke-V1BusinessSequence $session $session
Test ($script:queue.Count -eq 0 -and $script:requests -eq 18 -and $script:businessRequests -eq 18) 'Actual sequence must make exactly 18 business requests'
Write-Output "PASS $count V1 runner parser/manifest/allowlist/budget/sequence/array/redaction/preservation checks; no HTTP, SQL, host or private IO."
