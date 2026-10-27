[CmdletBinding()]
param([Parameter(Mandatory)][string]$ReportPath)
$ErrorActionPreference='Stop'

function Compare-V2Reconciliation($before,$after) {
    Assert ($before.Count -eq $after.Count) 'Snapshot table population differs.'
    $rows=@(foreach($name in $before.Keys|Where-Object {!$_.EndsWith('#count')}|Sort-Object) {
        Assert ($after.ContainsKey($name) -and $after.ContainsKey($name+'#count')) 'Snapshot key missing.'
        [ordered]@{Table=$name;BeforeHash=$before[$name];AfterHash=$after[$name];HashMatches=($before[$name] -ceq $after[$name]);BeforeCount=[long]$before[$name+'#count'];AfterCount=[long]$after[$name+'#count'];Delta=([long]$after[$name+'#count']-[long]$before[$name+'#count'])}
    })
    $guardPassed=$true
    try {Assert-V2Database $before $after $true}catch{$guardPassed=$false}
    return @{Tables=$rows;Matched=@($rows|Where-Object HashMatches).Count;Mismatched=@($rows|Where-Object {!$_.HashMatches}|ForEach-Object Table);OriginalGuardPassed=$guardPassed}
}

# Load definitions only: never invoke the acceptance runner's operational body.
$runner=Join-Path $PSScriptRoot 'Invoke-HrVehicleV2.ps1'
$tokens=$null;$errors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile($runner,[ref]$tokens,[ref]$errors)
if($errors.Count){throw 'Runner parser failure.'}
$required=@('Import-V2Common','Get-V2Profile','V2-Id','V2-RequireId','V2-Fields','V2-MembershipJson','V2-DatabaseSnapshot','Assert-V2Database','Assert-V2Target','V2-PriorSnapshot')
foreach($name in $required) {
    $definitions=@($ast.FindAll({param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst]},$true)|Where-Object Name -CEQ $name)
    if($definitions.Count -ne 1){throw 'Reconciliation helper missing or ambiguous.'}
    . ([scriptblock]::Create($definitions[0].Extent.Text))
}
Import-V2Common $PSScriptRoot
$script:readOnlyReader=(Get-Command Read-Sql).ScriptBlock
function Read-Sql([string]$query) {
    Assert ($query.TrimStart().StartsWith('SELECT ',[StringComparison]::Ordinal)) 'Only reviewed SELECT queries allowed.'
    return & $script:readOnlyReader $query
}
$profile=Get-V2Profile;$root=$profile.Root;$pipe=$profile.Pipe;$owner=$profile.Owner
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$destination=[IO.Path]::GetFullPath($ReportPath)
Assert ([IO.Path]::GetDirectoryName($destination) -ieq 'D:\CosmixVerification' -and [IO.Path]::GetExtension($destination) -ceq '.json') 'Report must be a new JSON artifact directly under D:\CosmixVerification.'
Assert (!(Test-Path -LiteralPath $destination)) 'Report exists; never overwrite evidence.'
for($p=[IO.Path]::GetDirectoryName($destination);$p;$p=[IO.Path]::GetDirectoryName($p)) {
    Assert (!((Get-Item -LiteralPath $p).Attributes -band [IO.FileAttributes]::ReparsePoint)) 'Linked report directory forbidden.'
}
Assert ($PSVersionTable.PSVersion -ge [version]'7.5' -and $IsWindows) 'PowerShell 7.5+ Windows required.'
Assert-V2Target
$startPath=Join-Path $root 'results/start.json'
Assert-PrivateSource $startPath
$startHash=(Get-FileHash -LiteralPath $startPath).Hash
$start=Get-Content -LiteralPath $startPath -Raw|ConvertFrom-Json -AsHashtable -DateKind String
Assert ($start.Before.Count -eq 324 -and !$start.ContainsKey('BaselineIds')) 'This reconstruction is scoped to the original 162-table partial V2 run.'
$stamp=([DateTimeOffset]::Parse($start.Utc)).ToUniversalTime().ToString('o')
Assert ($stamp.StartsWith('2026-10-26T08:31:35',[StringComparison]::Ordinal)) 'Unexpected original run start.'
$evidence=@{}
foreach($file in Get-ChildItem -LiteralPath (Join-Path $root 'results') -File) {
    Assert-PrivateSource $file.FullName $true
    $evidence[$file.FullName]=(Get-FileHash -LiteralPath $file.FullName).Hash
}
Assert-Snapshot $start.Prior (V2-PriorSnapshot) 'Original prior evidence'

