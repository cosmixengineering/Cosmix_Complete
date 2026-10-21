$ErrorActionPreference='Stop'
$helper=Join-Path $PSScriptRoot 'HrPermissionP1Profile.ps1'
if(!(Test-Path -LiteralPath $helper)){throw 'P1 pure guard implementation missing (expected initial red test).'}
. $helper
$script:checks=0
function Pass([bool]$value,[string]$name){if(!$value){throw $name};$script:checks++}
function Reject([scriptblock]$action,[string]$name){
    $failed=$false
    try {& $action | Out-Null} catch {$failed=$true}
    Pass $failed $name
}
$p=Get-P1Profile
$o=@{
    Server=$p.Server;Database='Cosmix_Testing';Owner=$p.Owner;Operator=$p.Owner;ProcessId=25768
    Family=$p.Family;State='ONLINE';Access='MULTI_USER';ReadOnly=$false
    Files=@($p.DataFile,$p.LogFile);Transport='Named pipe';Encryption='FALSE'
    Triggers=0;Synonyms=0;ExternalDependencies=0;RootExists=$false;PortOccupied=$false
    Root=$p.Root;Url=$p.Url;LinkedPath=$false;AccountCount=3;MarkerCount=0;MissingPermissionCount=5
}
Assert-P1Target $o
Pass $true 'exact target'
$retryTarget=$o.Clone()
$retryTarget.Root='C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261020P1Retry01'
Assert-P1Target $retryTarget
Pass $true 'exact approved retry root accepted'
$oldTarget=$o.Clone()
$oldTarget.Root='C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261020P1'
Reject {Assert-P1Target $oldTarget} 'original P1 root denied for retry'
foreach($field in @('Server','Database','Owner','Operator','Family','State','Access','Root','Url','Transport','Encryption')){
    $copy=$o.Clone();$copy[$field]='wrong'
    Reject {Assert-P1Target $copy} "wrong $field"
}
foreach($field in @('ReadOnly','RootExists','PortOccupied','LinkedPath')){
    $copy=$o.Clone();$copy[$field]=$true
    Reject {Assert-P1Target $copy} "wrong $field"
}
foreach($field in @('ProcessId','Triggers','Synonyms','ExternalDependencies','AccountCount','MarkerCount','MissingPermissionCount')){
    $copy=$o.Clone();$copy[$field]=999
    Reject {Assert-P1Target $copy} "wrong $field"
}
$copy=$o.Clone();$copy.Files=@($p.DataFile,'C:\wrong.ldf')
Reject {Assert-P1Target $copy} 'wrong database file'
$state=@{Requests=0;Logins=0}
1..100|ForEach-Object {Use-P1Budget $state $false}
Reject {Use-P1Budget $state $false} '101st request blocked'
$state=@{Requests=0;Logins=0}
1..5|ForEach-Object {Use-P1Budget $state $true}
Reject {Use-P1Budget $state $true} 'sixth login blocked'
$from=[datetimeoffset]'2026-10-20T12:00:00Z'
$bindings=@{
    AdminAccount=$p.AdminAccount;HrAccount=$p.HrAccount;HrEmployee=$p.HrEmployee
    AdminRole='11111111-1111-1111-1111-111111111111'
    RequesterRole='22222222-2222-2222-2222-222222222222'
    EffectiveFrom=$from.ToString('o');EffectiveTo=$from.AddHours(24).ToString('o')
}
$commands=Get-P1Commands $bindings 'AAAAAAAAAAA=' 'AAAAAAAAAAA=' 'AAAAAAAAAAA=' 'AAAAAAAAAAA='
Pass ($commands.Count -eq 6) 'six commands only'
foreach($c in $commands){
    Pass (Test-P1Command $c.Path $c.Body $c.Key $bindings) "exact $($c.Key)"
    Reject {Assert-P1Command $c.Path $c.Body 'unapproved-key' $bindings} 'wrong key'
    $bad=$c.Body.Clone();$bad.extra='unexpected'
    Reject {Assert-P1Command $c.Path $bad $c.Key $bindings} 'unknown field'
}
$assign=$commands[4]
foreach($hours in @(0,23,25)){
    $bad=$assign.Body.Clone();$bad.effectiveTo=$from.AddHours($hours).ToString('o')
    Reject {Assert-P1Command $assign.Path $bad $assign.Key $bindings} 'wrong expiry'
}
$bad=$commands[5].Body.Clone();$bad.scopes=@(@{kind=7;targetId=$null})
Reject {Assert-P1Command $commands[5].Path $bad $commands[5].Key $bindings} 'requester All denied'
$bad=$commands[5].Body.Clone();$bad.scopes=@(@{kind=2;targetId=$p.AdminAccount})
Reject {Assert-P1Command $commands[5].Path $bad $commands[5].Key $bindings} 'foreign employee denied'
$bad=$commands[2].Body.Clone();$bad.permissions=@('access.accounts.assign')
Reject {Assert-P1Command $commands[2].Path $bad $commands[2].Key $bindings} 'access delegation denied'
Reject {Assert-P1Command '/api/v1/hr/departments' @{} 'P1-create-admin' $bindings} 'business writes denied'
$expected=@(1..25|ForEach-Object {"migration-$_"})
Assert-P1Schema $expected $expected
Reject {Assert-P1Schema $expected @($expected[0..23])} 'missing migration'
$bad=$expected.Clone();$bad[0]='unknown'
Reject {Assert-P1Schema $expected $bad} 'changed migration'
foreach($method in @('PUT','PATCH','DELETE')){
    Pass (!(Test-P1Route $method '/api/v1/access/roles' $commands[0].Body $commands[0].Key $bindings)) 'unapproved verb'
}
foreach($path in @('/api/v1/hr/leaves','/api/v1/hr/deployments','/api/v1/jobs/dispatch',
    '/api/v1/access/roles?all=true','https://other.example/api/v1/access/roles','/api/v1/auth/web/refresh',
    "/api/v1/access/accounts/$($p.AdminAccount)/assignments/other")){
    Pass (!(Test-P1Route 'POST' $path @{} '' $bindings)) "forbidden route $path"
}
Pass (Test-P1Route GET '/api/v1/me/permissions' $null '' $bindings) 'permissions read'
Pass (!(Test-P1Route GET '/api/v1/me/permissions?all=true' $null '' $bindings)) 'query denied'
Pass (!(Test-P1Route GET '/api/v1/me/permissions' @{} '' $bindings)) 'GET body denied'
Pass (!(Test-P1Route POST '/api/v1/auth/web/login' @{email='real@example.com';password='offline'} '' $bindings)) 'foreign login denied'
$actors=@(
    @{Id=$p.AdminAccount;EmployeeId=$p.AdminEmployee;LoginName='admin@example.invalid';FullLegalName='Engr. Tariq Mehmood (Site Supervisor)';ApprovedLoginPhoneNumber='+12025550102';Status=3;WebAccessEnabled=$true;MfaRequired=$true;Unlocked=$true},
    @{Id=$p.HrAccount;EmployeeId=$p.HrEmployee;LoginName='hr@example.invalid';FullLegalName='Muhammad Rashid (Lead Engineer)';ApprovedLoginPhoneNumber='+12025550103';Status=3;WebAccessEnabled=$true;MfaRequired=$true;Unlocked=$true},
    @{Id='33333333-3333-3333-3333-333333333333';EmployeeId='44444444-4444-4444-4444-444444444444';LoginName='superadmin@example.invalid';FullLegalName='Engr. Imran Saeed (Project Director)';ApprovedLoginPhoneNumber='+12025550101';Status=3;WebAccessEnabled=$true;MfaRequired=$true;Unlocked=$true}
)
Assert-P1Actors $actors
Pass $true 'three synthetic actor bindings including SELECT-derived SA'
$upperActors=@($actors[0].Clone(),$actors[1].Clone(),$actors[2].Clone())
foreach($actor in $upperActors){
    $actor.Id=$actor.Id.ToUpperInvariant()
    $actor.EmployeeId=$actor.EmployeeId.ToUpperInvariant()
}
Assert-P1Actors $upperActors
Pass $true 'uppercase SQL GUIDs preserve exact identity'
foreach($index in @(0,1)){
    foreach($field in @('Id','EmployeeId')){
        $badActors=@($upperActors[0].Clone(),$upperActors[1].Clone(),$upperActors[2].Clone())
        $badActors[$index][$field]='99999999-9999-9999-9999-999999999999'
        Reject {Assert-P1Actors $badActors} "wrong typed GUID $index $field"
    }
}
foreach($field in @('LoginName','FullLegalName','ApprovedLoginPhoneNumber')){
    $badActors=@($actors[0].Clone(),$actors[1].Clone(),$actors[2].Clone());$badActors[2][$field]='foreign'
    Reject {Assert-P1Actors $badActors} "foreign SA $field"
}
foreach($field in @('MfaRequired','WebAccessEnabled','Unlocked')){
    $badActors=@($actors[0].Clone(),$actors[1].Clone(),$actors[2].Clone());$badActors[2][$field]=$false
    Reject {Assert-P1Actors $badActors} "SA $field required"
}
$badActors=@($actors[0].Clone(),$actors[1].Clone(),$actors[2].Clone());$badActors[2].Id=$p.AdminAccount
Reject {Assert-P1Actors $badActors} 'SA cannot reuse Admin binding'
Reject {Assert-P1Actors @($actors[0],$actors[1])} 'missing actor'

