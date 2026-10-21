[CmdletBinding()]
param([switch]$ApplyToSelectedOriginal)
$ErrorActionPreference='Stop';Set-StrictMode -Version Latest
$aHelperPath=Join-Path $PSScriptRoot 'RunGuardedCurrent12Clone.ps1';$aTokens=$null;$aErrors=$null
$aAst=[Management.Automation.Language.Parser]::ParseFile($aHelperPath,[ref]$aTokens,[ref]$aErrors)
if($aErrors.Count -ne 0){throw 'Reviewed preservation helpers do not parse.'}
foreach($aFunction in $aAst.FindAll({param($node)$node -is [Management.Automation.Language.FunctionDefinitionAst]},$false)){. ([scriptblock]::Create($aFunction.Extent.Text))}
$aPrivateRoot=[IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Cosmix/PartAContinuation/Database20261013'))
$aOperator=[Security.Principal.WindowsIdentity]::GetCurrent();$aOperatorName=$aOperator.Name
$aAllowedSids=@($aOperator.User.Value,'S-1-5-18','S-1-5-32-544');$aClock=[Diagnostics.Stopwatch]::StartNew()
$aConnections=[Collections.Generic.List[IDisposable]]::new();$aSource='Cosmix_Testing';$aClone='Cosmix_PartA_Acceptance_20261013'
$aTarget=if($ApplyToSelectedOriginal){$aSource}else{$aClone};$aAttemptRoot=$null;$aGuardFailure=$null;$aStage='B24 guarded preflight'
$aMigration='20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims'
$aBackend=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../cosmix-backend'))
$aSqlPath=Join-Path $aBackend "docs/sql/$aMigration.sql"
$aSqlSha='A0EEFC4D51E1BF1A0FA4BB970A8DCE8094A336EF6D9219BAD54A599136B525F2'
try {
 Check-Private $aPrivateRoot $true
 $aClones=@(Get-ChildItem -LiteralPath $aPrivateRoot -Directory -Filter 'clone-*');Require ($aClones.Count -eq 1) 'One original verified restore proof is required.'
 $aOriginalIntent=Get-Content -LiteralPath (Join-Path $aClones[0].FullName 'intent.json') -Raw|ConvertFrom-Json
 $aOriginalProof=Get-Content -LiteralPath (Join-Path $aClones[0].FullName 'frozen-a23-result.json') -Raw|ConvertFrom-Json
 Require ($aOriginalProof.RetainedRowsIdsAndSourceFactsPreserved -and $aOriginalProof.SourceUnchanged -and $aOriginalProof.FixtureMatches -eq 3 -and
  $aOriginalIntent.Operator -ieq $aOperatorName -and $aOriginalIntent.Instance -ceq 'CosmixUat_d893653346') 'Original backup/restore/fixture provenance differs.'
 $aExpectedFamily=[guid]$aOriginalIntent.Family
 $aManifestPath=Join-Path $aPrivateRoot 'FrozenA23-Migrations.json';Check-Private $aManifestPath
 Require ((Get-FileHash -LiteralPath $aManifestPath -Algorithm SHA256).Hash -ceq $aOriginalProof.ManifestSha256) 'A23 frozen graph changed.'
 $aPrefix=@(Get-Content -LiteralPath $aManifestPath -Raw|ConvertFrom-Json);Require ($aPrefix.Count -eq 23) 'A23 graph prefix differs.'
 $aHashInputs=@(
  @{Path="src/Cosmix.Infrastructure/Database/Migrations/$aMigration.cs";Sha='145F45652CEC957D267F103C1F3A8349CBEE6DDF6C37C4C5A405862AB9802C96'},
  @{Path="src/Cosmix.Infrastructure/Database/Migrations/$aMigration.Designer.cs";Sha='BA2ADD9B72E1D886769F376236028BACE97CBCEAB0C28F2041EB350A2F27F60F'},
  @{Path='src/Cosmix.Infrastructure/Database/Migrations/CosmixDbContextModelSnapshot.cs';Sha='D12E3C2783B08C1021C587137C3B56B1E838B2EC4ECE41323A002279B539D78E'},
  @{Path="docs/sql/$aMigration.sql";Sha=$aSqlSha},
  @{Path='src/Cosmix.Infrastructure/Database/Migrations/20261013121357_CompleteHrFoundationPartA.cs';Sha='377D2ED05117EF50B2A6F37F2E0FAFF64E15B00E42FDDAA0066D5689AAD04AA9'},
  @{Path='src/Cosmix.Infrastructure/Database/Migrations/20261013121357_CompleteHrFoundationPartA.Designer.cs';Sha='D17BFEFB4FD0A490FA623268667AD0B9404AF7393ADA04A63C08785CBDA3E02C'},
  @{Path='docs/sql/20261013121357_CompleteHrFoundationPartA.sql';Sha='787DB2C00217F6F9026CAA3AC56D9E5053CE71AA8925AE79304308133F8A1EF8'}
 )
 foreach($aInput in $aHashInputs){Require ((Get-FileHash -LiteralPath (Join-Path $aBackend $aInput.Path) -Algorithm SHA256).Hash -ceq $aInput.Sha) 'Reviewed frozen migration hash changed.'}
 $aSql=Read-BoundText $aSqlPath $aSqlSha;$aBatches=@(Split-ReviewedSql $aSql);Require ($aBatches.Count -eq 1) 'Reviewed B24 batch count changed.'
 $aInsert=[regex]::Matches($aSql,"(?is)INSERT\s+INTO\s+\[__EFMigrationsHistory\]\s*\([^)]*\)\s*VALUES\s*\(N?'([^']+)'\s*,\s*N?'([^']+)'\s*\)")
 Require ($aInsert.Count -eq 1 -and $aInsert[0].Groups[1].Value -ceq $aMigration) 'Reviewed B24 history insertion changed.'
 $aManifest=@($aPrefix)+@([pscustomobject]@{MigrationId=$aMigration;ProductVersion=$aInsert[0].Groups[2].Value})
 if($ApplyToSelectedOriginal){
  $aPrior=@(Get-ChildItem -LiteralPath $aPrivateRoot -Directory -Filter 'b24-clone-*');Require ($aPrior.Count -eq 1) 'One completed clone B24 acceptance is required.'
  $aAccepted=Get-Content -LiteralPath (Join-Path $aPrior[0].FullName 'result.json') -Raw|ConvertFrom-Json
  Require ($aAccepted.Target -ceq $aClone -and $aAccepted.SqlSha256 -ceq $aSqlSha -and $aAccepted.RetainedRowsIdsAndSourceFactsPreserved -and $aAccepted.IdempotentRerunVerified -and $aAccepted.MigrationCount -eq 24) 'Clone B24 SQL application/idempotency/preservation proof is missing.'
 }
 $aInfo=& SqlLocalDB info CosmixUat_d893653346 2>&1|Out-String
 Require ($LASTEXITCODE -eq 0 -and [regex]::Match($aInfo,'(?im)^[\t ]*Owner:[\t ]*([^\r\n]+)\r?$').Groups[1].Value.Trim() -ieq $aOperatorName) 'Existing alias owner differs.'
 & SqlLocalDB start CosmixUat_d893653346|Out-Null;Require ($LASTEXITCODE -eq 0) 'Existing selected startup failed.'
 $aInfo=& SqlLocalDB info CosmixUat_d893653346 2>&1|Out-String
 $aLivePipe=[regex]::Match($aInfo,'(?im)^[\t ]*Instance pipe name:[\t ]*([^\r\n]+)\r?$').Groups[1].Value.Trim();Require ($aLivePipe -match '^np:\\\\.\\pipe\\LOCALDB#[A-Fa-f0-9]{8}\\tsql\\query$') 'Only the exact live local pipe is allowed.'
 $aTargetConnection=Open-BoundConnection $aTarget;[void](Check-Database $aTargetConnection $aTarget);Check-History @(Read-History $aTargetConnection) 23
 $aBefore=@(Read-PreservationSnapshot $aTargetConnection)
 $aMode=if($ApplyToSelectedOriginal){'original'}else{'clone'};$aAttemptRoot=Join-Path $aPrivateRoot ('b24-'+$aMode+'-'+[guid]::NewGuid().ToString('N'));Protect-NewDirectory $aAttemptRoot
 $aBackup=Join-Path $aAttemptRoot 'Target23-CopyOnly-Checksum.bak';Require (-not(Test-Path -LiteralPath $aBackup)) 'Fresh backup path is occupied.'
 $aMaster=Open-BoundConnection 'master'
 New-PrivateJson 'intent.json' ([ordered]@{Target=$aTarget;Operator=$aOperatorName;Family=$aExpectedFamily;Before=$aBefore;History=@(Read-History $aTargetConnection);SqlSha256=$aSqlSha;InputHashes=$aHashInputs;FreshBackup=$aBackup;IndependentSourceReviewPassed=$true;AtUtc=[datetime]::UtcNow.ToString('O')})
 $aStage='B24 fresh23 backup'
 Execute-Sql $aMaster ('BACKUP DATABASE '+(Quote-Name $aTarget)+' TO DISK=@path WITH COPY_ONLY,CHECKSUM,NOFORMAT,NOINIT;') @{path=$aBackup}
 Check-Private $aBackup;$aBackupSha=(Get-FileHash -LiteralPath $aBackup -Algorithm SHA256).Hash
 $aHeader=@(Read-Rows $aMaster 'RESTORE HEADERONLY FROM DISK=@path;' @{path=$aBackup})
 Require ($aHeader.Count -eq 1 -and $aHeader[0].DatabaseName -ceq $aTarget -and [guid]$aHeader[0].FamilyGUID -eq $aExpectedFamily -and $aHeader[0].IsCopyOnly -and $aHeader[0].HasBackupChecksums -and -not $aHeader[0].IsDamaged) 'Fresh23 backup header mismatch.'
 Execute-Sql $aMaster 'RESTORE VERIFYONLY FROM DISK=@path WITH FILE=1,CHECKSUM;' @{path=$aBackup}
 Check-Preserved $aBefore @(Read-PreservationSnapshot $aTargetConnection $aBefore);Check-History @(Read-History $aTargetConnection) 23
 $aStage='reviewed B24 SQL application'
 Require ((Get-FileHash -LiteralPath $aSqlPath -Algorithm SHA256).Hash -ceq $aSqlSha) 'Reviewed SQL changed before application.'
 foreach($aBatch in $aBatches){[void](Check-Database $aTargetConnection $aTarget);Execute-Sql $aTargetConnection $aBatch}
 Check-History @(Read-History $aTargetConnection) 24;$aAfter=@(Read-PreservationSnapshot $aTargetConnection $aBefore);Check-Preserved $aBefore $aAfter $true
 $aRerun=$false
 if(-not $ApplyToSelectedOriginal){
  $aStage='clone B24 idempotent rerun';$aFullBefore=@(Read-PreservationSnapshot $aTargetConnection)
  foreach($aBatch in $aBatches){[void](Check-Database $aTargetConnection $aTarget);Execute-Sql $aTargetConnection $aBatch}
  Check-History @(Read-History $aTargetConnection) 24;Check-Preserved $aFullBefore @(Read-PreservationSnapshot $aTargetConnection $aFullBefore);$aRerun=$true
 }
 New-PrivateJson 'result.json' ([ordered]@{Target=$aTarget;MigrationCount=24;History=@(Read-History $aTargetConnection);Before=$aBefore;After=$aAfter;SqlSha256=$aSqlSha;Backup=$aBackup;BackupSha256=$aBackupSha;RetainedRowsIdsAndSourceFactsPreserved=$true;IdempotentRerunVerified=$aRerun;ProductionAccessed=$false;AtUtc=[datetime]::UtcNow.ToString('O')})
 Write-Output ([pscustomobject]@{AppliedTo=$aMode;MigrationCount=24;RetainedRowsIdsAndSourceFactsPreserved=$true;Fresh23BackupVerified=$true;IdempotentRerunVerified=$aRerun;ProductionAccessed=$false}|ConvertTo-Json -Compress)
} catch {
 if($null -ne $aAttemptRoot){try{New-PrivateJson 'failure.json' ([ordered]@{Stage=$aStage;Details=$_.Exception.ToString();Stack=$_.ScriptStackTrace;AtUtc=[datetime]::UtcNow.ToString('O');NoAutomaticCleanupOrRetry=$true})}catch{}}
 throw [InvalidOperationException]::new("B24 application stopped during $aStage. Review retained private evidence; no automatic cleanup/retry. $aGuardFailure")
}finally{foreach($aConnection in $aConnections){$aConnection.Dispose()}}