# Time-based memberships are candidates, not evidence of equality. Every original hash is compared below.
$filters=@{
    'hr.Vehicles'="Id<>'2349b065-e018-4a1c-9917-0808ea4e634b'"
    'hr.Policies'="Id<>'$(V2-Id 1)'"
    'hr.PolicyVersions'="Id NOT IN('$(V2-Id 2)','$(V2-Id 3)','$(V2-Id 4)')"
    'hr.VehicleCustodies'="Id<>'$(V2-Id 5)'";'hr.VehicleTrips'="Id<>'$(V2-Id 6)'"
    'hr.VehicleMileageEntries'="Id<>'$(V2-Id 7)'";'hr.FuelClaims'="Id<>'$(V2-Id 8)'"
    'hr.VehicleServiceRecords'="Id<>'f31f46a9-3bf3-4bd4-b34e-61125bc27e11'"
    'workflow.ApprovalRequests'="Id NOT IN('$(V2-Id 9)','$(V2-Id 10)') AND SubjectId<>'f31f46a9-3bf3-4bd4-b34e-61125bc27e11'"
    'workflow.ApprovalDecisions'="Id NOT IN('$(V2-Id 11)','$(V2-Id 12)')"
    'workflow.IdempotencyReceipts'="[Key] NOT IN('TEST-HR-V2-20261026-create','TEST-HR-V2-20261026-service-Tuning-2026-09-17')"
    'audit.AuditEvents'="OccurredAt<'$stamp'";'auth.UserSessions'="CreatedAt<'$stamp'"
    'auth.RefreshTokens'="IssuedAt<'$stamp'";'auth.MobileOtpChallenges'="IssuedAt<'$stamp'";'auth.OtpDeliveries'="CreatedAt<'$stamp'"
}
$script:baselineIds=@{}
foreach($name in $filters.Keys) {
    $json=V2-MembershipJson (Read-Sql ('SELECT (SELECT Id FROM '+$name+' WHERE '+$filters[$name]+' ORDER BY Id FOR JSON PATH);')) Id
    Assert ((ConvertFrom-Json -InputObject $json -NoEnumerate).Count -eq [long]$start.Before[$name+'#count']) ('Ambiguous reconstructed membership: '+$name)
    $script:baselineIds[$name]=$json
}
$script:baselineRateKeys=V2-MembershipJson (Read-Sql ("SELECT (SELECT [Key] FROM auth.AuthRateBuckets WHERE WindowStartsAt<'"+$stamp+"' OR [Key] IN('web-request-global','web-password-global','web-verify-global','request-global') OR [Key] IN(SELECT CONCAT(p.prefix,LOWER(CONVERT(varchar(36),a.Id))) FROM auth.UserAccounts a CROSS JOIN (VALUES('web-request-account:'),('web-password-account:'),('web-verify-account:')) p(prefix)) ORDER BY [Key] FOR JSON PATH);")) Key
Assert ((ConvertFrom-Json -InputObject $script:baselineRateKeys -NoEnumerate).Count -eq [long]$start.Before['auth.AuthRateBuckets#count']) 'Ambiguous reconstructed rate membership.'
$links=Read-Sql ("SELECT (SELECT COUNT(*) AS Sessions,COUNT(DISTINCT s.UserAccountId) AS Actors,COUNT(DISTINCT c.Id) AS Challenges,COUNT(DISTINCT d.Id) AS Deliveries,COUNT(DISTINCT t.Id) AS RefreshTokens FROM auth.UserSessions s JOIN auth.MobileOtpChallenges c ON c.Id=s.SmsOtpChallengeId AND c.UserAccountId=s.UserAccountId JOIN auth.OtpDeliveries d ON d.ChallengeId=c.Id JOIN auth.RefreshTokens t ON t.UserSessionId=s.Id WHERE s.CreatedAt>='"+$stamp+"' AND c.IssuedAt>='"+$stamp+"' AND d.CreatedAt>='"+$stamp+"' AND t.IssuedAt>='"+$stamp+"' AND s.UserAccountId IN('"+$profile.HrAccount+"','"+$profile.AdminAccount+"','"+$profile.SuperAccount+"') FOR JSON PATH);")|ConvertFrom-Json -AsHashtable -NoEnumerate
Assert ($links.Count -eq 1) 'Ambiguous auth link summary.'
foreach($key in @('Sessions','Actors','Challenges','Deliveries','RefreshTokens')){Assert ($links[0][$key] -eq 3) 'Ambiguous new authentication rows.'}
$revokedJson=Read-Sql ("SELECT (SELECT c.Id,c.UserAccountId,a.LoginName,c.IssuedAt,c.RevokedAt,c.Purpose FROM auth.MobileOtpChallenges c JOIN auth.UserAccounts a ON a.Id=c.UserAccountId WHERE c.IssuedAt<'"+$stamp+"' AND c.RevokedAt>='"+$stamp+"' ORDER BY c.Id FOR JSON PATH);")
$revoked=if($revokedJson){ConvertFrom-Json -InputObject $revokedJson -AsHashtable -NoEnumerate -DateKind String}else{,@()}
foreach($row in $revoked) {
    Assert ($row.UserAccountId -in @($profile.HrAccount,$profile.AdminAccount,$profile.SuperAccount)) 'Unexpected revoked challenge account; no identity exported.'
    Assert ($row.LoginName -cin @('hr@example.invalid','admin@example.invalid','superadmin@example.invalid')) 'Unexpected revoked challenge login; no identity exported.'
}
$after=V2-DatabaseSnapshot
$comparison=Compare-V2Reconciliation $start.Before $after
Assert-Snapshot $start.Prior (V2-PriorSnapshot) 'Post-inspection prior evidence'
$evidenceAfter=@{}
foreach($file in Get-ChildItem -LiteralPath (Join-Path $root 'results') -File){$evidenceAfter[$file.FullName]=(Get-FileHash -LiteralPath $file.FullName).Hash}
Assert-Snapshot $evidence $evidenceAfter 'Existing V2 results'
Assert-V2Target
$report=[ordered]@{
    Schema='hr-vehicle-v2-readonly-reconciliation-v1';CheckedUtc=[DateTimeOffset]::UtcNow
    Target=@{Server=$profile.Server;Pid=$profile.Pid;Family=$profile.Family;Migrations=26}
    StartFileSha256=$startHash;ScriptSha256=(Get-FileHash -LiteralPath $PSCommandPath).Hash;RunnerSha256=(Get-FileHash -LiteralPath $runner).Hash
    OriginalRowsUnchanged=$comparison.OriginalGuardPassed;MatchedTables=$comparison.Matched;TotalTables=$comparison.Tables.Count;MismatchedTables=$comparison.Mismatched
    PriorEvidence=@{Files=$start.Prior.Count;HashesMatch=$true};ExistingV2Results=@{Files=$evidence.Count;HashesMatch=$true}
    NewAuthenticationLinks=$links[0];PreExistingChallengesRevokedSinceStart=$revoked;Tables=$comparison.Tables
    Limitations=@('Original start.json contains table hashes/counts, not row memberships or row-level before values.',
        'Memberships are reconstructed candidates checked against original counts and hashes; the mismatching table remains unverified.',
        'Equality of other fields on the revoked challenge, including immutable fields, is UNKNOWN. No original values are inferred.',
        'Observed revocation does not establish the sole cause or full extent of the OTP-table mismatch.',
        'Existing UserAccounts column exclusions and selected rate-bucket exclusions are unchanged; matching hashes cover only the original snapshot scope.',
        'Queries are read-only but not a transaction-wide point-in-time snapshot. This report is not SQL acceptance or authorization to retry.')
    DatabaseWrites=$false;HostStarted=$false;PrivateEvidenceWrites=$false;CleanupPerformed=$false
}
$bytes=[Text.UTF8Encoding]::new($false).GetBytes((ConvertTo-Json -InputObject $report -Depth 12))
$stream=[IO.File]::Open($destination,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
try {$stream.Write($bytes);$stream.Flush($true)}finally{$stream.Dispose()}
Write-Output ('Report='+$destination)
Write-Output ('ReportSha256='+(Get-FileHash -LiteralPath $destination).Hash)
Write-Output ('Original table hashes matched='+$comparison.Matched+'/'+$comparison.Tables.Count+'; mismatches='+($comparison.Mismatched -join ','))
if(!$comparison.OriginalGuardPassed){exit 2}
