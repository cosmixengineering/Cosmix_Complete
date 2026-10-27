[CmdletBinding()]
param([string]$RequestVectorsPath='')
$ErrorActionPreference='Stop'
$script:passed=0
function Test([bool]$condition,[string]$name){if(!$condition){throw $name};$script:passed++}
$path=Join-Path $PSScriptRoot 'Invoke-HrVehicleV2.ps1'
$tokens=$null;$errors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile($path,[ref]$tokens,[ref]$errors)
Test ($errors.Count -eq 0) 'V2 parser errors'
# Definitions only. Never dot-source the runner or execute preflight, SQL, host or private IO.
foreach($definition in $ast.FindAll({param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst]},$true)) {
    . ([scriptblock]::Create($definition.Extent.Text))
}
Import-V2Common $PSScriptRoot
$text=$ast.Extent.Text
Test ($text.Contains("[string]`$Mode = 'Inspect'")) 'Inspect default'
Test ($text.IndexOf("if(`$Mode -eq 'Inspect'){return}") -lt $text.IndexOf('    New-PrivateDirectory $root')) 'Inspect precedes writes'
Test ($text.Contains('$ApprovedPacketSha256 -ceq $packetHash')) 'Exact packet binding'
Test ($text.Contains('$start.Environment.Clear()') -and !$text.Contains('COSMIX_V2_SEED')) 'Single host and cleared process environment'
Test (!$text.Contains('Database.Migrate') -and !$text.Contains('ExecuteNonQuery') -and !$text.Contains('sqllocaldb') -and !$text.Contains('Remove-Item')) 'No migration, SQL writes, instance start or deletion'
Test ($text.IndexOf("Write-Result 'acceptance.json'") -gt $text.IndexOf("'Post-host immutable sources'")) 'Acceptance requires post-host preservation'
$profile=Get-V2Profile;$marker=$profile.Marker;$base=[uri]'https://localhost:7481/'
Test ($profile.Pid -eq 22112 -and $profile.Pipe -ceq 'np:\\.\pipe\LOCALDB#8C03C2BC\tsql\query' -and $profile.MaxHttp -eq 160 -and $profile.MaxMfa -eq 3) 'Pinned process/budgets'
Test (!$text.Contains('48AA6F9F') -and !$text.Contains('26564')) 'Stale target forbidden'
Test ((V2-Id 6) -ceq '26102602-0006-4000-8000-000000000006') 'Fixed parent fixture identity'
$script:requests=0;$script:businessRequests=0;$script:expectedRequest=$null
$script:loginEmails=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$script:serviceIds=[Collections.Generic.List[string]]::new()
$script:vehicleId=$null;$script:pendingServiceId=$null;$script:pendingServiceKey=$null;$script:correctionId=$null;$script:reconciliationId=$null
$httpResults=[Collections.Generic.List[object]]::new()
$fake=[pscustomobject]@{Sent=0;Last=$null}
$fake|Add-Member ScriptMethod SendAsync {
    param($message)
    $this.Sent++;$this.Last=$message
    $message.RequestUri=[uri]::new([uri]'https://localhost:7481/',$message.RequestUri)
    $response=[Net.Http.HttpResponseMessage]::new([Net.HttpStatusCode]::OK)
    $response.Content=[Net.Http.StringContent]::new('[]')
    return [Threading.Tasks.Task]::FromResult($response)
}
function Reject([scriptblock]$action,[string]$name='Unsafe request reached transport') {
    $before=$fake.Sent;$denied=$false
    try{& $action|Out-Null}catch{$denied=$true}
    Test ($denied -and $fake.Sent -eq $before) $name
}
foreach($method in @('GET','POST','PUT','DELETE','PATCH')) {
    foreach($route in @('/api/v1/hr/departments','/api/v1/hr/vehicles/policies','/api/v1/hr/vehicles/custody/issue','/api/v1/auth/web/refresh','https://remote.invalid/api/v1/hr/vehicles','/api/v1/local-testing/vehicle-v2/seed?extra=1')) {
        Reject {Begin-Request $fake $method $route @{}}
        Reject {V2-Expect $method $route @{}}
    }
}
$create=@{registrationNumber=$marker;vehicleType='Motorcycle';makeModel=$profile.Model;assetReference=$marker}
V2-Expect POST '/api/v1/hr/vehicles' $create ($marker+'-create')
Reject {Begin-Request $fake POST '/api/v1/hr/vehicles' $create '' ($marker+'-create')} 'Write without CSRF'
foreach($field in $create.Keys) {
    $changed=$create.Clone();$changed[$field]='UNAPPROVED'
    Reject {Begin-Request $fake POST '/api/v1/hr/vehicles' $changed 'csrf' ($marker+'-create')}
}
$pending=Begin-Request $fake POST '/api/v1/hr/vehicles' $create 'csrf' ($marker+'-create')
$response=Finish-Request $pending
Test ($response.Value -is [array] -and $response.Value.Count -eq 0) 'Empty JSON array preserved'
Reject {Begin-Request $fake POST '/api/v1/hr/vehicles' $create 'csrf' ($marker+'-create')} 'One armed request only'
$script:vehicleId='10000000-0000-4000-8000-000000000001'
V2-Expect POST '/api/v1/local-testing/vehicle-v2/seed' @{vehicleId=$script:vehicleId}
Reject {Begin-Request $fake POST '/api/v1/local-testing/vehicle-v2/seed' @{vehicleId=(V2-Id 6)} 'csrf'} 'Seed must use API-created ID'
Reject {V2-Expect POST '/api/v1/local-testing/vehicle-v2/seed' @{vehicleId=$script:vehicleId;extra=1}} 'No extra seed fields'
$null=Finish-Request (Begin-Request $fake POST '/api/v1/local-testing/vehicle-v2/seed' @{vehicleId=$script:vehicleId} 'csrf')
foreach($email in @('hr@example.invalid','admin@example.invalid','superadmin@example.invalid')) {
    $null=Finish-Request (Begin-Request $fake POST '/api/v1/auth/web/login' @{email=$email;password='synthetic'} 'csrf')
}
Reject {Begin-Request $fake POST '/api/v1/auth/web/login' @{email='hr@example.invalid';password='synthetic'} 'csrf'} 'Fourth login blocked'
Reject {Begin-Request $fake POST '/api/v1/auth/web/login' @{email='other@example.invalid';password='synthetic'} 'csrf'} 'Unapproved actor blocked'
$script:requests=160
Reject {Begin-Request $fake GET '/api/v1/me/permissions' $null} 'Request 161 blocked'
Assert-Snapshot @{a='hash'} @{a='hash'} 'test';$script:passed++
Reject {Assert-Snapshot @{a='hash'} @{a='changed'} 'test'} 'Changed hash blocked'
Reject {Assert-Snapshot @{a='hash'} @{a='hash';b='new'} 'test'} 'Changed evidence population blocked'
Assert-V2Amounts @{originalAmount=1000;correctedAmount=1500;differenceAmount=500;currency='PKR'};$script:passed++
Reject {Assert-V2Amounts @{originalAmount=1000;correctedAmount=1500;differenceAmount=0;currency='PKR'}} 'Wrong difference blocked'
foreach($body in @('{"title":"idempotency_key_reused","password":"SENTINEL"}','{"title":"SENTINEL","cookie":"SENTINEL"}','{"title":"vehicle_service_eligibility_changed","detail":"SENTINEL"}')) {
    $response=[Net.Http.HttpResponseMessage]::new([Net.HttpStatusCode]::Conflict);$response.Content=[Net.Http.StringContent]::new($body)
    $request=[Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::Post,'https://localhost:7481/api/v1/hr/vehicles')
    $null=Finish-Request @{Task=[Threading.Tasks.Task]::FromResult($response);Request=$request}
    Test (($httpResults|ConvertTo-Json) -notmatch 'SENTINEL|password|cookie') 'Response data leaked into evidence'
}
Test ($httpResults[-1].ErrorCode -ceq 'vehicle_service_eligibility_changed') 'Maintenance ProblemDetails code retained safely'

