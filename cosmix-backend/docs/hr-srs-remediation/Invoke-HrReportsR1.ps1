[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ReleaseDirectory,
    [ValidateSet('Inspect','Execute')][string]$Mode='Inspect',
    [string]$ReviewedPacketSha256=''
)
$ErrorActionPreference='Stop'
$VerbosePreference='SilentlyContinue'
$DebugPreference='SilentlyContinue'
$root='C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261020R1'
$pipe='np:\\.\pipe\LOCALDB#9F3001F7\tsql\query'
$owner='COSMIXRND\Huma.Khan'
$base=[uri]'https://localhost:7481/'
$release=[IO.Path]::GetFullPath($ReleaseDirectory)
$assembly=Join-Path $release 'Cosmix.Api.dll'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$accountsPath='C:\Users\Huma.Khan\AppData\Local\Cosmix\LocalTesting\Test-Accounts.json'
$sourceSettings='C:\Users\Huma.Khan\AppData\Local\Cosmix\LocalTesting\Dashboard-20261019-Diagnostic.json'
$hrAccount='8475187f-bf3b-4f9c-a3ed-4bac32253005'
$employee='35d19922-70a8-4c80-8578-e1029360e9fc'
$marker='TEST-HR-R1-20261020-export'
$reason='TEST ONLY R1 report acceptance'
$checks=[Collections.Generic.List[object]]::new()
$httpResults=[Collections.Generic.List[object]]::new()
$clients=[Collections.Generic.List[object]]::new()
$sessions=[Collections.Generic.List[object]]::new()
$historicalIds=@{}
$resultsRelative='results'
$requests=0
$jobId=''
$hostProcess=$null
$started=$false
# Load only reviewed, side-effect-free function definitions; never execute the V1 runner.
$common=Join-Path $PSScriptRoot 'Invoke-HrVehicleV1.ps1'
$tokens=$null; $errors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile($common,[ref]$tokens,[ref]$errors)
if($errors.Count){throw 'Common helper parse failed.'}
$names=@('Assert','Check','Assert-PrivateSource','New-PrivateDirectory','Write-Private','Write-Json','Write-Result','New-Client','Login','Assert-Snapshot','Record-Counts')
foreach($name in $names) {
    $definitions=@($ast.FindAll({param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -ceq $name},$true))
    if($definitions.Count -ne 1){throw 'Common helper definition differs.'}
    . ([scriptblock]::Create($definitions[0].Extent.Text))
}
function Read-Sql([string]$query) {
    Assert ($query.TrimStart().StartsWith('SELECT ',[StringComparison]::OrdinalIgnoreCase)) 'Only SELECT is allowed.'
    $c=[System.Data.SqlClient.SqlConnection]::new("Server=$pipe;Database=Cosmix_Testing;Integrated Security=True;Encrypt=False;Connect Timeout=5;Pooling=False;Application Name=Cosmix.HrReports.R1.Inspect")
    try {
        $c.Open();$q=$c.CreateCommand();$q.CommandText=$query;$q.CommandTimeout=20
        try {
            $r=$q.ExecuteReader()
            try {
                $rows=[Collections.Generic.List[string]]::new()
                while($r.Read()){Assert ($r.FieldCount -eq 1) 'Unexpected SQL shape.';$rows.Add([string]$r.GetValue(0))}
                return ($rows -join [char]10).Trim()
            } finally {$r.Dispose()}
        } finally {$q.Dispose()}
    } finally {$c.Dispose()}
}
function Test-Route([string]$method,[string]$path,$body,[string]$key) {
    if(($method+' '+$path) -cin @('GET /api/v1/me/permissions','GET /api/v1/auth/web/csrf',
        'POST /api/v1/auth/web/login','POST /api/v1/auth/web/local-testing/otp',
        'POST /api/v1/auth/web/otp/verify','POST /api/v1/auth/web/logout')){
        return $path -cne '/api/v1/auth/web/login' -or $body.email -ceq 'hr@example.invalid'
    }
    if($method -ceq 'GET') {
        return $path -cin @("/api/v1/hr/reports/employee-master?employeeId=$employee&page=1&pageSize=1",
            "/api/v1/hr/reports/employee-master?employeeId=$employee&page=2&pageSize=1") -or
            ($script:jobId -and $path -cin @("/api/v1/hr/reports/exports/$script:jobId","/api/v1/hr/reports/exports/$script:jobId/download"))
    }
    return $method -ceq 'POST' -and $body.reason -ceq $reason -and
        $body.filter.employeeId -ceq $employee -and $body.filter.page -eq 1 -and $body.filter.pageSize -eq 1 -and
        (($path -ceq '/api/v1/hr/reports/employee-master/export-page' -and $body.format -in @(1,2) -and !$key) -or
        ($path -ceq '/api/v1/hr/reports/employee-master/export' -and $body.format -eq 3 -and $key -ceq $marker))
}
function Request($client,[string]$method,[string]$path,$body=$null,[string]$csrf='',[string]$key='') {
    Assert (Test-Route $method $path $body $key) 'Unapproved R1 request.'
    $script:requests++
    Assert ($script:requests -le 60) 'R1 request ceiling exceeded.'
    $message=[Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::new($method),$path)
    if($null -ne $body){$message.Content=[Net.Http.StringContent]::new(($body|ConvertTo-Json -Depth 12 -Compress),[Text.Encoding]::UTF8,'application/json')}
    if($csrf){$message.Headers.Add('X-CSRF-TOKEN',$csrf)}
    if($key){$message.Headers.Add('Idempotency-Key',$key)}
    try {
        $response=$client.SendAsync($message).GetAwaiter().GetResult()
        try {
            $bytes=$response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
            Assert ($bytes.Length -le 10MB) 'Response exceeds the reviewed download bound.'
            $type=[string]$response.Content.Headers.ContentType
            $value=$null
            if($type -match 'json' -and $bytes.Length){
                $value=ConvertFrom-Json -InputObject ([Text.Encoding]::UTF8.GetString($bytes)) -AsHashtable -NoEnumerate -DateKind String
            }
            $httpResults.Add(@{Method=$method;Path=($path -split '\?')[0];Status=[int]$response.StatusCode})
            return @{Status=[int]$response.StatusCode;Value=$value;Bytes=$bytes;ContentType=$type}
        } finally {$response.Dispose()}
    } finally {$message.Dispose()}
}
function Protected-Snapshot {
    $append=@('auth.UserSessions','auth.RefreshTokens','auth.MobileOtpChallenges','auth.OtpDeliveries','audit.AuditEvents',
        'reports.ReportExportJobs','jobs.OutboxMessages','files.StoredFiles','workflow.IdempotencyReceipts')
    $tables=(Read-Sql "SELECT (SELECT s.name AS [schema],t.name AS [table] FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE t.is_ms_shipped=0 ORDER BY s.name,t.name FOR JSON PATH);")|ConvertFrom-Json
    $snapshot=@{}
    foreach($table in $tables) {
        $plain=$table.schema+'.'+$table.table
        if($plain -cin @('auth.UserAccounts','auth.AuthRateBuckets')){continue}
        $qualified='['+$table.schema.Replace(']',']]')+'].['+$table.table.Replace(']',']]')+']'
        $literal=$qualified.Replace("'","''")
        $order=Read-Sql ("SELECT STRING_AGG(CONVERT(nvarchar(max),QUOTENAME(c.name)),',') WITHIN GROUP(ORDER BY ic.key_ordinal) FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE i.object_id=OBJECT_ID(N'"+$literal+"') AND i.is_primary_key=1;")
        Assert ($order -and $order -ne 'NULL') 'Missing deterministic primary key.'
        $where=''
        if($plain -cin $append) {
            if(!$historicalIds.ContainsKey($plain)){
                $decoded=ConvertFrom-Json -InputObject (Read-Sql "SELECT COALESCE((SELECT Id FROM $qualified ORDER BY Id FOR JSON PATH),N'[]');") -NoEnumerate -AsHashtable
                Assert ($decoded -is [array]) 'Historical IDs must be an array.'
                $ids=@(foreach($item in $decoded){([guid]$item.Id).ToString('D')})
                Assert ($ids.Count -eq [int](Read-Sql "SELECT COUNT(*) FROM $qualified;")) 'Historical ID count differs.'
                Assert ($ids.Count -le 10000) 'Historical row budget exceeded.'
                $historicalIds[$plain]=ConvertTo-Json -InputObject $ids -Compress
            }
            $json=$historicalIds[$plain].Replace("'","''")
            $where=" WHERE Id IN(SELECT CONVERT(uniqueidentifier,[value]) FROM OPENJSON(N'$json'))"
        }
        $snapshot[$plain]=Read-Sql "SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM $qualified$where ORDER BY $order FOR JSON PATH,INCLUDE_NULL_VALUES)),2);"
    }
    $snapshot['unapproved-accounts']=Read-Sql "SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM auth.UserAccounts WHERE Id<>'$hrAccount' ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES)),2);"
    $cols=Read-Sql "SELECT STRING_AGG(CONVERT(nvarchar(max),QUOTENAME(name)),',') WITHIN GROUP(ORDER BY column_id) FROM sys.columns WHERE object_id=OBJECT_ID('auth.UserAccounts') AND name NOT IN('FailedLoginCount','LockedUntil','RowVersion');"
    $snapshot['hr-account-security']=Read-Sql "SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT $cols FROM auth.UserAccounts WHERE Id='$hrAccount' FOR JSON PATH,INCLUDE_NULL_VALUES)),2);"
    $rateJson=$script:originalRateKeys.Replace("'","''")
    $snapshot['unrelated-rate-buckets']=Read-Sql "SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM auth.AuthRateBuckets WHERE [Key] NOT IN('web-request-global','web-password-global','web-verify-global','request-global','web-request-account:$hrAccount','web-password-account:$hrAccount','web-verify-account:$hrAccount') AND [Key] IN(SELECT [value] FROM OPENJSON(N'$rateJson')) ORDER BY [Key] FOR JSON PATH,INCLUDE_NULL_VALUES)),2);"
    return $snapshot
}
function Prior-Snapshot {
    $snapshot=@{}
    foreach($batch in @('Huma20261019C01','Huma20261019C02','Huma20261019C03','Huma20261019C04','Huma20261020V1')){
        $prior=Join-Path 'C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance' $batch
        Assert (Test-Path -LiteralPath $prior -PathType Container) 'Prior acceptance evidence missing.'
        foreach($directory in @((Get-Item -LiteralPath $prior))+@(Get-ChildItem -LiteralPath $prior -Directory -Recurse -Force)){
            Assert (!(($directory.Attributes)-band[IO.FileAttributes]::ReparsePoint)) 'Linked prior directory.'
        }
        foreach($file in Get-ChildItem -LiteralPath $prior -Recurse -File -Force){
            Assert-PrivateSource $file.FullName ($file.Name -cin @('host-stdout.log','host-stderr.log'))
            $snapshot[$file.FullName]=(Get-FileHash -LiteralPath $file.FullName).Hash
        }
    }
    return $snapshot
}
function New-Rows([string]$table) {
    Assert ($historicalIds.ContainsKey($table)) 'Unknown append-only verification table.'
    $ids=$historicalIds[$table].Replace("'","''")
    return "Id NOT IN(SELECT CONVERT(uniqueidentifier,[value]) FROM OPENJSON(N'$ids'))"
}
function Read-Protected([string]$serialized,[string]$purpose) {
    $ref=$serialized|ConvertFrom-Json -AsHashtable
    Assert ($ref.StorageKey -cmatch '^[a-f0-9]{32}\.enc$' -and $ref.ObjectVersion -cmatch '^[a-f0-9]{32}$' -and
        $ref.KeyReference -ceq 'hr-reports-r1' -and $ref.Sha256 -cmatch '^[A-F0-9]{64}$' -and
        $ref.SizeBytes -gt 0 -and $ref.SizeBytes -le 8000000) 'Protected reference differs.'
    $path=Join-Path (Join-Path $root 'files') $ref.StorageKey
    Assert-PrivateSource $path
    Assert-PrivateSource (Join-Path $root 'keys\files.key')
    $envelope=[IO.File]::ReadAllBytes($path)
    Assert ($envelope.Length -eq $ref.SizeBytes+32 -and [Text.Encoding]::ASCII.GetString($envelope,0,4) -ceq 'CXF1') 'Encrypted envelope differs.'
    $key=[IO.File]::ReadAllBytes((Join-Path $root 'keys\files.key'))
    $plain=[byte[]]::new([int]$ref.SizeBytes)
    try {
        Assert ($key.Length -eq 32) 'Private key length differs.'
        $aad=[Text.Json.JsonSerializer]::SerializeToUtf8Bytes([string[]]@('cosmix-private-file/v1',$purpose,$ref.StorageKey,$ref.ObjectVersion,$ref.Sha256,$ref.KeyReference,[string]$ref.SizeBytes),[Text.Json.JsonSerializerOptions]::Default)
        $cipher=[Security.Cryptography.AesGcm]::new($key,16)
        try {$cipher.Decrypt([byte[]]$envelope[4..15],[byte[]]$envelope[32..($envelope.Length-1)],[byte[]]$envelope[16..31],$plain,$aad)}
        finally {$cipher.Dispose()}
        Assert ([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($plain)) -ceq $ref.Sha256) 'Plaintext hash differs.'
        return ,$plain
    } finally {[Array]::Clear($key,0,$key.Length)}
}
function Verify-ReportState {
    Assert ($script:jobId -cmatch '^[a-f0-9-]{36}$') 'No captured report ID.'
    $new=New-Rows 'reports.ReportExportJobs'
    Assert ((Read-Sql "SELECT COUNT(*) FROM reports.ReportExportJobs WHERE $new;") -eq '1') 'Unexpected new report job.'
    $row=(Read-Sql "SELECT (SELECT Id,RequestedByUserId,ReportType,Format,State,StoredFileId,SourceLockReference,ProtectedFilterScopeReference FROM reports.ReportExportJobs WHERE Id='$script:jobId' FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);")|ConvertFrom-Json -AsHashtable
    Assert ($row.RequestedByUserId -eq $hrAccount -and $row.ReportType -ceq 'hr.rich-report.v1' -and $row.Format -eq 3 -and $row.State -eq 2 -and $row.StoredFileId) 'Report binding differs.'
    $new=New-Rows 'files.StoredFiles'
    Assert ((Read-Sql "SELECT COUNT(*) FROM files.StoredFiles WHERE $new;") -eq '1') 'Unexpected stored-file population.'
    $new=New-Rows 'workflow.IdempotencyReceipts'
    Assert ((Read-Sql "SELECT COUNT(*) FROM workflow.IdempotencyReceipts WHERE $new;") -eq '1') 'Unexpected new receipt.'
    Assert ((Read-Sql "SELECT COUNT(*) FROM workflow.IdempotencyReceipts WHERE [Key]='$marker' AND ActorUserId='$hrAccount' AND Operation='hr.report.export.request' AND CompletedAt IS NOT NULL;") -eq '1') 'Export receipt missing.'
    $new=New-Rows 'jobs.OutboxMessages'
    Assert ((Read-Sql "SELECT COUNT(*) FROM jobs.OutboxMessages WHERE $new;") -eq '2') 'Expected export and notification intents only.'
    $jobN=([guid]$script:jobId).ToString('N')
    $accountN=([guid]$hrAccount).ToString('N')
    Assert ((Read-Sql "SELECT COUNT(*) FROM jobs.OutboxMessages WHERE $new AND MessageType='hr.report-export.v1' AND DeduplicationKey='$jobN' AND State=2 AND Attempts=1 AND CompletedAt IS NOT NULL AND LeaseToken IS NULL;") -eq '1') 'Captured report dispatch differs.'
    $notificationKey="attendance.report-ready:${jobN}:${accountN}"
    Assert ((Read-Sql "SELECT COUNT(*) FROM jobs.OutboxMessages WHERE $new AND MessageType='notification.in-app.v1' AND DeduplicationKey='$notificationKey' AND State=0 AND Attempts=0 AND LeaseToken IS NULL;") -eq '1') 'Notification intent differs or was dispatched.'
    $scope=[Text.Encoding]::UTF8.GetString((Read-Protected $row.ProtectedFilterScopeReference "hr:report:filters:$accountN"))|ConvertFrom-Json -AsHashtable
    Assert ($scope.Requester.AccountId -eq $hrAccount -and $scope.Requester.EmployeeId -eq $employee -and
        $scope.Requester.SessionId -eq $hr.Id -and $scope.ReportType -ceq 'employee-master' -and $scope.Format -eq 3 -and
        $scope.Filter.EmployeeId -eq $employee -and @($scope.Selection.EmployeeIds).Count -eq 1 -and
        $scope.Selection.EmployeeIds[0] -eq $employee -and !$scope.Selection.IncludeUnscopedAudit) 'Protected source scope differs.'
    $output=[Text.Encoding]::UTF8.GetString((Read-Protected $row.SourceLockReference "hr:report:manifest:$jobN"))|ConvertFrom-Json -AsHashtable
    Assert ($output.JobId -eq $jobId -and $output.StoredFileId -eq $row.StoredFileId -and $output.Format -eq 3 -and
        $output.Metadata.TotalCount -eq 1 -and $output.Metadata.ReturnedCount -eq 1 -and $output.Metadata.Filter.EmployeeId -eq $employee) 'Protected report manifest differs.'
    $stored=(Read-Sql "SELECT (SELECT StorageKey,Sha256,SizeBytes,EncryptionKeyReference,UploadedByUserId,Extension,ContentType,DeletedAt FROM files.StoredFiles WHERE Id='$($row.StoredFileId)' FOR JSON PATH,WITHOUT_ARRAY_WRAPPER,INCLUDE_NULL_VALUES);")|ConvertFrom-Json -AsHashtable
    Assert ($stored.StorageKey -ceq $output.File.StorageKey -and $stored.Sha256 -ceq $output.File.Sha256 -and
        $stored.SizeBytes -eq $output.File.SizeBytes -and $stored.EncryptionKeyReference -ceq 'hr-reports-r1' -and
        $stored.UploadedByUserId -eq $hrAccount -and $stored.Extension -ceq '.pdf' -and
        $stored.ContentType -ceq 'application/pdf' -and $null -eq $stored.DeletedAt) 'SQL stored-file identity differs.'
    $bytes=Read-Protected ($output.File|ConvertTo-Json -Compress) "hr:report:pdf:$jobN"
    Assert ([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)) -ceq
        [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($pdf.Bytes))) 'Downloaded PDF differs from encrypted stored output.'
    $receipt=Read-Sql "SELECT ProtectedResultReference FROM workflow.IdempotencyReceipts WHERE [Key]='$marker' AND ActorUserId='$hrAccount' AND Operation='hr.report.export.request';"
    $receiptId=[Text.Encoding]::UTF8.GetString((Read-Protected $receipt "hr:report:receipt:$accountN"))|ConvertFrom-Json
    Assert ($receiptId -eq $jobId) 'Encrypted receipt is bound to another report.'
    Check 'SQL report, exact dispatch, encrypted output and receipt' $true
}

