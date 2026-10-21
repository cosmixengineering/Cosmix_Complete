[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ReleaseDirectory,
    [ValidateSet('Inspect','Execute')][string]$Mode='Inspect',
    [string]$ReviewedPacketSha256=''
)
# Inspect performs SELECT-only/private-source inspection. Neither mode is an offline test.
$ErrorActionPreference='Stop'
$VerbosePreference='SilentlyContinue';$DebugPreference='SilentlyContinue'
. (Join-Path $PSScriptRoot 'HrPermissionP1Profile.ps1')
$profile=Get-P1Profile
$root=$profile.Root;$pipe=$profile.Pipe;$owner=$profile.Owner;$base=[uri]($profile.Url+'/')
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$release=[IO.Path]::GetFullPath($ReleaseDirectory)
$assembly=Join-Path $release 'Cosmix.Api.dll'
$accountsPath='C:\Users\Huma.Khan\AppData\Local\Cosmix\LocalTesting\Test-Accounts.json'
$sourceSettings='C:\Users\Huma.Khan\AppData\Local\Cosmix\LocalTesting\Dashboard-20261019-Diagnostic.json'
$resultsRelative='results'
$checks=[Collections.Generic.List[object]]::new()
$httpResults=[Collections.Generic.List[object]]::new()
$clients=[Collections.Generic.List[object]]::new()
$sessions=[Collections.Generic.List[object]]::new()
$historicalIds=@{};$baselineCounts=@{};$budget=@{Requests=0;Logins=0}
$resendTimes=@{}
$bindings=@{};$hostProcess=$null;$started=$false;$created=$false
$common=Join-Path $PSScriptRoot 'Invoke-HrVehicleV1.ps1'
$reportCommon=Join-Path $PSScriptRoot 'Invoke-HrReportsR1.ps1'
# Import definitions only, never invoke either frozen runner's top-level code.
foreach($source in @(
    @{Path=$common;Names=@('Assert','Check','Assert-PrivateSource','New-PrivateDirectory','Write-Private',
        'Write-Json','Write-Result','New-Client','Login','Assert-Snapshot','Record-Counts')},
    @{Path=$reportCommon;Names=@('New-Rows')}
)){
    $tokens=$null;$errors=$null
    $ast=[Management.Automation.Language.Parser]::ParseFile($source.Path,[ref]$tokens,[ref]$errors)
    Assert-P1 ($errors.Count -eq 0) 'Frozen helper parse failed.'
    foreach($name in $source.Names){
        $definitions=@($ast.FindAll({param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -ceq $name},$true))
        Assert-P1 ($definitions.Count -eq 1) 'Frozen helper definition differs.'
        $definition=$definitions[0].Extent.Text
        if($name -eq 'Login'){
            Assert-P1 ($definition.Contains('$i -lt 20')) 'Frozen login polling bound differs.'
            # P1 dispatch is synchronous and scoped to the captured challenge; no polling worker.
            $definition=$definition.Replace('$i -lt 20','$i -lt 1')
        }
        . ([scriptblock]::Create($definition))
    }
}
function New-P1SqlConnection {
    $value=Get-P1SqlConnectionString
    Assert-P1SqlConnectionString $value
    return [System.Data.SqlClient.SqlConnection]::new($value)
}
function Read-Sql([string]$query) {
    # Only runner-owned SELECT statements reach this helper, as in the frozen R1 reader.
    Assert ($query.TrimStart().StartsWith('SELECT ',[StringComparison]::OrdinalIgnoreCase)) 'Only SELECT is allowed.'
    $connection=New-P1SqlConnection
    try {
        $connection.Open()
        $command=$connection.CreateCommand()
        try {
            $command.CommandText=$query;$command.CommandTimeout=20
            $reader=$command.ExecuteReader()
            try {
                Assert ($reader.FieldCount -eq 1) 'Unexpected SQL shape.'
                $rows=[Collections.Generic.List[string]]::new()
                while($reader.Read()){$rows.Add([string]$reader.GetValue(0))}
                Assert (!$reader.NextResult()) 'Unexpected additional SQL result.'
                return ($rows -join [char]10).Trim()
            } finally {$reader.Dispose()}
        } finally {$command.Dispose()}
    } finally {$connection.Dispose()}
}
function Request($client,[string]$method,[string]$path,$body=$null,[string]$csrf='',[string]$key=''){
    Assert (Test-P1Route $method $path $body $key $bindings) 'Unapproved P1 request.'
    Use-P1Budget $budget ($path -ceq '/api/v1/auth/web/login')
    $message=[Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::new($method),$path)
    if($null -ne $body){
        $json=$body|ConvertTo-Json -Depth 12 -Compress
        Assert ([Text.Encoding]::UTF8.GetByteCount($json) -le 8192) 'P1 body exceeds bound.'
        $message.Content=[Net.Http.StringContent]::new($json,[Text.Encoding]::UTF8,'application/json')
    }
    if($csrf){$message.Headers.Add('X-CSRF-TOKEN',$csrf)}
    if($key){$message.Headers.Add('Idempotency-Key',$key)}
    $message.Headers.Add('X-Correlation-ID','HR-P1-'+[guid]::NewGuid().ToString('N'))
    $timeout=[Threading.CancellationTokenSource]::new([timespan]::FromSeconds(45))
    try {
        $response=$client.SendAsync($message,[Net.Http.HttpCompletionOption]::ResponseHeadersRead,$timeout.Token).GetAwaiter().GetResult()
        try {
            $stream=$response.Content.ReadAsStream()
            $memory=[IO.MemoryStream]::new()
            try {
                $buffer=[byte[]]::new(4096)
                while(($n=$stream.ReadAsync($buffer,0,$buffer.Length,$timeout.Token).GetAwaiter().GetResult()) -gt 0){
                    Assert ($memory.Length+$n -le 1MB) 'P1 response exceeds bound.'
                    $memory.Write($buffer,0,$n)
                }
                $text=[Text.Encoding]::UTF8.GetString($memory.ToArray())
            } finally {$memory.Dispose();$stream.Dispose()}
            $value=if($text){ConvertFrom-Json -InputObject $text -AsHashtable -NoEnumerate -DateKind String}else{$null}
            $httpResults.Add(@{Method=$method;Path=$path;Status=[int]$response.StatusCode})
            if($path -ceq '/api/v1/auth/web/login' -and [int]$response.StatusCode -eq 202){
                $null=Get-P1ResendWaitMilliseconds $body.email $value.resendAvailableAt ([datetimeoffset]::UtcNow) $cooldownSeconds $lifetimeSeconds
                $resendTimes[$body.email]=[string]$value.resendAvailableAt
            }
            return @{Status=[int]$response.StatusCode;Value=$value}
        } finally {$response.Dispose()}
    } finally {$timeout.Dispose();$message.Dispose()}
}
function Wait-P1Resend([string]$email){
    Assert ($resendTimes.ContainsKey($email)) 'Missing captured server resend timestamp.'
    $wait=Get-P1ResendWaitMilliseconds $email $resendTimes[$email] ([datetimeoffset]::UtcNow) $cooldownSeconds $lifetimeSeconds
    if($wait -gt 0){Start-Sleep -Milliseconds $wait}
    Assert ([datetimeoffset]::UtcNow -ge [datetimeoffset]$resendTimes[$email]) 'Bounded resend wait ended early; no login attempted.'
}
function Protected-Snapshot {
    $append=@('auth.Roles','auth.Permissions','auth.RolePermissions','auth.UserRoleAssignments','auth.UserAccessScopes',
        'auth.UserSessions','auth.RefreshTokens','auth.MobileOtpChallenges','auth.OtpDeliveries',
        'audit.AuditEvents','workflow.IdempotencyReceipts')
    $tables=(Read-Sql "SELECT (SELECT s.name AS [schema],t.name AS [table] FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE t.is_ms_shipped=0 ORDER BY s.name,t.name FOR JSON PATH);")|ConvertFrom-Json
    $snapshot=@{}
    foreach($table in $tables){
        $plain=$table.schema+'.'+$table.table
        if($plain -cin @('auth.UserAccounts','auth.AuthRateBuckets')){continue}
        $qualified='['+$table.schema.Replace(']',']]')+'].['+$table.table.Replace(']',']]')+']'
        $literal=$qualified.Replace("'","''")
        if(!$baselineCounts.ContainsKey($plain)){$baselineCounts[$plain]=[int](Read-Sql "SELECT COUNT(*) FROM $qualified;")}
        $order=Read-Sql ("SELECT STRING_AGG(CONVERT(nvarchar(max),QUOTENAME(c.name)),',') WITHIN GROUP(ORDER BY ic.key_ordinal) FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE i.object_id=OBJECT_ID(N'"+$literal+"') AND i.is_primary_key=1;")
        Assert ($order -and $order -ne 'NULL') 'Missing deterministic primary key.'
        $where=''
        if($plain -cin $append){
            if(!$historicalIds.ContainsKey($plain)){
                $rows=ConvertFrom-Json -InputObject (Read-Sql "SELECT COALESCE((SELECT Id FROM $qualified ORDER BY Id FOR JSON PATH),N'[]');") -AsHashtable -NoEnumerate
                Assert ($rows -is [array] -and $rows.Count -le 10000) 'Historical ID bound exceeded.'
                Assert ($rows.Count -eq $baselineCounts[$plain]) 'Baseline changed during inspection.'
                $ids=@(foreach($row in $rows){([guid]$row.Id).ToString('D')})
                $historicalIds[$plain]=ConvertTo-Json -InputObject $ids -Compress
                $baselineCounts[$plain]=$ids.Count
            }
            $idJson=$historicalIds[$plain].Replace("'","''")
            $where=" WHERE Id IN(SELECT CONVERT(uniqueidentifier,[value]) FROM OPENJSON(N'$idJson'))"
        }
        # All business tables, outbox, files and existing access/auth history are protected.
        $snapshot[$plain]=Read-Sql "SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM $qualified$where ORDER BY $order FOR JSON PATH,INCLUDE_NULL_VALUES)),2);"
    }
    foreach($actor in $actors){
        $id=([guid]$actor.Id).ToString('D')
        $exceptions="'FailedLoginCount','LockedUntil','RowVersion'"
        if($id -cin @($profile.AdminAccount,$profile.HrAccount)){$exceptions+=",'SecurityVersion'"}
        $cols=Read-Sql "SELECT STRING_AGG(CONVERT(nvarchar(max),QUOTENAME(name)),',') WITHIN GROUP(ORDER BY column_id) FROM sys.columns WHERE object_id=OBJECT_ID('auth.UserAccounts') AND name NOT IN($exceptions);"
        $snapshot["account-$id"]=Read-Sql "SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT $cols FROM auth.UserAccounts WHERE Id='$id' FOR JSON PATH,INCLUDE_NULL_VALUES)),2);"
    }
    $rates=$originalRateKeys.Replace("'","''")
    $rateAllowed=@('web-request-global','web-password-global','web-verify-global','request-global')
    foreach($actor in $actors){foreach($prefix in @('web-request-account:','web-password-account:','web-verify-account:')){$rateAllowed+=($prefix+$actor.Id)}}
    $rateSql=($rateAllowed|ForEach-Object {"'$_'"}) -join ','
    $snapshot['unrelated-existing-rates']=Read-Sql "SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM auth.AuthRateBuckets WHERE [Key] NOT IN($rateSql) AND [Key] IN(SELECT [value] FROM OPENJSON(N'$rates')) ORDER BY [Key] FOR JSON PATH,INCLUDE_NULL_VALUES)),2);"
    $snapshot['account-population']=Read-Sql "SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT Id FROM auth.UserAccounts ORDER BY Id FOR JSON PATH)),2);"
    return $snapshot
}
function Prior-Snapshot {
    $snapshot=@{}
    foreach($batch in @('Huma20261019C01','Huma20261019C02','Huma20261019C03','Huma20261019C04','Huma20261020V1','Huma20261020R1','Huma20261020P1')){
        $prior=Join-Path ([IO.Path]::GetDirectoryName($root)) $batch
        Assert (Test-Path -LiteralPath $prior -PathType Container) 'Prior evidence missing.'
        foreach($item in @(Get-Item -LiteralPath $prior)+@(Get-ChildItem -LiteralPath $prior -Recurse -Force)){
            Assert (!($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) 'Linked prior evidence.'
            if(!$item.PSIsContainer){
                Assert-PrivateSource $item.FullName ($item.Name -cin @('host-stdout.log','host-stderr.log'))
                $snapshot[$item.FullName]=(Get-FileHash -LiteralPath $item.FullName).Hash
            }
        }
    }
    foreach($path in @($accountsPath,$sourceSettings)){
        Assert-PrivateSource $path
        $snapshot[$path]=(Get-FileHash -LiteralPath $path).Hash
    }
    return $snapshot
}
function Invoke-CommandPair($command){
    $first=Request $sa.Client POST $command.Path $command.Body $sa.Csrf $command.Key
    Check ($command.Key+': success') ($first.Status -eq 200)
    $retry=Request $sa.Client POST $command.Path $command.Body $sa.Csrf $command.Key
    Check ($command.Key+': exact replay') ($retry.Status -eq 200 -and
        ($first.Value|ConvertTo-Json -Depth 12 -Compress) -ceq ($retry.Value|ConvertTo-Json -Depth 12 -Compress))
    return $first.Value
}
function Invoke-Provisioning($versions){
    $roleVersions=@('AAAAAAAAAAA=','AAAAAAAAAAA=')
    for($i=0;$i -lt 4;$i++){
        $commands=Get-P1Commands $bindings $roleVersions[0] $roleVersions[1] $versions[0] $versions[1]
        $result=Invoke-CommandPair $commands[$i]
        if($i -lt 2){
            $id=([guid]$result.id).ToString('D')
            Assert ([guid]$id -ne [guid]::Empty) 'Missing role ID.'
            if($i -eq 0){$bindings.AdminRole=$id}else{$bindings.RequesterRole=$id}
        }
        $roleVersions[$i%2]=$result.version
    }
    $from=[datetimeoffset]::UtcNow
    $bindings.EffectiveFrom=$from.ToString('o');$bindings.EffectiveTo=$from.AddHours(24).ToString('o')
    $commands=Get-P1Commands $bindings $roleVersions[0] $roleVersions[1] $versions[0] $versions[1]
    $results=@()
    foreach($i in @(4,5)){
        $result=Invoke-CommandPair $commands[$i]
        $result.assignment.id=([guid]$result.assignment.id).ToString('D')
        $results+=,$result
    }
    return ,$results
}
function Verify-P1Bounds {
    $limits=@{'auth.Roles'=2;'auth.Permissions'=5;'auth.RolePermissions'=6;'auth.UserRoleAssignments'=2;
        'auth.UserAccessScopes'=2;'workflow.IdempotencyReceipts'=6;'auth.UserSessions'=5;
        'auth.RefreshTokens'=5;'auth.MobileOtpChallenges'=5;'auth.OtpDeliveries'=5;'audit.AuditEvents'=200}
    foreach($table in $limits.Keys){
        $new=New-Rows $table
        Assert ([int](Read-Sql "SELECT COUNT(*) FROM $table WHERE $new;") -le $limits[$table]) "Partial execution exceeded $table bound."
    }
    foreach($actor in $actors){
        $current=[int](Read-Sql "SELECT SecurityVersion FROM auth.UserAccounts WHERE Id='$($actor.Id)';")
        $maximum=if($actor.Id -ceq $saActor.Id){0}else{1}
        Assert ($current -ge [int]$actor.SecurityVersion -and $current -le [int]$actor.SecurityVersion+$maximum) 'Partial account security increment exceeded.'
    }
    Assert ($budget.Requests -le 100 -and $budget.Logins -le 5) 'Request/session budget exceeded.'
    Assert ([int](Read-Sql 'SELECT COUNT(*) FROM auth.AuthRateBuckets;') -le $beforeCounts.RateBuckets+50) 'Rate bucket bound exceeded.'
}
function Verify-P1State {
    $expected=@{'auth.Roles'=2;'auth.Permissions'=5;'auth.RolePermissions'=6;'auth.UserRoleAssignments'=2;
        'auth.UserAccessScopes'=2;'workflow.IdempotencyReceipts'=6;'auth.UserSessions'=5;
        'auth.RefreshTokens'=5;'auth.MobileOtpChallenges'=5;'auth.OtpDeliveries'=5}
    foreach($table in $expected.Keys){
        $new=New-Rows $table
        Assert ([int](Read-Sql "SELECT COUNT(*) FROM $table WHERE $new;") -eq $expected[$table]) "Unexpected new $table rows."
    }
    $saId=([guid]$saActor.Id).ToString('D')
    foreach($index in @(0,1)){
        $role=if($index -eq 0){$bindings.AdminRole}else{$bindings.RequesterRole}
        $code=if($index -eq 0){$profile.AdminCode}else{$profile.RequesterCode}
        $permissions=if($index -eq 0){$profile.AdminPermissions}else{$profile.RequesterPermissions}
        $account=$actors[$index]
        $assignment=$assignmentResults[$index].assignment.id
        Assert ((Read-Sql "SELECT COUNT(*) FROM auth.Roles WHERE Id='$role' AND Code='$code' AND Name='$code' AND SystemRole=0 AND IsActive=1 AND CreatedByUserId='$saId';") -eq '1') 'Role binding differs.'
        $actual=(Read-Sql "SELECT p.Code FROM auth.RolePermissions rp JOIN auth.Permissions p ON p.Id=rp.PermissionId WHERE rp.RoleId='$role' AND rp.GrantedByUserId='$saId' AND rp.RevokedAt IS NULL ORDER BY p.Code;") -split '\r?\n'
        Assert (($actual -join '|') -ceq (($permissions|Sort-Object -CaseSensitive) -join '|')) 'Role grants differ.'
        $from=$bindings.EffectiveFrom;$to=$bindings.EffectiveTo
        Assert ((Read-Sql "SELECT COUNT(*) FROM auth.UserRoleAssignments WHERE Id='$assignment' AND RoleId='$role' AND UserAccountId='$($account.Id)' AND EmployeeId='$($account.EmployeeId)' AND AssignedByUserId='$saId' AND EffectiveFrom=CONVERT(datetimeoffset,'$from') AND EffectiveTo=CONVERT(datetimeoffset,'$to') AND RevokedAt IS NULL;") -eq '1') 'Assignment binding differs.'
        $scope=if($index -eq 0){"Kind=7 AND EmployeeId IS NULL"}else{"Kind=2 AND EmployeeId='$($profile.HrEmployee)'"}
        Assert ((Read-Sql "SELECT COUNT(*) FROM auth.UserAccessScopes WHERE UserRoleAssignmentId='$assignment' AND $scope AND DepartmentId IS NULL AND TeamId IS NULL AND SiteId IS NULL AND ProjectId IS NULL;") -eq '1') 'Assignment scope differs.'
    }
    $new=New-Rows 'workflow.IdempotencyReceipts'
    $receipts=(Read-Sql "SELECT (SELECT [Key],Operation,ActorUserId,CompletedAt FROM workflow.IdempotencyReceipts WHERE $new ORDER BY [Key] FOR JSON PATH);")|ConvertFrom-Json
    $operations=@{'P1-create-admin'='access.role.created';'P1-create-requester'='access.role.created';
        'P1-grant-admin'='access.role.permissions.granted';'P1-grant-requester'='access.role.permissions.granted';
        'P1-assign-admin'='access.assignment.created';'P1-assign-requester'='access.assignment.created'}
    Assert (@($receipts.Key|Select-Object -Unique).Count -eq 6) 'Receipt keys differ.'
    foreach($receipt in $receipts){
        Assert ($operations.ContainsKey($receipt.Key) -and $receipt.Operation -ceq $operations[$receipt.Key] -and
            $receipt.ActorUserId -ceq $saId -and $receipt.CompletedAt) 'Receipt binding differs.'
    }
    foreach($actor in $actors){
        $increment=if($actor.Id -ceq $saId){0}else{1}
        Assert ((Read-Sql "SELECT SecurityVersion FROM auth.UserAccounts WHERE Id='$($actor.Id)';") -eq [string]([int]$actor.SecurityVersion+$increment)) 'Account security increment differs.'
        Assert ((Read-Sql "SELECT COUNT(*) FROM auth.UserAccounts WHERE Id='$($actor.Id)' AND FailedLoginCount=0 AND LockedUntil IS NULL;") -eq '1') 'Unexpected login failure/lock.'
    }
    $sessionIds=(@($sessions|ForEach-Object {"'"+([guid]$_.Id).ToString('D')+"'"}) -join ',')
    $new=New-Rows 'auth.UserSessions'
    Assert ((Read-Sql "SELECT COUNT(*) FROM auth.UserSessions WHERE $new AND Id NOT IN($sessionIds);") -eq '0') 'Uncaptured session.'
    $accountIds=(@($actors|ForEach-Object {"'"+([guid]$_.Id).ToString('D')+"'"}) -join ',')
    Assert ((Read-Sql "SELECT COUNT(*) FROM auth.UserSessions WHERE $new AND UserAccountId NOT IN($accountIds);") -eq '0') 'Foreign session.'
    Assert ((Read-Sql "SELECT COUNT(*) FROM auth.UserSessions s WHERE $new AND s.RevokedAt IS NULL AND s.SecurityVersion=(SELECT a.SecurityVersion FROM auth.UserAccounts a WHERE a.Id=s.UserAccountId);") -eq '0') 'A new session retains authority after logout.'
    $new=New-Rows 'auth.RefreshTokens'
    Assert ((Read-Sql "SELECT COUNT(*) FROM auth.RefreshTokens WHERE $new AND UserSessionId NOT IN($sessionIds);") -eq '0') 'Uncaptured refresh-token family.'
    $new=New-Rows 'auth.MobileOtpChallenges'
    Assert ((Read-Sql "SELECT COUNT(*) FROM auth.MobileOtpChallenges WHERE $new AND (UserAccountId NOT IN($accountIds) OR Purpose<>'WebLogin' OR ConsumedAt IS NULL);") -eq '0') 'Unexpected OTP challenge.'
    $new=New-Rows 'auth.OtpDeliveries'
    Assert ((Read-Sql "SELECT COUNT(*) FROM auth.OtpDeliveries WHERE $new AND (Status<>1 OR Attempts<>1 OR ProtectedPayload IS NOT NULL);") -eq '0') 'OTP delivery differs.'
    $newChallenges=(New-Rows 'auth.MobileOtpChallenges').Replace('Id NOT IN','c.Id NOT IN')
    Assert ((Read-Sql "SELECT COUNT(*) FROM auth.OtpDeliveries d WHERE $new AND NOT EXISTS(SELECT 1 FROM auth.MobileOtpChallenges c WHERE c.Id=d.ChallengeId AND $newChallenges);") -eq '0') 'Delivery references a pre-existing challenge.'
    $new=New-Rows 'audit.AuditEvents'
    Assert ([int](Read-Sql "SELECT COUNT(*) FROM audit.AuditEvents WHERE $new;") -le 200) 'Audit bound exceeded.'
    Assert ((Read-Sql "SELECT COUNT(*) FROM audit.AuditEvents WHERE $new AND CorrelationId NOT LIKE 'HR-P1-%';") -eq '0') 'Unrelated new audit.'
    Assert ([int](Read-Sql 'SELECT COUNT(*) FROM auth.AuthRateBuckets;') -le $beforeCounts.RateBuckets+50) 'Rate bucket bound exceeded.'
    Assert-Snapshot $protected (Protected-Snapshot) 'Protected SQL'
    Assert-Snapshot $prior (Prior-Snapshot) 'Retained private files'
}

Assert ($PSVersionTable.PSVersion -ge [version]'7.5' -and $IsWindows) 'PowerShell 7.5+ on Windows required.'
Assert ([Security.Principal.WindowsIdentity]::GetCurrent().Name -ieq $owner) 'Unexpected operator.'
Assert ($release.StartsWith('D:\CosmixVerification\',[StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $assembly -PathType Leaf)) 'Reviewed frozen Release required.'
Assert (!(Test-Path -LiteralPath $root)) 'P1 private root already exists; no reset/adoption.'
Assert (@(Get-NetTCPConnection -State Listen -LocalPort 7481 -ErrorAction SilentlyContinue).Count -eq 0) 'Port 7481 occupied.'
foreach($path in @($root,$release)){
    for($part=$path;$part;$part=[IO.Path]::GetDirectoryName($part)){
        if(Test-Path -LiteralPath $part){Assert (!((Get-Item -LiteralPath $part).Attributes -band [IO.FileAttributes]::ReparsePoint)) 'Linked ancestor forbidden.'}
    }
}
$process=Get-CimInstance Win32_Process -Filter 'ProcessId=25768'
Assert ($process -and $process.Name -eq 'sqlservr.exe' -and $process.CommandLine.Contains('-sLOCALDB#9F3001F7') -and $process.CommandLine.Contains('\Instances\MSSQLLocalDB\master.mdf')) 'SQL process differs.'
$processOwner=Invoke-CimMethod -InputObject $process -MethodName GetOwner
Assert ($processOwner.ReturnValue -eq 0 -and ($processOwner.Domain+'\'+$processOwner.User) -ieq $owner) 'SQL process owner differs.'
$observed=(Read-Sql "SELECT (SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) AS Server,DB_NAME() AS [Database],SUSER_SNAME(d.owner_sid) AS Owner,ORIGINAL_LOGIN() AS Operator,r.family_guid AS Family,CONVERT(int,SERVERPROPERTY('ProcessID')) AS ProcessId,d.state_desc AS State,d.user_access_desc AS Access,d.is_read_only AS ReadOnly,(SELECT COUNT(*) FROM sys.triggers WHERE is_disabled=0 AND is_ms_shipped=0) AS Triggers,(SELECT COUNT(*) FROM sys.synonyms) AS Synonyms,(SELECT COUNT(*) FROM sys.sql_expression_dependencies WHERE referenced_server_name IS NOT NULL OR referenced_database_name IS NOT NULL) AS ExternalDependencies FROM sys.databases d JOIN sys.database_recovery_status r ON r.database_id=d.database_id WHERE d.database_id=DB_ID() FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);")|ConvertFrom-Json -AsHashtable
$observed.Files=@((Read-Sql 'SELECT physical_name FROM sys.database_files ORDER BY file_id;') -split '\r?\n')
$transport=(Read-Sql 'SELECT CONCAT(net_transport,''|'',encrypt_option) FROM sys.dm_exec_connections WHERE session_id=@@SPID;') -split '\|'
$observed.Transport=$transport[0];$observed.Encryption=$transport[1]
$observed.Root=$root;$observed.Url=$profile.Url;$observed.RootExists=$false;$observed.PortOccupied=$false;$observed.LinkedPath=$false
$observed.AccountCount=[int](Read-Sql 'SELECT COUNT(*) FROM auth.UserAccounts;')
$observed.MarkerCount=[int](Read-Sql "SELECT (SELECT COUNT(*) FROM auth.Roles WHERE Code LIKE 'TEST-HR-P1-20261020%')+(SELECT COUNT(*) FROM workflow.IdempotencyReceipts WHERE [Key] LIKE 'P1-%');")
$codes=(@(($profile.AdminPermissions+$profile.RequesterPermissions)|Select-Object -Unique)|ForEach-Object {"'$_'"}) -join ','
$observed.MissingPermissionCount=5-[int](Read-Sql "SELECT COUNT(*) FROM auth.Permissions WHERE Code IN($codes);")
Assert-P1Target $observed
$expected=@(Get-ChildItem -LiteralPath (Join-Path $repo 'src/Cosmix.Infrastructure/Database/Migrations') -Filter '*.cs'|Where-Object Name -Match '^\d{14}_[^.]+\.cs$'|Sort-Object Name|ForEach-Object BaseName)
$applied=(Read-Sql 'SELECT MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;') -split '\r?\n'
Assert-P1Schema $expected $applied
Assert ($applied[-1] -ceq '20261015144424_AddFinancialAdjustmentsAndOutstationDeployments') 'Final schema differs.'
$actors=ConvertFrom-Json -InputObject (Read-Sql "SELECT (SELECT a.Id,a.EmployeeId,a.LoginName,a.Status,a.WebAccessEnabled,a.MfaRequired,a.SecurityVersion,e.ApprovedLoginPhoneNumber,e.FullLegalName,CAST(CASE WHEN a.LockedUntil IS NULL OR a.LockedUntil<=SYSUTCDATETIME() THEN 1 ELSE 0 END AS bit) AS Unlocked FROM auth.UserAccounts a JOIN hr.Employees e ON e.Id=a.EmployeeId ORDER BY a.LoginName FOR JSON PATH);") -AsHashtable -NoEnumerate
Assert-P1Actors $actors
foreach($actor in $actors){
    $actor.Id=([guid]$actor.Id).ToString('D')
    $actor.EmployeeId=([guid]$actor.EmployeeId).ToString('D')
}
$saActor=$actors[2]
# Web MFA only calls binding.Verify; UserAccount.PhoneVerifiedAt is not changed by this login path.
$activePhones=[int](Read-Sql 'SELECT COUNT(*) FROM auth.LoginPhoneBindings WHERE RevokedAt IS NULL;')
$approvedPhones=@(foreach($actor in $actors){
    [int](Read-Sql "SELECT COUNT(*) FROM auth.LoginPhoneBindings b JOIN auth.UserAccounts a ON a.Id=b.UserAccountId JOIN hr.EmployeeOnboardingDecisions d ON d.Id=a.ApprovalDecisionId JOIN hr.EmployeeSubmissions s ON s.Id=d.SubmissionId LEFT JOIN auth.LoginPhoneChanges c ON c.Id=b.LoginPhoneChangeId WHERE a.Id='$($actor.Id)' AND a.Status=3 AND d.EmployeeId=a.EmployeeId AND d.Decision=1 AND d.DecidedAt<=SYSUTCDATETIME() AND s.EmployeeId=a.EmployeeId AND b.ApprovedSubmissionId=s.Id AND b.PhoneNumber='$($actor.ApprovedLoginPhoneNumber)' AND b.RevokedAt IS NULL AND b.CreatedAt<=SYSUTCDATETIME() AND b.VerifiedAt IS NOT NULL AND b.VerifiedAt>=b.CreatedAt AND b.VerifiedAt<=SYSUTCDATETIME() AND ((b.LoginPhoneChangeId IS NULL AND b.PhoneNumber=s.ApprovedLoginPhoneNumber) OR (c.UserAccountId=a.Id AND c.NewPhoneNumber=b.PhoneNumber AND c.CompletionChallengeId IS NOT NULL AND c.SupersededAt IS NULL AND c.ApprovedAt<=b.CreatedAt AND c.CompletedAt<=SYSUTCDATETIME() AND c.CompletedAt=b.VerifiedAt));")
})
Assert-P1PhoneCounts $activePhones $approvedPhones
foreach($item in @(@{Id=$actors[0].Id;Kind=1},@{Id=$saActor.Id;Kind=2})){
    Assert ([int](Read-Sql "SELECT COUNT(*) FROM auth.UserRoleAssignments u JOIN auth.Roles r ON r.Id=u.RoleId WHERE u.UserAccountId='$($item.Id)' AND r.SystemRole=$($item.Kind) AND r.IsActive=1 AND u.EffectiveFrom<=SYSUTCDATETIME() AND (u.EffectiveTo IS NULL OR SYSUTCDATETIME()<u.EffectiveTo) AND (u.RevokedAt IS NULL OR SYSUTCDATETIME()<u.RevokedAt);") -gt 0) 'Required genuine fixed authority absent.'
}
$required=@('access.roles.create','access.roles.permissions.manage','access.accounts.read','access.accounts.assign')
$requiredSql=($required|ForEach-Object {"'$_'"}) -join ','
Assert ((Read-Sql "SELECT COUNT(DISTINCT p.Code) FROM auth.UserRoleAssignments u JOIN auth.Roles r ON r.Id=u.RoleId JOIN auth.RolePermissions rp ON rp.RoleId=r.Id JOIN auth.Permissions p ON p.Id=rp.PermissionId JOIN auth.UserAccessScopes s ON s.UserRoleAssignmentId=u.Id WHERE u.UserAccountId='$($saActor.Id)' AND r.IsActive=1 AND s.Kind=7 AND p.Code IN($requiredSql) AND u.EffectiveFrom<=SYSUTCDATETIME() AND (u.EffectiveTo IS NULL OR SYSUTCDATETIME()<u.EffectiveTo) AND (u.RevokedAt IS NULL OR SYSUTCDATETIME()<u.RevokedAt) AND rp.RevokedAt IS NULL AND rp.GrantedAt<=SYSUTCDATETIME();") -eq '4') 'Existing SA provisioning grants absent.'
Assert ((Read-Sql 'SELECT COUNT(*) FROM auth.OtpDeliveries WHERE Status NOT IN(1,2,3);') -eq '0') 'Nonterminal existing OTP work.'
Assert ((Read-Sql "SELECT COUNT(*) FROM auth.MobileOtpChallenges WHERE Purpose='WebLogin' AND ConsumedAt IS NULL AND RevokedAt IS NULL;") -eq '0') 'Existing challenge would be mutated.'
Assert-PrivateSource $accountsPath
Assert-PrivateSource $sourceSettings
$authLimits=(Get-Content -LiteralPath $sourceSettings -Raw|ConvertFrom-Json -AsHashtable).MobileAuthentication
$cooldownSeconds=[int]$authLimits.ResendCooldownSeconds
$lifetimeSeconds=[int]$authLimits.OtpLifetimeSeconds
Assert-P1CooldownLimits $cooldownSeconds $lifetimeSeconds
$authLimits=$null
$credentials=Get-Content -LiteralPath $accountsPath -Raw|ConvertFrom-Json
Assert (@($credentials).Count -eq 3) 'Credential population differs.'
foreach($actor in $actors){
    $credential=@($credentials|Where-Object Email -CEQ $actor.LoginName)
    Assert ($credential.Count -eq 1 -and $credential[0].Password -is [string] -and $credential[0].Password.Length -gt 0 -and $credential[0].Password.Length -le 1024) 'Credential binding differs.'
}
$rateRows=ConvertFrom-Json -InputObject (Read-Sql 'SELECT (SELECT [Key] FROM auth.AuthRateBuckets ORDER BY [Key] FOR JSON PATH);') -NoEnumerate
Assert (@($rateRows).Count -le 10000) 'Rate baseline bound exceeded.'
$originalRateKeys=ConvertTo-Json -InputObject @($rateRows|ForEach-Object Key) -Compress
$protected=Protected-Snapshot;$prior=Prior-Snapshot;$beforeCounts=Record-Counts
$packet=[Collections.Generic.List[string]]::new()
foreach($file in Get-ChildItem -LiteralPath $release -Recurse -Force){
    Assert (!($file.Attributes -band [IO.FileAttributes]::ReparsePoint)) 'Linked release asset.'
    if(!$file.PSIsContainer){$packet.Add($file.FullName+'|'+(Get-FileHash -LiteralPath $file.FullName).Hash)}
}
foreach($file in @($PSCommandPath,$common,$reportCommon,(Join-Path $PSScriptRoot 'HrPermissionP1Profile.ps1'),
    (Join-Path $repo 'src/Cosmix.Api/Program.cs'),(Join-Path $repo 'src/Cosmix.Api/Development/LocalTesting.cs'))+
    @(Get-ChildItem -LiteralPath (Join-Path $repo 'src/Cosmix.Api/Development') -Filter 'HrPermissionP1*.cs'|ForEach-Object FullName)){
    $packet.Add($file+'|'+(Get-FileHash -LiteralPath $file).Hash)
}
foreach($name in $protected.Keys|Sort-Object){$packet.Add('protected|'+$name+'|'+$protected[$name])}
foreach($name in $prior.Keys|Sort-Object){$packet.Add('prior|'+$name+'|'+$prior[$name])}
$packet.Add('actors|'+($actors|ConvertTo-Json -Depth 8 -Compress))
$packet.Add('auth-counts|'+($beforeCounts|ConvertTo-Json -Compress))
$packet.Add('counts|'+(($baselineCounts.GetEnumerator()|Sort-Object Key|ForEach-Object {"$($_.Key)=$($_.Value)"}) -join '|'))
$packetHash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes((($packet|Sort-Object -CaseSensitive) -join [char]10))))
[pscustomobject]@{Mode=$Mode;Target=$observed.Server;Database=$observed.Database;Migrations=25;PrivateRootAbsent=$true;PortAvailable=$true;PacketSha256=$packetHash;MaxHttpRequests=100;MaxMfaSessions=5;Writes=$false}
if($Mode -eq 'Inspect'){$credentials=$null;return}
Assert ($ReviewedPacketSha256 -cmatch '^[A-F0-9]{64}$' -and $ReviewedPacketSha256 -ceq $packetHash) 'Reviewed packet changed; no writes.'
try {
    New-PrivateDirectory $root;$created=$true
    foreach($dir in @('config','files','keys','keys\data-protection','results')){New-PrivateDirectory (Join-Path $root $dir)}
    $source=Get-Content -LiteralPath $sourceSettings -Raw|ConvertFrom-Json -AsHashtable
    $mobile=$source.MobileAuthentication
    $mobile.Crypto.KeyVersion='hr-p1'
    $mobile.Crypto.VerifierKeyBase64=[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    $mobile.Crypto.EncryptionKeyBase64=[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    $mobile.Sms=@{Provider='LocalTesting';Endpoint='';BearerToken=''}
    $config=@{
        Urls=$profile.Url;AllowedHosts='localhost'
        P1=@{SuperAdminAccount=$saActor.Id;SuperAdminEmployee=$saActor.EmployeeId}
        LocalTesting=@{Enabled=$true;ExistingAcceptanceProfile=$profile.Profile;PrivateDirectory=$root;AllowUnencryptedLocalPipe=$true}
        ConnectionStrings=@{CosmixDatabase="Server=$pipe;Database=Cosmix_Testing;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=5;Application Name=Cosmix.HrPermissions.P1"}
        PrivateFiles=@{Directory=(Join-Path $root 'files');KeyFile=(Join-Path $root 'keys\files.key');KeyReference='hr-permissions-p1'}
        Jwt=@{Issuer=$profile.Application;Audience=$profile.Application;SigningKey=[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))}
        MobileAuthentication=$mobile;WebAuthentication=$source.WebAuthentication
        EmployeeFileScanner=@{Enabled=$false};AttendanceJobs=@{Enabled=$false}
        SelfHostedFace=@{Enabled=$false};FaceCapture=@{Enabled=$false};FaceTemplateStorage=@{Enabled=$false}
        FaceStorageRetention=@{Enabled=$false};FaceStorageRetentionJournal=@{Enabled=$false}
        Cors=@{AllowedOrigins=@('https://localhost:5173')}
    }
    Write-Private 'keys\files.key' ([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    Write-Json 'config\host.json' $config
    $config=$null;$source=$null;$mobile=$null
    Write-Result 'start.json' @{Target=$observed;PacketSha256=$packetHash;ProtectedSnapshots=$protected;PriorFiles=$prior;CountsBefore=$beforeCounts;AccessCounts=$baselineCounts;Release=$release;Utc=[datetimeoffset]::UtcNow}
    $start=[Diagnostics.ProcessStartInfo]::new((Get-Command dotnet -CommandType Application).Source)
    $start.ArgumentList.Add($assembly);$start.UseShellExecute=$false;$start.CreateNoWindow=$true
    $start.WorkingDirectory=$release;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    $start.Environment.Clear()
    foreach($key in @('SystemRoot','WINDIR','USERPROFILE','LOCALAPPDATA','APPDATA','PATH','TEMP','TMP','ProgramFiles','ProgramW6432','DOTNET_ROOT','COMPUTERNAME','USERNAME','USERDOMAIN')){
        $value=[Environment]::GetEnvironmentVariable($key);if($value){$start.Environment[$key]=$value}
    }
    $start.Environment['COSMIX_EXISTING_TESTING_HOST']=$profile.Profile
    $start.Environment['COSMIX_LOCAL_TESTING_SETTINGS']=Join-Path $root 'config\host.json'
    $start.Environment['ASPNETCORE_ENVIRONMENT']='Development'
    $hostProcess=[Diagnostics.Process]::new();$hostProcess.StartInfo=$start
    Assert ($hostProcess.Start()) 'Owned P1 host failed to start.'
    $started=$true
    $stdout=$hostProcess.StandardOutput.ReadToEndAsync();$stderr=$hostProcess.StandardError.ReadToEndAsync()
    $ready=$false
    for($i=0;$i -lt 60;$i++){
        Assert (!$hostProcess.HasExited) 'Owned host exited during startup.'
        $listeners=@(Get-NetTCPConnection -State Listen -LocalPort 7481 -ErrorAction SilentlyContinue)
        if($listeners.Count){
            Assert (@($listeners|Where-Object {$_.OwningProcess -ne $hostProcess.Id -or $_.LocalAddress -notin @('127.0.0.1','::1')}).Count -eq 0) 'Listener ownership differs.'
            $ready=$true;break
        }
        Start-Sleep -Milliseconds 500
    }
    Assert $ready 'Owned host startup timeout.'
    $sa=Login $credentials 'superadmin@example.invalid'
    $oldAdmin=Login $credentials 'admin@example.invalid'
    $oldHr=Login $credentials 'hr@example.invalid'
    $versions=@()
    foreach($id in @($profile.AdminAccount,$profile.HrAccount)){
        $read=Request $sa.Client GET "/api/v1/access/accounts/$id"
        Check 'account version read' ($read.Status -eq 200 -and $read.Value.id -ceq $id)
        $versions+=,$read.Value.version
    }
    $assignmentResults=Invoke-Provisioning $versions
    foreach($session in @($oldAdmin,$oldHr)){
        Check ($session.Email+': previous session invalidated') ((Request $session.Client GET '/api/v1/me/permissions').Status -eq 401)
    }
    Wait-P1Resend 'admin@example.invalid'
    $admin=Login $credentials 'admin@example.invalid'
    Wait-P1Resend 'hr@example.invalid'
    $hr=Login $credentials 'hr@example.invalid';$credentials=$null
    foreach($entry in @(@{Session=$admin;Index=0;Codes=$profile.AdminPermissions;Kind=7;Target=$null},
        @{Session=$hr;Index=1;Codes=$profile.RequesterPermissions;Kind=2;Target=$profile.HrEmployee})){
        $assignment=$assignmentResults[$entry.Index].assignment.id
        $grants=@($entry.Session.Grants|Where-Object {$_.roleAssignmentId -ceq $assignment})
        Check 'new assignment exact permissions/scope' ($grants.Count -eq $entry.Codes.Count -and
            (($grants.permission|Sort-Object -CaseSensitive) -join '|') -ceq (($entry.Codes|Sort-Object -CaseSensitive) -join '|') -and
            @($grants|Where-Object {$_.scopeKind -ne $entry.Kind -or $_.targetId -cne $entry.Target}).Count -eq 0)
    }
    foreach($session in @($admin,$hr,$sa)){
        $retained=$session.Cookies.GetCookieHeader($base)
        Check 'logout requires CSRF' ((Request $session.Client POST '/api/v1/auth/web/logout' @{}).Status -eq 400)
        Check 'authenticated logout' ((Request $session.Client POST '/api/v1/auth/web/logout' @{} $session.Csrf).Status -eq 204)
        $session.LoggedOut=$true
        $jar=[Net.CookieContainer]::new();$jar.SetCookies($base,$retained.Replace(';',','))
        $replay=New-Client $jar
        Check 'logged-out retained cookie denied' ((Request $replay GET '/api/v1/me/permissions').Status -eq 401)
    }
    Verify-P1State
    Write-Result 'acceptance.json' @{Checks=$checks.ToArray();HttpResults=$httpResults.ToArray();Budget=$budget;Counts=(Record-Counts);Bindings=$bindings;Assignments=$assignmentResults;ProtectedSnapshotsUnchanged=$true;PriorFilesUnchanged=$true;MigrationsApplied=$false;BusinessRoutesExercised=$false;Utc=[datetimeoffset]::UtcNow}
} catch {
    if($created -and (Test-Path -LiteralPath (Join-Path $root 'results'))){
        Write-Result 'failure.json' @{Checks=$checks.ToArray();HttpResults=$httpResults.ToArray();Budget=$budget;FailureType=$_.Exception.GetType().FullName;Message='Stopped without reset, repair or automatic replay.';Utc=[datetimeoffset]::UtcNow}
    }
    throw 'P1 stopped. Retain private evidence and review partial state; no automatic rerun.'
} finally {
    $credentials=$null
    foreach($client in $clients){$client.Dispose()}
    if($started){
        if(!$hostProcess.HasExited){$hostProcess.Kill();$hostProcess.WaitForExit(10000)|Out-Null}
        Assert $hostProcess.HasExited 'Owned process did not exit; manual review required.'
        Write-Private 'results\host-stdout.log' ([Text.UTF8Encoding]::new($false).GetBytes($stdout.GetAwaiter().GetResult()))
        Write-Private 'results\host-stderr.log' ([Text.UTF8Encoding]::new($false).GetBytes($stderr.GetAwaiter().GetResult()))
        Write-Result 'host-final.json' @{ProcessId=$hostProcess.Id;Exited=$true;DatabaseStopped=$false;CleanupPerformed=$false}
        $hostProcess.Dispose()
    }
    if($created){
        Assert-Snapshot $protected (Protected-Snapshot) 'Post-host protected SQL'
        Assert-Snapshot $prior (Prior-Snapshot) 'Post-host prior files'
        Verify-P1Bounds
        Write-Result 'post-host-preservation.json' @{ProtectedSnapshots=$protected;Unchanged=$true;Counts=(Record-Counts);Budget=$budget}
    }
}