# Exercise the executor's actual six-command functions against an in-memory HTTP substitute.
# Its top-level preflight, Request implementation and live IO helpers are never evaluated.
$runner=Join-Path $PSScriptRoot 'Invoke-HrPermissionP1.ps1'
$tokens=$null;$errors=$null
$runnerAst=[Management.Automation.Language.Parser]::ParseFile($runner,[ref]$tokens,[ref]$errors)
Pass ($errors.Count -eq 0) 'executor parses'
foreach($name in @('Invoke-CommandPair','Invoke-Provisioning')){
    $found=@($runnerAst.FindAll({param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -ceq $name},$true))
    Pass ($found.Count -eq 1) "executor has $name"
    . ([scriptblock]::Create($found[0].Extent.Text))
}
function Assert([bool]$value,[string]$message){Assert-P1 $value $message}
function Check([string]$name,[bool]$value){Assert-P1 $value $name}
$sa=@{Client='offline-not-an-HTTP-client';Csrf='offline'}
$mockCalls=[Collections.Generic.List[object]]::new()
$mockFailure=0
$v=@(1..6|ForEach-Object {[Convert]::ToBase64String([byte[]]@($_,0,0,0,0,0,0,0))})
function Request($client,$method,$path,$body,$csrf,$key){
    Assert-P1Command $path $body $key $bindings
    $mockCalls.Add(@{Key=$key;Path=$path;Body=($body|ConvertTo-Json -Depth 12 -Compress)})
    if($mockFailure -eq $mockCalls.Count){return @{Status=409;Value=$null}}
    $result=switch -CaseSensitive ($key){
        'P1-create-admin' {@{id='11111111-1111-1111-1111-111111111111';version=$v[0]}}
        'P1-create-requester' {@{id='22222222-2222-2222-2222-222222222222';version=$v[1]}}
        'P1-grant-admin' {Assert ($body.expectedRoleVersion -ceq $v[0]) 'admin creation version';@{id=$bindings.AdminRole;version=$v[2]}}
        'P1-grant-requester' {Assert ($body.expectedRoleVersion -ceq $v[1]) 'requester creation version';@{id=$bindings.RequesterRole;version=$v[3]}}
        'P1-assign-admin' {
            Assert ($body.expectedRoleVersion -ceq $v[2] -and $body.expectedAccountVersion -ceq $v[4]) 'admin fresh versions'
            @{assignment=@{id='55555555-5555-5555-5555-555555555555'};accountVersion=$v[4]}
        }
        'P1-assign-requester' {
            Assert ($body.expectedRoleVersion -ceq $v[3] -and $body.expectedAccountVersion -ceq $v[5]) 'HR fresh versions'
            @{assignment=@{id='66666666-6666-6666-6666-666666666666'};accountVersion=$v[5]}
        }
        default {throw 'Unexpected mock command.'}
    }
    return @{Status=200;Value=$result}
}
$bindings=@{}
$result=Invoke-Provisioning @($v[4],$v[5])
Pass ($mockCalls.Count -eq 12 -and $result.Count -eq 2) 'six commands plus six immediate replays'
for($i=0;$i -lt 12;$i+=2){
    Pass ($mockCalls[$i].Key -ceq $mockCalls[$i+1].Key -and
        $mockCalls[$i].Body -ceq $mockCalls[$i+1].Body -and $mockCalls[$i].Path -ceq $mockCalls[$i+1].Path) 'identical replay'
}
Pass (([datetimeoffset]$bindings.EffectiveTo-[datetimeoffset]$bindings.EffectiveFrom) -eq [timespan]::FromHours(24)) 'one captured 24-hour window'
foreach($failure in @(1,2)){
    $bindings=@{};$mockCalls.Clear();$mockFailure=$failure
    Reject {Invoke-Provisioning @($v[4],$v[5])} 'uncertain command stops provisioning'
    Pass ($mockCalls.Count -eq $failure) 'no later command or automatic retry after failure'
}
$state=@{Requests=100;Logins=0}
Reject {Use-P1Budget $state $true} 'HTTP ceiling precedes login'
Pass ($state.Requests -eq 100 -and $state.Logins -eq 0) 'rejected request does not consume login'
$state=@{Requests=5;Logins=5}
Reject {Use-P1Budget $state $true} 'MFA ceiling precedes request'
Pass ($state.Requests -eq 5 -and $state.Logins -eq 5) 'rejected login leaves counters unchanged'
$connectionString=Get-P1SqlConnectionString
Assert-P1SqlConnectionString $connectionString
Pass $true 'exact bounded P1 inspection pool'
foreach($change in @(
    @('Data Source=np:\\.\pipe\LOCALDB#9F3001F7\tsql\query','Data Source=localhost'),
    @('Initial Catalog=Cosmix_Testing','Initial Catalog=master'),
    @('Integrated Security=True','Integrated Security=False'),
    @('Encrypt=False','Encrypt=True'),@('Pooling=True','Pooling=False'),
    @('Min Pool Size=0','Min Pool Size=1'),@('Max Pool Size=1','Max Pool Size=2'),
    @('Connect Timeout=30','Connect Timeout=0'),@('Connect Timeout=30','Connect Timeout=5'),
    @('ConnectRetryCount=0','ConnectRetryCount=1'),
    @('Application Name=Cosmix.HrPermissions.P1.Inspect','Application Name=Other')
)){
    $badConnection=$connectionString.Replace($change[0],$change[1])
    Reject {Assert-P1SqlConnectionString $badConnection} "wrong SQL option $($change[0])"
}
foreach($extra in @('User ID=unexpected','Password=unexpected','AttachDbFilename=C:\other.mdf',
    'Failover Partner=other','TrustServerCertificate=True','MultipleActiveResultSets=True')){
    Reject {Assert-P1SqlConnectionString ($connectionString+';'+$extra)} 'extra SQL option denied'
}
$sqlReader=@($runnerAst.FindAll({param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -ceq 'Read-Sql'},$true))
Pass ($sqlReader.Count -eq 1) 'P1 owns its SELECT reader'
. ([scriptblock]::Create($sqlReader[0].Extent.Text))
# In-memory stand-ins only. No SqlConnection is constructed or opened by these tests.
$sqlEvents=[Collections.Generic.List[string]]::new()
$sqlFailure='';$sqlColumns=1;$sqlExtraResult=$false
function New-P1SqlConnection {
    $sqlEvents.Add('new')
    $reader=[pscustomobject]@{FieldCount=$sqlColumns;Index=-1;Rows=@('first','second')}
    $reader|Add-Member ScriptMethod Read {$this.Index++;return $this.Index -lt $this.Rows.Count}
    $reader|Add-Member ScriptMethod GetValue {param($index) if($sqlFailure -eq 'read'){throw 'offline read failure'};return $this.Rows[$this.Index]}
    $reader|Add-Member ScriptMethod NextResult {return $sqlExtraResult}
    $reader|Add-Member ScriptMethod Dispose {$sqlEvents.Add('reader-disposed')}
    $command=[pscustomobject]@{CommandText='';CommandTimeout=0;Reader=$reader}
    $command|Add-Member ScriptMethod ExecuteReader {
        $sqlEvents.Add('execute')
        Assert ($this.CommandTimeout -eq 20) 'command timeout must remain bounded'
        if($sqlFailure -eq 'execute'){throw 'offline command failure'}
        return $this.Reader
    }
    $command|Add-Member ScriptMethod Dispose {$sqlEvents.Add('command-disposed')}
    $connection=[pscustomobject]@{Command=$command}
    $connection|Add-Member ScriptMethod Open {$sqlEvents.Add('open');if($sqlFailure -eq 'open'){throw 'offline connection failure'}}
    $connection|Add-Member ScriptMethod CreateCommand {return $this.Command}
    $connection|Add-Member ScriptMethod Dispose {$sqlEvents.Add('connection-disposed')}
    return $connection
}
foreach($query in @('UPDATE auth.Roles SET IsActive=0','DELETE FROM auth.Roles','EXEC procedure','INSERT INTO x VALUES(1)','')){
    $sqlEvents.Clear()
    Reject {Read-Sql $query} 'non-SELECT denied'
    Pass ($sqlEvents.Count -eq 0) 'rejected statement never constructs connection'
}
$sqlEvents.Clear()
Pass ((Read-Sql 'SELECT example;') -ceq "first`nsecond") 'SELECT retains one-column row handling'
Pass (($sqlEvents -join '|') -ceq 'new|open|execute|reader-disposed|command-disposed|connection-disposed') 'reader command connection disposed in order'
$sqlEvents.Clear()
$null=Read-Sql 'SELECT first;';$null=Read-Sql 'SELECT second;'
Pass (@($sqlEvents|Where-Object {$_ -eq 'open'}).Count -eq 2 -and
    @($sqlEvents|Where-Object {$_ -eq 'connection-disposed'}).Count -eq 2) 'sequential logical leases returned to bounded pool'
