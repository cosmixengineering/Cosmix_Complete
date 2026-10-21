[CmdletBinding()]
param([Parameter(Mandatory=$true)][switch]$ApplyToSelectedOriginal)
$ErrorActionPreference='Stop';Set-StrictMode -Version Latest
if(-not $ApplyToSelectedOriginal){throw 'Explicit selected-original application is required.'}
$aHelperPath=Join-Path $PSScriptRoot 'RunGuardedCurrent12Clone.ps1'
$aTokens=$null;$aErrors=$null;$aAst=[Management.Automation.Language.Parser]::ParseFile($aHelperPath,[ref]$aTokens,[ref]$aErrors)
if($aErrors.Count -ne 0){throw 'Reviewed helper source does not parse.'}
foreach($aFunction in $aAst.FindAll({param($node)$node -is [Management.Automation.Language.FunctionDefinitionAst]},$false)){
 . ([scriptblock]::Create($aFunction.Extent.Text))
}
$aPrivateRoot=[IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Cosmix/PartAContinuation/Database20261013'))
$aOperator=[Security.Principal.WindowsIdentity]::GetCurrent();$aOperatorName=$aOperator.Name
$aAllowedSids=@($aOperator.User.Value,'S-1-5-18','S-1-5-32-544');$aClock=[Diagnostics.Stopwatch]::StartNew()
$aConnections=[Collections.Generic.List[IDisposable]]::new();$aSource='Cosmix_Testing';$aClone='Cosmix_PartA_Acceptance_20261013'
$aGuardFailure=$null;$aAttemptRoot=$null;$aStage='selected-original preflight'
try {
 Check-Private $aPrivateRoot $true
 $aClones=@(Get-ChildItem -LiteralPath $aPrivateRoot -Directory -Filter 'clone-*')
 Require ($aClones.Count -eq 1) 'Exactly one owned successful clone proof is required.'
 $aProofRoot=$aClones[0].FullName;Check-Private $aProofRoot $true
 $aProofPath=Join-Path $aProofRoot 'frozen-a23-result.json';Check-Private $aProofPath
 $aProof=Get-Content -LiteralPath $aProofPath -Raw|ConvertFrom-Json
 $aRestore=Get-Content -LiteralPath (Join-Path $aProofRoot 'restore-verified.json') -Raw|ConvertFrom-Json
 $aOriginalIntent=Get-Content -LiteralPath (Join-Path $aProofRoot 'intent.json') -Raw|ConvertFrom-Json
 Require ($aProof.Source -ceq $aSource -and $aProof.Clone -ceq $aClone -and $aProof.SourceUnchanged -and
  $aProof.RetainedRowsIdsAndSourceFactsPreserved -and $aProof.FixtureMatches -eq 3 -and $aProof.Accounts -eq 5 -and $aProof.Employees -eq 14 -and
  $aRestore.TrueRestorePerformed -and $aRestore.ExactOriginalColumnsCompared -and $aRestore.SourceUnchanged -and
  $aOriginalIntent.Operator -ieq $aOperatorName -and $aOriginalIntent.Instance -ceq 'CosmixUat_d893653346') 'Actual completed clone/restore proof differs from the selected target.'
 $aExpectedFamily=[guid]$aOriginalIntent.Family
 $aManifestPath=Join-Path $aPrivateRoot 'FrozenA23-Migrations.json';Check-Private $aManifestPath
 $aSqlPath=Join-Path $PSScriptRoot 'Current12-to-FrozenA23.sql'
 Require ((Get-FileHash -LiteralPath $aManifestPath -Algorithm SHA256).Hash -ceq $aProof.ManifestSha256 -and
  (Get-FileHash -LiteralPath $aSqlPath -Algorithm SHA256).Hash -ceq $aProof.SqlSha256 -and
  (Get-FileHash -LiteralPath $aProof.Backup -Algorithm SHA256).Hash -ceq $aProof.BackupSha256) 'Frozen SQL/graph/verified fresh backup hashes changed.'
 $aManifest=@(Get-Content -LiteralPath $aManifestPath -Raw|ConvertFrom-Json)
 Require ($aManifest.Count -eq 23) 'Frozen prefix must contain exactly23 migrations.'
 $aSql=Read-BoundText $aSqlPath $aProof.SqlSha256;$aBatches=@(Split-ReviewedSql $aSql)
 Require ($aBatches.Count -eq 11) 'Reviewed pending range differs.'
 $aInfo=& SqlLocalDB info CosmixUat_d893653346 2>&1|Out-String
 Require ($LASTEXITCODE -eq 0 -and [regex]::Match($aInfo,'(?im)^[\t ]*Owner:[\t ]*([^\r\n]+)\r?$').Groups[1].Value.Trim() -ieq $aOperatorName) 'Existing selected instance owner differs.'
 & SqlLocalDB start CosmixUat_d893653346|Out-Null;Require ($LASTEXITCODE -eq 0) 'Existing selected instance startup failed.'
 $aInfo=& SqlLocalDB info CosmixUat_d893653346 2>&1|Out-String
 $aLivePipe=[regex]::Match($aInfo,'(?im)^[\t ]*Instance pipe name:[\t ]*([^\r\n]+)\r?$').Groups[1].Value.Trim()
 Require ($aLivePipe -match '^np:\\\\.\\pipe\\LOCALDB#[A-Fa-f0-9]{8}\\tsql\\query$') 'No exact existing local pipe.'
 $aSourceConnection=Open-BoundConnection $aSource;$aCloneConnection=Open-BoundConnection $aClone
 [void](Check-Database $aSourceConnection $aSource);[void](Check-Database $aCloneConnection $aClone)
 Check-History @(Read-History $aSourceConnection) 12;Check-History @(Read-History $aCloneConnection) 23
 $aBefore=@($aOriginalIntent.SourceSnapshot)
 Check-Preserved $aBefore @(Read-PreservationSnapshot $aSourceConnection $aBefore)
 Check-Preserved $aBefore @(Read-PreservationSnapshot $aCloneConnection $aBefore) $true
 $aMaster=Open-BoundConnection 'master'
 Execute-Sql $aMaster 'RESTORE VERIFYONLY FROM DISK=@path WITH FILE=1,CHECKSUM;' @{path=$aProof.Backup}
 $aAttemptRoot=Join-Path $aPrivateRoot ('original-upgrade-'+[guid]::NewGuid().ToString('N'));Protect-NewDirectory $aAttemptRoot
 New-PrivateJson 'intent.json' ([ordered]@{Source=$aSource;Operator=$aOperatorName;Family=$aExpectedFamily;UserTargetSelection='Existing isolated local testing database';VerifiedCloneProof=$aProofPath;Backup=$aProof.Backup;BackupSha256=$aProof.BackupSha256;SqlSha256=$aProof.SqlSha256;ManifestSha256=$aProof.ManifestSha256;BaselineHistory=@(Read-History $aSourceConnection);Before=$aBefore;OriginalSchemaWritesAllowed=$true;AtUtc=[datetime]::UtcNow.ToString('O')})
 $aStage='selected-original reviewed A23 range';$aCompleted=0
 foreach($aBatch in $aBatches){[void](Check-Database $aSourceConnection $aSource);Execute-Sql $aSourceConnection $aBatch;$aCompleted++}
 $aStage='selected-original preservation verification'
 $aHistory=@(Read-History $aSourceConnection);Check-History $aHistory 23
 $aAfter=@(Read-PreservationSnapshot $aSourceConnection $aBefore);Check-Preserved $aBefore $aAfter $true
 New-PrivateJson 'result.json' ([ordered]@{Source=$aSource;History=$aHistory;Before=$aBefore;After=$aAfter;RetainedRowsIdsAndSourceFactsPreserved=$true;Backup=$aProof.Backup;BackupSha256=$aProof.BackupSha256;SqlSha256=$aProof.SqlSha256;CompletedBatches=$aCompleted;FixtureMatches=3;Accounts=5;Employees=14;ProductionAccessed=$false;AtUtc=[datetime]::UtcNow.ToString('O')})
 Write-Output ([pscustomobject]@{SelectedExistingTestingDatabaseUpgraded=$true;MigrationCount=23;CompletedBatches=$aCompleted;Accounts=5;Employees=14;RetainedRowsIdsAndSourceFactsPreserved=$true;ActualIsolatedRestoreVerified=$true;ProductionAccessed=$false}|ConvertTo-Json -Compress)
} catch {
 if($null -ne $aAttemptRoot){try{New-PrivateJson 'failure.json' ([ordered]@{Stage=$aStage;Details=$_.Exception.ToString();Stack=$_.ScriptStackTrace;AtUtc=[datetime]::UtcNow.ToString('O');NoAutomaticCleanupOrRetry=$true})}catch{}}
 throw [InvalidOperationException]::new("Selected target application stopped during $aStage. Review private evidence; no automatic cleanup/retry. $aGuardFailure")
} finally {foreach($aConnection in $aConnections){$aConnection.Dispose()}}