# Reproduce empty SQL scalar JSON followed by nonempty live tables, without a connection.
Test ((V2-MembershipJson '' Id) -ceq '[]') 'Empty SQL scalar membership normalized'
Test ((V2-MembershipJson $null Key) -ceq '[]') 'NULL SQL scalar membership normalized'
Reject {V2-MembershipJson '.' Id} 'Malformed membership rejected'
Reject {V2-MembershipJson '{}' Id} 'Non-array membership rejected'
Reject {V2-MembershipJson '[{"Id":"10000000-0000-4000-8000-000000000001"},{"Id":"10000000-0000-4000-8000-000000000001"}]' Id} 'Duplicate membership rejected'
$script:baselineIds=@{};$script:baselineRateKeys=$null;$script:snapshotPopulated=$false
$script:snapshotQueries=[Collections.Generic.List[string]]::new()
function Read-Sql($query) {
    $script:snapshotQueries.Add($query)
    if($query.Contains('s.name AS')){return '[{"schema":"hr","table":"Vehicles"},{"schema":"auth","table":"AuthRateBuckets"}]'}
    if($query.Contains('is_primary_key')){if($query.Contains('AuthRateBuckets')){return '[Key]'};return '[Id]'}
    if($query.StartsWith('SELECT (SELECT')){return ''}
    if($query.StartsWith('SELECT COUNT')){if($script:snapshotPopulated){return '1'};return '0'}
    if($query.Contains("OPENJSON(N'')")){throw 'JSON unexpected character at position 0.'}
    if($query.Contains('HASHBYTES')){return ''}
    throw 'Unexpected offline snapshot query.'
}
$emptySnapshot=V2-DatabaseSnapshot $true
Test ($script:baselineIds['hr.Vehicles'] -ceq '[]' -and $script:baselineRateKeys -ceq '[]') 'Captured empty memberships are valid JSON'
$script:snapshotPopulated=$true
$populatedSnapshot=V2-DatabaseSnapshot
Assert-V2Database $emptySnapshot $populatedSnapshot $true;$script:passed++
Test (@($script:snapshotQueries|Where-Object { $_.Contains("OPENJSON(N'[]')") }).Count -eq 4) 'Both snapshots use valid empty OPENJSON arrays'
$script:baselineIds=@{}
Reject {V2-DatabaseSnapshot} 'Missing membership must not recapture current rows'
$sqlCount=$script:snapshotQueries.Count
Reject {Invoke-V2ReadOnlyReconciliation @{Before=@{'hr.Vehicles#count'='1'}} @{'hr.Vehicles'='[]'} '[]'} 'Reconstruction requires original count'
Test ($script:snapshotQueries.Count -eq $sqlCount) 'Invalid reconstruction failed before SQL'
Test ($text.Contains('BaselineIds=$script:baselineIds;BaselineRateKeys=$script:baselineRateKeys')) 'Future start evidence retains memberships'

