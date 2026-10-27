[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ReleaseDirectory,
    [ValidateSet('Inspect','Execute')][string]$Mode = 'Inspect',
    [string]$ApprovedPacketSha256 = ''
)
$ErrorActionPreference='Stop'
$VerbosePreference='SilentlyContinue'
$DebugPreference='SilentlyContinue'

# Import definitions, never the V1 runner's operational body. Both files enter the packet.
function Import-V2Common([string]$Directory=$PSScriptRoot) {
    $tokens=$null;$errors=$null
    $ast=[Management.Automation.Language.Parser]::ParseFile((Join-Path $Directory 'Invoke-HrVehicleV1.ps1'),[ref]$tokens,[ref]$errors)
    if($errors.Count){throw 'Common runner parser failure.'}
    foreach($name in @('Assert','Check','Read-Sql','Assert-PrivateSource','New-PrivateDirectory','Write-Private','Write-Json','Write-Result','New-Client','Request','Login','Assert-Snapshot')) {
        $definitions=@($ast.FindAll({param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst]},$true)|Where-Object Name -CEQ $name)
        if($definitions.Count -ne 1){throw 'Common helper missing or ambiguous.'}
        . ([scriptblock]::Create($definitions[0].Extent.Text.Replace(('function '+$name),('function script:'+$name))))
    }
}
function Get-V2Profile {
    return @{
        Name='HumaHrVehicle20261026V2';Marker='TEST-HR-V2-20261026';Model='TEST ONLY V2 historical vehicle acceptance'
        Root='C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261026V2'
        Pipe='np:\\.\pipe\LOCALDB#8C03C2BC\tsql\query';Owner='COSMIXRND\Huma.Khan';Pid=22112
        Server='Software-PC1\LOCALDB#8C03C2BC';Family='D9C34627-18A2-41CF-A804-821E39516303'
        HrAccount='8475187f-bf3b-4f9c-a3ed-4bac32253005';HrEmployee='35d19922-70a8-4c80-8578-e1029360e9fc'
        AdminAccount='94a83b62-5b56-4450-9175-2a7a9e3261bf';AdminEmployee='7c505086-87ea-4fd2-980e-823ecccbe36a'
        SuperAccount='6c7ab9c5-c94c-4ad7-a67e-6143a970d26f';SuperEmployee='3e1e81a4-c2c2-452b-b784-41792aa3aad0'
        MaxHttp=160;MaxMfa=3
    }
}
function V2-Id([int]$number) {
    Assert ($number -ge 1 -and $number -le 12) 'Unknown fixture identity.'
    return '26102602-'+$number.ToString('0000')+'-4000-8000-'+$number.ToString('000000000000')
}
function V2-RequireId($value) {
    $id=[guid]::Empty
    Assert ([guid]::TryParseExact([string]$value,'D',[ref]$id) -and $id -ne [guid]::Empty) 'Invalid resource identity.'
    return $id.ToString('D')
}
function V2-Canonical($value) {
    if($value -is [Collections.IDictionary]) {
        $sorted=[ordered]@{}
        foreach($key in $value.Keys|Sort-Object -CaseSensitive){$sorted[$key]=V2-Canonical $value[$key]}
        return $sorted
    }
    if($value -is [array]) {return ,@($value|ForEach-Object {V2-Canonical $_})}
    return $value
}
function V2-Equal($actual,$expected) {
    return (ConvertTo-Json -InputObject (V2-Canonical $actual) -Depth 30 -Compress) -ceq
        (ConvertTo-Json -InputObject (V2-Canonical $expected) -Depth 30 -Compress)
}
function V2-Fields($body,[string[]]$fields) {
    if($body -isnot [Collections.IDictionary] -or $body.Count -ne $fields.Count){return $false}
    foreach($field in $fields){if(!$body.Contains($field)){return $false}}
    return $true
}
function Test-V2Request([string]$method,[string]$path,$body,[string]$key) {
    $v='/api/v1/hr/vehicles';$trip=$v+'/trips/'+(V2-Id 6);$fuel=$v+'/fuel-claims/'+(V2-Id 8)
    if($method -ceq 'GET') {
        return $null -eq $body -and !$key -and $path -cin @(($trip+'/attribution-correction-context'),($fuel+'/reconciliation-context'))
    }
    if($method -cne 'POST'){return $false}
    if($path -ceq $v) {
        return $key -ceq ($marker+'-create') -and (V2-Equal $body @{registrationNumber=$marker;vehicleType='Motorcycle';makeModel=$profile.Model;assetReference=$marker})
    }
    if(!$script:vehicleId){return $false}
    if($path -ceq '/api/v1/local-testing/vehicle-v2/seed') {
        return !$key -and (V2-Equal $body @{vehicleId=$script:vehicleId})
    }
    if($path -ceq '/api/v1/me/vehicles/maintenance') {
        if(!(V2-Fields $body @('vehicleId','serviceKind','serviceDate','odometerKm','approvedOutstationKm','authorizedWorkshopReference','evidenceReference','claimedAmount','currency'))){return $false}
        $kind=if($body.serviceKind -eq 1){'Tuning'}else{'EngineOverhaul'}
        $amount=if($body.serviceKind -eq 1){50}else{75}
        return $body.vehicleId -ceq $script:vehicleId -and $body.serviceKind -in @(1,2) -and
            $body.serviceDate -cin @('2026-09-17','2026-09-18','2026-09-19','2026-09-20') -and
            $body.odometerKm -eq 1200 -and $body.approvedOutstationKm -eq 200 -and $body.claimedAmount -eq $amount -and
            $body.currency -ceq 'PKR' -and $body.authorizedWorkshopReference -ceq 'TEST-HR-V2-WORKSHOP' -and
            $body.evidenceReference -ceq ($marker+'-'+$body.serviceDate.Substring(8,2)+'-'+$kind) -and $key -ceq ($marker+'-service-'+$kind+'-'+$body.serviceDate)
    }
    if($script:pendingServiceId -and $path -ceq ($v+'/maintenance/'+$script:pendingServiceId+'/approve')) {
        return (V2-Fields $body @()) -and $key -ceq ($script:pendingServiceKey+'-approve')
    }
    if($path -ceq ($trip+'/attribution-corrections')) {
        return $key -cin @(($marker+'-correction'),($marker+'-correction-stale')) -and (V2-Fields $body @('expectedTripVersion','expectedLatestRequestId','expectedSourceFingerprint','segments','evidenceReference','reason')) -and
            $body.expectedTripVersion -is [string] -and $body.expectedTripVersion.Length -gt 0 -and $null -eq $body.expectedLatestRequestId -and
            (!$key.EndsWith('-stale') -or $body.expectedTripVersion -ceq 'AAAAAAAAAAA=') -and
            $body.expectedSourceFingerprint -cmatch '^[A-F0-9]{64}$' -and (V2-Equal $body.segments (V2-Segments)) -and $body.evidenceReference -ceq $profile.Model -and
            ($body.reason -ceq $profile.Model -or ($script:correctionId -and $key -ceq ($marker+'-correction') -and $body.reason -ceq 'TEST ONLY V2 changed payload'))
    }
    if($path -ceq ($fuel+'/reconciliations')) {
        return $key -ceq ($marker+'-reconciliation') -and (V2-Fields $body @('expectedLatestRequestId','expectedSourceFingerprint','evidenceReference','reason')) -and
            $null -eq $body.expectedLatestRequestId -and $body.expectedSourceFingerprint -cmatch '^[A-F0-9]{64}$' -and $body.evidenceReference -ceq $profile.Model -and
            ($body.reason -ceq $profile.Model -or ($script:reconciliationId -and $body.reason -ceq 'TEST ONLY V2 changed payload'))
    }
    $review=($script:correctionId -and $path -ceq ($trip+'/attribution-corrections/'+$script:correctionId+'/review') -and $key -cin @(($marker+'-correction-review'),($marker+'-correction-review-self'),($marker+'-correction-review-stale'))) -or
        ($script:reconciliationId -and $path -ceq ($fuel+'/reconciliations/'+$script:reconciliationId+'/review') -and $key -cin @(($marker+'-reconciliation-review'),($marker+'-reconciliation-review-self'),($marker+'-reconciliation-review-stale')))
    return $review -and (V2-Fields $body @('expectedVersion','decision','evidenceReference','reason')) -and
        $body.expectedVersion -is [string] -and $body.expectedVersion.Length -gt 0 -and (!$key.EndsWith('-stale') -or $body.expectedVersion -ceq 'AAAAAAAAAAA=') -and $body.decision -eq 1 -and $body.evidenceReference -ceq $profile.Model -and $body.reason -ceq $profile.Model
}
function V2-Expect([string]$method,[string]$path,$body=$null,[string]$key='') {
    Assert (Test-V2Request $method $path $body $key) 'Request outside V2 allowlist.'
    $script:expectedRequest=@{Method=$method;Path=$path;Body=$body;Key=$key}
}
function Begin-Request($client,[string]$method,[string]$path,$body,[string]$csrf='',[string]$key='') {
    $route=$method+' '+$path
    $auth=$route -cin @('GET /api/v1/me/permissions','GET /api/v1/auth/web/csrf','POST /api/v1/auth/web/login','POST /api/v1/auth/web/local-testing/otp','POST /api/v1/auth/web/otp/verify','POST /api/v1/auth/web/logout')
    if($auth) {
        Assert (!$key) 'Auth idempotency key forbidden.'
        if($method -ceq 'GET'){Assert ($null -eq $body) 'GET body forbidden.'}
        if($path -ceq '/api/v1/auth/web/login') {
            Assert ($body -is [Collections.IDictionary] -and $body.Count -eq 2 -and $body.email -cin @('hr@example.invalid','admin@example.invalid','superadmin@example.invalid') -and $body.password -is [string] -and $body.password.Length -gt 0) 'Unapproved login.'
            Assert ($script:loginEmails.Count -lt 3 -and !$script:loginEmails.Contains($body.email)) 'Three unique local MFA logins only.'
        }
    } else {
        $expected=$script:expectedRequest
        Assert ($null -ne $expected -and $method -ceq $expected.Method -and $path -ceq $expected.Path -and $key -ceq $expected.Key -and (V2-Equal $body $expected.Body) -and (Test-V2Request $method $path $body $key)) 'Request differs from the current bounded V2 step.'
        Assert ($method -cne 'POST' -or $csrf.Length -gt 0) 'Business write requires CSRF.'
    }
    Assert ($script:requests -lt 160) 'Bounded HTTP request ceiling exceeded.'
    if($path -ceq '/api/v1/auth/web/login'){$null=$script:loginEmails.Add($body.email)}
    $script:requests++
    if(!$auth){$script:expectedRequest=$null;$script:businessRequests++}
    $message=[Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::new($method),$path)
    if($null -ne $body){$message.Content=[Net.Http.StringContent]::new(($body|ConvertTo-Json -Depth 30 -Compress),[Text.Encoding]::UTF8,'application/json')}
    if($csrf){$message.Headers.Add('X-CSRF-TOKEN',$csrf)}
    if($key){$message.Headers.Add('Idempotency-Key',$key)}
    $message.Headers.Add('X-Correlation-ID','HR-V2-'+[guid]::NewGuid().ToString('N'))
    return @{Task=$client.SendAsync($message);Request=$message}
}
function V2-Call($session,[string]$method,[string]$path,$body=$null,[string]$suffix='') {
    $key=if($suffix){$marker+'-'+$suffix}else{''}
    V2-Expect $method $path $body $key
    $r=Request $session.Client $method $path $body $session.Csrf $key
    if($r.Status -ne 200){$script:unexpectedHttp=@{Status=$r.Status;ErrorCode=$r.ErrorCode}}
    Check ($method+' '+$path+' HTTP '+$r.Status+' '+$r.ErrorCode) ($r.Status -eq 200)
    return $r.Value
}
function Finish-Request($pending) {
    $response=$pending.Task.GetAwaiter().GetResult()
    try {
        $text=$response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        $value=if($text){try {ConvertFrom-Json -InputObject $text -AsHashtable -NoEnumerate -DateKind String}catch {$null}}else{$null}
        # Never retain arbitrary ProblemDetails titles, details, response bodies or auth material.
        $allowed=@('Unauthorized','Forbidden','Bad Request','An unexpected error occurred','access_denied','invalid_authentication',
            'idempotency_key_reused','conflicting_change','concurrent_change_retry','access_version_conflict','access_write_conflict','access_temporarily_busy',
            'vehicle_self_decision_forbidden','vehicle_attribution_trip_version_conflict','vehicle_adjustment_version_conflict',
            'vehicle_service_eligibility_changed','vehicle_service_cycle_evidence_unavailable','vehicle_service_cycle_chronology_conflict',
            'vehicle_service_once_already_consumed','vehicle_service_cycle_policy_transition_unconfigured','vehicle_service_cycle_order_ambiguous',
            'vehicle_service_evidence_already_used','vehicle_service_cost_not_covered','vehicle_service_in_future')
        $title=if($value -is [Collections.IDictionary] -and $value.title -is [string] -and $value.title -cin $allowed){$value.title}else{$null}
        $httpResults.Add(@{Method=$pending.Request.Method.Method;Path=$pending.Request.RequestUri.AbsolutePath;Status=[int]$response.StatusCode;ErrorCode=$title})
        return @{Status=[int]$response.StatusCode;Value=$value;ErrorCode=$title}
    } finally {$response.Dispose();$pending.Request.Dispose()}
}
function V2-Denied($session,[string]$path,$body,[string]$suffix,[int]$status,[string]$code) {
    $key=$marker+'-'+$suffix
    V2-Expect POST $path $body $key
    $result=Request $session.Client POST $path $body $session.Csrf $key
    Check ('Denied '+$suffix) ($result.Status -eq $status -and $result.Value.title -ceq $code)
}
function V2-ConcurrentCreate($admin,$body) {
    $path='/api/v1/hr/vehicles';$key=$marker+'-create'
    V2-Expect POST $path $body $key
    $one=Begin-Request (New-Client $admin.Cookies) POST $path $body $admin.Csrf $key
    V2-Expect POST $path $body $key
    $two=Begin-Request (New-Client $admin.Cookies) POST $path $body $admin.Csrf $key
    [Threading.Tasks.Task]::WhenAll([Threading.Tasks.Task[]]@($one.Task,$two.Task)).GetAwaiter().GetResult()|Out-Null
    $a=Finish-Request $one;$b=Finish-Request $two
    Check 'Concurrent create replays one vehicle' ($a.Status -eq 200 -and $b.Status -eq 200 -and $a.Value.id -eq $b.Value.id)
    return $a.Value
}
function V2-Segments {
    return @(
        @{businessDate='2026-09-15';startedAt='2026-09-15T18:00:00Z';endedAt='2026-09-15T19:00:00Z';odometerStartKm=1000;odometerEndKm=1100;mapDistanceKm=100;policyVersionId=(V2-Id 2);commuteDistanceId=$null;evidenceReference=($marker+'-segment-1')},
        @{businessDate='2026-09-16';startedAt='2026-09-15T19:00:00Z';endedAt='2026-09-16T01:00:00Z';odometerStartKm=1100;odometerEndKm=1200;mapDistanceKm=100;policyVersionId=(V2-Id 3);commuteDistanceId=$null;evidenceReference=($marker+'-segment-2')}
    )
}
function Invoke-V2Services($hr,$admin,[bool]$after) {
    $dates=if($after){@('2026-09-19','2026-09-20')}else{@('2026-09-17','2026-09-18')}
    foreach($date in $dates) {
        foreach($kind in @('Tuning','EngineOverhaul')) {
            $suffix='service-'+$kind+'-'+$date
            $amount=if($kind -ceq 'Tuning'){50}else{75}
            $kindValue=if($kind -ceq 'Tuning'){1}else{2}
            $body=@{vehicleId=$script:vehicleId;serviceKind=$kindValue;serviceDate=$date;odometerKm=1200;approvedOutstationKm=200;authorizedWorkshopReference='TEST-HR-V2-WORKSHOP';evidenceReference=($marker+'-'+$date.Substring(8,2)+'-'+$kind);claimedAmount=$amount;currency='PKR'}
            $service=V2-Call $hr POST '/api/v1/me/vehicles/maintenance' $body $suffix
            $id=V2-RequireId $service.id
            $script:pendingServiceId=$id;$script:pendingServiceKey=$marker+'-'+$suffix
            Assert ($service.vehicleId -eq $script:vehicleId -and $service.employeeId -eq $profile.HrEmployee -and $service.approvalState -ceq 'Pending') 'Maintenance submission differs.'
            $approved=V2-Call $admin POST ('/api/v1/hr/vehicles/maintenance/'+$id+'/approve') @{} ($suffix+'-approve')
            Assert ($approved.id -eq $id -and $approved.approvalState -ceq 'Approved') 'Maintenance approval differs.'
            $script:serviceIds.Add($id)
            $again=V2-Call $admin POST ('/api/v1/hr/vehicles/maintenance/'+$id+'/approve') @{} ($suffix+'-approve')
            Assert ($again.id -eq $id -and $again.approvalState -ceq 'Approved') 'Maintenance replay differs.'
        }
    }
}
function Invoke-V2Sequence($hr,$admin,$super) {
    $v='/api/v1/hr/vehicles'
    $body=@{registrationNumber=$marker;vehicleType='Motorcycle';makeModel=$profile.Model;assetReference=$marker}
    $created=V2-ConcurrentCreate $admin $body
    $script:vehicleId=V2-RequireId $created.id
    Assert ($created.registrationNumber -ceq $marker -and $created.vehicleType -ceq 'Motorcycle' -and $created.assetReference -ceq $marker) 'Created vehicle differs.'
    $seed=V2-Call $super POST '/api/v1/local-testing/vehicle-v2/seed' @{vehicleId=$script:vehicleId}
    Assert ($seed.vehicleId -eq $script:vehicleId -and $seed.tripId -eq (V2-Id 6) -and $seed.fuelClaimId -eq (V2-Id 8) -and $seed.sourceRows -eq 12 -and $seed.privateFiles -eq 5 -and (V2-Equal @($seed.policyVersionIds) @((V2-Id 2),(V2-Id 3),(V2-Id 4)))) 'Seed response differs.'
    $script:seedSnapshot=V2-SourceSnapshot
    Invoke-V2Services $hr $admin $false
    $tripPath=$v+'/trips/'+(V2-Id 6)
    $context=V2-Call $admin GET ($tripPath+'/attribution-correction-context')
    Assert ($context.tripId -eq (V2-Id 6) -and $context.employeeId -eq $profile.HrEmployee -and $context.mileageEntryId -eq (V2-Id 7) -and $null -eq $context.latestRequestId -and $context.sourceFingerprint -cmatch '^[A-F0-9]{64}$' -and $context.tripVersion) 'Correction context differs.'
    $proposal=@{expectedTripVersion=$context.tripVersion;expectedLatestRequestId=$null;expectedSourceFingerprint=$context.sourceFingerprint;segments=(V2-Segments);evidenceReference=$profile.Model;reason=$profile.Model}
    $stale=$proposal.Clone();$stale.expectedTripVersion='AAAAAAAAAAA='
    V2-Denied $admin ($tripPath+'/attribution-corrections') $stale 'correction-stale' 409 'vehicle_attribution_trip_version_conflict'
    $correction=V2-Call $admin POST ($tripPath+'/attribution-corrections') $proposal 'correction'
    $script:correctionId=V2-RequireId $correction.requestId
    Assert ($correction.state -ceq 'Pending' -and $correction.version) 'Correction proposal differs.'
    $changed=$proposal.Clone();$changed.reason='TEST ONLY V2 changed payload'
    V2-Denied $admin ($tripPath+'/attribution-corrections') $changed 'correction' 409 'idempotency_key_reused'
    $review=@{expectedVersion=$correction.version;decision=1;evidenceReference=$profile.Model;reason=$profile.Model}
    V2-Denied $admin ($tripPath+'/attribution-corrections/'+$script:correctionId+'/review') $review 'correction-review-self' 403 'vehicle_self_decision_forbidden'
    $stale=$review.Clone();$stale.expectedVersion='AAAAAAAAAAA='
    V2-Denied $super ($tripPath+'/attribution-corrections/'+$script:correctionId+'/review') $stale 'correction-review-stale' 409 'vehicle_adjustment_version_conflict'
    $approved=V2-Call $super POST ($tripPath+'/attribution-corrections/'+$script:correctionId+'/review') $review 'correction-review'
    Assert ($approved.requestId -eq $script:correctionId -and $approved.state -ceq 'Approved' -and $approved.decisionId) 'Correction approval differs.'
    Assert ((@($approved.segments)|Measure-Object -Property eligibleKm -Sum).Sum -eq 200) 'Corrected eligible distance differs.'
    $fuelPath=$v+'/fuel-claims/'+(V2-Id 8)
    $context=V2-Call $admin GET ($fuelPath+'/reconciliation-context')
    Assert ($context.resourceId -eq (V2-Id 8) -and $context.tripId -eq (V2-Id 6) -and $context.correctionRequestId -eq $script:correctionId -and $null -eq $context.latestRequestId -and $context.sourceFingerprint -cmatch '^[A-F0-9]{64}$') 'Fuel context identity differs.'
    Assert-V2Amounts $context
    $proposal=@{expectedLatestRequestId=$null;expectedSourceFingerprint=$context.sourceFingerprint;evidenceReference=$profile.Model;reason=$profile.Model}
    $recon=V2-Call $admin POST ($fuelPath+'/reconciliations') $proposal 'reconciliation'
    $script:reconciliationId=V2-RequireId $recon.requestId
    Assert ($recon.state -ceq 'Pending' -and $recon.version) 'Reconciliation proposal differs.'
    $changed=$proposal.Clone();$changed.reason='TEST ONLY V2 changed payload'
    V2-Denied $admin ($fuelPath+'/reconciliations') $changed 'reconciliation' 409 'idempotency_key_reused'
    $review=@{expectedVersion=$recon.version;decision=1;evidenceReference=$profile.Model;reason=$profile.Model}
    V2-Denied $admin ($fuelPath+'/reconciliations/'+$script:reconciliationId+'/review') $review 'reconciliation-review-self' 403 'vehicle_self_decision_forbidden'
    $stale=$review.Clone();$stale.expectedVersion='AAAAAAAAAAA='
    V2-Denied $super ($fuelPath+'/reconciliations/'+$script:reconciliationId+'/review') $stale 'reconciliation-review-stale' 409 'vehicle_adjustment_version_conflict'
    $approved=V2-Call $super POST ($fuelPath+'/reconciliations/'+$script:reconciliationId+'/review') $review 'reconciliation-review'
    Assert ($approved.requestId -eq $script:reconciliationId -and $approved.state -ceq 'Approved' -and $approved.decisionId -and $approved.settlementState -ceq 'NotRecorded') 'Reconciliation approval differs.'
    Assert-V2Amounts $approved.calculation
    Invoke-V2Services $hr $admin $true
    Assert ($script:serviceIds.Count -eq 8 -and @($script:serviceIds|Select-Object -Unique).Count -eq 8) 'Eight distinct service records required.'
    Assert-Snapshot $script:seedSnapshot (V2-SourceSnapshot) 'Immutable seeded sources'
}
function Assert-V2Amounts($value) {
    Assert ($value.originalAmount -eq 1000 -and $value.correctedAmount -eq 1500 -and $value.differenceAmount -eq 500 -and $value.currency -ceq 'PKR') 'Expected original 1000, corrected 1500, difference +500 PKR.'
}
function V2-SourceSnapshot {
    $result=@{}
    if($null -eq $script:sourceFiles) {
        $script:sourceFiles=@(Get-ChildItem -LiteralPath (Join-Path $root 'files') -File -Force|ForEach-Object FullName)
        Assert ($script:sourceFiles.Count -ge 5) 'Seeded protected files missing.'
    }
    foreach($file in $script:sourceFiles) {
        Assert-PrivateSource $file
        $result['file|'+$file]=(Get-FileHash -LiteralPath $file).Hash
    }
    foreach($table in @('Policies','PolicyVersions','VehicleCustodies','VehicleTrips','VehicleMileageEntries','FuelClaims')) {
        $result[$table]=Read-Sql ("SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM hr.["+$table+"] WHERE CONVERT(varchar(36),Id) LIKE '26102602-%' ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES)),2);")
    }
    foreach($table in @('ApprovalRequests','ApprovalDecisions')) {
        $result[$table]=Read-Sql ("SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM workflow.["+$table+"] WHERE CONVERT(varchar(36),Id) LIKE '26102602-%' ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES)),2);")
    }
    return $result
}
function V2-PriorSnapshot {
    $parent=[IO.Path]::GetDirectoryName($root);$result=@{}
    Assert (Test-Path -LiteralPath $parent -PathType Container) 'Prior evidence parent missing.'
    $queue=[Collections.Generic.Queue[string]]::new();$queue.Enqueue($parent)
    while($queue.Count) {
        $directory=$queue.Dequeue()
        Assert (!((Get-Item -LiteralPath $directory).Attributes -band [IO.FileAttributes]::ReparsePoint)) 'Linked evidence ancestor.'
        foreach($entry in Get-ChildItem -LiteralPath $directory -Force) {
            if($entry.FullName -ieq $root){continue}
            Assert (!($entry.Attributes -band [IO.FileAttributes]::ReparsePoint)) 'Linked evidence entry.'
            if($entry.PSIsContainer){$queue.Enqueue($entry.FullName)}else {
                Assert-PrivateSource $entry.FullName $true
                $result[$entry.FullName]=(Get-FileHash -LiteralPath $entry.FullName).Hash
            }
        }
    }
    Assert ($result.Count -ge 24) 'Prior evidence population below known minimum.'
    return $result
}
function V2-MembershipJson($raw,[ValidateSet('Id','Key')][string]$field) {
    Assert ($null -eq $raw -or $raw -is [string]) 'Membership must be JSON text.'
    # SQL's scalar FOR JSON subquery can be NULL for zero rows. OPENJSON('') is invalid.
    if([string]::IsNullOrWhiteSpace($raw)){return '[]'}
    $rows=ConvertFrom-Json -InputObject $raw -AsHashtable -NoEnumerate -DateKind String
    Assert ($rows -is [array]) 'Membership must be a JSON array.'
    $seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach($row in $rows) {
        Assert ((V2-Fields $row @($field)) -and $row[$field] -is [string] -and $row[$field].Length -gt 0) 'Invalid membership entry.'
        $key=if($field -ceq 'Id'){V2-RequireId $row[$field]}else{$row[$field]}
        Assert ($key.Length -le 450 -and $seen.Add($key)) 'Invalid or duplicate membership key.'
    }
    return ConvertTo-Json -InputObject $rows -Depth 4 -Compress
}
function Invoke-V2ReadOnlyReconciliation($start,$candidateIds,[string]$candidateRateKeys) {
    # Candidates are not a new baseline: original counts AND original hashes must match.
    $savedIds=$script:baselineIds;$savedRateKeys=$script:baselineRateKeys
    try {
        $script:baselineIds=@{}
        foreach($name in $candidateIds.Keys) {
            Assert ($start.Before.ContainsKey($name+'#count')) 'Unknown membership table.'
            $json=V2-MembershipJson $candidateIds[$name] Id
            Assert ((ConvertFrom-Json -InputObject $json -NoEnumerate).Count -eq [long]$start.Before[$name+'#count']) ('Original membership count differs: '+$name)
            $script:baselineIds[$name]=$json
        }
        $script:baselineRateKeys=V2-MembershipJson $candidateRateKeys Key
        Assert ((ConvertFrom-Json -InputObject $script:baselineRateKeys -NoEnumerate).Count -eq [long]$start.Before['auth.AuthRateBuckets#count']) 'Original rate membership count differs.'
        $after=V2-DatabaseSnapshot
        Assert-V2Database $start.Before $after $true
        Assert-Snapshot $start.Prior (V2-PriorSnapshot) 'Original prior evidence'
        return $after
    } finally {$script:baselineIds=$savedIds;$script:baselineRateKeys=$savedRateKeys}
}
function V2-DatabaseSnapshot([bool]$capture=$false) {
    # Preserve existing rows in growing tables; unchanged tables are hashed in full.
    $tables=(Read-Sql "SELECT (SELECT s.name AS [schema],t.name AS [table] FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE t.is_ms_shipped=0 ORDER BY s.name,t.name FOR JSON PATH);")|ConvertFrom-Json
    $result=@{}
    $growing=@('hr.Vehicles','hr.Policies','hr.PolicyVersions','hr.VehicleCustodies','hr.VehicleTrips','hr.VehicleMileageEntries','hr.FuelClaims','hr.VehicleServiceRecords','workflow.ApprovalRequests','workflow.ApprovalDecisions','workflow.IdempotencyReceipts','audit.AuditEvents','auth.UserSessions','auth.RefreshTokens','auth.MobileOtpChallenges','auth.OtpDeliveries')
    foreach($table in $tables) {
        $qualified='['+$table.schema.Replace(']',']]')+'].['+$table.table.Replace(']',']]')+']'
        $name=$table.schema+'.'+$table.table;$literal=$qualified.Replace("'","''")
        $order=Read-Sql ("SELECT STRING_AGG(CONVERT(nvarchar(max),QUOTENAME(c.name)),',') WITHIN GROUP(ORDER BY ic.key_ordinal) FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE i.object_id=OBJECT_ID(N'"+$literal+"') AND i.is_primary_key=1;")
        Assert ([bool]$order) 'Deterministic primary key required.'
        $where='';$columns='*'
        if($name -cin $growing) {
            Assert ($order -ceq '[Id]') 'Growing table needs reviewed Id primary key.'
            if($capture){$script:baselineIds[$name]=Read-Sql ("SELECT (SELECT Id FROM "+$qualified+" ORDER BY Id FOR JSON PATH);")}
            Assert ($script:baselineIds.ContainsKey($name)) 'Missing baseline membership.'
            $script:baselineIds[$name]=V2-MembershipJson $script:baselineIds[$name] Id
            $where=" WHERE Id IN(SELECT Id FROM OPENJSON(N'"+$script:baselineIds[$name].Replace("'","''")+"') WITH(Id uniqueidentifier '$.Id'))"
        } elseif($name -ceq 'auth.UserAccounts') {
            $columns=Read-Sql "SELECT STRING_AGG(CONVERT(nvarchar(max),QUOTENAME(name)),',') WITHIN GROUP(ORDER BY column_id) FROM sys.columns WHERE object_id=OBJECT_ID('auth.UserAccounts') AND name NOT IN('FailedLoginCount','LockedUntil','RowVersion');"
        } elseif($name -ceq 'auth.AuthRateBuckets') {
            if($capture){$script:baselineRateKeys=Read-Sql 'SELECT (SELECT [Key] FROM auth.AuthRateBuckets ORDER BY [Key] FOR JSON PATH);'}
            Assert ($null -ne $script:baselineRateKeys) 'Missing rate membership.'
            $script:baselineRateKeys=V2-MembershipJson $script:baselineRateKeys Key
            $where=" WHERE [Key] IN(SELECT [Key] FROM OPENJSON(N'"+$script:baselineRateKeys.Replace("'","''")+"') WITH([Key] nvarchar(450) '$.Key')) AND [Key] NOT IN('web-request-global','web-password-global','web-verify-global','request-global') AND [Key] NOT IN(SELECT CONCAT(p.prefix,LOWER(CONVERT(varchar(36),a.Id))) FROM auth.UserAccounts a CROSS JOIN (VALUES('web-request-account:'),('web-password-account:'),('web-verify-account:')) p(prefix) WHERE a.LoginName IN('hr@example.invalid','admin@example.invalid','superadmin@example.invalid'))"
        }
        $result[$name]=Read-Sql ("SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT "+$columns+" FROM "+$qualified+$where+" ORDER BY "+$order+" FOR JSON PATH,INCLUDE_NULL_VALUES)),2);")
        $result[$name+'#count']=Read-Sql ("SELECT COUNT(*) FROM "+$qualified+';')
    }
    return $result
}
function Assert-V2Database($before,$after,[bool]$partial=$false) {
    Assert ($before.Count -eq $after.Count) 'Table population differs.'
    $deltas=@{'hr.Vehicles'=1;'hr.Policies'=1;'hr.PolicyVersions'=3;'hr.VehicleCustodies'=1;'hr.VehicleTrips'=1;'hr.VehicleMileageEntries'=1;'hr.FuelClaims'=1;'hr.VehicleServiceRecords'=8;'workflow.ApprovalRequests'=12;'workflow.ApprovalDecisions'=12;'workflow.IdempotencyReceipts'=21;'auth.UserSessions'=3;'auth.RefreshTokens'=3;'auth.MobileOtpChallenges'=3;'auth.OtpDeliveries'=3}
    foreach($name in $before.Keys) {
        Assert ($after.ContainsKey($name)) 'Snapshot key missing.'
        if($name.EndsWith('#count')) {
            $table=$name.Substring(0,$name.Length-6);$delta=[long]$after[$name]-[long]$before[$name]
            if($table -ceq 'audit.AuditEvents'){$minimum=if($partial){0}else{21};Assert ($delta -ge $minimum -and $delta -le 160) 'Audit count outside bound.'}
            elseif($table -ceq 'auth.AuthRateBuckets'){Assert ($delta -ge 0 -and $delta -le 100) 'Rate bucket count outside bound.'}
            else {$expected=if($deltas.ContainsKey($table)){$deltas[$table]}else{0};Assert (($delta -eq $expected) -or ($partial -and $delta -ge 0 -and $delta -le $expected)) ('Unexpected row delta: '+$table)}
        }else {Assert ($before[$name] -ceq $after[$name]) ('Original rows changed: '+$name)}
    }
}
function Assert-V2Target {
    $process=Get-CimInstance Win32_Process -Filter 'ProcessId=22112'
    Assert ($process -and $process.Name -ceq 'sqlservr.exe' -and $process.CommandLine.Contains('-sLOCALDB#8C03C2BC') -and $process.CommandLine.Contains('\Instances\MSSQLLocalDB\master.mdf')) 'V2 target unavailable or changed; no writes, instance start, attach or retarget performed.'
    $identity=Invoke-CimMethod -InputObject $process -MethodName GetOwner
    Assert ($identity.ReturnValue -eq 0 -and ($identity.Domain+'\'+$identity.User) -ieq $owner) 'SQL process owner differs.'
    $binding=Read-Sql "SELECT CONCAT(CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) COLLATE DATABASE_DEFAULT,'|',ORIGINAL_LOGIN() COLLATE DATABASE_DEFAULT,'|',SUSER_SNAME(d.owner_sid) COLLATE DATABASE_DEFAULT,'|',CONVERT(varchar(36),r.family_guid) COLLATE DATABASE_DEFAULT,'|',d.state_desc COLLATE DATABASE_DEFAULT,'|',d.user_access_desc COLLATE DATABASE_DEFAULT,'|',d.is_read_only) FROM sys.databases d JOIN sys.database_recovery_status r ON r.database_id=d.database_id WHERE d.database_id=DB_ID();"
    Assert ($binding -ieq 'Software-PC1\LOCALDB#8C03C2BC|COSMIXRND\Huma.Khan|COSMIXRND\Huma.Khan|D9C34627-18A2-41CF-A804-821E39516303|ONLINE|MULTI_USER|0') 'SQL target differs.'
    Assert ((Read-Sql "SELECT CONVERT(int,SERVERPROPERTY('ProcessID'));") -ceq '22112') 'SQL PID differs.'
    Assert ((Read-Sql "SELECT net_transport FROM sys.dm_exec_connections WHERE session_id=@@SPID;") -ceq 'Named pipe') 'Named pipe required.'
    Assert ((Read-Sql "SELECT STRING_AGG(CONVERT(nvarchar(max),physical_name),'|') WITHIN GROUP(ORDER BY file_id) FROM sys.database_files;") -ieq 'C:\Users\Huma.Khan\Cosmix_Testing.mdf|C:\Users\Huma.Khan\Cosmix_Testing_log.ldf') 'Database files differ.'
    Assert ((Read-Sql 'SELECT COUNT(*) FROM sys.database_files WHERE state<>0 OR type NOT IN(0,1);') -ceq '0') 'Database file state differs.'
    Assert ((Read-Sql 'SELECT (SELECT COUNT(*) FROM sys.triggers WHERE is_disabled=0 AND is_ms_shipped=0)+(SELECT COUNT(*) FROM sys.synonyms)+(SELECT COUNT(*) FROM sys.sql_expression_dependencies WHERE referenced_server_name IS NOT NULL OR referenced_database_name IS NOT NULL);') -ceq '0') 'Unreviewed SQL dependency.'
    $expected=@(Get-ChildItem -LiteralPath (Join-Path $repo 'src/Cosmix.Infrastructure/Database/Migrations') -Filter '*.cs'|Where-Object Name -Match '^\d{14}_[^.]+\.cs$'|Sort-Object Name|ForEach-Object BaseName)
    $applied=(Read-Sql 'SELECT MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;') -split '\r?\n'
    Assert ($expected.Count -eq 26 -and ($expected -join '|') -ceq ($applied -join '|')) 'Exact 26 migrations required; no migration execution.'
    Assert ((Read-Sql 'SELECT COUNT(*) FROM auth.OtpDeliveries WHERE Status NOT IN(1,2,3);') -ceq '0') 'Nonterminal OTP work exists.'
    Assert ((Read-Sql 'SELECT COUNT(*) FROM auth.UserAccounts;') -ceq '3') 'Synthetic account population differs.'
    $actors=@((Read-Sql "SELECT (SELECT a.Id,a.EmployeeId,a.LoginName FROM auth.UserAccounts a JOIN hr.Employees e ON e.Id=a.EmployeeId WHERE a.Status=3 AND a.WebAccessEnabled=1 AND (a.LockedUntil IS NULL OR a.LockedUntil<=SYSUTCDATETIME()) AND ((a.LoginName='hr@example.invalid' AND e.ApprovedLoginPhoneNumber='+12025550103') OR (a.LoginName='admin@example.invalid' AND e.ApprovedLoginPhoneNumber='+12025550102') OR (a.LoginName='superadmin@example.invalid' AND e.ApprovedLoginPhoneNumber='+12025550101')) ORDER BY a.LoginName FOR JSON PATH);")|ConvertFrom-Json)
    Assert ($actors.Count -eq 3) 'Synthetic account bindings differ.'
    Assert ($actors[0].Id -eq $profile.AdminAccount -and $actors[0].EmployeeId -eq $profile.AdminEmployee -and $actors[1].Id -eq $profile.HrAccount -and $actors[1].EmployeeId -eq $profile.HrEmployee -and $actors[2].LoginName -ceq 'superadmin@example.invalid') 'Reviewed actors differ.'
    $script:superAccount=V2-RequireId $actors[2].Id;$script:superEmployee=V2-RequireId $actors[2].EmployeeId
    Assert ($script:superAccount -ceq $profile.SuperAccount -and $script:superEmployee -ceq $profile.SuperEmployee) 'SuperAdmin identity differs.'
}
function Assert-V2Space([long]$freeBytes) {
    Assert ($freeBytes -ge 1GB) 'V2 requires at least 1 GiB free on C: before execution; no cleanup performed.'
}

Import-V2Common
$profile=Get-V2Profile;$root=$profile.Root;$pipe=$profile.Pipe;$owner=$profile.Owner;$marker=$profile.Marker
$base=[uri]'https://localhost:7481/';$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$release=[IO.Path]::GetFullPath($ReleaseDirectory);$assembly=Join-Path $release 'Cosmix.Api.dll'
$accountsPath='C:\Users\Huma.Khan\AppData\Local\Cosmix\LocalTesting\Test-Accounts.json'
$sourceSettings='C:\Users\Huma.Khan\AppData\Local\Cosmix\LocalTesting\Dashboard-20261019-Diagnostic.json'
$resultsRelative='results';$checks=[Collections.Generic.List[object]]::new();$httpResults=[Collections.Generic.List[object]]::new()
$clients=[Collections.Generic.List[object]]::new();$sessions=[Collections.Generic.List[object]]::new()
$script:requests=0;$script:businessRequests=0;$script:loginEmails=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$script:serviceIds=[Collections.Generic.List[string]]::new();$script:baselineIds=@{};$script:expectedRequest=$null;$script:sourceFiles=$null;$script:seedSnapshot=$null
$hostProcess=$null;$started=$false;$completed=$false;$primaryFailure=$null;$script:unexpectedHttp=$null
Assert ($IsWindows -and $PSVersionTable.PSVersion -ge [version]'7.5') 'PowerShell 7.5+ Windows required.'
Assert ([Security.Principal.WindowsIdentity]::GetCurrent().Name -ieq $owner) 'Unexpected operator.'
Assert ($release.StartsWith('D:\CosmixVerification\',[StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $assembly)) 'Frozen Release required.'
Assert (!(Test-Path -LiteralPath $root)) 'V2 root exists; no reset or replay.'
Assert-V2Space ([IO.DriveInfo]::new('C:\').AvailableFreeSpace)
Assert (@(Get-NetTCPConnection -State Listen -LocalPort 7481 -ErrorAction SilentlyContinue).Count -eq 0) 'Port 7481 occupied.'
foreach($path in @($root,$release)) {
    for($p=$path;$p;$p=[IO.Path]::GetDirectoryName($p)){if(Test-Path -LiteralPath $p){Assert (!((Get-Item -LiteralPath $p).Attributes -band [IO.FileAttributes]::ReparsePoint)) 'Linked ancestor forbidden.'}}
}
Assert-V2Target
Assert-PrivateSource $accountsPath;Assert-PrivateSource $sourceSettings
$credentials=Get-Content -LiteralPath $accountsPath -Raw|ConvertFrom-Json
foreach($email in @('hr@example.invalid','admin@example.invalid','superadmin@example.invalid')){Assert (@($credentials|Where-Object Email -CEQ $email).Count -eq 1) 'Credential entry missing or ambiguous.'}
$credentials=$null
Assert ((Read-Sql "SELECT COUNT(*) FROM hr.Vehicles WHERE RegistrationNumber='$marker';") -ceq '0') 'V2 vehicle already exists.'
foreach($item in @(@('hr.Policies',1),@('hr.PolicyVersions',2),@('hr.PolicyVersions',3),@('hr.PolicyVersions',4),@('hr.VehicleCustodies',5),@('hr.VehicleTrips',6),@('hr.VehicleMileageEntries',7),@('hr.FuelClaims',8),@('workflow.ApprovalRequests',9),@('workflow.ApprovalRequests',10),@('workflow.ApprovalDecisions',11),@('workflow.ApprovalDecisions',12))) {
    Assert ((Read-Sql ('SELECT COUNT(*) FROM '+$item[0]+" WHERE Id='"+(V2-Id $item[1])+"';")) -ceq '0') 'V2 fixture identity already exists.'
}
$before=V2-DatabaseSnapshot $true;$prior=V2-PriorSnapshot
$packet=[Collections.Generic.List[string]]::new()
# Parent owns release/source correspondence; the packet pins all source inputs and the frozen release.
foreach($directory in @($release,(Join-Path $repo 'src'))) {
    foreach($file in Get-ChildItem -LiteralPath $directory -Recurse -File -Force|Where-Object {$_.FullName -notmatch '[\\/](bin|obj)[\\/]' -or $directory -eq $release}|Sort-Object FullName) {
        Assert (!($file.Attributes -band [IO.FileAttributes]::ReparsePoint)) 'Linked packet file.'
        $packet.Add($file.FullName+'|'+(Get-FileHash -LiteralPath $file.FullName).Hash)
    }
}
foreach($path in @($PSCommandPath,(Join-Path $PSScriptRoot 'Invoke-HrVehicleV1.ps1'),$accountsPath,$sourceSettings)){$packet.Add($path+'|'+(Get-FileHash -LiteralPath $path).Hash)}
foreach($name in $before.Keys|Sort-Object){$packet.Add('database|'+$name+'|'+$before[$name])}
foreach($name in $prior.Keys|Sort-Object){$packet.Add('prior|'+$name+'|'+$prior[$name])}
$packetHash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes(($packet -join [char]10))))
[pscustomobject]@{Mode=$Mode;Profile=$profile.Name;PacketSha256=$packetHash;Migrations=26;MaxHttpRequests=160;MaxLocalMfa=3;SeedRows=12;Services=8;CorrectionAndReconciliationRequests=2;CorrectionAndReconciliationDecisions=2;Writes=$false}
if($Mode -eq 'Inspect'){return}
Assert ($ApprovedPacketSha256 -cmatch '^[A-F0-9]{64}$' -and $ApprovedPacketSha256 -ceq $packetHash) 'Exact inspected packet required.'
Assert-V2Space ([IO.DriveInfo]::new('C:\').AvailableFreeSpace)
try {
    New-PrivateDirectory $root
    foreach($dir in @('config','files','keys','keys\data-protection','results')){New-PrivateDirectory (Join-Path $root $dir)}
    $settings=Get-Content -LiteralPath $sourceSettings -Raw|ConvertFrom-Json -AsHashtable
    $mobile=$settings.MobileAuthentication;$web=$settings.WebAuthentication
    $mobile.Crypto.KeyVersion='hr-v2'
    $mobile.Crypto.VerifierKeyBase64=[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    $mobile.Crypto.EncryptionKeyBase64=[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    $mobile.Sms=@{Provider='LocalTesting';Endpoint='';BearerToken=''}
    $config=@{
        Urls=$base.AbsoluteUri.TrimEnd('/');AllowedHosts='localhost'
        LocalTesting=@{Enabled=$true;ExistingAcceptanceProfile=$profile.Name;PrivateDirectory=$root;AllowUnencryptedLocalPipe=$true}
        ConnectionStrings=@{CosmixDatabase="Server=$pipe;Database=Cosmix_Testing;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=5;Application Name=Cosmix.HrVehicle.V2"}
        PrivateFiles=@{Directory=(Join-Path $root 'files');KeyFile=(Join-Path $root 'keys\files.key');KeyReference='hr-vehicle-v2'}
        Jwt=@{Issuer='Cosmix.HrVehicle.V2';Audience='Cosmix.HrVehicle.V2';SigningKey=[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))}
        MobileAuthentication=$mobile;WebAuthentication=$web
        EmployeeFileScanner=@{Enabled=$false};AttendanceJobs=@{Enabled=$false};SelfHostedFace=@{Enabled=$false};FaceCapture=@{Enabled=$false};FaceTemplateStorage=@{Enabled=$false};FaceStorageRetention=@{Enabled=$false};FaceStorageRetentionJournal=@{Enabled=$false}
        Cors=@{AllowedOrigins=@('https://localhost:5173')}
    }
    Write-Private 'keys\files.key' ([Security.Cryptography.RandomNumberGenerator]::GetBytes(32));Write-Json 'config\host.json' $config
    $settings=$null;$config=$null;$mobile=$null;$web=$null
    Write-Result 'start.json' @{PacketSha256=$packetHash;Before=$before;BaselineIds=$script:baselineIds;BaselineRateKeys=$script:baselineRateKeys;Prior=$prior;Release=$release;Utc=[DateTimeOffset]::UtcNow}
    Assert-V2Target
    Assert-V2Space ([IO.DriveInfo]::new('C:\').AvailableFreeSpace)
    $start=[Diagnostics.ProcessStartInfo]::new((Get-Command dotnet -CommandType Application).Source)
    $start.ArgumentList.Add($assembly);$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.WorkingDirectory=$release
    $start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true;$start.Environment.Clear()
    foreach($key in @('SystemRoot','WINDIR','USERPROFILE','LOCALAPPDATA','APPDATA','PATH','TEMP','TMP','ProgramFiles','ProgramW6432','DOTNET_ROOT','COMPUTERNAME','USERNAME','USERDOMAIN')){$value=[Environment]::GetEnvironmentVariable($key);if($value){$start.Environment[$key]=$value}}
    $start.Environment['COSMIX_EXISTING_TESTING_HOST']=$profile.Name;$start.Environment['COSMIX_LOCAL_TESTING_SETTINGS']=Join-Path $root 'config\host.json';$start.Environment['ASPNETCORE_ENVIRONMENT']='Development'
    $hostProcess=[Diagnostics.Process]::new();$hostProcess.StartInfo=$start
    Assert ($hostProcess.Start()) 'Owned host start failed.';$started=$true
    $stdout=$hostProcess.StandardOutput.ReadToEndAsync();$stderr=$hostProcess.StandardError.ReadToEndAsync();$ready=$false
    for($i=0;$i -lt 60;$i++) {
        Assert (!$hostProcess.HasExited) 'Owned host exited during startup.'
        $listeners=@(Get-NetTCPConnection -State Listen -LocalPort 7481 -ErrorAction SilentlyContinue)
        if($listeners.Count){Assert (@($listeners|Where-Object {$_.OwningProcess -ne $hostProcess.Id -or $_.LocalAddress -notin @('127.0.0.1','::1')}).Count -eq 0) 'Loopback host ownership differs.';$ready=$true;break}
        Start-Sleep -Milliseconds 500
    }
    Assert $ready 'Host readiness timeout.'
    $credentials=Get-Content -LiteralPath $accountsPath -Raw|ConvertFrom-Json
    $hr=Login $credentials 'hr@example.invalid';$admin=Login $credentials 'admin@example.invalid';$super=Login $credentials 'superadmin@example.invalid';$credentials=$null
    Invoke-V2Sequence $hr $admin $super
    foreach($session in $sessions){Check ($session.Email+': logout') ((Request $session.Client POST '/api/v1/auth/web/logout' @{} $session.Csrf).Status -eq 204);$session.LoggedOut=$true;Check ($session.Email+': revoked') ((Request $session.Client GET '/api/v1/me/permissions').Status -eq 401)}
    Assert-V2Database $before (V2-DatabaseSnapshot)
    Assert-Snapshot $prior (V2-PriorSnapshot) 'Prior evidence'
    $completed=$true
} catch {
    $primaryFailure=$_
    try {
        if(Test-Path -LiteralPath (Join-Path $root 'results')){Write-Result 'failure.json' @{Checks=$checks.ToArray();HttpResults=$httpResults.ToArray();Requests=$script:requests;FailureType=$primaryFailure.Exception.GetType().FullName;UnexpectedHttp=$script:unexpectedHttp;Message='Stopped; no reset, extra logout, repair or replay.'}}
    } catch {Write-Warning 'Could not record primary failure; existing evidence retained.'}
    throw ('V2 stopped; HTTP '+$script:unexpectedHttp.Status+' '+$script:unexpectedHttp.ErrorCode+'. Inspect private redacted evidence. No reset or replay.')
} finally {
    try {
    foreach($client in $clients){$client.Dispose()}
    if($hostProcess -and $started) {
        if(!$hostProcess.HasExited){$hostProcess.Kill();Assert ($hostProcess.WaitForExit(10000)) 'Owned host did not stop.'}
        Write-Private 'results\host-stdout.log' ([Text.UTF8Encoding]::new($false).GetBytes($stdout.GetAwaiter().GetResult()))
        Write-Private 'results\host-stderr.log' ([Text.UTF8Encoding]::new($false).GetBytes($stderr.GetAwaiter().GetResult()))
        Write-Result 'host-final.json' @{ProcessId=$hostProcess.Id;Exited=$hostProcess.HasExited;DatabaseStopped=$false;CleanupPerformed=$false};$hostProcess.Dispose()
        Assert-Snapshot $prior (V2-PriorSnapshot) 'Post-host prior evidence'
        Assert-V2Database $before (V2-DatabaseSnapshot) (!$completed)
        if($script:seedSnapshot){Assert-Snapshot $script:seedSnapshot (V2-SourceSnapshot) 'Post-host immutable sources'}
        if($completed) {
            Write-Result 'acceptance.json' @{Checks=$checks.ToArray();HttpResults=$httpResults.ToArray();Requests=$script:requests;VehicleId=$script:vehicleId;ServiceIds=$script:serviceIds.ToArray();CorrectionId=$script:correctionId;ReconciliationId=$script:reconciliationId;OriginalAmount=1000;CorrectedAmount=1500;Difference=500;SettlementState='NotRecorded';MigrationsApplied=$false;PriorEvidenceUnchanged=$true;OriginalRowsUnchanged=$true;CompletedUtc=[DateTimeOffset]::UtcNow}
        }
    }
    } catch {
        if($null -eq $primaryFailure){throw}
        $preservationFailureType=$_.Exception.GetType().FullName
        Write-Warning 'Post-stop preservation verification failed; primary failure retained. Preservation is NOT established.'
        try {Write-Result 'preservation-failure.json' @{FailureType=$preservationFailureType;OriginalRowsUnchanged=$false;VerificationCompleted=$false}}
        catch {Write-Warning 'Could not record preservation failure; existing evidence retained.'}
    }
}
