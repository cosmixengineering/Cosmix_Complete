[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ReleaseDirectory,
    [ValidateSet('Inspect','Execute','ResumeReviewedStartupFailure')][string]$Mode = 'Inspect'
)
$ErrorActionPreference = 'Stop'
$VerbosePreference = 'SilentlyContinue'
$DebugPreference = 'SilentlyContinue'
$root = 'C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261019C01'
$pipe = 'np:\\.\pipe\LOCALDB#2E40033B\tsql\query'
$owner = 'COSMIXRND\Huma.Khan'
$base = [uri]'https://localhost:7481/'
$release = [IO.Path]::GetFullPath($ReleaseDirectory)
$assembly = Join-Path $release 'Cosmix.Api.dll'
$accountsPath = 'C:\Users\Huma.Khan\AppData\Local\Cosmix\LocalTesting\Test-Accounts.json'
$sourceSettings = 'C:\Users\Huma.Khan\AppData\Local\Cosmix\LocalTesting\Dashboard-20261019-Diagnostic.json'
$checks = [Collections.Generic.List[object]]::new()
$clients = [Collections.Generic.List[object]]::new()
$sessions = [Collections.Generic.List[object]]::new()
$hostProcess = $null
$script:started = $false
$script:requests = 0
$resultsRelative = 'results'

