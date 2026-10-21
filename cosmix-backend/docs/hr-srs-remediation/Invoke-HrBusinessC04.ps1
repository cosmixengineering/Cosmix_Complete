[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ReleaseDirectory,
    [ValidateSet('Inspect','Execute')][string]$Mode = 'Inspect',
    [string]$ApprovedPacketSha256 = ''
)
$ErrorActionPreference = 'Stop'
$VerbosePreference = 'SilentlyContinue'
$DebugPreference = 'SilentlyContinue'
$root = 'C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261019C04'
$pipe = 'np:\\.\pipe\LOCALDB#9F3001F7\tsql\query'
$owner = 'COSMIXRND\Huma.Khan'
$base = [uri]'https://localhost:7481/'
$release = [IO.Path]::GetFullPath($ReleaseDirectory)
$assembly = Join-Path $release 'Cosmix.Api.dll'
$accountsPath = 'C:\Users\Huma.Khan\AppData\Local\Cosmix\LocalTesting\Test-Accounts.json'
$sourceSettings = 'C:\Users\Huma.Khan\AppData\Local\Cosmix\LocalTesting\Dashboard-20261019-Diagnostic.json'
$checks = [Collections.Generic.List[object]]::new()
$httpResults = [Collections.Generic.List[object]]::new()
$clients = [Collections.Generic.List[object]]::new()
$sessions = [Collections.Generic.List[object]]::new()
$hostProcess = $null
$script:started = $false
$script:requests = 0
$script:departmentId = [guid]::Empty.ToString()
$script:historicalIds = @{}
$departmentCode = 'TEST-C04-20261019'
$resultsRelative = 'results'

