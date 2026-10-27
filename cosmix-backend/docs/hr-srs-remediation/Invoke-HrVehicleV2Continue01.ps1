[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ReleaseDirectory,
    [ValidateSet('Inspect','Execute')][string]$Mode='Inspect',
    [string]$ApprovedPacketSha256=''
)
$ErrorActionPreference='Stop'
$VerbosePreference='SilentlyContinue';$DebugPreference='SilentlyContinue'

function Import-C01([string]$Directory=$PSScriptRoot) {
    $tokens=$null;$errors=$null
    $ast=[Management.Automation.Language.Parser]::ParseFile((Join-Path $Directory 'Invoke-HrVehicleV2.ps1'),[ref]$tokens,[ref]$errors)
    if($errors.Count){throw 'V2 definition parser failure.'}
    $names=@('Import-V2Common','Get-V2Profile','V2-Id','V2-RequireId','V2-Canonical','V2-Equal','V2-Fields','Test-V2Request','V2-Expect','Begin-Request','Finish-Request','V2-Call','V2-Denied','V2-Segments','Assert-V2Amounts','V2-MembershipJson','V2-DatabaseSnapshot','V2-PriorSnapshot','Assert-V2Target','Assert-V2Space')
    foreach($name in $names) {
        $definition=@($ast.FindAll({param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst]},$true)|Where-Object Name -CEQ $name)
        if($definition.Count -ne 1){throw 'Missing or ambiguous V2 definition.'}
        $target=if($name -cin @('Test-V2Request','Begin-Request','Finish-Request')){'C01-Original-'+$name}else{$name}
        . ([scriptblock]::Create($definition[0].Extent.Text.Replace(('function '+$name+'('),('function script:'+$target+'(')).Replace(('function '+$name+' {'),('function script:'+$target+' {'))))
    }
    Import-V2Common $Directory
    function script:Read-Sql([string]$query) {return C01-ReadSql $query}
}
function New-C01ReadConnection([string]$connectionString) {
    return [System.Data.SqlClient.SqlConnection]::new($connectionString)
}
function Invoke-C01ReadScope([scriptblock]$action) {
    Assert ($null -eq $c01ReadContext) 'Nested inspection connection lifetime forbidden.'
    $c01ReadContext=@{Connection=$null;Attempted=$false;Faulted=$false;ClientId=$null;SessionId=$null;Pins=(Get-C01Profile)}
    try {. $action} finally {
        if($null -ne $c01ReadContext.Connection){$c01ReadContext.Connection.Dispose()}
    }
}
function C01-ReadColumn([string]$query) {
    $command=$c01ReadContext.Connection.CreateCommand()
    try {
        $command.CommandTimeout=20;$command.CommandText=$query
        $reader=$command.ExecuteReader()
        try {
            $rows=[Collections.Generic.List[string]]::new()
            while($reader.Read()){Assert ($reader.FieldCount -eq 1) 'Expected one redacted inspection column.';$rows.Add([string]$reader.GetValue(0))}
            return ($rows -join [char]10).Trim()
        } finally {$reader.Dispose()}
    } finally {$command.Dispose()}
}
function Assert-C01ReadBinding {
    $c=$c01ReadContext.Connection;$p=$c01ReadContext.Pins
    Assert ($c.State -eq 'Open' -and $c.DataSource -ceq $p.Pipe -and $c.Database -ceq 'Cosmix_Testing' -and $c.ClientConnectionId -eq $c01ReadContext.ClientId) 'Inspection connection closed or changed; no reconnect.'
    # Separate JSON columns avoid cross-collation concatenation. Every query remains outside a transaction.
    $binding=C01-ReadColumn @'
SELECT (SELECT DB_NAME() AS [Database],CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) AS [Server],CONVERT(int,SERVERPROPERTY('ProcessID')) AS Pid,
ORIGINAL_LOGIN() AS [Login],SUSER_SNAME(d.owner_sid) AS [Owner],CONVERT(varchar(36),r.family_guid) AS Family,d.state_desc AS [State],d.user_access_desc AS [Access],d.is_read_only AS [ReadOnly],
(SELECT net_transport FROM sys.dm_exec_connections WHERE session_id=@@SPID) AS Transport,
(SELECT STRING_AGG(CONVERT(nvarchar(max),physical_name),'|') WITHIN GROUP(ORDER BY file_id) FROM sys.database_files) AS Files,
(SELECT COUNT(*) FROM sys.database_files WHERE state<>0 OR type NOT IN(0,1)) AS FileIssues,
@@TRANCOUNT AS TransactionCount,XACT_STATE() AS TransactionState,@@OPTIONS AS Options,@@SPID AS SessionId
FROM sys.databases d JOIN sys.database_recovery_status r ON r.database_id=d.database_id WHERE d.database_id=DB_ID() FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
'@
    $b=ConvertFrom-Json -InputObject $binding -AsHashtable
    Assert ($b.Database -ceq 'Cosmix_Testing' -and $b.Server -ieq $p.Server -and $b.Pid -eq $p.Pid -and $b.Login -ieq $p.Owner -and $b.Owner -ieq $p.Owner -and $b.Family -ieq $p.Family -and $b.State -ceq 'ONLINE' -and $b.Access -ceq 'MULTI_USER' -and $b.ReadOnly -eq 0 -and $b.Transport -ceq 'Named pipe' -and $b.Files -ieq 'C:\Users\Huma.Khan\Cosmix_Testing.mdf|C:\Users\Huma.Khan\Cosmix_Testing_log.ldf' -and $b.FileIssues -eq 0) 'Inspection SQL target drift; no retarget or reconnect.'
    # A SELECT can observe its own committable autocommit transaction with no explicit nesting.
    Assert ($b.ContainsKey('TransactionCount') -and $b.ContainsKey('TransactionState') -and $b.ContainsKey('Options') -and
        $b.TransactionCount -eq 0 -and $b.TransactionState -in @(0,1) -and ([long]$b.Options -band 2) -eq 0) 'Inspection has explicit/implicit transaction state or is uncommittable.'
    Assert ($b.SessionId -gt 0 -and ($null -eq $c01ReadContext.SessionId -or $b.SessionId -eq $c01ReadContext.SessionId)) 'Inspection SQL session changed.'
    $c01ReadContext.SessionId=$b.SessionId
}
function C01-ReadSql([string]$query) {
    Assert ($null -ne $c01ReadContext -and !$c01ReadContext.Faulted) 'Inspection lifetime absent or faulted; no retry.'
    try {
        if(!$c01ReadContext.Attempted) {
            $c01ReadContext.Attempted=$true
            Assert ($pipe -ceq $c01ReadContext.Pins.Pipe) 'Inspection pipe differs from the exact pin.'
            # Retain the approved local-pipe TLS exception; read intent is not a database permission grant.
            $c01ReadContext.Connection=New-C01ReadConnection "Server=$pipe;Database=Cosmix_Testing;Integrated Security=True;Encrypt=False;TrustServerCertificate=True;Connect Timeout=5;Pooling=False;ConnectRetryCount=0;Enlist=False;ApplicationIntent=ReadOnly;Application Name=Cosmix.HrVehicle.V2Continue01.Inspect"
            $c01ReadContext.Connection.Open()
            $c01ReadContext.ClientId=$c01ReadContext.Connection.ClientConnectionId
        }
        Assert-C01ReadBinding
        $value=C01-ReadColumn $query
        Assert-C01ReadBinding
        return $value
    } catch {$c01ReadContext.Faulted=$true;throw}
}
function Get-C01Profile {
    $p=Get-V2Profile
    $p.Name='HumaHrVehicle20261026V2Continue01';$p.SourceRoot=$p.Root
    $p.Root+='Continue01';$p.MaxHttp=100
    $p.ApplicationName='Cosmix.HrVehicle.V2Continue01';$p.KeyReference='hr-vehicle-v2-continue01';$p.CryptoVersion='hr-v2-c01'
    $p.VehicleId='2349b065-e018-4a1c-9917-0808ea4e634b';$p.PendingServiceId='f31f46a9-3bf3-4bd4-b34e-61125bc27e11'
    $p.PendingHeaderId='183f96a7-b507-410e-99ad-f9e832cc3ef3'
    $p.ManifestPath='D:\CosmixVerification\vehicle-v2-continuation-c537fea73e3c41b483e5d5ef4a2e064c.json'
    $p.ManifestSha256='F7E273D086484CD633B941C477F25F09F14281B861963E6908C99B0F41B98E95'
    return $p
}
function Test-V2Request([string]$method,[string]$path,$body,[string]$key) {
    if($path -ceq '/api/v1/hr/vehicles' -or $path.StartsWith('/api/v1/local-testing/')){return $false}
    if($path -ceq '/api/v1/me/vehicles/maintenance' -and $body.serviceKind -eq 1 -and $body.serviceDate -ceq '2026-09-17' -and !$script:lateReplays){return $false}
    return C01-Original-Test-V2Request $method $path $body $key
}
function Begin-Request($client,[string]$method,[string]$path,$body,[string]$csrf='',[string]$key='') {
    Assert ([DateTimeOffset]::UtcNow -lt [DateTimeOffset]'2026-10-26T19:00:00Z') 'Continuation approval cutoff reached.'
    Assert ($script:requests -lt 100) 'Continuation HTTP ceiling exceeded.'
    if($path -ceq '/api/v1/auth/web/local-testing/otp') {
        Assert ((V2-Fields $body @('challengeId')) -and $script:otpReads.Count -lt 3 -and $script:otpReads.Add((V2-RequireId $body.challengeId))) 'One synchronous OTP read per new challenge only.'
    }
    return C01-Original-Begin-Request $client $method $path $body $csrf $key
}
function C01-ConcurrentPendingApproval($admin) {
    $script:pendingServiceId=$profile.PendingServiceId;$script:pendingServiceKey=$marker+'-service-Tuning-2026-09-17'
    $path='/api/v1/hr/vehicles/maintenance/'+$script:pendingServiceId+'/approve';$key=$script:pendingServiceKey+'-approve'
    V2-Expect POST $path @{} $key
    $one=Begin-Request (New-Client $admin.Cookies) POST $path @{} $admin.Csrf $key
    V2-Expect POST $path @{} $key
    $two=Begin-Request (New-Client $admin.Cookies) POST $path @{} $admin.Csrf $key
    [Threading.Tasks.Task]::WhenAll([Threading.Tasks.Task[]]@($one.Task,$two.Task)).GetAwaiter().GetResult()|Out-Null
    $a=Finish-Request $one;$b=Finish-Request $two
    Assert ($a.Status -eq 200 -and $b.Status -eq 200 -and $a.Value.id -eq $profile.PendingServiceId -and $b.Value.id -eq $profile.PendingServiceId -and $a.Value.approvalState -ceq 'Approved' -and $b.Value.approvalState -ceq 'Approved') 'Concurrent pending approval/replay differs.'
    $script:serviceIds.Add($profile.PendingServiceId)
}
function Finish-Request($pending) {
    $otp=$pending.Request.RequestUri.OriginalString.EndsWith('/api/v1/auth/web/local-testing/otp')
    $result=C01-Original-Finish-Request $pending
    if($otp){Assert ($result.Status -eq 200) 'Synchronous OTP unavailable; stop without polling or recovery.'}
    return $result
}
function C01-ApproveService($admin,[string]$id,[string]$suffix) {
    $script:pendingServiceId=$id;$script:pendingServiceKey=$marker+'-'+$suffix
    foreach($attempt in 1..2) {
        $r=V2-Call $admin POST ('/api/v1/hr/vehicles/maintenance/'+$id+'/approve') @{} ($suffix+'-approve')
        Assert ($r.id -eq $id -and $r.approvalState -ceq 'Approved') 'Service approval/replay differs.'
    }
    $script:serviceIds.Add($id)
}
function C01-NewService($hr,$admin,[string]$date,[int]$kind) {
    $label=if($kind -eq 1){'Tuning'}else{'EngineOverhaul'};$amount=if($kind -eq 1){50}else{75}
    $suffix='service-'+$label+'-'+$date
    $body=@{vehicleId=$profile.VehicleId;serviceKind=$kind;serviceDate=$date;odometerKm=1200;approvedOutstationKm=200;authorizedWorkshopReference='TEST-HR-V2-WORKSHOP';evidenceReference=($marker+'-'+$date.Substring(8,2)+'-'+$label);claimedAmount=$amount;currency='PKR'}
    $r=V2-Call $hr POST '/api/v1/me/vehicles/maintenance' $body $suffix
    $id=V2-RequireId $r.id
    Assert ($r.vehicleId -eq $profile.VehicleId -and $r.employeeId -eq $profile.HrEmployee -and $r.approvalState -ceq 'Pending') 'Service submission differs.'
    $script:serviceReplays[$suffix]=@{Id=$id;Body=$body}
    C01-ApproveService $admin $id $suffix
}
function Invoke-C01Sequence($hr,$admin,$super) {
    C01-ConcurrentPendingApproval $admin
    $script:serviceReplays['service-Tuning-2026-09-17']=@{Id=$profile.PendingServiceId;Body=@{vehicleId=$profile.VehicleId;serviceKind=1;serviceDate='2026-09-17';odometerKm=1200;approvedOutstationKm=200;authorizedWorkshopReference='TEST-HR-V2-WORKSHOP';evidenceReference=($marker+'-17-Tuning');claimedAmount=50;currency='PKR'}}
    C01-NewService $hr $admin '2026-09-17' 2
    C01-NewService $hr $admin '2026-09-18' 1
    C01-NewService $hr $admin '2026-09-18' 2
    $trip='/api/v1/hr/vehicles/trips/'+(V2-Id 6)
    $c=V2-Call $admin GET ($trip+'/attribution-correction-context')
    Assert ($c.tripId -eq (V2-Id 6) -and $c.employeeId -eq $profile.HrEmployee -and $c.mileageEntryId -eq (V2-Id 7) -and $null -eq $c.latestRequestId -and $c.sourceFingerprint -cmatch '^[A-F0-9]{64}$' -and $c.tripVersion) 'Correction context differs.'
    $proposal=@{expectedTripVersion=$c.tripVersion;expectedLatestRequestId=$null;expectedSourceFingerprint=$c.sourceFingerprint;segments=(V2-Segments);evidenceReference=$profile.Model;reason=$profile.Model}
    $stale=$proposal.Clone();$stale.expectedTripVersion='AAAAAAAAAAA='
    V2-Denied $admin ($trip+'/attribution-corrections') $stale 'correction-stale' 409 'vehicle_attribution_trip_version_conflict'
    $r=V2-Call $admin POST ($trip+'/attribution-corrections') $proposal 'correction'
    $script:correctionId=V2-RequireId $r.requestId
    Assert ($r.state -ceq 'Pending' -and $r.version) 'Correction proposal differs.'
    $changed=$proposal.Clone();$changed.reason='TEST ONLY V2 changed payload'
    V2-Denied $admin ($trip+'/attribution-corrections') $changed 'correction' 409 'idempotency_key_reused'
    $review=@{expectedVersion=$r.version;decision=1;evidenceReference=$profile.Model;reason=$profile.Model}
    V2-Denied $admin ($trip+'/attribution-corrections/'+$script:correctionId+'/review') $review 'correction-review-self' 403 'vehicle_self_decision_forbidden'
    $stale=$review.Clone();$stale.expectedVersion='AAAAAAAAAAA='
    V2-Denied $super ($trip+'/attribution-corrections/'+$script:correctionId+'/review') $stale 'correction-review-stale' 409 'vehicle_adjustment_version_conflict'
    $r=V2-Call $super POST ($trip+'/attribution-corrections/'+$script:correctionId+'/review') $review 'correction-review'
    Assert ($r.requestId -eq $script:correctionId -and $r.state -ceq 'Approved' -and $r.decisionId -and (@($r.segments)|Measure-Object eligibleKm -Sum).Sum -eq 200) 'Correction approval differs.'
    $fuel='/api/v1/hr/vehicles/fuel-claims/'+(V2-Id 8)
    $c=V2-Call $admin GET ($fuel+'/reconciliation-context')
    Assert ($c.resourceId -eq (V2-Id 8) -and $c.tripId -eq (V2-Id 6) -and $c.correctionRequestId -eq $script:correctionId -and $null -eq $c.latestRequestId -and $c.sourceFingerprint -cmatch '^[A-F0-9]{64}$') 'Reconciliation context differs.'
    Assert-V2Amounts $c
    $proposal=@{expectedLatestRequestId=$null;expectedSourceFingerprint=$c.sourceFingerprint;evidenceReference=$profile.Model;reason=$profile.Model}
    $r=V2-Call $admin POST ($fuel+'/reconciliations') $proposal 'reconciliation'
    $script:reconciliationId=V2-RequireId $r.requestId
    Assert ($r.state -ceq 'Pending' -and $r.version) 'Reconciliation proposal differs.'
    $changed=$proposal.Clone();$changed.reason='TEST ONLY V2 changed payload'
    V2-Denied $admin ($fuel+'/reconciliations') $changed 'reconciliation' 409 'idempotency_key_reused'
    $review=@{expectedVersion=$r.version;decision=1;evidenceReference=$profile.Model;reason=$profile.Model}
    V2-Denied $admin ($fuel+'/reconciliations/'+$script:reconciliationId+'/review') $review 'reconciliation-review-self' 403 'vehicle_self_decision_forbidden'
    $stale=$review.Clone();$stale.expectedVersion='AAAAAAAAAAA='
    V2-Denied $super ($fuel+'/reconciliations/'+$script:reconciliationId+'/review') $stale 'reconciliation-review-stale' 409 'vehicle_adjustment_version_conflict'
    $r=V2-Call $super POST ($fuel+'/reconciliations/'+$script:reconciliationId+'/review') $review 'reconciliation-review'
    Assert ($r.requestId -eq $script:reconciliationId -and $r.state -ceq 'Approved' -and $r.decisionId -and $r.settlementState -ceq 'NotRecorded') 'Reconciliation approval differs.'
    Assert-V2Amounts $r.calculation
    foreach($date in @('2026-09-19','2026-09-20')){foreach($kind in 1..2){C01-NewService $hr $admin $date $kind}}
    $script:lateReplays=$true
    foreach($date in @('2026-09-17','2026-09-19')) {
        foreach($kind in @('Tuning','EngineOverhaul')) {
            $suffix='service-'+$kind+'-'+$date;$saved=$script:serviceReplays[$suffix]
            $r=V2-Call $hr POST '/api/v1/me/vehicles/maintenance' $saved.Body $suffix
            Assert ($r.id -eq $saved.Id -and $r.approvalState -ceq 'Pending') 'Historical submission replay differs.'
            $script:pendingServiceId=$saved.Id;$script:pendingServiceKey=$marker+'-'+$suffix
            $r=V2-Call $admin POST ('/api/v1/hr/vehicles/maintenance/'+$saved.Id+'/approve') @{} ($suffix+'-approve')
            Assert ($r.id -eq $saved.Id -and $r.approvalState -ceq 'Approved') 'After-successor approval replay differs.'
        }
    }
    Assert ($script:businessRequests -eq 44 -and $script:serviceIds.Count -eq 8 -and @($script:serviceIds|Select-Object -Unique).Count -eq 8) 'Continuation finite sequence differs.'
}