function Assert([bool]$condition,[string]$message) { if (!$condition) { throw $message } }
function Check([string]$name,[bool]$condition) {
    $checks.Add(@{Name=$name;Passed=$condition})
    Assert $condition ('Acceptance failed: '+$name)
    Write-Host ('PASS '+$name)
}
function Read-Sql([string]$query) {
    $result = & sqlcmd -S $pipe -E -C -d Cosmix_Testing -b -l 5 -t 20 -h -1 -W -Q ('SET NOCOUNT ON; '+$query)
    Assert ($LASTEXITCODE -eq 0) 'Read-only SQL inspection failed.'
    return (($result | Where-Object { $_.Trim() }) -join "`n").Trim()
}
function Assert-PrivateSource([string]$path) {
    $file = Get-Item -LiteralPath $path
    Assert (!$file.PSIsContainer -and $file.Length -gt 0) 'Private source is not a nonempty file.'
    for ($p=$path; $p; $p=[IO.Path]::GetDirectoryName($p)) {
        Assert (!((Get-Item -LiteralPath $p).Attributes -band [IO.FileAttributes]::ReparsePoint)) 'Private source has a linked ancestor.'
    }
    $sid=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    $acl=Get-Acl -LiteralPath $path
    Assert ($acl.GetOwner([Security.Principal.SecurityIdentifier]).Value -eq $sid) 'Private source owner differs.'
    foreach($rule in $acl.GetAccessRules($true,$true,[Security.Principal.SecurityIdentifier])) {
        Assert ($rule.AccessControlType -ne [Security.AccessControl.AccessControlType]::Allow -or $rule.IdentityReference.Value -eq $sid) 'Private source grants another identity access.'
    }
}
function New-PrivateDirectory([string]$path) {
    Assert ($path -eq $root -or $path.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)) 'Private path outside target.'
    Assert (!(Test-Path -LiteralPath $path)) 'New private path already exists.'
    [IO.Directory]::CreateDirectory($path) | Out-Null
    $acl=[Security.AccessControl.DirectorySecurity]::new()
    $sid=[Security.Principal.WindowsIdentity]::GetCurrent().User
    $acl.SetOwner($sid); $acl.SetAccessRuleProtection($true,$false)
    $rule=[Security.AccessControl.FileSystemAccessRule]::new($sid,'FullControl','ContainerInherit,ObjectInherit','None','Allow')
    $acl.AddAccessRule($rule); Set-Acl -LiteralPath $path -AclObject $acl
}
function Write-Private([string]$relative,[byte[]]$bytes) {
    $path=[IO.Path]::GetFullPath((Join-Path $root $relative))
    Assert ($path.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)) 'Write outside private target.'
    $stream=[IO.File]::Open($path,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try {$stream.Write($bytes);$stream.Flush($true)} finally {$stream.Dispose()}
}
function Write-Json([string]$relative,$value) {
    Write-Private $relative ([Text.UTF8Encoding]::new($false).GetBytes(($value | ConvertTo-Json -Depth 30)))
}
function Write-Result([string]$name,$value) { Write-Json ($resultsRelative+'\'+$name) $value }
function New-Client($cookies) {
    $handler=[Net.Http.HttpClientHandler]::new()
    $handler.CookieContainer=$cookies; $handler.AllowAutoRedirect=$false
    $client=[Net.Http.HttpClient]::new($handler)
    $client.BaseAddress=$base; $client.Timeout=[TimeSpan]::FromSeconds(45)
    $client.DefaultRequestVersion=[version]'1.1'
    $clients.Add($client)
    return $client
}
function Begin-Request($client,[string]$method,[string]$path,$body,[string]$csrf='',[string]$key='') {
    Assert ($path.StartsWith('/api/v1/')) 'Request outside reviewed API.'
    $script:requests++
    Assert ($script:requests -le 120) 'Bounded HTTP request ceiling exceeded.'
    $message=[Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::new($method),$path)
    if ($null -ne $body) { $message.Content=[Net.Http.StringContent]::new(($body|ConvertTo-Json -Depth 20 -Compress),[Text.Encoding]::UTF8,'application/json') }
    if ($csrf) {$message.Headers.Add('X-CSRF-TOKEN',$csrf)}
    if ($key) {$message.Headers.Add('Idempotency-Key',$key)}
    $message.Headers.Add('X-Correlation-ID','HR-C01-'+[guid]::NewGuid().ToString('N'))
    return @{Task=$client.SendAsync($message);Request=$message}
}
function Finish-Request($pending) {
    $response=$pending.Task.GetAwaiter().GetResult()
    try {
        $text=$response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        $value=if($text){try {ConvertFrom-Json -InputObject $text -AsHashtable}catch {$null}}else{$null}
        return @{Status=[int]$response.StatusCode;Value=$value}
    } finally {$response.Dispose();$pending.Request.Dispose()}
}
function Request($client,[string]$method,[string]$path,$body=$null,[string]$csrf='',[string]$key='') {
    return Finish-Request (Begin-Request $client $method $path $body $csrf $key)
}
function Login($credentials,[string]$email) {
    $credential=@($credentials|Where-Object Email -CEQ $email)
    Assert ($credential.Count -eq 1 -and $credential[0].Password) 'Known synthetic credentials missing.'
    $jar=[Net.CookieContainer]::new();$client=New-Client $jar
    $anonymous=Request $client GET '/api/v1/me/permissions'
    Check ($email+': anonymous denied') ($anonymous.Status -eq 401)
    $csrf=Request $client GET '/api/v1/auth/web/csrf'
    Assert ($csrf.Status -eq 200 -and $csrf.Value.requestToken) 'Anonymous CSRF missing.'
    $token=$csrf.Value.requestToken
    $login=Request $client POST '/api/v1/auth/web/login' @{email=$email;password=$credential[0].Password} $token
    Check ($email+': password starts MFA') ($login.Status -eq 202 -and $login.Value.challengeId)
    $before=Request $client GET '/api/v1/me/permissions'
    Check ($email+': pre-MFA denied') ($before.Status -eq 401)
    $otp=$null
    for($i=0;$i -lt 20;$i++) {
        $read=Request $client POST '/api/v1/auth/web/local-testing/otp' @{challengeId=$login.Value.challengeId} $token
        if($read.Status -eq 200){$otp=$read.Value.code;break}
        Assert ($read.Status -eq 404) 'Unexpected OTP response.'
        Start-Sleep -Milliseconds 500
    }
    Assert ($otp -match '^\d{6}$') 'No local OTP delivered within the bound.'
    $verified=Request $client POST '/api/v1/auth/web/otp/verify' @{challengeId=$login.Value.challengeId;code=$otp} $token
    $otp=$null
    Check ($email+': actual MFA session') ($verified.Status -eq 200 -and $verified.Value.sessionId)
    $csrf=Request $client GET '/api/v1/auth/web/csrf'
    Assert ($csrf.Status -eq 200 -and $csrf.Value.requestToken) 'Authenticated CSRF missing.'
    $session=@{Client=$client;Cookies=$jar;Csrf=$csrf.Value.requestToken;Email=$email;Id=$verified.Value.sessionId;LoggedOut=$false}
    $sessions.Add($session)
    $permissions=Request $client GET '/api/v1/me/permissions'
    Check ($email+': authenticated permissions') ($permissions.Status -eq 200)
    $session.Grants=$permissions.Value
    return $session
}

Assert ($PSVersionTable.PSVersion.Major -ge 7 -and $IsWindows) 'PowerShell 7 on Windows required.'
Assert ([Security.Principal.WindowsIdentity]::GetCurrent().Name -ieq $owner) 'Unexpected operator.'
Assert ($release.StartsWith('D:\CosmixVerification\',[StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $assembly)) 'Frozen Release required.'
$resume = $Mode -eq 'ResumeReviewedStartupFailure'
Assert ($resume -or !(Test-Path -LiteralPath $root)) 'C01 already exists; inspect retained evidence, never replay setup.'
Assert (@(Get-NetTCPConnection -State Listen -LocalPort 7481 -ErrorAction SilentlyContinue).Count -eq 0) 'Port 7481 is occupied.'
for($p=[IO.Path]::GetDirectoryName($root);$p;$p=[IO.Path]::GetDirectoryName($p)) {
    if(Test-Path -LiteralPath $p){Assert (!((Get-Item -LiteralPath $p).Attributes -band [IO.FileAttributes]::ReparsePoint)) 'Linked ancestor forbidden.'}
}
$binding=Read-Sql "SELECT CONCAT(CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')),'|',ORIGINAL_LOGIN(),'|',SUSER_SNAME(d.owner_sid),'|',CONVERT(varchar(36),r.family_guid)) FROM sys.databases d JOIN sys.database_recovery_status r ON r.database_id=d.database_id WHERE d.database_id=DB_ID();"
Assert ($binding -ieq 'Software-PC1\LOCALDB#2E40033B|COSMIXRND\Huma.Khan|COSMIXRND\Huma.Khan|d9c34627-18a2-41cf-a804-821e39516303') 'SQL target binding changed.'
Assert ((Read-Sql "SELECT CONVERT(int,SERVERPROPERTY('ProcessID'));") -eq '24152') 'SQL process identity changed.'
Assert ((Read-Sql "SELECT STRING_AGG(CONVERT(nvarchar(max),physical_name),'|') WITHIN GROUP(ORDER BY file_id) FROM sys.database_files;") -ieq 'C:\Users\Huma.Khan\Cosmix_Testing.mdf|C:\Users\Huma.Khan\Cosmix_Testing_log.ldf') 'Existing database file identity changed.'
Assert ((Read-Sql 'SELECT (SELECT COUNT(*) FROM sys.triggers WHERE is_disabled=0 AND is_ms_shipped=0)+(SELECT COUNT(*) FROM sys.synonyms)+(SELECT COUNT(*) FROM sys.sql_expression_dependencies WHERE referenced_server_name IS NOT NULL OR referenced_database_name IS NOT NULL);') -eq '0') 'Database has an unreviewed trigger or external dependency.'
Assert ((Read-Sql 'SELECT COUNT(*) FROM dbo.__EFMigrationsHistory;') -eq '25') 'Current schema count differs.'
Assert ((Read-Sql 'SELECT COUNT(*) FROM auth.OtpDeliveries WHERE Status IN(0,4);') -eq '0') 'Existing pending OTP deliveries must not be processed.'
$employeeDigest=Read-Sql "SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM hr.Employees ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES)),2);"
$accountCount=Read-Sql 'SELECT COUNT(*) FROM auth.UserAccounts;'
Assert ($accountCount -eq '3') 'Synthetic account population changed.'
Assert-PrivateSource $accountsPath
Assert-PrivateSource $sourceSettings
if ($resume) {
    foreach($relative in @('results\failure.json','results\host-final.json','results\start.json','results\host-stderr.log','config\host.json','keys\files.key')) {
        Assert-PrivateSource (Join-Path $root $relative)
    }
    $failure=Get-Content -LiteralPath (Join-Path $root 'results\failure.json') -Raw|ConvertFrom-Json
    $final=Get-Content -LiteralPath (Join-Path $root 'results\host-final.json') -Raw|ConvertFrom-Json
    $original=Get-Content -LiteralPath (Join-Path $root 'results\start.json') -Raw|ConvertFrom-Json
    $diagnostic=Get-Content -LiteralPath (Join-Path $root 'results\host-stderr.log') -Raw
    Assert ($failure.Requests -eq 0 -and @($failure.Checks).Count -eq 0 -and $final.Exited -eq $true) 'Only the reviewed pre-request startup failure may resume.'
    Assert ($diagnostic.Contains('CurrentHrTestingHost.ConfigureServices') -and $diagnostic.Contains('C01 existing-database host validation failed')) 'Failure is not the reviewed framework-service guard failure.'
    Assert ($original.Target -eq $binding -and $original.EmployeeDigest -eq $employeeDigest) 'Original target or employee baseline changed.'
    Assert (!(Test-Path -LiteralPath (Join-Path $root 'results\acceptance.json'))) 'An acceptance run already exists.'
    $resultsRelative='results\attempt02'
    Assert (!(Test-Path -LiteralPath (Join-Path $root $resultsRelative))) 'The reviewed resume was already attempted; no automatic replay.'
}
if($Mode -eq 'Inspect') {
    [pscustomobject]@{Mode='ReadOnly';Target=$binding;Migrations=25;Accounts=3;RootAbsent=$true;Port7481Available=$true;Writes=$false}
    return
}

try {
    if (!$resume) {
    New-PrivateDirectory $root
    foreach($dir in @('config','files','keys','keys\data-protection','results')) {New-PrivateDirectory (Join-Path $root $dir)}
    $settings=Get-Content -LiteralPath $sourceSettings -Raw|ConvertFrom-Json -AsHashtable
    $mobile=$settings.MobileAuthentication;$web=$settings.WebAuthentication
    $mobile.Crypto.KeyVersion='hr-c01'
    $mobile.Crypto.VerifierKeyBase64=[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    $mobile.Crypto.EncryptionKeyBase64=[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    $mobile.Sms=@{Provider='LocalTesting';Endpoint='';BearerToken=''}
    $config=@{
        Urls=$base.AbsoluteUri.TrimEnd('/');AllowedHosts='localhost'
        LocalTesting=@{Enabled=$true;ExistingAcceptanceProfile='HumaHrAcceptance20261019C01';PrivateDirectory=$root}
        ConnectionStrings=@{CosmixDatabase="Server=$pipe;Database=Cosmix_Testing;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=5;Application Name=Cosmix.HrAcceptance.C01"}
        PrivateFiles=@{Directory=(Join-Path $root 'files');KeyFile=(Join-Path $root 'keys\files.key');KeyReference='hr-acceptance-c01'}
        Jwt=@{Issuer='Cosmix.HrAcceptance.C01';Audience='Cosmix.HrAcceptance.C01';SigningKey=[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))}
        MobileAuthentication=$mobile;WebAuthentication=$web
        EmployeeFileScanner=@{Enabled=$false};AttendanceJobs=@{Enabled=$false}
        SelfHostedFace=@{Enabled=$false};FaceCapture=@{Enabled=$false};FaceTemplateStorage=@{Enabled=$false}
        FaceStorageRetention=@{Enabled=$false};FaceStorageRetentionJournal=@{Enabled=$false}
        Cors=@{AllowedOrigins=@('https://localhost:5173')}
    }
    Write-Private 'keys\files.key' ([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    Write-Json 'config\host.json' $config
    $config=$null;$settings=$null;$mobile=$null;$web=$null
    } else {
        // Preserve the first attempt and reuse its private keys/settings without regenerating or resetting anything.
        New-PrivateDirectory (Join-Path $root $resultsRelative)
    }
    $hashes=@{}
    foreach($name in @('Cosmix.Api','Cosmix.Application','Cosmix.Infrastructure','Cosmix.Domain')) {$hashes[$name]=(Get-FileHash -LiteralPath (Join-Path $release ($name+'.dll'))).Hash}
    Write-Result 'start.json' @{Target=$binding;MigrationCount=25;EmployeeDigest=$employeeDigest;Release=$release;Hashes=$hashes;SettingsSha256=(Get-FileHash -LiteralPath (Join-Path $root 'config\host.json')).Hash;ReviewedStartupResume=$resume;Utc=[DateTimeOffset]::UtcNow;MaxHttpRequests=120;BusinessWrites='One new marked department; retry/concurrency updates and deactivate; no employee/grant changes'}
    $start=[Diagnostics.ProcessStartInfo]::new((Get-Command dotnet -CommandType Application).Source)
    $start.ArgumentList.Add($assembly);$start.UseShellExecute=$false;$start.CreateNoWindow=$true
    $start.WorkingDirectory=$release;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    $start.Environment.Clear()
    foreach($key in @('SystemRoot','WINDIR','USERPROFILE','LOCALAPPDATA','APPDATA','PATH','TEMP','TMP','ProgramFiles','ProgramW6432','DOTNET_ROOT','COMPUTERNAME','USERNAME','USERDOMAIN')) {
        $value=[Environment]::GetEnvironmentVariable($key);if($value){$start.Environment[$key]=$value}
    }
    $start.Environment['COSMIX_EXISTING_TESTING_HOST']='HumaHrAcceptance20261019C01'
    $start.Environment['COSMIX_LOCAL_TESTING_SETTINGS']=Join-Path $root 'config\host.json'
    $start.Environment['ASPNETCORE_ENVIRONMENT']='Development'
    $hostProcess=[Diagnostics.Process]::new();$hostProcess.StartInfo=$start
    Assert ($hostProcess.Start()) 'Owned host failed to start.'
    $script:started=$true
    $stdout=$hostProcess.StandardOutput.ReadToEndAsync();$stderr=$hostProcess.StandardError.ReadToEndAsync()
    $ready=$false
    for($i=0;$i -lt 60;$i++) {
        Assert (!$hostProcess.HasExited) 'Owned host exited during startup; inspect private output.'
        $listeners=@(Get-NetTCPConnection -State Listen -LocalPort 7481 -ErrorAction SilentlyContinue)
        if($listeners.Count){Assert (@($listeners|Where-Object {$_.OwningProcess -ne $hostProcess.Id -or $_.LocalAddress -notin @('127.0.0.1','::1')}).Count -eq 0) 'Port ownership differs.';$ready=$true;break}
        Start-Sleep -Milliseconds 500
    }
    Assert $ready 'Owned HTTPS host not listening within bound.'
    $credentials=Get-Content -LiteralPath $accountsPath -Raw|ConvertFrom-Json
    $hr=Login $credentials 'hr@example.invalid'
    $admin=Login $credentials 'admin@example.invalid'
    $credentials=$null
    $queue='/api/v1/leaves/admin/queue?pendingOnly=true&from=2026-01-01&to=2026-12-31&pageSize=5'
    Check 'HR cannot use Admin leave review' ((Request $hr.Client GET $queue).Status -eq 403)
    Check 'Explicit Admin leave queue permitted' ((Request $admin.Client GET $queue).Status -eq 200)
    $actor=if(@($hr.Grants|Where-Object permission -EQ 'hr.organization.manage').Count){$hr}else{$admin}
    Assert (@($actor.Grants|Where-Object permission -EQ 'hr.organization.manage').Count -gt 0) 'No existing authorized organization reviewer; grants not changed.'
    $code='TEST-C01-'+[guid]::NewGuid().ToString('N').Substring(0,12)
    $body=@{code=$code;name='TEST ONLY C01 concurrency department'};$key=$code+'-create'
    Check 'Business request without CSRF denied' ((Request $actor.Client POST '/api/v1/hr/departments' $body '' $key).Status -eq 400)
    $parallelA=New-Client $actor.Cookies;$parallelB=New-Client $actor.Cookies
    $one=Begin-Request $parallelA POST '/api/v1/hr/departments' $body $actor.Csrf $key
    $two=Begin-Request $parallelB POST '/api/v1/hr/departments' $body $actor.Csrf $key
    [Threading.Tasks.Task]::WhenAll([Threading.Tasks.Task[]]@($one.Task,$two.Task)).GetAwaiter().GetResult()|Out-Null
    $a=Finish-Request $one;$b=Finish-Request $two
    Check 'Concurrent same-key requests return one resource' ($a.Status -eq 201 -and $b.Status -eq 201 -and $a.Value.id -eq $b.Value.id)
    $id=$a.Value.id;Assert ([guid]::TryParse($id,[ref]([guid]::Empty))) 'Invalid created ID.'
    Check 'Same key with changed payload conflicts' ((Request $actor.Client POST '/api/v1/hr/departments' @{code=$code;name='TEST ONLY changed payload'} $actor.Csrf $key).Status -eq 409)
    $path='/api/v1/hr/departments/'+$id
    $updateA=@{name='TEST ONLY C01 winner A';expectedVersion=$a.Value.version;reason='Synthetic concurrent update A'}
    $updateB=@{name='TEST ONLY C01 winner B';expectedVersion=$a.Value.version;reason='Synthetic concurrent update B'}
    $one=Begin-Request $parallelA PUT $path $updateA $actor.Csrf ($code+'-A')
    $two=Begin-Request $parallelB PUT $path $updateB $actor.Csrf ($code+'-B')
    [Threading.Tasks.Task]::WhenAll([Threading.Tasks.Task[]]@($one.Task,$two.Task)).GetAwaiter().GetResult()|Out-Null
    $a=Finish-Request $one;$b=Finish-Request $two
    Check 'Competing separate HTTP updates reject stale version' ((@($a.Status,$b.Status)|Sort-Object) -join ',' -eq '200,409')
    $current=Request $actor.Client GET $path
    Check 'Fresh request reads persisted winning update' ($current.Status -eq 200 -and $current.Value.name -in @($updateA.name,$updateB.name))
    $history=Request $actor.Client GET ($path+'/history?pageSize=10')
    Check 'Only create and winning update retained in history' ($history.Status -eq 200 -and $history.Value.items.Count -eq 2)
    $inactive=Request $actor.Client POST ($path+'/status') @{isActive=$false;expectedVersion=$current.Value.version;reason='Synthetic test complete; retain inactive evidence'} $actor.Csrf ($code+'-inactive')
    Check 'Synthetic department retained inactive' ($inactive.Status -eq 200 -and !$inactive.Value.isActive)
    $sqlCount=Read-Sql ("SELECT COUNT(*) FROM hr.Departments WHERE Id='"+$id+"' AND Code='"+$code+"' AND IsActive=0;")
    Check 'Independent SQL confirms exactly one retained inactive row' ($sqlCount -eq '1')
    $atomicCount=Read-Sql ("SELECT COUNT(*) FROM hr.OrganizationChanges c JOIN audit.AuditEvents a ON a.Id=c.AuditEventId JOIN workflow.IdempotencyReceipts r ON r.ProtectedResultReference=REPLACE(CONVERT(varchar(36),c.Id),'-','') AND r.Operation=c.Operation AND r.ActorUserId=a.ActorUserId WHERE c.ResourceId='"+$id+"' AND r.[Key] LIKE '"+$code+"-%' AND r.CompletedAt IS NOT NULL;")
    Check 'Exactly three business changes retain matching audit and completed receipts' ($atomicCount -eq '3')
    Check 'No migration was applied' ((Read-Sql 'SELECT COUNT(*) FROM dbo.__EFMigrationsHistory;') -eq '25')
    Check 'All existing employee rows unchanged' ((Read-Sql "SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM hr.Employees ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES)),2);") -eq $employeeDigest)
    foreach($session in $sessions) {
        $retained=$session.Cookies.GetCookieHeader($base)
        Check ($session.Email+': missing-CSRF logout denied') ((Request $session.Client POST '/api/v1/auth/web/logout' @{}).Status -eq 400)
        Check ($session.Email+': authenticated logout') ((Request $session.Client POST '/api/v1/auth/web/logout' @{} $session.Csrf).Status -eq 204)
        $session.LoggedOut=$true
        $jar=[Net.CookieContainer]::new();$jar.SetCookies($base,$retained.Replace(';',','))
        $replay=New-Client $jar
        Check ($session.Email+': revoked cookie denied') ((Request $replay GET '/api/v1/me/permissions').Status -eq 401)
    }
    Write-Result 'acceptance.json' @{Checks=$checks.ToArray();Requests=$script:requests;DepartmentId=$id;DepartmentCode=$code;MigrationCount=25;AccountCount=3;DepartmentCount=1;AtomicBusinessAuditReceiptCount=3;MigrationApplied=$false;EmployeeRowsUnchanged=$true;Scope='Auth and Organization concurrency only, not full HR acceptance';CompletedUtc=[DateTimeOffset]::UtcNow}
} catch {
    if(Test-Path -LiteralPath (Join-Path $root 'results')) {
        Write-Result 'failure.json' @{Checks=$checks.ToArray();Requests=$script:requests;FailureType=$_.Exception.GetType().FullName;Message='Acceptance stopped. Review the last redacted check and private host output; no reset or automatic replay.';Utc=[DateTimeOffset]::UtcNow}
    }
    throw
} finally {
    foreach($session in $sessions) {
        if(!$session.LoggedOut){try{$null=Request $session.Client POST '/api/v1/auth/web/logout' @{} $session.Csrf}catch{}}
    }
    foreach($client in $clients){$client.Dispose()}
    if($hostProcess -and $script:started) {
        if(!$hostProcess.HasExited){$hostProcess.Kill();$hostProcess.WaitForExit(10000)|Out-Null}
        Write-Private ($resultsRelative+'\host-stdout.log') ([Text.UTF8Encoding]::new($false).GetBytes($stdout.GetAwaiter().GetResult()))
        Write-Private ($resultsRelative+'\host-stderr.log') ([Text.UTF8Encoding]::new($false).GetBytes($stderr.GetAwaiter().GetResult()))
        Write-Result 'host-final.json' @{ProcessId=$hostProcess.Id;Exited=$hostProcess.HasExited;DatabaseStopped=$false;CleanupPerformed=$false}
        $hostProcess.Dispose()
    }
}