# Exercise the actual operational finally block; secondary failures must not replace the primary.
$operation=@($ast.EndBlock.Statements|Where-Object {$_ -is [Management.Automation.Language.TryStatementAst]})[-1]
$finalText=$operation.Finally.Extent.Text
$finalizer=[scriptblock]::Create($finalText.Substring(1,$finalText.Length-2))
$broken=[pscustomobject]@{};$broken|Add-Member ScriptMethod Dispose {throw 'secondary'}
$clients=@($broken);$script:preservationEvidence=$null
function Write-Result($name,$value){$script:preservationEvidence=@{Name=$name;Value=$value}}
$caught=$null
try {try {throw 'primary'}catch{$primaryFailure=$_;throw}finally{& $finalizer}}catch{$caught=$_.Exception.Message}
Test ($caught -ceq 'primary') 'Finalization masked original failure'
Test ($script:preservationEvidence.Name -ceq 'preservation-failure.json' -and !$script:preservationEvidence.Value.VerificationCompleted) 'Secondary failure explicitly records unverified preservation'
$primaryFailure=$null
Reject {& $finalizer} 'Finalization failure without a primary must fail'

$commonTokens=$null;$commonErrors=$null
$commonAst=[Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'Invoke-HrVehicleV1.ps1'),[ref]$commonTokens,[ref]$commonErrors)
$imports=@('Assert','Check','Read-Sql','Assert-PrivateSource','New-PrivateDirectory','Write-Private','Write-Json','Write-Result','New-Client','Request','Login','Assert-Snapshot')
foreach($definition in $commonAst.FindAll({param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst]},$true)|Where-Object Name -CIn $imports) {
    Test (!$definition.Extent.Text.Contains('baselineIds') -and !$definition.Extent.Text.Contains('baselineRateKeys') -and !$definition.Extent.Text.Contains('$script:before')) ('Imported helper baseline isolation: '+$definition.Name)
}