function C01-Rows([string]$json) {
    if([string]::IsNullOrWhiteSpace($json)){return ,@()}
    $rows=ConvertFrom-Json -InputObject $json -AsHashtable -NoEnumerate -DateKind String
    Assert ($rows -is [array]) 'Expected a SQL JSON array.'
    return ,$rows
}
function C01-MemberMap($rows) {
    $map=@{}
    foreach($row in $rows) {
        $key=@{}
        foreach($name in $row.Key.Keys) {
            $g=[guid]::Empty;$value=$row.Key[$name]
            $key[$name]=if($value -is [string] -and [guid]::TryParseExact($value,'D',[ref]$g)){$g.ToString('D')}else{$value}
        }
        $identity=ConvertTo-Json -InputObject (V2-Canonical $key) -Compress
        Assert (!$map.ContainsKey($identity) -and $row.Sha256 -cmatch '^[A-F0-9]{64}$') 'Ambiguous row membership.'
        $map[$identity]=$row.Sha256
    }
    return $map
}
function Assert-C01Manifest {
    Assert ((Get-FileHash -LiteralPath $profile.ManifestPath).Hash -ceq $profile.ManifestSha256) 'Reconciliation manifest hash differs.'
    $manifest=Get-Content -LiteralPath $profile.ManifestPath -Raw|ConvertFrom-Json -AsHashtable -DateKind String
    Assert ($manifest.SqlTableCount -eq 162 -and $manifest.SqlRowsUnchangedDuringInspection -and $manifest.OldRootUnchanged -and $manifest.Sources.Count -eq 12 -and $manifest.OldReferencePurposeAllowlist.Count -eq 10 -and $manifest.UnreferencedObjects.Count -eq 0) 'Reconciliation evidence incomplete.'
    Assert ($manifest.Vehicle.Id -eq $profile.VehicleId -and $manifest.PendingService.Id -eq $profile.PendingServiceId -and $manifest.PendingHeader.Id -eq $profile.PendingHeaderId -and $manifest.PendingHeader.State -eq 0 -and $null -eq $manifest.PendingHeader.ClosedAt) 'Reconciled pending identity differs.'
    Assert ($manifest.ServiceDecisionCount -eq 0 -and $manifest.ApprovalReceiptCount -eq 0 -and $manifest.ProposalCount -eq 0 -and $manifest.PendingMaintenanceCount -eq 1) 'Run is not the reviewed partial state.'
    $tables=C01-Rows (Read-Sql 'SELECT (SELECT s.name AS [schema],t.name AS [table] FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE t.is_ms_shipped=0 ORDER BY s.name,t.name FOR JSON PATH);')
    Assert ($tables.Count -eq $manifest.SqlTableMemberships.Count) 'Manifest table population changed.'
    foreach($table in $tables) {
        $name=$table.schema+'.'+$table.table
        Assert ($manifest.SqlTableMemberships.ContainsKey($name)) 'Table absent from manifest.'
        $qualified='['+$table.schema.Replace(']',']]')+'].['+$table.table.Replace(']',']]')+']';$literal=$qualified.Replace("'","''")
        $pk=Read-Sql ("SELECT STRING_AGG(CONVERT(nvarchar(max),'t.'+QUOTENAME(c.name)),',') WITHIN GROUP(ORDER BY ic.key_ordinal) FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE i.object_id=OBJECT_ID(N'"+$literal+"') AND i.is_primary_key=1;")
        Assert ([bool]$pk) 'Manifest table needs deterministic primary key.'
        $rows=C01-Rows (Read-Sql ("SELECT (SELECT JSON_QUERY((SELECT "+$pk+" FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)) AS [Key],CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT t.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)),2) AS Sha256 FROM "+$qualified+" t ORDER BY "+$pk+' FOR JSON PATH);'))
        Assert-Snapshot (C01-MemberMap $manifest.SqlTableMemberships[$name]) (C01-MemberMap $rows) ('Pinned manifest rows '+$name)
    }
    Assert-C01OldInventory $manifest.OldRootInventory
    return $manifest
}
function Assert-C01OldInventory($expected) {
    $inventory=@{}
    foreach($entry in Get-ChildItem -LiteralPath $profile.SourceRoot -Recurse -Force) {
        Assert (!($entry.Attributes -band [IO.FileAttributes]::ReparsePoint)) 'Linked old evidence forbidden.'
        $relative=[IO.Path]::GetRelativePath($profile.SourceRoot,$entry.FullName)
        Assert ($expected.ContainsKey($relative)) 'Unexpected old evidence member.'
        $old=$expected[$relative]
        Assert ($entry.PSIsContainer -eq $old.Directory -and [int]$entry.Attributes -eq $old.Attributes) 'Old evidence metadata differs.'
        if(!$entry.PSIsContainer){Assert-PrivateSource $entry.FullName $true;Assert ($entry.Length -eq $old.Length -and (Get-FileHash -LiteralPath $entry.FullName).Hash -ceq $old.Sha256) 'Old evidence content differs.'}
        $inventory[$relative]=$true
    }
    Assert ($inventory.Count -eq $expected.Count) 'Old evidence membership differs.'
}
function Assert-C01AuthPreflight {
    $actors="'"+$profile.HrAccount+"','"+$profile.AdminAccount+"','"+$profile.SuperAccount+"'"
    Assert ((Read-Sql ("SELECT COUNT(*) FROM auth.MobileOtpChallenges WHERE UserAccountId IN("+$actors+") AND Purpose='WebLogin' AND ConsumedAt IS NULL AND RevokedAt IS NULL;")) -ceq '0') 'Old open WebLogin challenge exists; stop, never exempt or revoke old OTP rows.'
    Assert ((Read-Sql ("SELECT COUNT(*) FROM auth.UserAccounts WHERE Id IN("+$actors+") AND (FailedLoginCount<>0 OR LockedUntil IS NOT NULL OR MfaRequired<>1);")) -ceq '0') 'Synthetic authentication counters/lock/MFA state differs.'
    $pins=@(
        @($profile.HrAccount,0,'74bb8943-7ec8-4b25-979f-4e30163e6935','1fcf6ecb-66ec-4da5-9cc2-e12d3d337ce3','ebcbfdd9-0a62-4b55-ade9-067d593504b5'),
        @($profile.AdminAccount,1,'af8b84dd-f0c0-4aca-987a-c12e7a3de5eb','5e351971-9e17-4b1b-81c4-efed06c33701','f59553a4-07ba-4d6d-b7e8-6357214753c3'),
        @($profile.SuperAccount,2,'9e89086c-c377-4f3b-b0b7-faf2c0845ca3','35b58f56-c750-4f63-84f2-053e179ed88a','9074fff1-63a5-4620-a77a-9537c1c17702'))
    foreach($actor in $pins) {
        $binding=" FROM auth.UserRoleAssignments a JOIN auth.Roles r ON r.Id=a.RoleId JOIN auth.UserAccessScopes s ON s.UserRoleAssignmentId=a.Id WHERE a.UserAccountId='"+$actor[0]+"' AND r.SystemRole="+$actor[1]+" AND r.Id='"+$actor[2]+"' AND a.Id='"+$actor[3]+"' AND s.Id='"+$actor[4]+"' AND r.IsActive=1 AND s.Kind=7 AND s.EmployeeId IS NULL AND s.DepartmentId IS NULL AND s.TeamId IS NULL AND s.SiteId IS NULL AND s.ProjectId IS NULL AND a.EffectiveFrom<=SYSUTCDATETIME() AND (a.EffectiveTo IS NULL OR a.EffectiveTo>SYSUTCDATETIME()) AND a.RevokedAt IS NULL"
        Assert ((Read-Sql ('SELECT COUNT(*)'+$binding+';')) -ceq '1') 'Pinned role assignment with All scope required; other existing HR assignments are retained.'
        Assert ((Read-Sql ("SELECT COUNT(DISTINCT p.Code) FROM auth.RolePermissions rp JOIN auth.Permissions p ON p.Id=rp.PermissionId WHERE rp.RoleId='"+$actor[2]+"' AND rp.RevokedAt IS NULL AND rp.GrantedAt<=SYSUTCDATETIME() AND p.Code IN('attendance.vehicle.maintenance.manage','attendance.vehicle.trips.manage','attendance.vehicle.fuel.manage','attendance.vehicle.self');")) -ceq '4') 'Pinned main role lacks required current grants.'
    }
}
function C01-RateState {
    return C01-Rows (Read-Sql "SELECT (SELECT [Key],WindowStartsAt,WindowEndsAt,[Count],CONVERT(varchar(18),RowVersion,1) AS RowVersion FROM auth.AuthRateBuckets WHERE [Key] IN('web-request-global','web-password-global','web-verify-global','request-global') OR [Key] IN(SELECT CONCAT(p.prefix,LOWER(CONVERT(varchar(36),a.Id))) FROM auth.UserAccounts a CROSS JOIN (VALUES('web-request-account:'),('web-password-account:'),('web-verify-account:')) p(prefix) WHERE a.LoginName IN('hr@example.invalid','admin@example.invalid','superadmin@example.invalid')) ORDER BY [Key] FOR JSON PATH);")
}
function Assert-C01Rates($old,$current) {
    $seen=@{};foreach($r in $current){Assert (!$seen.ContainsKey($r.Key)) 'Duplicate mutable rate key.';$seen[$r.Key]=$r}
    foreach($r in $old) {
        Assert ($seen.ContainsKey($r.Key)) 'Existing authentication rate key removed.';$now=$seen[$r.Key]
        if($now.WindowStartsAt -ceq $r.WindowStartsAt) {
            Assert ($now.WindowEndsAt -ceq $r.WindowEndsAt -and $now.Count -ge $r.Count -and $now.Count-$r.Count -le 100) 'Rate counter/window changed outside normal consumption.'
            if($now.Count -eq $r.Count){Assert ($now.RowVersion -ceq $r.RowVersion) 'Unused rate bucket changed.'}
        }else {
            $start=[DateTimeOffset]::Parse($now.WindowStartsAt);$end=[DateTimeOffset]::Parse($now.WindowEndsAt)
            Assert ($start -ge $script:startedUtc -and $start -le [DateTimeOffset]::UtcNow -and $end -gt $start -and $now.Count -ge 1 -and $now.Count -le 100) 'Rate reset outside continuation bounds.'
            Assert (($end-$start) -eq ([DateTimeOffset]::Parse($r.WindowEndsAt)-[DateTimeOffset]::Parse($r.WindowStartsAt))) 'Rate window duration changed.'
        }
    }
}
function C01-HeaderState {
    $rows=C01-Rows (Read-Sql ("SELECT (SELECT State,ClosedAt,CONVERT(varchar(18),RowVersion,1) AS RowVersion FROM workflow.ApprovalRequests WHERE Id='"+$profile.PendingHeaderId+"' FOR JSON PATH,INCLUDE_NULL_VALUES);"))
    Assert ($rows.Count -eq 1) 'Pinned pending header missing.'
    return $rows[0]
}
function Assert-C01HeaderTransition($before,$after,[bool]$complete) {
    Assert ($before.State -eq 0 -and $null -eq $before.ClosedAt -and $before.RowVersion) 'Initial header must be pending.'
    if($after.State -eq 0){Assert (!$complete -and $null -eq $after.ClosedAt -and $after.RowVersion -ceq $before.RowVersion) 'Pending header fields changed.';return}
    Assert ($after.State -eq 1 -and $after.ClosedAt -and $after.RowVersion -cne $before.RowVersion) 'Only pending-to-approved transition allowed.'
    $closed=[DateTimeOffset]::Parse($after.ClosedAt)
    Assert ($closed -ge $script:startedUtc -and $closed -le [DateTimeOffset]::UtcNow) 'Header closure outside continuation.'
}
function C01-Snapshot([bool]$capture=$false) {
    $result=V2-DatabaseSnapshot $capture
    # All original headers retain every field, except three fields on the single pinned header.
    $projection=Read-Sql ("SELECT STRING_AGG(CONVERT(nvarchar(max),CASE WHEN name IN('State','ClosedAt','RowVersion') THEN 'CASE WHEN Id='''+CONVERT(varchar(36),'"+$profile.PendingHeaderId+"')+''' THEN NULL ELSE '+QUOTENAME(name)+' END AS '+QUOTENAME(name) ELSE QUOTENAME(name) END),',') WITHIN GROUP(ORDER BY column_id) FROM sys.columns WHERE object_id=OBJECT_ID('workflow.ApprovalRequests');")
    $members=$script:baselineIds['workflow.ApprovalRequests'].Replace("'","''")
    $result['workflow.ApprovalRequests']=Read-Sql ("SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT "+$projection+" FROM workflow.ApprovalRequests WHERE Id IN(SELECT Id FROM OPENJSON(N'"+$members+"') WITH(Id uniqueidentifier '$.Id')) ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES)),2);")
    return $result
}
function Assert-C01Snapshot($before,$after,[bool]$complete) {
    Assert ($before.Count -eq $after.Count) 'Continuation table population differs.'
    $deltas=@{'hr.VehicleServiceRecords'=7;'workflow.ApprovalRequests'=9;'workflow.ApprovalDecisions'=10;'workflow.IdempotencyReceipts'=19;'auth.UserSessions'=3;'auth.RefreshTokens'=3;'auth.MobileOtpChallenges'=3;'auth.OtpDeliveries'=3}
    foreach($name in $before.Keys) {
        Assert ($after.ContainsKey($name)) 'Continuation snapshot key missing.'
        if(!$name.EndsWith('#count')){Assert ($before[$name] -ceq $after[$name]) ('Existing rows changed: '+$name);continue}
        $table=$name.Substring(0,$name.Length-6);$delta=[long]$after[$name]-[long]$before[$name]
        if($table -ceq 'audit.AuditEvents'){Assert ($delta -ge $(if($complete){19}else{0}) -and $delta -le 100) 'Continuation audit bound exceeded.'}
        elseif($table -ceq 'auth.AuthRateBuckets'){Assert ($delta -ge 0 -and $delta -le 100) 'Continuation rate bound exceeded.'}
        else {$expected=if($deltas.ContainsKey($table)){$deltas[$table]}else{0};Assert (($complete -and $delta -eq $expected) -or (!$complete -and $delta -ge 0 -and $delta -le $expected)) ('Continuation row delta differs: '+$table)}
    }
}
function Assert-C01Preservation([bool]$complete) {
    Assert-C01OldInventory $manifest.OldRootInventory
    $after=C01-Snapshot
    Assert-C01Snapshot $before $after $complete
    $headerAfter=C01-HeaderState
    Assert-C01HeaderTransition $headerBefore $headerAfter $complete
    $decisions=C01-Rows (Read-Sql ("SELECT (SELECT Decision,ActorUserId,DecidedAt,ProtectedChangeReference FROM workflow.ApprovalDecisions WHERE ApprovalRequestId='"+$profile.PendingHeaderId+"' FOR JSON PATH);"))
    Assert ($decisions.Count -eq $(if($headerAfter.State -eq 1){1}else{0})) 'Pending-header decision population differs.'
    if($decisions.Count){Assert ($decisions[0].Decision -eq 1 -and $decisions[0].ActorUserId -eq $profile.AdminAccount -and $decisions[0].DecidedAt -ceq $headerAfter.ClosedAt -and $decisions[0].ProtectedChangeReference -ceq $manifest.PendingService.EligibilityCalculationPrivateReference) 'Pending-header decision fields differ.'}
    Assert ((Read-Sql 'SELECT COUNT(*) FROM auth.UserAccounts WHERE FailedLoginCount<>0 OR LockedUntil IS NOT NULL;') -ceq '0') 'Authentication counter/lock lifecycle differs.'
    $rateAfter=C01-RateState;Assert-C01Rates $rateBefore $rateAfter
    Assert-Snapshot $prior (V2-PriorSnapshot) 'All original private evidence'
    $files=@(Get-ChildItem -LiteralPath (Join-Path $root 'files') -File -Force)
    Assert ($files.Count -le 49 -and (!$complete -or $files.Count -eq 49)) 'Continuation private business file count differs.'
    foreach($file in $files){Assert-PrivateSource $file.FullName;Assert ($file.Extension -ceq '.enc') 'Unexpected private business file.'}
    Write-Result $(if($complete){'preservation.json'}else{'partial-preservation.json'}) @{Before=$before;After=$after;HeaderBefore=$headerBefore;HeaderAfter=$headerAfter;RateBefore=$rateBefore;RateAfter=$rateAfter;PendingHeaderDecisions=$decisions;PriorEvidenceUnchanged=$true;ExistingOtpRowsUnchanged=$true;PrivateBusinessFiles=$files.Count;OriginalV2FullPreservationClaim=$false}
}

