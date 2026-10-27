[CmdletBinding()]
param([string]$RequestVectorsPath='')
$ErrorActionPreference='Stop';$script:passed=0
function Test([bool]$condition,[string]$name){if(!$condition){throw $name};$script:passed++}
function Reject([scriptblock]$action,[string]$name){$blocked=$false;try{& $action|Out-Null}catch{$blocked=$true};Test $blocked $name}
$tokens=$null;$errors=$null;$path=Join-Path $PSScriptRoot 'Invoke-HrVehicleV2Continue01.ps1'
$ast=[Management.Automation.Language.Parser]::ParseFile($path,[ref]$tokens,[ref]$errors)
Test ($errors.Count -eq 0) 'Continuation parser'
foreach($d in $ast.FindAll({param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst]},$true)){. ([scriptblock]::Create($d.Extent.Text))}
Import-C01 $PSScriptRoot
$profile=Get-C01Profile;$marker=$profile.Marker;$base=[uri]'https://localhost:7481/'
& {
    # Exercise the actual connection lifetime and Read-Sql wrapper, with transport objects only faked.
    $pipe=$profile.Pipe
    function New-C01ReadConnection([string]$connectionString) {
        $script:connectionStringSeen=$connectionString
        return $fake
    }
    function New-FakeSqlConnection {
        $c=[pscustomobject]@{State='Closed';DataSource=$profile.Pipe;Database='Cosmix_Testing';ClientConnectionId=[guid]::NewGuid();Opens=0;Disposals=0;Commands=0;CommandDisposals=0;Readers=0;ReaderDisposals=0;UserQueries=0;FailOpen=$false;FailQuery=$false;Identity=@{Database='Cosmix_Testing';Server=$profile.Server;Pid=$profile.Pid;Login=$profile.Owner;Owner=$profile.Owner;Family=$profile.Family;State='ONLINE';Access='MULTI_USER';ReadOnly=0;Transport='Named pipe';Files='C:\Users\Huma.Khan\Cosmix_Testing.mdf|C:\Users\Huma.Khan\Cosmix_Testing_log.ldf';FileIssues=0;TransactionCount=0;TransactionState=0;Options=5432;SessionId=51}}
        $c|Add-Member ScriptMethod Open {$this.Opens++;if($this.FailOpen){throw 'Fake open timeout'};$this.State='Open'}
        $c|Add-Member ScriptMethod Dispose {$this.Disposals++;$this.State='Closed'}
        $c|Add-Member ScriptMethod CreateCommand {
            $this.Commands++
            $cmd=[pscustomobject]@{Connection=$this;CommandText='';CommandTimeout=0}
            $cmd|Add-Member ScriptMethod Dispose {$this.Connection.CommandDisposals++}
            $cmd|Add-Member ScriptMethod ExecuteReader {
                Test ($this.CommandTimeout -eq 20) 'Read-only command timeout remains twenty seconds'
                if($this.CommandText.Contains('@@TRANCOUNT')){$values=@(($this.Connection.Identity|ConvertTo-Json -Compress))}
                else {$this.Connection.UserQueries++;if($this.Connection.FailQuery){throw 'Fake query timeout'};$values=@('first','second')}
                $this.Connection.Readers++
                $r=[pscustomobject]@{Connection=$this.Connection;Values=$values;Index=-1;FieldCount=1}
                $r|Add-Member ScriptMethod Read {$this.Index++;return $this.Index -lt $this.Values.Count}
                $r|Add-Member ScriptMethod GetValue {param($index) return $this.Values[$this.Index]}
                $r|Add-Member ScriptMethod Dispose {$this.Connection.ReaderDisposals++}
                return $r
            }
            return $cmd
        }
        return $c
    }
    function Assert-FakeSqlDisposed {
        Test ($fake.Opens -eq 1 -and $fake.Disposals -eq 1) 'One attempted open and deterministic connection disposal'
        Test ($fake.Commands -eq $fake.CommandDisposals -and $fake.Readers -eq $fake.ReaderDisposals) 'All SQL commands and readers disposed'
    }
    $fake=New-FakeSqlConnection
    $value=Invoke-C01ReadScope {Read-Sql 'SELECT 1;';Read-Sql 'SELECT 2;';return 'Inspect returned'}
    Test ($value.Count -eq 3 -and $value[0] -ceq "first`nsecond" -and $value[2] -ceq 'Inspect returned') 'Shared wrapper preserves scalar/JSON chunk output and Inspect return'
    Assert-FakeSqlDisposed
    $builder=[System.Data.SqlClient.SqlConnectionStringBuilder]::new($script:connectionStringSeen)
    Test ($builder.DataSource -ceq $profile.Pipe -and $builder.InitialCatalog -ceq 'Cosmix_Testing' -and $builder.IntegratedSecurity -and $builder.ConnectTimeout -eq 5 -and !$builder.Pooling -and !$builder.Enlist -and $builder.ConnectRetryCount -eq 0 -and $builder.ApplicationIntent -eq 'ReadOnly') 'Exact pipe, five seconds, read intent, no pooling/enlistment/reconnect'
    $fake=New-FakeSqlConnection
    Reject {Invoke-C01ReadScope {$null=Read-Sql 'SELECT 1;';throw 'Fake preflight failure'}} 'Preflight failure propagated'
    Assert-FakeSqlDisposed
    $fake=New-FakeSqlConnection
    Invoke-C01ReadScope {$null=Read-Sql 'SELECT 1;';try {$null=Read-Sql 'SELECT 2;'} finally {$null=Read-Sql 'SELECT 3;'}}
    Test ($fake.UserQueries -eq 3) 'Execute completion and final preservation share one connection'
    Assert-FakeSqlDisposed
    $fake=New-FakeSqlConnection
    Invoke-C01ReadScope {
        $null=Read-Sql 'SELECT 1;'
        $commands=$fake.Commands
        $nested=@{Entered=$false}
        Reject {Invoke-C01ReadScope {$nested.Entered=$true}} 'Nested read-only lifetime rejected'
        Test (!$nested.Entered -and $fake.Commands -eq $commands -and $fake.Opens -eq 1 -and $fake.Disposals -eq 0) 'Nested action never runs and neither opens nor disposes the outer connection'
        $null=Read-Sql 'SELECT 2;'
    }
    Assert-FakeSqlDisposed
    foreach($state in @(0,1)) {
        $fake=New-FakeSqlConnection;$fake.Identity.TransactionState=$state
        $value=Invoke-C01ReadScope {Read-Sql 'SELECT 1;'}
        Test ($value -ceq "first`nsecond" -and $fake.UserQueries -eq 1) ('Autocommit accepted: count0/state'+$state+'/options5432')
        Assert-FakeSqlDisposed
    }
    foreach($failure in @('open','query','server','database','sqldatabase','family','owner','pid','files','transaction','transactionstate','implicit0','implicit1','closed','clientid','session')) {
        $fake=New-FakeSqlConnection
        if($failure -ceq 'open'){$fake.FailOpen=$true}
        elseif($failure -ceq 'query'){$fake.FailQuery=$true}
        elseif($failure -ceq 'server'){$fake.Identity.Server='wrong'}
        elseif($failure -ceq 'database'){$fake.Database='wrong'}
        elseif($failure -ceq 'sqldatabase'){$fake.Identity.Database='wrong'}
        elseif($failure -ceq 'family'){$fake.Identity.Family=[guid]::NewGuid().ToString()}
        elseif($failure -ceq 'owner'){$fake.Identity.Owner='wrong'}
        elseif($failure -ceq 'pid'){$fake.Identity.Pid=1}
        elseif($failure -ceq 'files'){$fake.Identity.Files='wrong'}
        elseif($failure -ceq 'transaction'){$fake.Identity.TransactionCount=1}
        elseif($failure -ceq 'transactionstate'){$fake.Identity.TransactionState=-1}
        elseif($failure -cin @('implicit0','implicit1')){$fake.Identity.Options=5434;$fake.Identity.TransactionState=[int]$failure.Substring(8)}
        Invoke-C01ReadScope {
            if($failure -cin @('closed','clientid','session')) {
                $null=Read-Sql 'SELECT 1;'
                if($failure -ceq 'closed'){$fake.State='Closed'}
                elseif($failure -ceq 'clientid'){$fake.ClientConnectionId=[guid]::NewGuid()}
                else {$fake.Identity.SessionId=52}
            }
            $priorQueries=$fake.UserQueries
            Reject {Read-Sql 'SELECT 2;'} ('Connection fault denied: '+$failure)
            Test ($fake.UserQueries -eq ($priorQueries+$(if($failure -ceq 'query'){1}else{0}))) 'Wrong target/state never reaches inspection query'
            $priorCommands=$fake.Commands
            Reject {Read-Sql 'SELECT 3;'} 'Faulted lifetime cannot reopen or retry'
            Test ($fake.Commands -eq $priorCommands) 'Faulted lifetime sends no further command'
        }
        Assert-FakeSqlDisposed
    }
    Reject {Read-Sql 'SELECT 1;'} 'No read outside owned connection lifetime'
}
$script:vehicleId=$profile.VehicleId;$script:requests=0;$script:businessRequests=0;$script:expectedRequest=$null
$script:pendingServiceId=$null;$script:pendingServiceKey=$null;$script:correctionId=$null;$script:reconciliationId=$null;$script:lateReplays=$false
$script:serviceIds=[Collections.Generic.List[string]]::new();$script:serviceReplays=@{}
$script:otpReads=[Collections.Generic.HashSet[string]]::new();$script:loginEmails=[Collections.Generic.HashSet[string]]::new()
$httpResults=[Collections.Generic.List[object]]::new();$script:sequence=[Collections.Generic.List[object]]::new();$script:cached=@{};$script:serviceNumber=0;$script:clientsCreated=0
function Check([string]$name,[bool]$condition){Test $condition $name}
function New-FakeClient($actor) {
    $script:clientsCreated++
    $c=[pscustomobject]@{Actor=$actor;ClientNumber=$script:clientsCreated}
    $c|Add-Member ScriptMethod SendAsync {
        param($message)
        $message.RequestUri=[uri]::new([uri]'https://localhost:7481/',$message.RequestUri);$route=$message.RequestUri.AbsolutePath
        $body=if($message.Content){$message.Content.ReadAsStringAsync().GetAwaiter().GetResult()|ConvertFrom-Json -AsHashtable -DateKind String}else{$null}
        $key=if($message.Headers.Contains('Idempotency-Key')){@($message.Headers.GetValues('Idempotency-Key'))[0]}else{''};$status=200
        if($key.EndsWith('-self')){$status=403;$value=@{title='vehicle_self_decision_forbidden'}}
        elseif($key.EndsWith('-stale')){$status=409;$value=@{title=$(if($key.EndsWith('-correction-stale')){'vehicle_attribution_trip_version_conflict'}else{'vehicle_adjustment_version_conflict'})}}
        elseif($body -and $body.reason -ceq 'TEST ONLY V2 changed payload'){$status=409;$value=@{title='idempotency_key_reused'}}
        elseif($key -and $script:cached.ContainsKey($key)){$value=$script:cached[$key]}
        else {
            $value=switch -Regex ($route) {
                '^/api/v1/me/vehicles/maintenance$' {$script:serviceNumber++;@{id=('20000000-0000-4000-8000-'+$script:serviceNumber.ToString('000000000000'));vehicleId=$profile.VehicleId;employeeId=$profile.HrEmployee;approvalState='Pending'};break}
                '/maintenance/[^/]+/approve$' {@{id=$script:pendingServiceId;approvalState='Approved'};break}
                '/attribution-correction-context$' {@{tripId=(V2-Id 6);employeeId=$profile.HrEmployee;mileageEntryId=(V2-Id 7);latestRequestId=$null;sourceFingerprint=('A'*64);tripVersion='AAAAAAAAAAE='};break}
                '/attribution-corrections$' {@{requestId='30000000-0000-4000-8000-000000000001';state='Pending';version='AAAAAAAAAAI='};break}
                '/attribution-corrections/[^/]+/review$' {@{requestId=$script:correctionId;state='Approved';decisionId='30000000-0000-4000-8000-000000000002';segments=@(@{eligibleKm=100},@{eligibleKm=100})};break}
                '/reconciliation-context$' {@{resourceId=(V2-Id 8);tripId=(V2-Id 6);employeeId=$profile.HrEmployee;correctionRequestId=$script:correctionId;latestRequestId=$null;sourceFingerprint=('B'*64);originalAmount=1000;correctedAmount=1500;differenceAmount=500;currency='PKR'};break}
                '/reconciliations$' {@{requestId='40000000-0000-4000-8000-000000000001';state='Pending';version='AAAAAAAAAAM='};break}
                '/reconciliations/[^/]+/review$' {@{requestId=$script:reconciliationId;state='Approved';decisionId='40000000-0000-4000-8000-000000000002';settlementState='NotRecorded';calculation=@{originalAmount=1000;correctedAmount=1500;differenceAmount=500;currency='PKR'}};break}
                default {throw 'Unexpected offline route.'}
            }
            if($key){$script:cached[$key]=$value}
        }
        $script:sequence.Add(@{method=$message.Method.Method;path=$route;key=$key;body=$body;actorAccountId=$this.Actor;expectedStatus=$status;response=$value;clientNumber=$this.ClientNumber})
        $response=[Net.Http.HttpResponseMessage]::new([Net.HttpStatusCode]$status);$response.Content=[Net.Http.StringContent]::new(($value|ConvertTo-Json -Depth 20 -Compress))
        return [Threading.Tasks.Task]::FromResult($response)
    }
    return $c
}
function New-Client($cookies){return New-FakeClient $cookies}
function New-FakeSession($actor){return @{Client=(New-FakeClient $actor);Cookies=$actor;Csrf='synthetic'}}
$hr=New-FakeSession $profile.HrAccount;$admin=New-FakeSession $profile.AdminAccount;$super=New-FakeSession $profile.SuperAccount
$script:cached[$marker+'-service-Tuning-2026-09-17']=@{id=$profile.PendingServiceId;vehicleId=$profile.VehicleId;employeeId=$profile.HrEmployee;approvalState='Pending'}
foreach($path in @('/api/v1/hr/vehicles','/api/v1/local-testing/vehicle-v2/seed','/api/v1/hr/vehicles/policies','/api/v1/auth/web/refresh')){Reject {V2-Expect POST $path @{}} ('Forbidden route '+$path)}
Invoke-C01Sequence $hr $admin $super
Test ($script:requests -eq 44 -and $script:businessRequests -eq 44) 'Finite 44-request business sequence'
Test ($script:sequence[0].clientNumber -ne $script:sequence[1].clientNumber -and $script:sequence[0].key -ceq $script:sequence[1].key) 'Pending approval uses separate connections with one key'
Test ($script:serviceNumber -eq 7 -and $script:serviceIds.Count -eq 8) 'Seven new services, eight approved'
Test (@($script:sequence|Where-Object expectedStatus -EQ 409).Count -eq 5 -and @($script:sequence|Where-Object expectedStatus -EQ 403).Count -eq 2) 'Seven original denials retained'
Test (@($script:sequence|Where-Object {$_.path -match '/review$' -and $_.expectedStatus -eq 200 -and $_.actorAccountId -cne $profile.SuperAccount}).Count -eq 0) 'Successful adjustment reviews use genuine SuperAdmin'
Test (@($script:sequence|Where-Object {$_.path.EndsWith('/reconciliation-context') -and $_.response.employeeId -eq $profile.HrEmployee}).Count -eq 1) 'Synthetic reconciliation context retains the real DTO employee binding'
foreach($v in $script:sequence|Select-Object -Last 8){Test ($v.key -match '-service-(Tuning|EngineOverhaul)-2026-09-(17|19)(-approve)?$') 'Final service replays follow all successors'}
$oldReplay=@($script:sequence|Where-Object key -CEQ ($marker+'-service-Tuning-2026-09-17'))
Test ($oldReplay.Count -eq 1 -and ($oldReplay[0].body|ConvertTo-Json -Compress) -match '"odometerKm":1200[,}]' -and ($oldReplay[0].body|ConvertTo-Json -Compress) -match '"claimedAmount":50[,}]' -and $oldReplay[0].response.id -eq $profile.PendingServiceId) 'Original numeric submission payload retained exactly'
$script:requests=100
Reject {Begin-Request $hr.Client GET '/api/v1/me/permissions' $null} 'Request101 blocked before transport'
Test ($profile.Name -ceq 'HumaHrVehicle20261026V2Continue01' -and $profile.MaxHttp -eq 100 -and $profile.CryptoVersion -ceq 'hr-v2-c01') 'Continuation profile identity'
$text=$ast.Extent.Text
Test ($text.IndexOf("if(`$Mode -ceq 'Inspect'){return}") -lt $text.IndexOf('    New-PrivateDirectory $root') -and $text.Contains('$ApprovedPacketSha256 -ceq $packetHash')) 'Inspect precedes provisioning and exact packet gates Execute'
Test (!$text.Contains('preparation incomplete') -and !$text.Contains('ExecuteNonQuery') -and !$text.Contains('Remove-Item') -and !$text.Contains('Database.Migrate')) 'Operational runner contains no scaffold, SQL writes, deletion or migration'
Test ($text.Contains('2026-10-26T19:00:00Z') -and $text.Contains('$start.Environment.Clear()') -and $text.Contains('$start.CreateNoWindow=$true')) 'Cutoff and isolated hidden owned host'
Test ($text.Contains('BaselineIds=$script:baselineIds') -and $text.Contains('BaselineRateKeys=$script:baselineRateKeys') -and $text.Contains('HeaderBefore=$headerBefore') -and $text.Contains('RateBefore=$rateBefore')) 'Future private evidence retains memberships and allowed transition values'
Test ($profile.ManifestSha256 -ceq 'F7E273D086484CD633B941C477F25F09F14281B861963E6908C99B0F41B98E95' -and $profile.PendingHeaderId -ceq '183f96a7-b507-410e-99ad-f9e832cc3ef3') 'Exact Hume manifest and pending header'
$script:requests=0;$script:loginEmails.Clear()
foreach($email in @('hr@example.invalid','admin@example.invalid','superadmin@example.invalid')){$null=$script:loginEmails.Add($email)}
$n=$script:sequence.Count
Reject {Begin-Request $hr.Client POST '/api/v1/auth/web/login' @{email='hr@example.invalid';password='synthetic'} 'csrf'} 'Fourth MFA login blocked'
Test ($script:sequence.Count -eq $n) 'Fourth login never reached transport'
$challenge='50000000-0000-4000-8000-000000000001';$null=$script:otpReads.Add($challenge)
Reject {Begin-Request $hr.Client POST '/api/v1/auth/web/local-testing/otp' @{challengeId=$challenge} 'csrf'} 'OTP challenge reread blocked'
$response=[Net.Http.HttpResponseMessage]::new([Net.HttpStatusCode]::NotFound);$response.Content=[Net.Http.StringContent]::new('{}')
$message=[Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::Post,'https://localhost:7481/api/v1/auth/web/local-testing/otp')
Reject {Finish-Request @{Task=[Threading.Tasks.Task]::FromResult($response);Request=$message}} 'Unexpected synchronous OTP status stops without polling'
$script:startedUtc=[DateTimeOffset]::UtcNow.AddMinutes(-2)
$header=@{State=0;ClosedAt=$null;RowVersion='0x0000000000000001'}
$approved=@{State=1;ClosedAt=[DateTimeOffset]::UtcNow.AddMinutes(-1).ToString('o');RowVersion='0x0000000000000002'}
Assert-C01HeaderTransition $header $approved $true;$script:passed++
Assert-C01HeaderTransition $header $header $false;$script:passed++
Reject {Assert-C01HeaderTransition $header $header $true} 'Complete run cannot retain pending header'
$bad=$approved.Clone();$bad.State=3
Reject {Assert-C01HeaderTransition $header $bad $false} 'Rejection is outside the single approved transition'
$bad=$approved.Clone();$bad.ClosedAt=$script:startedUtc.AddMinutes(-1).ToString('o')
Reject {Assert-C01HeaderTransition $header $bad $true} 'Backdated header close blocked'
$bad=$approved.Clone();$bad.RowVersion=$header.RowVersion
Reject {Assert-C01HeaderTransition $header $bad $true} 'Approved header requires actual rowversion transition'
$sample=@{'auth.MobileOtpChallenges'='otp';'auth.MobileOtpChallenges#count'='171';'workflow.ApprovalRequests'='protected';'workflow.ApprovalRequests#count'='21'}
$observed=@{'auth.MobileOtpChallenges'='otp';'auth.MobileOtpChallenges#count'='174';'workflow.ApprovalRequests'='protected';'workflow.ApprovalRequests#count'='30'}
Assert-C01Snapshot $sample $observed $true;$script:passed++
$bad=$observed.Clone();$bad['auth.MobileOtpChallenges']='changed'
Reject {Assert-C01Snapshot $sample $bad $false} 'Existing OTP changes blocked even on partial failure'
$bad=$observed.Clone();$bad['workflow.ApprovalRequests']='changed'
Reject {Assert-C01Snapshot $sample $bad $false} 'Other workflow headers and pinned immutable fields remain protected'
$bad=$observed.Clone();$bad['workflow.ApprovalRequests#count']='31'
Reject {Assert-C01Snapshot $sample $bad $false} 'Excess workflow rows blocked'
$oldRate=@{Key='web-request-global';WindowStartsAt=$script:startedUtc.AddMinutes(-10).ToString('o');WindowEndsAt=$script:startedUtc.AddMinutes(5).ToString('o');Count=3;RowVersion='v1'}
$newRate=$oldRate.Clone();$newRate.Count=4;$newRate.RowVersion='v2'
Assert-C01Rates @($oldRate) @($newRate);$script:passed++
$bad=$newRate.Clone();$bad.Count=2
Reject {Assert-C01Rates @($oldRate) @($bad)} 'Rate decrease in same window blocked'
$bad=$newRate.Clone();$bad.WindowEndsAt=$script:startedUtc.AddHours(1).ToString('o')
Reject {Assert-C01Rates @($oldRate) @($bad)} 'Rate window extension blocked'
Reject {Assert-C01Rates @($oldRate) @()} 'Old rate key deletion blocked'
$script:sqlQueries=[Collections.Generic.List[string]]::new();$script:openChallenges='1'
function Read-Sql($query) {
    $script:sqlQueries.Add($query)
    if($query.Contains('ConsumedAt IS NULL AND RevokedAt IS NULL')){return $script:openChallenges}
    if($query.Contains('FailedLoginCount')){return '0'}
    if($query.Contains('COUNT(DISTINCT p.Code)')){return '4'}
    if($query.Contains('JOIN auth.Roles')){return '1'}
    throw 'Unexpected offline SQL query.'
}
Reject {Assert-C01AuthPreflight} 'Any old open WebLogin challenge refuses execution'
Test ($script:sqlQueries.Count -eq 1) 'Open challenge stops before further preflight'
$script:openChallenges='0';Assert-C01AuthPreflight;$script:passed++
foreach($pin in @('1fcf6ecb-66ec-4da5-9cc2-e12d3d337ce3','5e351971-9e17-4b1b-81c4-efed06c33701','35b58f56-c750-4f63-84f2-053e179ed88a')){Test (($script:sqlQueries -join "`n").Contains($pin)) 'Exact main assignment pinned without rejecting other HR assignments'}
Assert-Snapshot (C01-MemberMap @(@{Key=@{Id='AAAAAAAA-0000-4000-8000-000000000001'};Sha256=('A'*64)})) (C01-MemberMap @(@{Key=@{Id='aaaaaaaa-0000-4000-8000-000000000001'};Sha256=('A'*64)})) 'GUID casing normalization';$script:passed++
Reject {C01-MemberMap @(@{Key=@{Id='x'};Sha256=('A'*64)},@{Key=@{Id='x'};Sha256=('A'*64)})} 'Duplicate manifest membership blocked'
Test ((C01-Rows '').Count -eq 0 -and (C01-Rows '[]').Count -eq 0) 'Empty SQL JSON normalized without fabricated rows'
& {
    # Mock the filesystem in memory; never change original evidence to test the guard.
    $file=[pscustomobject]@{FullName=(Join-Path $profile.SourceRoot 'test.enc');PSIsContainer=$false;Attributes=[IO.FileAttributes]::Archive;Length=3}
    $empty=[pscustomobject]@{FullName=(Join-Path $profile.SourceRoot 'empty');PSIsContainer=$true;Attributes=[IO.FileAttributes]::Directory}
    $entries=@($file,$empty);$hash='A'*64;$script:inventorySqlCalls=0;$script:inventoryWrites=0
    $expected=@{'test.enc'=@{Directory=$false;Attributes=32;Length=3;Sha256=$hash};empty=@{Directory=$true;Attributes=16}}
    $manifest=@{OldRootInventory=$expected}
    function Get-ChildItem {param($LiteralPath,[switch]$Recurse,[switch]$Force) Assert ($LiteralPath -ceq $profile.SourceRoot -and $Recurse -and $Force) 'Unexpected inventory traversal.';return $entries}
    function Get-FileHash {param($LiteralPath) return @{Hash=$hash}}
    function Assert-PrivateSource {param($path,$allowEmpty)}
    function Read-Sql {param($query) $script:inventorySqlCalls++;throw 'SQL forbidden in inventory regression.'}
    function Write-Result {param($name,$value) $script:inventoryWrites++;throw 'Private writes forbidden in inventory regression.'}
    Assert-C01OldInventory $expected;$script:passed++
    $file.Attributes=[IO.FileAttributes]::Archive -bor [IO.FileAttributes]::ReadOnly
    Reject {Assert-C01OldInventory $expected} 'Changed old file attributes fail despite identical contents'
    Reject {Assert-C01Preservation $true} 'Successful-run postflight rejects changed old attributes'
    $file.Attributes=[IO.FileAttributes]::Archive
    $empty.Attributes=[IO.FileAttributes]::Directory -bor [IO.FileAttributes]::Hidden
    Reject {Assert-C01OldInventory $expected} 'Changed old directory attributes fail'
    $empty.Attributes=[IO.FileAttributes]::Directory
    $entries=@($file)
    Reject {Assert-C01OldInventory $expected} 'Removed empty old directory fails with unchanged file hashes'
    Reject {Assert-C01Preservation $false} 'Partial-run postflight rejects removed empty directory'
    $entries=@($file,$empty,[pscustomobject]@{FullName=(Join-Path $profile.SourceRoot 'extra');PSIsContainer=$true;Attributes=[IO.FileAttributes]::Directory})
    Reject {Assert-C01OldInventory $expected} 'Added empty old directory fails'
    $entries=@($file,$empty);$hash='B'*64
    Reject {Assert-C01OldInventory $expected} 'Changed old file contents still fail'
    Test ($script:inventorySqlCalls -eq 0 -and $script:inventoryWrites -eq 0) 'Inventory failures stop postflight before SQL or evidence writes'
}
$manifestDefinition=@($ast.FindAll({param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst]},$true)|Where-Object Name -CEQ 'Assert-C01Manifest')[0]
Test ($manifestDefinition.Extent.Text.Contains('Assert-C01OldInventory $manifest.OldRootInventory')) 'Preflight and postflight share the exact manifest inventory guard'
if($RequestVectorsPath) {
    $destination=[IO.Path]::GetFullPath($RequestVectorsPath)
    Test ([IO.Path]::GetDirectoryName($destination) -ieq 'D:\CosmixVerification' -and [IO.Path]::GetExtension($destination) -ceq '.json') 'Offline vectors artifact path'
    $bytes=[Text.UTF8Encoding]::new($false).GetBytes((ConvertTo-Json -InputObject $script:sequence.ToArray() -Depth 30))
    $stream=[IO.File]::Open($destination,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try{$stream.Write($bytes);$stream.Flush($true)}finally{$stream.Dispose()}
    Write-Output ('RequestVectors='+$destination);Write-Output ('RequestVectorsSha256='+(Get-FileHash -LiteralPath $destination).Hash)
}
Write-Output "PASS $script:passed continuation offline checks; no HTTP, SQL, host or private IO."