function Assert([bool]$condition,[string]$message) { if (!$condition) { throw $message } }
function Check([string]$name,[bool]$condition) {
    $checks.Add(@{Name=$name;Passed=$condition})
    Assert $condition ('Acceptance failed: '+$name)
    Write-Host ('PASS '+$name)
}
function Read-Sql([string]$query) {
    # Explicit user-approved local-pipe exception; not a production TLS acceptance test.
    $connection=[System.Data.SqlClient.SqlConnection]::new("Server=$pipe;Database=Cosmix_Testing;Integrated Security=True;Encrypt=False;TrustServerCertificate=True;Connect Timeout=5;Pooling=False;Application Name=Cosmix.HrBusiness.C04.Inspect")
    try {
        $connection.Open()
        $command=$connection.CreateCommand();$command.CommandTimeout=20;$command.CommandText=$query
        try {
            $reader=$command.ExecuteReader()
            try {
                $rows=[Collections.Generic.List[string]]::new()
                while($reader.Read()) {Assert ($reader.FieldCount -eq 1) 'Expected one redacted inspection column.';$rows.Add([string]$reader.GetValue(0))}
                return ($rows -join [char]10).Trim()
            } finally {$reader.Dispose()}
        } finally {$command.Dispose()}
    } finally {$connection.Dispose()}
}
function Assert-PrivateSource([string]$path,[bool]$allowEmpty=$false) {
    $file = Get-Item -LiteralPath $path
    Assert (!$file.PSIsContainer -and ($file.Length -gt 0 -or $allowEmpty)) 'Private source is not a nonempty file.'
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
    $route=$method+' '+$path
    $authRoute = $route -cin @('GET /api/v1/me/permissions','GET /api/v1/auth/web/csrf','POST /api/v1/auth/web/login','POST /api/v1/auth/web/local-testing/otp','POST /api/v1/auth/web/otp/verify','POST /api/v1/auth/web/logout')
    $businessRoute = ($method -eq 'GET' -and $path -eq '/api/v1/leaves/admin/queue?pendingOnly=true&from=2026-01-01&to=2026-12-31&pageSize=5') -or
        ($method -eq 'POST' -and $path -eq '/api/v1/hr/departments' -and $body.code -ceq $departmentCode -and $body.name.StartsWith('TEST ONLY C04 ')) -or
        ($script:departmentId -ne [guid]::Empty.ToString() -and (
            ($method -cin @('GET','PUT') -and $path -eq ('/api/v1/hr/departments/'+$script:departmentId)) -or
            ($method -eq 'GET' -and $path -eq ('/api/v1/hr/departments/'+$script:departmentId+'/history?pageSize=10')) -or
            ($method -eq 'POST' -and $path -eq ('/api/v1/hr/departments/'+$script:departmentId+'/status'))))
    Assert ($authRoute -or $businessRoute) 'Request outside reviewed C04 allowlist.'
    if($path -eq '/api/v1/auth/web/login') { Assert ($body.email -cin @('hr@example.invalid','admin@example.invalid')) 'Unapproved test account.' }
    $script:requests++
    Assert ($script:requests -le 160) 'Bounded HTTP request ceiling exceeded.'
    $message=[Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::new($method),$path)
    if ($null -ne $body) { $message.Content=[Net.Http.StringContent]::new(($body|ConvertTo-Json -Depth 20 -Compress),[Text.Encoding]::UTF8,'application/json') }
    if ($csrf) {$message.Headers.Add('X-CSRF-TOKEN',$csrf)}
    if ($key) {$message.Headers.Add('Idempotency-Key',$key)}
    $message.Headers.Add('X-Correlation-ID','HR-C04-'+[guid]::NewGuid().ToString('N'))
    return @{Task=$client.SendAsync($message);Request=$message}
}
function Finish-Request($pending) {
    $response=$pending.Task.GetAwaiter().GetResult()
    try {
        $text=$response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        $value=if($text){try {ConvertFrom-Json -InputObject $text -AsHashtable}catch {$null}}else{$null}
        # Retain status and an allowlisted error code, never response bodies or auth material.
        $title=if($value -is [Collections.IDictionary] -and $value.title -is [string] -and
            $value.title -cin @('Unauthorized','Forbidden','Bad Request','An unexpected error occurred',
                'access_denied','invalid_authentication','organization_version_conflict','idempotency_key_reused',
                'invalid_expected_version','conflicting_change','concurrent_change_retry','access_version_conflict',
                'access_write_conflict','access_temporarily_busy')){$value.title}else{$null}
        $httpResults.Add(@{Method=$pending.Request.Method.Method;Path=$pending.Request.RequestUri.AbsolutePath;Status=[int]$response.StatusCode;ErrorCode=$title})
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
Assert (!(Test-Path -LiteralPath $root)) 'C04 already exists; no reset or automatic replay.'
Assert (@(Get-NetTCPConnection -State Listen -LocalPort 7481 -ErrorAction SilentlyContinue).Count -eq 0) 'Port 7481 is occupied.'
for($p=[IO.Path]::GetDirectoryName($root);$p;$p=[IO.Path]::GetDirectoryName($p)) {
    if(Test-Path -LiteralPath $p){Assert (!((Get-Item -LiteralPath $p).Attributes -band [IO.FileAttributes]::ReparsePoint)) 'Linked ancestor forbidden.'}
}
$sqlProcess=Get-CimInstance Win32_Process -Filter 'ProcessId=25768'
Assert ($null -ne $sqlProcess -and $sqlProcess.Name -eq 'sqlservr.exe' -and
    $sqlProcess.CommandLine.Contains('-sLOCALDB#9F3001F7') -and
    $sqlProcess.CommandLine.Contains('\Instances\MSSQLLocalDB\master.mdf')) 'Existing SQL process binding changed.'
$sqlOwner=Invoke-CimMethod -InputObject $sqlProcess -MethodName GetOwner
Assert ($sqlOwner.ReturnValue -eq 0 -and ($sqlOwner.Domain+'\'+$sqlOwner.User) -ieq $owner) 'SQL process owner differs.'
$binding=Read-Sql "SELECT CONCAT(CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')),'|',ORIGINAL_LOGIN(),'|',SUSER_SNAME(d.owner_sid),'|',CONVERT(varchar(36),r.family_guid),'|',d.state_desc,'|',d.user_access_desc,'|',d.is_read_only) FROM sys.databases d JOIN sys.database_recovery_status r ON r.database_id=d.database_id WHERE d.database_id=DB_ID();"
Assert ($binding -ieq 'Software-PC1\LOCALDB#9F3001F7|COSMIXRND\Huma.Khan|COSMIXRND\Huma.Khan|d9c34627-18a2-41cf-a804-821e39516303|ONLINE|MULTI_USER|0') 'SQL target binding changed.'
Assert ((Read-Sql "SELECT CONVERT(int,SERVERPROPERTY('ProcessID'));") -eq '25768') 'SQL process identity changed.'
$transport=Read-Sql "SELECT CONCAT(net_transport,'|',encrypt_option) FROM sys.dm_exec_connections WHERE session_id=@@SPID;"
Assert ($transport -cin @('Named pipe|FALSE','Named pipe|TRUE')) 'Transport outside the approved local-pipe exception.'
Assert ((Read-Sql "SELECT STRING_AGG(CONVERT(nvarchar(max),physical_name),'|') WITHIN GROUP(ORDER BY file_id) FROM sys.database_files;") -ieq 'C:\Users\Huma.Khan\Cosmix_Testing.mdf|C:\Users\Huma.Khan\Cosmix_Testing_log.ldf') 'Database file identity changed.'
Assert ((Read-Sql 'SELECT COUNT(*) FROM sys.database_files WHERE state<>0 OR type NOT IN(0,1);') -eq '0') 'Database file state differs.'
Assert ((Read-Sql 'SELECT (SELECT COUNT(*) FROM sys.triggers WHERE is_disabled=0 AND is_ms_shipped=0)+(SELECT COUNT(*) FROM sys.synonyms)+(SELECT COUNT(*) FROM sys.sql_expression_dependencies WHERE referenced_server_name IS NOT NULL OR referenced_database_name IS NOT NULL);') -eq '0') 'Unreviewed trigger or external dependency.'
Assert ((Read-Sql 'SELECT COUNT(*) FROM auth.OtpDeliveries WHERE Status NOT IN(1,2,3);') -eq '0') 'Pre-existing nonterminal OTP work.'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$expected=@(Get-ChildItem -LiteralPath (Join-Path $repo 'src/Cosmix.Infrastructure/Database/Migrations') -Filter '*.cs' |
    Where-Object Name -Match '^\d{14}_[^.]+\.cs$' | Sort-Object Name | ForEach-Object BaseName)
$applied=(Read-Sql 'SELECT MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;') -split '\r?\n'
Assert ($expected.Count -eq 25 -and ($expected -join '|') -ceq ($applied -join '|')) 'Migration history differs; migrations will not be applied.'
Assert ((Read-Sql 'SELECT COUNT(*) FROM auth.UserAccounts;') -eq '3') 'Account population differs.'
$approvedAccounts=Read-Sql "SELECT COUNT(*) FROM auth.UserAccounts a JOIN hr.Employees e ON e.Id=a.EmployeeId WHERE a.Status=3 AND a.WebAccessEnabled=1 AND (a.LockedUntil IS NULL OR a.LockedUntil<=SYSUTCDATETIME()) AND ((a.LoginName='hr@example.invalid' AND e.ApprovedLoginPhoneNumber='+12025550103' AND e.FullLegalName='Muhammad Rashid (Lead Engineer)') OR (a.LoginName='admin@example.invalid' AND e.ApprovedLoginPhoneNumber='+12025550102' AND e.FullLegalName='Engr. Tariq Mehmood (Site Supervisor)'));"
Assert ($approvedAccounts -eq '2') 'Approved synthetic account bindings changed.'
$adminAuthority=Read-Sql "SELECT COUNT(*) FROM auth.UserAccounts a JOIN auth.UserRoleAssignments u ON u.UserAccountId=a.Id JOIN auth.Roles r ON r.Id=u.RoleId JOIN auth.RolePermissions rp ON rp.RoleId=r.Id JOIN auth.Permissions p ON p.Id=rp.PermissionId JOIN auth.UserAccessScopes s ON s.UserRoleAssignmentId=u.Id WHERE a.LoginName='admin@example.invalid' AND r.SystemRole=1 AND r.Code='ADMIN' AND r.IsActive=1 AND p.Code='attendance.leaves.review' AND s.Kind=7 AND u.EffectiveFrom<=SYSUTCDATETIME() AND (u.EffectiveTo IS NULL OR SYSUTCDATETIME()<u.EffectiveTo) AND (u.RevokedAt IS NULL OR SYSUTCDATETIME()<u.RevokedAt) AND rp.RevokedAt IS NULL AND rp.GrantedAt<=SYSUTCDATETIME();"
Assert ($adminAuthority -eq '1') 'Expected exact existing Admin leave-review authority differs; permissions will not be changed.'
Assert-PrivateSource $accountsPath
Assert-PrivateSource $sourceSettings
$credentials=Get-Content -LiteralPath $accountsPath -Raw|ConvertFrom-Json
foreach($email in @('hr@example.invalid','admin@example.invalid')) {
    $entry=@($credentials|Where-Object Email -CEQ $email)
    Assert ($entry.Count -eq 1 -and $entry[0].Password) 'Approved credential entry unavailable.'
}
$credentials=$null
# Hash only; never export business rows or print private content.
function Protected-Snapshot {
    $tables=Read-Sql "SELECT (SELECT s.name AS [schema],t.name AS [table] FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE t.is_ms_shipped=0 AND NOT (s.name='auth' AND t.name IN('UserAccounts','UserSessions','RefreshTokens','MobileOtpChallenges','OtpDeliveries','AuthRateBuckets')) AND NOT (s.name='audit' AND t.name='AuditEvents') ORDER BY s.name,t.name FOR JSON PATH);"
    $snapshot=@{}
    foreach($table in ($tables|ConvertFrom-Json)) {
        $schema='['+$table.schema.Replace(']',']]')+']'
        $name='['+$table.table.Replace(']',']]')+']'
        $qualified=$schema+'.'+$name
        $literal=$qualified.Replace("'","''")
        $order=Read-Sql ("SELECT STRING_AGG(CONVERT(nvarchar(max),QUOTENAME(c.name)),',') WITHIN GROUP(ORDER BY ic.key_ordinal) FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE i.object_id=OBJECT_ID(N'"+$literal+"') AND i.is_primary_key=1;")
        Assert ($order -and $order -ne 'NULL') 'Protected table lacks a deterministic primary key.'
        $where = ''
        if($qualified -ceq '[hr].[Departments]') {$where=" WHERE Id<>'"+$script:departmentId+"'"}
        elseif($qualified -ceq '[hr].[OrganizationChanges]') {$where=" WHERE ResourceId<>'"+$script:departmentId+"'"}
        elseif($qualified -ceq '[workflow].[IdempotencyReceipts]') {$where=" WHERE [Key] NOT LIKE 'TEST-C04-20261019%' "}
        $snapshot[$qualified]=Read-Sql ("SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM "+$qualified+$where+" ORDER BY "+$order+" FOR JSON PATH,INCLUDE_NULL_VALUES)),2);")
    }
    $snapshot['unapproved-accounts']=Read-Sql "SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM auth.UserAccounts WHERE LoginName NOT IN('hr@example.invalid','admin@example.invalid') ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES)),2);"
    $accountColumns=Read-Sql "SELECT STRING_AGG(CONVERT(nvarchar(max),QUOTENAME(name)),',') WITHIN GROUP(ORDER BY column_id) FROM sys.columns WHERE object_id=OBJECT_ID('auth.UserAccounts') AND name NOT IN('FailedLoginCount','LockedUntil','RowVersion');"
    $snapshot['approved-account-security']=Read-Sql ("SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT "+$accountColumns+" FROM auth.UserAccounts WHERE LoginName IN('hr@example.invalid','admin@example.invalid') ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES)),2);")
    foreach($table in @('auth.UserSessions','auth.RefreshTokens','auth.MobileOtpChallenges','auth.OtpDeliveries','audit.AuditEvents')) {
        if(!$script:historicalIds.ContainsKey($table)) {
            $rows=@((Read-Sql ("SELECT (SELECT Id FROM "+$table+" ORDER BY Id FOR JSON PATH);"))|ConvertFrom-Json)
            Assert ($rows.Count -le 10000) 'Historical preservation bound exceeded.'
            $ids=@($rows|ForEach-Object {[guid]$_.Id | ForEach-Object ToString})
            $script:historicalIds[$table]=ConvertTo-Json -InputObject $ids -Compress
        }
        $json=$script:historicalIds[$table].Replace("'","''")
        $snapshot['historical-'+$table]=Read-Sql ("SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM "+$table+" WHERE Id IN(SELECT CONVERT(uniqueidentifier,[value]) FROM OPENJSON(N'"+$json+"')) ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES)),2);")
    }
    # Fresh verifier keys create new IP/email/phone buckets. Only these existing
    # global/account counters are touched by the two reviewed normal logins.
    $snapshot['unrelated-rate-buckets']=Read-Sql "SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM auth.AuthRateBuckets b WHERE b.[Key] NOT IN('web-request-global','web-password-global','web-verify-global','request-global') AND b.[Key] NOT IN(SELECT CONCAT(p.prefix,LOWER(CONVERT(varchar(36),a.Id))) FROM auth.UserAccounts a CROSS JOIN (VALUES('web-request-account:'),('web-password-account:'),('web-verify-account:')) p(prefix) WHERE a.LoginName IN('hr@example.invalid','admin@example.invalid')) AND b.[Key] IN(SELECT [value] FROM OPENJSON(N'__RATE_KEYS__')) ORDER BY b.[Key] FOR JSON PATH,INCLUDE_NULL_VALUES)),2);".Replace('__RATE_KEYS__',$script:originalRateKeys.Replace("'","''"))
    return $snapshot
}
function C01-Snapshot {
    $old='C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261019C01'
    Assert (Test-Path -LiteralPath $old) 'Original C01 evidence missing.'
    $snapshot=@{}
    foreach($file in Get-ChildItem -LiteralPath @($old,'C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261019C02','C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261019C03') -Recurse -File) {
        Assert-PrivateSource $file.FullName ($file.Name -cin @('host-stdout.log','host-stderr.log'))
        $snapshot[$file.FullName]=(Get-FileHash -LiteralPath $file.FullName).Hash
    }
    return $snapshot
}
function Assert-Snapshot($before,$after,[string]$label) {
    Assert ($before.Count -eq $after.Count) ($label+' population changed.')
    foreach($key in $before.Keys) {Assert ($after.ContainsKey($key) -and $before[$key] -ceq $after[$key]) ($label+' changed; no repair performed.')}
}
function Record-Counts {
    $json=Read-Sql "SELECT (SELECT (SELECT COUNT(*) FROM auth.UserAccounts) AS Accounts,(SELECT COUNT(*) FROM dbo.__EFMigrationsHistory) AS Migrations,(SELECT COUNT(*) FROM auth.UserSessions) AS Sessions,(SELECT COUNT(*) FROM auth.RefreshTokens) AS RefreshTokens,(SELECT COUNT(*) FROM auth.MobileOtpChallenges) AS Challenges,(SELECT COUNT(*) FROM auth.OtpDeliveries) AS Deliveries,(SELECT COUNT(*) FROM auth.AuthRateBuckets) AS RateBuckets,(SELECT COUNT(*) FROM audit.AuditEvents) AS Audits FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);"
    return ($json|ConvertFrom-Json -AsHashtable)
}
function Assert-BusinessState {
    Assert ($script:departmentId -ne [guid]::Empty.ToString() -and $script:winningKey -cin @('TEST-C04-20261019-A','TEST-C04-20261019-B')) 'Winning business identity missing.'
    $expectedName=if($script:winningKey.EndsWith('-A')){'TEST ONLY C04 winner A'}else{'TEST ONLY C04 winner B'}
    Assert ((Read-Sql ("SELECT COUNT(*) FROM hr.Departments WHERE Id='"+$script:departmentId+"' AND Code='TEST-C04-20261019' AND Name='"+$expectedName+"' AND IsActive=0;")) -eq '1') 'Final inactive winner differs.'
    $query=@"
WITH expected AS (
 SELECT 'organization.Department.create' AS operation, 'TEST-C04-20261019-create' AS receipt_key
 UNION ALL SELECT 'organization.Department.update','$script:winningKey'
 UNION ALL SELECT 'organization.Department.status','TEST-C04-20261019-inactive'
), linked AS (
 SELECT e.operation,e.receipt_key,c.Id AS change_id,a.Id AS audit_id,r.Id AS receipt_id
 FROM expected e
 LEFT JOIN hr.OrganizationChanges c ON c.Operation=e.operation AND c.ResourceId='$script:departmentId' AND c.Resource='Department'
 LEFT JOIN audit.AuditEvents a ON a.Id=c.AuditEventId AND a.EventType=e.operation
 LEFT JOIN workflow.IdempotencyReceipts r ON r.ProtectedResultReference=REPLACE(CONVERT(varchar(36),c.Id),'-','')
  AND r.Operation=c.Operation AND r.ActorUserId=a.ActorUserId AND r.[Key]=e.receipt_key AND r.CompletedAt IS NOT NULL
)
SELECT COUNT(*) FROM (SELECT operation FROM linked GROUP BY operation
 HAVING COUNT(*)=1 AND COUNT(change_id)=1 AND COUNT(audit_id)=1 AND COUNT(receipt_id)=1) valid;
"@
    Assert ((Read-Sql $query) -eq '3') 'Each exact business operation requires its own single audit and completed receipt.'
    Assert ((Read-Sql "SELECT COUNT(*) FROM workflow.IdempotencyReceipts WHERE [Key] LIKE 'TEST-C04-20261019%';") -eq '3') 'Unexpected business receipt population.'
    Assert ((Read-Sql ("SELECT COUNT(*) FROM hr.OrganizationChanges WHERE ResourceId='"+$script:departmentId+"';")) -eq '3') 'Unexpected business history population.'
}
Assert ((Read-Sql "SELECT COUNT(*) FROM hr.Departments WHERE Code='TEST-C04-20261019';") -eq '0') 'C04 department already exists; no reuse or cleanup.'
Assert ((Read-Sql "SELECT COUNT(*) FROM workflow.IdempotencyReceipts WHERE [Key] LIKE 'TEST-C04-20261019%';") -eq '0') 'C04 receipts already exist.'
$rateRows=@((Read-Sql "SELECT (SELECT [Key] FROM auth.AuthRateBuckets ORDER BY [Key] FOR JSON PATH);")|ConvertFrom-Json)
Assert ($rateRows.Count -le 10000) 'Rate bucket preservation bound exceeded.'
$script:originalRateKeys=ConvertTo-Json -InputObject @($rateRows|ForEach-Object Key) -Compress
$protected=Protected-Snapshot
$c01=C01-Snapshot
$beforeCounts=Record-Counts
$packet=[Collections.Generic.List[string]]::new()
foreach($file in Get-ChildItem -LiteralPath $release -Recurse -File | Sort-Object FullName) {
    Assert (!(($file.Attributes) -band [IO.FileAttributes]::ReparsePoint)) 'Linked runtime file.'
    $packet.Add($file.FullName+'|'+(Get-FileHash -LiteralPath $file.FullName).Hash)
}
foreach($relative in @('src/Cosmix.Api/Program.cs','src/Cosmix.Api/Development/LocalTesting.cs',
    'src/Cosmix.Api/Development/HrBusinessTestingGuard.cs','src/Cosmix.Api/Development/HrBusinessTestingHost.cs',
    'src/Cosmix.Api/Development/HrBusinessTestingDatabase.cs','src/Cosmix.Api/Development/HrBusinessTestingBoundary.cs',
    'docs/hr-srs-remediation/Invoke-HrBusinessC04.ps1')) {
    $path=Join-Path $repo $relative
    $packet.Add($relative+'|'+(Get-FileHash -LiteralPath $path).Hash)
}
$packet.Add('source-settings|'+(Get-FileHash -LiteralPath $sourceSettings).Hash)
$packet.Add('credentials|'+(Get-FileHash -LiteralPath $accountsPath).Hash)
$packetHash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes(($packet -join [char]10))))
[pscustomobject]@{Mode=$Mode;Target=$binding;ProcessId=25768;Migrations=$applied.Count;ApprovedAccounts=2;ProtectedTables=$protected.Count;C01Files=$c01.Count;PrivateRootAbsent=$true;Port7481Available=$true;PacketSha256=$packetHash;Transport=$transport;LocalPipeExceptionApproved=$true;Writes=$false}
if($Mode -eq 'Inspect') {return}
Assert ($ApprovedPacketSha256 -cmatch '^[A-F0-9]{64}$' -and $ApprovedPacketSha256 -ceq $packetHash) 'Exact reviewed packet approval required before writes.'

try {
    New-PrivateDirectory $root
    foreach($dir in @('config','files','keys','keys\data-protection','results')) {New-PrivateDirectory (Join-Path $root $dir)}
    $settings=Get-Content -LiteralPath $sourceSettings -Raw|ConvertFrom-Json -AsHashtable
    $mobile=$settings.MobileAuthentication;$web=$settings.WebAuthentication
    $mobile.Crypto.KeyVersion='hr-c04'
    $mobile.Crypto.VerifierKeyBase64=[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    $mobile.Crypto.EncryptionKeyBase64=[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    $mobile.Sms=@{Provider='LocalTesting';Endpoint='';BearerToken=''}
    $config=@{
        Urls=$base.AbsoluteUri.TrimEnd('/');AllowedHosts='localhost'
        LocalTesting=@{Enabled=$true;ExistingAcceptanceProfile='HumaHrBusiness20261019C04';PrivateDirectory=$root;AllowUnencryptedLocalPipe=$true}
        ConnectionStrings=@{CosmixDatabase="Server=$pipe;Database=Cosmix_Testing;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=5;Application Name=Cosmix.HrBusiness.C04"}
        PrivateFiles=@{Directory=(Join-Path $root 'files');KeyFile=(Join-Path $root 'keys\files.key');KeyReference='hr-business-c04'}
        Jwt=@{Issuer='Cosmix.HrBusiness.C04';Audience='Cosmix.HrBusiness.C04';SigningKey=[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))}
        MobileAuthentication=$mobile;WebAuthentication=$web
        EmployeeFileScanner=@{Enabled=$false};AttendanceJobs=@{Enabled=$false}
        SelfHostedFace=@{Enabled=$false};FaceCapture=@{Enabled=$false};FaceTemplateStorage=@{Enabled=$false}
        FaceStorageRetention=@{Enabled=$false};FaceStorageRetentionJournal=@{Enabled=$false}
        Cors=@{AllowedOrigins=@('https://localhost:5173')}
    }
    Write-Private 'keys\files.key' ([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    Write-Json 'config\host.json' $config
    $config=$null;$settings=$null;$mobile=$null;$web=$null

    Write-Result 'start.json' @{Target=$binding;PacketSha256=$packetHash;ProtectedTables=$protected;C01Evidence=$c01;CountsBefore=$beforeCounts;Release=$release;Utc=[DateTimeOffset]::UtcNow;MaxHttpRequests=160;BusinessWrites='One marked synthetic department: create, concurrent update and deactivate; bounded audit/history/receipts'}
    $start=[Diagnostics.ProcessStartInfo]::new((Get-Command dotnet -CommandType Application).Source)
    $start.ArgumentList.Add($assembly);$start.UseShellExecute=$false;$start.CreateNoWindow=$true
    $start.WorkingDirectory=$release;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    $start.Environment.Clear()
    foreach($key in @('SystemRoot','WINDIR','USERPROFILE','LOCALAPPDATA','APPDATA','PATH','TEMP','TMP','ProgramFiles','ProgramW6432','DOTNET_ROOT','COMPUTERNAME','USERNAME','USERDOMAIN')) {
        $value=[Environment]::GetEnvironmentVariable($key);if($value){$start.Environment[$key]=$value}
    }
    $start.Environment['COSMIX_EXISTING_TESTING_HOST']='HumaHrBusiness20261019C04'
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
    $code=$departmentCode
    $body=@{code=$code;name='TEST ONLY C04 concurrency department'};$key=$code+'-create'
    Check 'Business request without CSRF denied' ((Request $actor.Client POST '/api/v1/hr/departments' $body '' $key).Status -eq 400)
    $parallelA=New-Client $actor.Cookies;$parallelB=New-Client $actor.Cookies
    $one=Begin-Request $parallelA POST '/api/v1/hr/departments' $body $actor.Csrf $key
    $two=Begin-Request $parallelB POST '/api/v1/hr/departments' $body $actor.Csrf $key
    [Threading.Tasks.Task]::WhenAll([Threading.Tasks.Task[]]@($one.Task,$two.Task)).GetAwaiter().GetResult()|Out-Null
    $a=Finish-Request $one;$b=Finish-Request $two
    Check 'Concurrent same-key requests return one resource' ($a.Status -eq 201 -and $b.Status -eq 201 -and $a.Value.id -eq $b.Value.id)
    $id=$a.Value.id;$script:departmentId=$id;Assert ([guid]::TryParse($id,[ref]([guid]::Empty))) 'Invalid created ID.'
    Check 'Same key with changed payload conflicts' ((Request $actor.Client POST '/api/v1/hr/departments' @{code=$code;name='TEST ONLY C04 changed payload'} $actor.Csrf $key).Status -eq 409)
    $path='/api/v1/hr/departments/'+$id
    $updateA=@{name='TEST ONLY C04 winner A';expectedVersion=$a.Value.version;reason='Synthetic concurrent update A'}
    $updateB=@{name='TEST ONLY C04 winner B';expectedVersion=$a.Value.version;reason='Synthetic concurrent update B'}
    $one=Begin-Request $parallelA PUT $path $updateA $actor.Csrf ($code+'-A')
    $two=Begin-Request $parallelB PUT $path $updateB $actor.Csrf ($code+'-B')
    [Threading.Tasks.Task]::WhenAll([Threading.Tasks.Task[]]@($one.Task,$two.Task)).GetAwaiter().GetResult()|Out-Null
    $a=Finish-Request $one;$b=Finish-Request $two
    Check 'Competing separate HTTP updates reject stale version' ((@($a.Status,$b.Status)|Sort-Object) -join ',' -eq '200,409')
    $winner=if($a.Status -eq 200){$a}else{$b}
    $winningPayload=if($a.Status -eq 200){$updateA}else{$updateB}
    $script:winningKey=if($a.Status -eq 200){$code+'-A'}else{$code+'-B'}
    $current=Request $actor.Client GET $path
    Check 'Fresh request reads persisted winning update' ($current.Status -eq 200 -and $current.Value.name -ceq $winningPayload.name -and $current.Value.name -ceq $winner.Value.name -and $current.Value.version -ceq $winner.Value.version)
    $history=Request $actor.Client GET ($path+'/history?pageSize=10')
    Check 'Only create and winning update retained in history' ($history.Status -eq 200 -and $history.Value.items.Count -eq 2)
    $inactive=Request $actor.Client POST ($path+'/status') @{isActive=$false;expectedVersion=$current.Value.version;reason='Synthetic test complete; retain inactive evidence'} $actor.Csrf ($code+'-inactive')
    Check 'Synthetic department retained inactive' ($inactive.Status -eq 200 -and !$inactive.Value.isActive)
    $sqlCount=Read-Sql ("SELECT COUNT(*) FROM hr.Departments WHERE Id='"+$id+"' AND Code='"+$code+"' AND IsActive=0;")
    Check 'Independent SQL confirms exactly one retained inactive row' ($sqlCount -eq '1')
    $atomicCount=Read-Sql ("SELECT COUNT(*) FROM hr.OrganizationChanges c JOIN audit.AuditEvents a ON a.Id=c.AuditEventId JOIN workflow.IdempotencyReceipts r ON r.ProtectedResultReference=REPLACE(CONVERT(varchar(36),c.Id),'-','') AND r.Operation=c.Operation AND r.ActorUserId=a.ActorUserId WHERE c.ResourceId='"+$id+"' AND r.[Key] LIKE '"+$code+"-%' AND r.CompletedAt IS NOT NULL;")
    Check 'Exactly three business changes retain matching audit and completed receipts' ($atomicCount -eq '3')
    Assert-BusinessState
    Check 'No migration was applied' ((Read-Sql 'SELECT COUNT(*) FROM dbo.__EFMigrationsHistory;') -eq '25')

    foreach($session in $sessions) {
        $retained=$session.Cookies.GetCookieHeader($base)
        Check ($session.Email+': missing-CSRF logout denied') ((Request $session.Client POST '/api/v1/auth/web/logout' @{}).Status -eq 400)
        Check ($session.Email+': authenticated logout') ((Request $session.Client POST '/api/v1/auth/web/logout' @{} $session.Csrf).Status -eq 204)
        $session.LoggedOut=$true
        $jar=[Net.CookieContainer]::new();$jar.SetCookies($base,$retained.Replace(';',','))
        $replay=New-Client $jar
        Check ($session.Email+': revoked cookie denied') ((Request $replay GET '/api/v1/me/permissions').Status -eq 401)
    }
    Assert-Snapshot $protected (Protected-Snapshot) 'Protected database tables'
    Assert-Snapshot $c01 (C01-Snapshot) 'Original C01 evidence'
    $counts=Record-Counts;$delta=@{}
    foreach($name in $beforeCounts.Keys){$delta[$name]=[long]$counts[$name]-[long]$beforeCounts[$name]}
    Assert ($delta.Accounts -eq 0 -and $delta.Migrations -eq 0 -and $delta.Sessions -eq 2 -and $delta.RefreshTokens -eq 2 -and $delta.Challenges -eq 2 -and $delta.Deliveries -eq 2) 'Auth/schema population outside the bounded flow.'
    Assert ((Read-Sql "SELECT COUNT(*) FROM workflow.IdempotencyReceipts WHERE [Key] LIKE 'TEST-C04-20261019%';") -eq '3') 'Unexpected business receipt population.'
    Assert ((Read-Sql ("SELECT COUNT(*) FROM hr.OrganizationChanges WHERE ResourceId='"+$script:departmentId+"';")) -eq '3') 'Unexpected business history population.'
    Write-Result 'acceptance.json' @{Checks=$checks.ToArray();HttpResults=$httpResults.ToArray();Requests=$script:requests;Counts=$counts;CountsBefore=$beforeCounts;CountsDelta=$delta;ProtectedTablesUnchanged=$true;C01ThroughC03Unchanged=$true;MigrationApplied=$false;DepartmentId=$script:departmentId;DepartmentCount=1;AtomicBusinessAuditReceiptCount=3;Scope='Auth, leave queue access and organization concurrency; not full HR acceptance';CompletedUtc=[DateTimeOffset]::UtcNow}
} catch {
    if(Test-Path -LiteralPath (Join-Path $root 'results')) {
        Write-Result 'failure.json' @{Checks=$checks.ToArray();HttpResults=$httpResults.ToArray();Requests=$script:requests;FailureType=$_.Exception.GetType().FullName;Message='Stopped; no reset, repair, extra logout or automatic replay.';Utc=[DateTimeOffset]::UtcNow}
    }
    throw
} finally {
    foreach($client in $clients){$client.Dispose()}
    if($hostProcess -and $script:started) {
        if(!$hostProcess.HasExited){$hostProcess.Kill();$hostProcess.WaitForExit(10000)|Out-Null}
        Write-Private ($resultsRelative+'\host-stdout.log') ([Text.UTF8Encoding]::new($false).GetBytes($stdout.GetAwaiter().GetResult()))
        Write-Private ($resultsRelative+'\host-stderr.log') ([Text.UTF8Encoding]::new($false).GetBytes($stderr.GetAwaiter().GetResult()))
        Write-Result 'host-final.json' @{ProcessId=$hostProcess.Id;Exited=$hostProcess.HasExited;DatabaseStopped=$false;CleanupPerformed=$false}
        $hostProcess.Dispose()
        # Verify preservation again after the owned process has stopped writing.
        Assert-Snapshot $protected (Protected-Snapshot) 'Post-host protected tables'
        Assert-Snapshot $c01 (C01-Snapshot) 'Post-host C01/C02 evidence'
        if(Test-Path -LiteralPath (Join-Path $root 'results\acceptance.json')) {
            Assert-BusinessState
            $finalCounts=Record-Counts
            foreach($name in $counts.Keys){Assert ($counts[$name] -eq $finalCounts[$name]) 'Post-host record population drift.'}
        }
        Write-Result 'post-host-preservation.json' @{ProtectedSnapshots=$protected.Count;RetainedFiles=$c01.Count;Unchanged=$true;Counts=(Record-Counts);Utc=[DateTimeOffset]::UtcNow}
    }
}