foreach($stage in @('open','execute','read')){
    $sqlEvents.Clear();$sqlFailure=$stage
    Reject {Read-Sql 'SELECT example;'} "SQL $stage failure propagated"
    Pass (@($sqlEvents|Where-Object {$_ -eq 'open'}).Count -eq 1 -and $sqlEvents[-1] -eq 'connection-disposed') 'no retry/fallback; connection disposed'
    if($stage -ne 'open'){Pass ($sqlEvents.Contains('command-disposed')) 'failed command disposed'}
    if($stage -eq 'read'){Pass ($sqlEvents.Contains('reader-disposed')) 'failed reader disposed'}
}
$sqlFailure='';$sqlColumns=2
Reject {Read-Sql 'SELECT example;'} 'multiple columns denied'
$sqlColumns=1;$sqlExtraResult=$true
Reject {Read-Sql 'SELECT example;'} 'additional result denied'
$sqlExtraResult=$false
$now=[datetimeoffset]'2026-10-20T12:00:00Z'
Pass ((Get-P1ResendWaitMilliseconds 'admin@example.invalid' '2026-10-20T11:59:00Z' $now 30 60) -eq 0) 'expired resend timestamp: no wait'
Pass ((Get-P1ResendWaitMilliseconds 'hr@example.invalid' $now.ToString('o') $now 30 60) -eq 0) 'current resend timestamp: no wait'
Pass ((Get-P1ResendWaitMilliseconds 'admin@example.invalid' $now.AddSeconds(30).ToString('o') $now 30 60) -eq 30250) 'future resend waits once with 250ms margin'
Pass ((Get-P1ResendWaitMilliseconds 'superadmin@example.invalid' $now.AddSeconds(599).ToString('o') $now 599 600) -eq 599250) 'maximum valid provider cooldown bounded'
Reject {Get-P1ResendWaitMilliseconds 'admin@example.invalid' $now.AddSeconds(32).ToString('o') $now 30 60} 'excess server deadline denied'
foreach($stamp in @('','bad','2026-10-20T12:00:30','2026-10-20T12:00:30+05:00')){
    Reject {Get-P1ResendWaitMilliseconds 'admin@example.invalid' $stamp $now 30 60} 'invalid resend timestamp denied'
}
foreach($limits in @(@(0,60),@(29,60),@(60,60),@(30,59),@(30,601))){
    Reject {Get-P1ResendWaitMilliseconds 'admin@example.invalid' $now.ToString('o') $now $limits[0] $limits[1]} 'invalid provider bounds denied'
}
Reject {Get-P1ResendWaitMilliseconds 'foreign@example.invalid' $now.ToString('o') $now 30 60} 'foreign cooldown account denied'
Assert-P1PhoneCounts 3 @(1,1,1)
Pass $true 'three approved verified phone bindings'
foreach($counts in @(@(0,1,1),@(2,1,1),@(1,1))){
    Reject {Assert-P1PhoneCounts 3 $counts} 'missing or duplicate approved binding denied'
}
Reject {Assert-P1PhoneCounts 4 @(1,1,1)} 'extra active phone binding denied'
foreach($file in Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*P1*.ps1'){
    $tokens=$null;$errors=$null
    $null=[Management.Automation.Language.Parser]::ParseFile($file.FullName,[ref]$tokens,[ref]$errors)
    Pass ($errors.Count -eq 0) "parse $($file.Name)"
}
Write-Output "PASS $checks P1 offline guard checks. No SQL, HTTP, host, credential or private-file access."