# Exercise the actual complete sequence against an in-memory transport.
function Check([string]$name,[bool]$condition){Test $condition $name}
function V2-SourceSnapshot{return @{immutable='synthetic-hash'}}
$script:requests=0;$script:businessRequests=0;$script:expectedRequest=$null;$script:serviceIds.Clear();$script:vehicleId=$null
$script:sequence=[Collections.Generic.List[object]]::new();$script:serviceNumber=0
$script:transport={
    param($message)
    $this.Sent++;$this.Last=$message
    $message.RequestUri=[uri]::new([uri]'https://localhost:7481/',$message.RequestUri)
    $route=$message.RequestUri.AbsolutePath
    $body=if($message.Content){$message.Content.ReadAsStringAsync().GetAwaiter().GetResult()|ConvertFrom-Json -AsHashtable -DateKind String}else{$null}
    $key=if($message.Headers.Contains('Idempotency-Key')){@($message.Headers.GetValues('Idempotency-Key'))[0]}else{''}
    $status=200
    $value=switch -Regex ($route) {
        '^/api/v1/hr/vehicles$' {@{id='10000000-0000-4000-8000-000000000001';registrationNumber=$marker;vehicleType='Motorcycle';assetReference=$marker};break}
        '/vehicle-v2/seed$' {@{vehicleId=$script:vehicleId;tripId=(V2-Id 6);fuelClaimId=(V2-Id 8);policyVersionIds=@((V2-Id 2),(V2-Id 3),(V2-Id 4));sourceRows=12;privateFiles=5};break}
        '^/api/v1/me/vehicles/maintenance$' {$script:serviceNumber++;@{id=('20000000-0000-4000-8000-'+$script:serviceNumber.ToString('000000000000'));vehicleId=$script:vehicleId;employeeId=$profile.HrEmployee;approvalState='Pending'};break}
        '/maintenance/[^/]+/approve$' {@{id=$script:pendingServiceId;approvalState='Approved'};break}
        '/attribution-correction-context$' {@{tripId=(V2-Id 6);employeeId=$profile.HrEmployee;mileageEntryId=(V2-Id 7);latestRequestId=$null;sourceFingerprint=('A'*64);tripVersion='AAAAAAAAAAE='};break}
        '/attribution-corrections$' {@{requestId='30000000-0000-4000-8000-000000000001';state='Pending';version='AAAAAAAAAAI='};break}
        '/attribution-corrections/[^/]+/review$' {@{requestId=$script:correctionId;state='Approved';decisionId='30000000-0000-4000-8000-000000000002';segments=@(@{eligibleKm=100},@{eligibleKm=100})};break}
        '/reconciliation-context$' {@{resourceId=(V2-Id 8);employeeId=$profile.HrEmployee;tripId=(V2-Id 6);correctionRequestId=$script:correctionId;latestRequestId=$null;sourceFingerprint=('B'*64);originalAmount=1000;correctedAmount=1500;differenceAmount=500;currency='PKR'};break}
        '/reconciliations$' {@{requestId='40000000-0000-4000-8000-000000000001';state='Pending';version='AAAAAAAAAAM='};break}
        '/reconciliations/[^/]+/review$' {@{requestId=$script:reconciliationId;state='Approved';decisionId='40000000-0000-4000-8000-000000000002';settlementState='NotRecorded';calculation=@{originalAmount=1000;correctedAmount=1500;differenceAmount=500;currency='PKR'}};break}
        default {throw 'Unexpected synthetic route.'}
    }
    if($key.EndsWith('-self')) {$status=403;$value=@{title='vehicle_self_decision_forbidden'}}
    elseif($key.EndsWith('-stale')) {$status=409;$value=@{title=$(if($key.EndsWith('-correction-stale')){'vehicle_attribution_trip_version_conflict'}else{'vehicle_adjustment_version_conflict'})}}
    elseif($body -and $body.reason -ceq 'TEST ONLY V2 changed payload') {$status=409;$value=@{title='idempotency_key_reused'}}
    $script:sequence.Add(@{method=$message.Method.Method;path=$route;key=$key;body=$body;actorAccountId=$this.ActorAccountId;expectedStatus=$status;response=$value})
    $response=[Net.Http.HttpResponseMessage]::new([Net.HttpStatusCode]$status)
    $response.Content=[Net.Http.StringContent]::new(($value|ConvertTo-Json -Depth 20 -Compress))
    return [Threading.Tasks.Task]::FromResult($response)
}
function New-FakeSession([string]$actor) {
    $client=[pscustomobject]@{Sent=0;Last=$null;ActorAccountId=$actor}
    $client|Add-Member ScriptMethod SendAsync $script:transport
    return @{Client=$client;Cookies=$client;Csrf='synthetic'}
}
function New-Client($cookies){return $cookies}
$hr=New-FakeSession $profile.HrAccount;$admin=New-FakeSession $profile.AdminAccount;$super=New-FakeSession $profile.SuperAccount
Invoke-V2Sequence $hr $admin $super
Test ($script:requests -eq 40 -and $script:businessRequests -eq 40) 'Bounded sequence count differs'
Test ($script:sequence[0].Path -ceq '/api/v1/hr/vehicles' -and $script:sequence[1].Path -ceq '/api/v1/hr/vehicles' -and $script:sequence[2].Path -ceq '/api/v1/local-testing/vehicle-v2/seed') 'Concurrent create must precede seed'
Test ($script:sequence[0].key -ceq $script:sequence[1].key -and (V2-Equal $script:sequence[0].body $script:sequence[1].body)) 'Concurrent create request identity'
Test ($script:serviceIds.Count -eq 8) 'Eight distinct services'
Test (@($script:sequence|Where-Object {$_.Path -ceq '/api/v1/me/vehicles/maintenance' -and $_.Body.serviceDate -cin @('2026-09-17','2026-09-18')}).Count -eq 4) 'Four pre-correction services'
Test (@($script:sequence|Where-Object {$_.Path -ceq '/api/v1/me/vehicles/maintenance' -and $_.Body.serviceDate -cin @('2026-09-19','2026-09-20')}).Count -eq 4) 'Four post-correction services'
Test (@($script:sequence|Where-Object {$_.Path -ceq '/api/v1/me/vehicles/maintenance' -and $_.Body.approvedOutstationKm -eq 200}).Count -eq 8) 'Fixture service distance is 200'
Test (@($script:sequence|Where-Object path -CEQ '/api/v1/me/vehicles/maintenance'|ForEach-Object {$_.body.evidenceReference}|Select-Object -Unique).Count -eq 8) 'Eight unique maintenance evidence references'
Test (@($script:sequence|Where-Object expectedStatus -EQ 409).Count -eq 5 -and @($script:sequence|Where-Object expectedStatus -EQ 403).Count -eq 2) 'Exactly seven denial probes'
Test (@($script:sequence|Where-Object {$_.key.EndsWith('-self') -and $_.actorAccountId -cne $profile.AdminAccount}).Count -eq 0) 'Self-review uses proposer account'
Test (@($script:sequence|Where-Object {$_.key.EndsWith('-review-stale') -and $_.actorAccountId -cne $profile.SuperAccount}).Count -eq 0) 'Stale review uses independent reviewer'
$before=@{'hr.VehicleServiceRecords'='hash';'hr.VehicleServiceRecords#count'='0';'hr.Employees'='hash';'hr.Employees#count'='3'}
Assert-V2Database $before @{'hr.VehicleServiceRecords'='hash';'hr.VehicleServiceRecords#count'='8';'hr.Employees'='hash';'hr.Employees#count'='3'};$script:passed++
Assert-V2Database $before @{'hr.VehicleServiceRecords'='hash';'hr.VehicleServiceRecords#count'='2';'hr.Employees'='hash';'hr.Employees#count'='3'} $true;$script:passed++
Reject {Assert-V2Database $before @{'hr.VehicleServiceRecords'='changed';'hr.VehicleServiceRecords#count'='2';'hr.Employees'='hash';'hr.Employees#count'='3'} $true} 'Failure path preserves original rows'
Reject {Assert-V2Database $before @{'hr.VehicleServiceRecords'='hash';'hr.VehicleServiceRecords#count'='9';'hr.Employees'='hash';'hr.Employees#count'='3'} $true} 'Partial execution cannot exceed approved rows'
Reject {Assert-V2Database $before @{'hr.VehicleServiceRecords'='hash';'hr.VehicleServiceRecords#count'='8';'hr.Employees'='hash';'hr.Employees#count'='4'}} 'Unapproved table growth blocked'
$script:sqlCalls=0
function Get-CimInstance {param($ClassName,$Filter) return $null}
function Read-Sql {param($query) $script:sqlCalls++;throw 'Offline SQL forbidden.'}
Reject {Assert-V2Target} 'Unavailable target must fail before SQL or writes'
Test ($script:sqlCalls -eq 0) 'Unavailable target issued SQL'
Assert-V2Space 1GB;$script:passed++
Reject {Assert-V2Space (1GB-1)} 'Insufficient free space blocked'
Reject {Assert-V2Space 0} 'Full drive blocked'
Test ($text.Contains('d.state_desc COLLATE DATABASE_DEFAULT') -and $text.Contains('d.user_access_desc COLLATE DATABASE_DEFAULT')) 'Catalog collation normalization'
$reconcileTokens=$null;$reconcileErrors=$null
$reconcileAst=[Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'Reconcile-HrVehicleV2ReadOnly.ps1'),[ref]$reconcileTokens,[ref]$reconcileErrors)
Test ($reconcileErrors.Count -eq 0) 'Standalone reconciliation parser'
$compareDefinition=@($reconcileAst.FindAll({param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst]},$true)|Where-Object Name -CEQ 'Compare-V2Reconciliation')[0]
. ([scriptblock]::Create($compareDefinition.Extent.Text))
$original=@{'auth.MobileOtpChallenges'='original';'auth.MobileOtpChallenges#count'='168';'hr.Employees'='same';'hr.Employees#count'='28'}
$observed=@{'auth.MobileOtpChallenges'='different';'auth.MobileOtpChallenges#count'='171';'hr.Employees'='same';'hr.Employees#count'='28'}
$comparison=Compare-V2Reconciliation $original $observed
Test (!$comparison.OriginalGuardPassed -and $comparison.Matched -eq 1 -and $comparison.Mismatched.Count -eq 1 -and $comparison.Mismatched[0] -ceq 'auth.MobileOtpChallenges') 'OTP mismatch is never excluded or accepted'
$observed['auth.MobileOtpChallenges']='original'
Test ((Compare-V2Reconciliation $original $observed).OriginalGuardPassed) 'Unchanged originals with bounded additions'
$observed['hr.Employees#count']='29'
Test (!(Compare-V2Reconciliation $original $observed).OriginalGuardPassed) 'Reconciliation rejects unrelated table growth'
$reconcileText=$reconcileAst.Extent.Text
Test ($reconcileText.Contains('[IO.FileMode]::CreateNew') -and $reconcileText.Contains('if(!$comparison.OriginalGuardPassed){exit 2}')) 'Mismatch report fails closed without overwriting'
Test (!$reconcileText.Contains('V2-DatabaseSnapshot $true') -and !$reconcileText.Contains('ExecuteNonQuery') -and !$reconcileText.Contains('c.Verifier') -and !$reconcileText.Contains('c.DeviceHash')) 'Reconciliation does not recapture baseline or export OTP secrets'
if($RequestVectorsPath) {
    $destination=[IO.Path]::GetFullPath($RequestVectorsPath)
    Test ($destination.StartsWith('D:\CosmixVerification\',[StringComparison]::OrdinalIgnoreCase) -and [IO.Path]::GetExtension($destination) -ceq '.json') 'Vectors must be a non-operational verification artifact'
    $bytes=[Text.UTF8Encoding]::new($false).GetBytes((ConvertTo-Json -InputObject $script:sequence.ToArray() -Depth 30))
    $stream=[IO.File]::Open($destination,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try{$stream.Write($bytes);$stream.Flush($true)}finally{$stream.Dispose()}
    Write-Output ('RequestVectors='+$destination)
    Write-Output ('RequestVectorsSha256='+(Get-FileHash -LiteralPath $destination).Hash)
}
Write-Output "PASS $script:passed V2 runner parser/allowlist/budgets/sequence/preservation/redaction checks; no HTTP, SQL, host or private IO."