Assert ($PSVersionTable.PSVersion -ge [version]'7.5' -and $IsWindows) 'PowerShell 7.5+ Windows required.'
Assert ([Security.Principal.WindowsIdentity]::GetCurrent().Name -ieq $owner) 'Operator differs.'
Assert ($release.StartsWith('D:\CosmixVerification\',[StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $assembly)) 'Frozen Release required.'
Assert (!(Test-Path -LiteralPath $root)) 'R1 root exists; no reuse/reset.'
Assert (@(Get-NetTCPConnection -State Listen -LocalPort 7481 -ErrorAction SilentlyContinue).Count -eq 0) 'Port 7481 occupied.'
for($p=[IO.Path]::GetDirectoryName($root);$p;$p=[IO.Path]::GetDirectoryName($p)){
    if(Test-Path -LiteralPath $p){Assert (!((Get-Item -LiteralPath $p).Attributes-band[IO.FileAttributes]::ReparsePoint)) 'Linked target ancestor.'}
}
$binding=Read-Sql "SELECT CONCAT(CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')),'|',DB_NAME(),'|',ORIGINAL_LOGIN(),'|',SUSER_SNAME(d.owner_sid),'|',CONVERT(varchar(36),r.family_guid),'|',d.state_desc,'|',CONVERT(int,SERVERPROPERTY('ProcessID'))) FROM sys.databases d JOIN sys.database_recovery_status r ON r.database_id=d.database_id WHERE d.database_id=DB_ID();"
Assert ($binding -ieq 'Software-PC1\LOCALDB#9F3001F7|Cosmix_Testing|COSMIXRND\Huma.Khan|COSMIXRND\Huma.Khan|d9c34627-18a2-41cf-a804-821e39516303|ONLINE|25768') 'SQL identity changed.'
Assert ((Read-Sql "SELECT STRING_AGG(CONVERT(nvarchar(max),physical_name),'|') WITHIN GROUP(ORDER BY file_id) FROM sys.database_files;") -ieq 'C:\Users\Huma.Khan\Cosmix_Testing.mdf|C:\Users\Huma.Khan\Cosmix_Testing_log.ldf') 'Database files differ.'
$process=Get-CimInstance Win32_Process -Filter 'ProcessId=25768'
Assert ($process -and $process.Name -eq 'sqlservr.exe' -and $process.CommandLine.Contains('-sLOCALDB#9F3001F7')) 'SQL process differs.'
$processOwner=Invoke-CimMethod -InputObject $process -MethodName GetOwner
Assert ($processOwner.ReturnValue -eq 0 -and ($processOwner.Domain+'\'+$processOwner.User) -ieq $owner) 'SQL process owner differs.'
$expected=@(Get-ChildItem -LiteralPath (Join-Path $repo 'src/Cosmix.Infrastructure/Database/Migrations') -Filter '*.cs'|Where-Object Name -Match '^\d{14}_[^.]+\.cs$'|Sort-Object Name|ForEach-Object BaseName)
$applied=(Read-Sql 'SELECT MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;') -split '\r?\n'
Assert ($expected.Count -eq 25 -and ($expected -join '|') -ceq ($applied -join '|')) 'Migration mismatch; no migration will run.'
Assert ((Read-Sql 'SELECT COUNT(*) FROM auth.OtpDeliveries WHERE Status NOT IN(1,2,3);') -eq '0') 'Existing OTP work is not terminal.'
Assert ((Read-Sql "SELECT COUNT(*) FROM auth.MobileOtpChallenges WHERE UserAccountId='$hrAccount' AND Purpose='WebLogin' AND ConsumedAt IS NULL AND RevokedAt IS NULL;") -eq '0') 'Prior HR login challenges would change; stop before writes.'
Assert ((Read-Sql "SELECT COUNT(*) FROM auth.UserAccounts WHERE Id='$hrAccount' AND EmployeeId='$employee' AND LoginName='hr@example.invalid' AND Status=3 AND WebAccessEnabled=1 AND (LockedUntil IS NULL OR LockedUntil<=SYSUTCDATETIME());") -eq '1') 'Synthetic HR binding differs.'
Assert ((Read-Sql "SELECT COUNT(DISTINCT p.Code) FROM auth.UserRoleAssignments u JOIN auth.Roles r ON r.Id=u.RoleId JOIN auth.RolePermissions rp ON rp.RoleId=r.Id JOIN auth.Permissions p ON p.Id=rp.PermissionId JOIN auth.UserAccessScopes s ON s.UserRoleAssignmentId=u.Id WHERE u.UserAccountId='$hrAccount' AND r.IsActive=1 AND s.Kind=7 AND p.Code IN('hr.reports.read','hr.reports.export','hr.employees.read','hr.employees.identity.read') AND u.EffectiveFrom<=SYSUTCDATETIME() AND (u.EffectiveTo IS NULL OR SYSUTCDATETIME()<u.EffectiveTo) AND (u.RevokedAt IS NULL OR SYSUTCDATETIME()<u.RevokedAt) AND rp.RevokedAt IS NULL AND rp.GrantedAt<=SYSUTCDATETIME();") -eq '4') 'Required current report grants absent; no provisioning.'
Assert ((Read-Sql "SELECT COUNT(*) FROM workflow.IdempotencyReceipts WHERE [Key]='$marker';") -eq '0') 'R1 receipt already exists.'
Assert ((Read-Sql "SELECT COUNT(*) FROM reports.ReportExportJobs WHERE RequestedByUserId='$hrAccount' AND ReportType='hr.rich-report.v1';") -eq '0') 'Existing HR export found; no adoption or replay.'
Assert-PrivateSource $accountsPath
Assert-PrivateSource $sourceSettings
$rateRows=@(((Read-Sql "SELECT (SELECT [Key] FROM auth.AuthRateBuckets ORDER BY [Key] FOR JSON PATH);")|ConvertFrom-Json))
$originalRateKeys=ConvertTo-Json -InputObject @($rateRows|ForEach-Object Key) -Compress
$protected=Protected-Snapshot
$prior=Prior-Snapshot
$beforeCounts=Record-Counts
$packet=[Collections.Generic.List[string]]::new()
foreach($file in Get-ChildItem -LiteralPath $release -Recurse -File|Sort-Object FullName){
    Assert (!(($file.Attributes)-band[IO.FileAttributes]::ReparsePoint)) 'Linked release asset.'
    $packet.Add($file.FullName+'|'+(Get-FileHash -LiteralPath $file.FullName).Hash)
}
foreach($path in @($PSCommandPath,$common,$sourceSettings,$accountsPath)){
    $packet.Add($path+'|'+(Get-FileHash -LiteralPath $path).Hash)
}
foreach($name in $protected.Keys|Sort-Object){$packet.Add('protected|'+$name+'|'+$protected[$name])}
foreach($name in $prior.Keys|Sort-Object){$packet.Add('prior|'+$name+'|'+$prior[$name])}
$packetHash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes(($packet -join [char]10))))
[pscustomobject]@{Mode=$Mode;Target=$binding;Migrations=$applied.Count;ProtectedSnapshots=$protected.Count;RetainedFiles=$prior.Count;PrivateRootAbsent=$true;PortAvailable=$true;PacketSha256=$packetHash;MaxHttpRequests=60;Writes=$false}
if($Mode -eq 'Inspect'){return}
Assert ($ReviewedPacketSha256 -cmatch '^[A-F0-9]{64}$' -and $ReviewedPacketSha256 -ceq $packetHash) 'Read-only packet changed; no writes allowed.'
try {
    New-PrivateDirectory $root
    foreach($dir in @('config','files','keys','keys\data-protection','results')){New-PrivateDirectory (Join-Path $root $dir)}
    $source=Get-Content -LiteralPath $sourceSettings -Raw|ConvertFrom-Json -AsHashtable
    $mobile=$source.MobileAuthentication
    $mobile.Crypto.KeyVersion='hr-r1'
    $mobile.Crypto.VerifierKeyBase64=[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    $mobile.Crypto.EncryptionKeyBase64=[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    $mobile.Sms=@{Provider='LocalTesting';Endpoint='';BearerToken=''}
    $config=@{
        Urls=$base.AbsoluteUri.TrimEnd('/');AllowedHosts='localhost'
        LocalTesting=@{Enabled=$true;ExistingAcceptanceProfile='HumaHrReports20261020R1';PrivateDirectory=$root;AllowUnencryptedLocalPipe=$true}
        ConnectionStrings=@{CosmixDatabase="Server=$pipe;Database=Cosmix_Testing;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=5;Application Name=Cosmix.HrReports.R1"}
        PrivateFiles=@{Directory=(Join-Path $root 'files');KeyFile=(Join-Path $root 'keys\files.key');KeyReference='hr-reports-r1'}
        Jwt=@{Issuer='Cosmix.HrReports.R1';Audience='Cosmix.HrReports.R1';SigningKey=[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))}
        MobileAuthentication=$mobile;WebAuthentication=$source.WebAuthentication
        EmployeeFileScanner=@{Enabled=$false};AttendanceJobs=@{Enabled=$false}
        SelfHostedFace=@{Enabled=$false};FaceCapture=@{Enabled=$false};FaceTemplateStorage=@{Enabled=$false}
        FaceStorageRetention=@{Enabled=$false};FaceStorageRetentionJournal=@{Enabled=$false}
        Cors=@{AllowedOrigins=@('https://localhost:5173')}
    }
    Write-Private 'keys\files.key' ([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    Write-Json 'config\host.json' $config
    $config=$null;$source=$null;$mobile=$null
    Write-Result 'start.json' @{Target=$binding;PacketSha256=$packetHash;ProtectedSnapshots=$protected;PriorFiles=$prior;CountsBefore=$beforeCounts;Release=$release;Utc=[DateTimeOffset]::UtcNow}
    $start=[Diagnostics.ProcessStartInfo]::new((Get-Command dotnet -CommandType Application).Source)
    $start.ArgumentList.Add($assembly);$start.UseShellExecute=$false;$start.CreateNoWindow=$true
    $start.WorkingDirectory=$release;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    $start.Environment.Clear()
    foreach($key in @('SystemRoot','WINDIR','USERPROFILE','LOCALAPPDATA','APPDATA','PATH','TEMP','TMP','ProgramFiles','ProgramW6432','DOTNET_ROOT','COMPUTERNAME','USERNAME','USERDOMAIN')){
        $value=[Environment]::GetEnvironmentVariable($key);if($value){$start.Environment[$key]=$value}
    }
    $start.Environment['COSMIX_EXISTING_TESTING_HOST']='HumaHrReports20261020R1'
    $start.Environment['COSMIX_LOCAL_TESTING_SETTINGS']=Join-Path $root 'config\host.json'
    $start.Environment['ASPNETCORE_ENVIRONMENT']='Development'
    $hostProcess=[Diagnostics.Process]::new();$hostProcess.StartInfo=$start
    Assert ($hostProcess.Start()) 'Owned report host failed to start.'
    $started=$true
    $stdout=$hostProcess.StandardOutput.ReadToEndAsync();$stderr=$hostProcess.StandardError.ReadToEndAsync()
    $ready=$false
    for($i=0;$i -lt 60;$i++){
        Assert (!$hostProcess.HasExited) 'Owned host exited during startup.'
        $listeners=@(Get-NetTCPConnection -State Listen -LocalPort 7481 -ErrorAction SilentlyContinue)
        if($listeners.Count){Assert (@($listeners|Where-Object {$_.OwningProcess -ne $hostProcess.Id -or $_.LocalAddress -notin @('127.0.0.1','::1')}).Count -eq 0) 'Listener ownership differs.';$ready=$true;break}
        Start-Sleep -Milliseconds 500
    }
    Assert $ready 'Owned report host did not listen within the bound.'
    $credentials=Get-Content -LiteralPath $accountsPath -Raw|ConvertFrom-Json
    $hr=Login $credentials 'hr@example.invalid'
    $credentials=$null
    $preview=Request $hr.Client GET "/api/v1/hr/reports/employee-master?employeeId=$employee&page=1&pageSize=1"
    Check 'synthetic employee preview HTTP' ($preview.Status -eq 200)
    $rows=@($preview.Value.rows)
    Check 'preview exact synthetic employee and count' ($rows.Count -eq 1 -and $rows[0].employeeId -eq $employee -and
        $preview.Value.metadata.totalCount -eq 1 -and $preview.Value.metadata.returnedCount -eq 1)
    $name=$rows[0].fullName
    Assert ($name -is [string] -and $name) 'Missing synthetic display name.'
    $empty=Request $hr.Client GET "/api/v1/hr/reports/employee-master?employeeId=$employee&page=2&pageSize=1"
    Check 'second page does not leak another employee' ($empty.Status -eq 200 -and @($empty.Value.rows).Count -eq 0 -and $empty.Value.metadata.totalCount -eq 1)
    $filter=@{employeeId=$employee;page=1;pageSize=1}
    foreach($format in @(1,2)){
        $download=Request $hr.Client POST '/api/v1/hr/reports/employee-master/export-page' @{format=$format;reason=$reason;filter=$filter} $hr.Csrf
        Check "page export format $format HTTP" ($download.Status -eq 200)
        if($format -eq 1){
            $csv=[Text.Encoding]::UTF8.GetString($download.Bytes).TrimStart([char]0xFEFF)
            $sections=$csv -split '\r?\n\r?\n',2
            Assert ($sections.Count -eq 2) 'CSV metadata separator missing.'
            $data=@($sections[1]|ConvertFrom-Csv)
            Check 'CSV exact one synthetic employee' ($data.Count -eq 1 -and $data[0].'Full Name' -ceq $name -and
                $data[0].'Employee #' -eq [string]$rows[0].employeeNumber)
            Write-Private 'results\employee-master.csv' $download.Bytes
        } else {
            $stream=[IO.MemoryStream]::new($download.Bytes,$false)
            $zip=[IO.Compression.ZipArchive]::new($stream,[IO.Compression.ZipArchiveMode]::Read)
            try {
                $entry=$zip.GetEntry('xl/worksheets/sheet2.xml')
                Assert ($null -ne $entry) 'XLSX sheet missing.'
                $reader=[IO.StreamReader]::new($entry.Open())
                try {$xml=[xml]$reader.ReadToEnd()} finally {$reader.Dispose()}
                $sheetRows=@($xml.SelectNodes("//*[local-name()='sheetData']/*[local-name()='row']"))
                $header=@($sheetRows|Where-Object {$_.InnerText.Contains('Employee #') -and $_.InnerText.Contains('Full Name')})
                Assert ($header.Count -eq 1) 'XLSX data header differs.'
                $dataRows=@($sheetRows|Where-Object {[int]$_.r -gt [int]$header[0].r})
                Check 'XLSX exact one synthetic employee' ($dataRows.Count -eq 1 -and $dataRows[0].InnerText.Contains($name))
            } finally {$zip.Dispose();$stream.Dispose()}
            Write-Private 'results\employee-master.xlsx' $download.Bytes
        }
    }
    $body=@{format=3;reason=$reason;filter=$filter}
    $job=Request $hr.Client POST '/api/v1/hr/reports/employee-master/export' $body $hr.Csrf $marker
    Check 'durable PDF accepted' ($job.Status -eq 202 -and $job.Value.id)
    $script:jobId=([guid]$job.Value.id).ToString('D')
    $retry=Request $hr.Client POST '/api/v1/hr/reports/employee-master/export' $body $hr.Csrf $marker
    Check 'same-key request retains exact job' ($retry.Status -eq 202 -and $retry.Value.id -eq $script:jobId)
    $status=Request $hr.Client GET "/api/v1/hr/reports/exports/$script:jobId"
    Check 'normal pending response triggers captured-job dispatch' ($status.Status -eq 200 -and $status.Value.state -ceq 'pending')
    $status=Request $hr.Client GET "/api/v1/hr/reports/exports/$script:jobId"
    Check 'captured report status after bounded dispatch' ($status.Status -eq 200 -and $status.Value.state -ceq 'completed' -and
        $status.Value.totalCount -eq 1 -and $status.Value.returnedCount -eq 1)
    $pdf=Request $hr.Client GET "/api/v1/hr/reports/exports/$script:jobId/download"
    Check 'authenticated PDF download' ($pdf.Status -eq 200 -and $pdf.ContentType -like 'application/pdf*' -and [Text.Encoding]::ASCII.GetString($pdf.Bytes,0,[Math]::Min(5,$pdf.Bytes.Length)) -ceq '%PDF-')
    Write-Private 'results\employee-master.pdf' $pdf.Bytes
    Verify-ReportState
    $retained=$hr.Cookies.GetCookieHeader($base)
    Check 'missing-CSRF logout denied' ((Request $hr.Client POST '/api/v1/auth/web/logout' @{}).Status -eq 400)
    Check 'authenticated logout' ((Request $hr.Client POST '/api/v1/auth/web/logout' @{} $hr.Csrf).Status -eq 204)
    $hr.LoggedOut=$true
    $jar=[Net.CookieContainer]::new();$jar.SetCookies($base,$retained.Replace(';',','))
    $replay=New-Client $jar
    Check 'revoked cookie denied' ((Request $replay GET '/api/v1/me/permissions').Status -eq 401)
    Assert-Snapshot $protected (Protected-Snapshot) 'Protected SQL'
    Assert-Snapshot $prior (Prior-Snapshot) 'Prior acceptance files'
    $counts=Record-Counts
    Assert ($counts.Accounts -eq $beforeCounts.Accounts -and $counts.Migrations -eq 25 -and
        $counts.Sessions-$beforeCounts.Sessions -eq 1 -and $counts.RefreshTokens-$beforeCounts.RefreshTokens -eq 1 -and
        $counts.Challenges-$beforeCounts.Challenges -eq 1 -and $counts.Deliveries-$beforeCounts.Deliveries -eq 1 -and
        $counts.Audits-$beforeCounts.Audits -le 100) 'Auth/audit counts exceeded.'
    Write-Result 'acceptance.json' @{Checks=$checks.ToArray();HttpResults=$httpResults.ToArray();Requests=$requests;CountsBefore=$beforeCounts;Counts=$counts;JobId=$jobId;ProtectedSnapshotsUnchanged=$true;PriorFilesUnchanged=$true;MigrationsApplied=$false;Utc=[DateTimeOffset]::UtcNow}
} catch {
    if(Test-Path -LiteralPath (Join-Path $root 'results')){
        Write-Result 'failure.json' @{Checks=$checks.ToArray();HttpResults=$httpResults.ToArray();Requests=$requests;FailureType=$_.Exception.GetType().FullName;Message='Stopped without reset, repair or automatic replay.';Utc=[DateTimeOffset]::UtcNow}
    }
    throw 'R1 stopped. Private redacted evidence retained; no automatic replay.'
} finally {
    foreach($client in $clients){$client.Dispose()}
    if($hostProcess -and $started){
        if(!$hostProcess.HasExited){$hostProcess.Kill();$hostProcess.WaitForExit(10000)|Out-Null}
        Write-Private 'results\host-stdout.log' ([Text.UTF8Encoding]::new($false).GetBytes($stdout.GetAwaiter().GetResult()))
        Write-Private 'results\host-stderr.log' ([Text.UTF8Encoding]::new($false).GetBytes($stderr.GetAwaiter().GetResult()))
        Write-Result 'host-final.json' @{ProcessId=$hostProcess.Id;Exited=$hostProcess.HasExited;DatabaseStopped=$false;CleanupPerformed=$false}
        $hostProcess.Dispose()
        Assert-Snapshot $protected (Protected-Snapshot) 'Post-host SQL'
        Assert-Snapshot $prior (Prior-Snapshot) 'Post-host prior files'
        Write-Result 'post-host-preservation.json' @{ProtectedSnapshots=$protected.Count;PriorFiles=$prior.Count;Unchanged=$true;Counts=(Record-Counts)}
    }
}