# Inspect has no writes; Execute requires the exact packet and a never-used continuation root.
Import-C01
Invoke-C01ReadScope {
$profile=Get-C01Profile;$root=$profile.Root;$pipe=$profile.Pipe;$owner=$profile.Owner;$marker=$profile.Marker
$base=[uri]'https://localhost:7481/';$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$release=[IO.Path]::GetFullPath($ReleaseDirectory);$assembly=Join-Path $release 'Cosmix.Api.dll'
$accountsPath='C:\Users\Huma.Khan\AppData\Local\Cosmix\LocalTesting\Test-Accounts.json'
$sourceSettings='C:\Users\Huma.Khan\AppData\Local\Cosmix\LocalTesting\Dashboard-20261019-Diagnostic.json'
$resultsRelative='results';$checks=[Collections.Generic.List[object]]::new();$httpResults=[Collections.Generic.List[object]]::new();$clients=[Collections.Generic.List[object]]::new();$sessions=[Collections.Generic.List[object]]::new()
$script:vehicleId=$profile.VehicleId;$script:requests=0;$script:businessRequests=0;$script:expectedRequest=$null;$script:unexpectedHttp=$null
$script:serviceIds=[Collections.Generic.List[string]]::new();$script:serviceReplays=@{};$script:baselineIds=@{};$script:lateReplays=$false
$script:loginEmails=[Collections.Generic.HashSet[string]]::new();$script:otpReads=[Collections.Generic.HashSet[string]]::new();$script:startedUtc=[DateTimeOffset]::UtcNow
$hostProcess=$null;$started=$false;$completed=$false;$primaryFailure=$null
Assert ($IsWindows -and $PSVersionTable.PSVersion -ge [version]'7.5') 'PowerShell7.5+ Windows required.'
Assert ([Security.Principal.WindowsIdentity]::GetCurrent().Name -ieq $owner) 'Unexpected operator.'
Assert ($script:startedUtc -lt [DateTimeOffset]'2026-10-26T19:00:00Z') 'Continuation cutoff reached.'
Assert (!(Test-Path -LiteralPath $root)) 'Continuation root already exists; no retry/reset.'
Assert ($release.StartsWith('D:\CosmixVerification\',[StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $assembly)) 'Frozen release required.'
Assert-V2Space ([IO.DriveInfo]::new('C:\').AvailableFreeSpace)
Assert (@(Get-NetTCPConnection -State Listen -LocalPort 7481 -ErrorAction SilentlyContinue).Count -eq 0) 'Port7481 occupied.'
foreach($path in @($root,$release,$profile.ManifestPath)) {for($p=$path;$p;$p=[IO.Path]::GetDirectoryName($p)){if(Test-Path -LiteralPath $p){Assert (!((Get-Item -LiteralPath $p).Attributes -band [IO.FileAttributes]::ReparsePoint)) 'Linked input ancestor.'}}}
Assert-V2Target
$manifest=Assert-C01Manifest
Assert-C01AuthPreflight
Assert-PrivateSource $accountsPath;Assert-PrivateSource $sourceSettings
$credentials=Get-Content -LiteralPath $accountsPath -Raw|ConvertFrom-Json
foreach($email in @('hr@example.invalid','admin@example.invalid','superadmin@example.invalid')){Assert (@($credentials|Where-Object Email -CEQ $email).Count -eq 1) 'Synthetic credential missing or ambiguous.'}
$credentials=$null
$before=C01-Snapshot $true;$headerBefore=C01-HeaderState;$rateBefore=C01-RateState;$prior=V2-PriorSnapshot
$packet=[Collections.Generic.List[string]]::new()
foreach($directory in @($release,(Join-Path $repo 'src'))) {
    foreach($file in Get-ChildItem -LiteralPath $directory -Recurse -File -Force|Where-Object {$_.FullName -notmatch '[\\/](bin|obj)[\\/]' -or $directory -eq $release}|Sort-Object FullName) {
        Assert (!($file.Attributes -band [IO.FileAttributes]::ReparsePoint)) 'Linked packet input.';$packet.Add($file.FullName+'|'+(Get-FileHash -LiteralPath $file.FullName).Hash)
    }
}
foreach($path in @($PSCommandPath,(Join-Path $PSScriptRoot 'Invoke-HrVehicleV2.ps1'),(Join-Path $PSScriptRoot 'Invoke-HrVehicleV1.ps1'),$profile.ManifestPath,$accountsPath,$sourceSettings)){$packet.Add($path+'|'+(Get-FileHash -LiteralPath $path).Hash)}
foreach($name in $before.Keys|Sort-Object){$packet.Add('database|'+$name+'|'+$before[$name])}
foreach($name in $prior.Keys|Sort-Object){$packet.Add('prior|'+$name+'|'+$prior[$name])}
$packetHash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes(($packet -join [char]10))))
[pscustomobject]@{Mode=$Mode;Profile=$profile.Name;PacketSha256=$packetHash;Migrations=26;MaxHttpRequests=100;MaxLocalMfa=3;BusinessRequests=44;NewServices=7;ExistingPendingApproval=1;NewPrivateBusinessFiles=49;Writes=$false}
if($Mode -ceq 'Inspect'){return}
Assert ($ApprovedPacketSha256 -cmatch '^[A-F0-9]{64}$' -and $ApprovedPacketSha256 -ceq $packetHash) 'Exact inspected packet required.'
Assert-V2Space ([IO.DriveInfo]::new('C:\').AvailableFreeSpace)
try {
    New-PrivateDirectory $root
    foreach($dir in @('config','files','keys','keys\data-protection','results')){New-PrivateDirectory (Join-Path $root $dir)}
    $settings=Get-Content -LiteralPath $sourceSettings -Raw|ConvertFrom-Json -AsHashtable
    $mobile=$settings.MobileAuthentication;$web=$settings.WebAuthentication
    $mobile.Crypto.KeyVersion=$profile.CryptoVersion
    $mobile.Crypto.VerifierKeyBase64=[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32));$mobile.Crypto.EncryptionKeyBase64=[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    $mobile.Sms=@{Provider='LocalTesting';Endpoint='';BearerToken=''}
    $config=@{
        Urls=$base.AbsoluteUri.TrimEnd('/');AllowedHosts='localhost'
        LocalTesting=@{Enabled=$true;ExistingAcceptanceProfile=$profile.Name;PrivateDirectory=$root;AllowUnencryptedLocalPipe=$true}
        ConnectionStrings=@{CosmixDatabase="Server=$pipe;Database=Cosmix_Testing;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=5;Application Name=$($profile.ApplicationName)"}
        PrivateFiles=@{Directory=(Join-Path $root 'files');KeyFile=(Join-Path $root 'keys\files.key');KeyReference=$profile.KeyReference}
        Jwt=@{Issuer=$profile.ApplicationName;Audience=$profile.ApplicationName;SigningKey=[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))}
        MobileAuthentication=$mobile;WebAuthentication=$web
        EmployeeFileScanner=@{Enabled=$false};AttendanceJobs=@{Enabled=$false};SelfHostedFace=@{Enabled=$false};FaceCapture=@{Enabled=$false};FaceTemplateStorage=@{Enabled=$false};FaceStorageRetention=@{Enabled=$false};FaceStorageRetentionJournal=@{Enabled=$false};Cors=@{AllowedOrigins=@('https://localhost:5173')}
    }
    Write-Private 'keys\files.key' ([Security.Cryptography.RandomNumberGenerator]::GetBytes(32));Write-Json 'config\host.json' $config
    $config=$null;$settings=$null;$mobile=$null;$web=$null
    Write-Result 'start.json' @{PacketSha256=$packetHash;ManifestSha256=$profile.ManifestSha256;Before=$before;BaselineIds=$script:baselineIds;BaselineRateKeys=$script:baselineRateKeys;HeaderBefore=$headerBefore;RateBefore=$rateBefore;Prior=$prior;Release=$release;Utc=$script:startedUtc;OriginalV2PreservationLimitation='161/162 original hashes matched; original OTP other-field equality unknown.'}
    Assert-V2Target;Assert-C01AuthPreflight
    $null=Assert-C01Manifest
    Assert-V2Space ([IO.DriveInfo]::new('C:\').AvailableFreeSpace)
    $start=[Diagnostics.ProcessStartInfo]::new((Get-Command dotnet -CommandType Application).Source)
    $start.ArgumentList.Add($assembly);$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.WorkingDirectory=$release;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true;$start.Environment.Clear()
    foreach($key in @('SystemRoot','WINDIR','USERPROFILE','LOCALAPPDATA','APPDATA','PATH','TEMP','TMP','ProgramFiles','ProgramW6432','DOTNET_ROOT','COMPUTERNAME','USERNAME','USERDOMAIN')){$value=[Environment]::GetEnvironmentVariable($key);if($value){$start.Environment[$key]=$value}}
    $start.Environment['COSMIX_EXISTING_TESTING_HOST']=$profile.Name;$start.Environment['COSMIX_LOCAL_TESTING_SETTINGS']=Join-Path $root 'config\host.json';$start.Environment['ASPNETCORE_ENVIRONMENT']='Development'
    $hostProcess=[Diagnostics.Process]::new();$hostProcess.StartInfo=$start;Assert ($hostProcess.Start()) 'Owned continuation host failed to start.';$started=$true
    $stdout=$hostProcess.StandardOutput.ReadToEndAsync();$stderr=$hostProcess.StandardError.ReadToEndAsync();$ready=$false
    for($i=0;$i -lt 60;$i++) {
        Assert (!$hostProcess.HasExited) 'Owned host exited during startup.'
        $listeners=@(Get-NetTCPConnection -State Listen -LocalPort 7481 -ErrorAction SilentlyContinue)
        if($listeners.Count){Assert (@($listeners|Where-Object {$_.OwningProcess -ne $hostProcess.Id -or $_.LocalAddress -notin @('127.0.0.1','::1')}).Count -eq 0) 'Loopback host ownership differs.';$ready=$true;break};Start-Sleep -Milliseconds 500
    }
    Assert $ready 'Owned host readiness timeout.'
    $credentials=Get-Content -LiteralPath $accountsPath -Raw|ConvertFrom-Json
    $hr=Login $credentials 'hr@example.invalid';$admin=Login $credentials 'admin@example.invalid';$super=Login $credentials 'superadmin@example.invalid';$credentials=$null
    Invoke-C01Sequence $hr $admin $super
    foreach($session in $sessions){Check ($session.Email+': logout') ((Request $session.Client POST '/api/v1/auth/web/logout' @{} $session.Csrf).Status -eq 204);$session.LoggedOut=$true;Check ($session.Email+': revoked') ((Request $session.Client GET '/api/v1/me/permissions').Status -eq 401)}
    $completed=$true
} catch {
    $primaryFailure=$_
    try {if(Test-Path -LiteralPath (Join-Path $root 'results')){Write-Result 'failure.json' @{FailureType=$primaryFailure.Exception.GetType().FullName;UnexpectedHttp=$script:unexpectedHttp;Requests=$script:requests;Checks=$checks.ToArray();HttpResults=$httpResults.ToArray();RetryPerformed=$false}}}catch{Write-Warning 'Could not record failure; existing evidence retained.'}
    throw ('Continuation stopped; HTTP '+$script:unexpectedHttp.Status+' '+$script:unexpectedHttp.ErrorCode+'. No repair or retry.')
} finally {
    try {
        if($hostProcess -and $started) {
            if(!$hostProcess.HasExited){$hostProcess.Kill();Assert ($hostProcess.WaitForExit(10000)) 'Owned host did not stop.'}
            Write-Private 'results\host-stdout.log' ([Text.UTF8Encoding]::new($false).GetBytes($stdout.GetAwaiter().GetResult()));Write-Private 'results\host-stderr.log' ([Text.UTF8Encoding]::new($false).GetBytes($stderr.GetAwaiter().GetResult()))
            Write-Result 'host-final.json' @{ProcessId=$hostProcess.Id;Exited=$hostProcess.HasExited;DatabaseStopped=$false;CleanupPerformed=$false};$hostProcess.Dispose()
            Assert-C01Preservation $completed
            if($completed){Write-Result 'acceptance.json' @{Requests=$script:requests;Checks=$checks.ToArray();HttpResults=$httpResults.ToArray();ServiceIds=$script:serviceIds.ToArray();CorrectionId=$script:correctionId;ReconciliationId=$script:reconciliationId;OriginalAmount=1000;CorrectedAmount=1500;Difference=500;SettlementState='NotRecorded';ContinuationPreservationVerified=$true;OriginalV2FullPreservationClaim=$false;CompletedUtc=[DateTimeOffset]::UtcNow}}
        }
    } catch {
        if($null -eq $primaryFailure){throw}
        Write-Warning 'Continuation preservation NOT established; original failure retained.'
        try{Write-Result 'preservation-failure.json' @{FailureType=$_.Exception.GetType().FullName;VerificationCompleted=$false}}catch{Write-Warning 'Could not record preservation failure.'}
    } finally {foreach($client in $clients){$client.Dispose()}}
}
}
