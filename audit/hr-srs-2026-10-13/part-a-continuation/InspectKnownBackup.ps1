$ErrorActionPreference='Stop';Set-StrictMode -Version Latest
$aOperator=[System.Security.Principal.WindowsIdentity]::GetCurrent().Name
$aIntent=Get-Content -LiteralPath (Join-Path $env:LOCALAPPDATA 'Cosmix/LocalTesting/Attendance-Office-Migration-20261013-B05-intent.json') -Raw|ConvertFrom-Json
$aBackup='C:/Users/Huma.Khan/Cosmix_Testing_Attendance_20261013_B05.bak'
if((Resolve-Path -LiteralPath $aBackup).Path -ine (Resolve-Path -LiteralPath $aIntent.Backup).Path -or
 (Get-FileHash -LiteralPath $aBackup -Algorithm SHA256).Hash -ine $aIntent.BackupSha256 -or $aIntent.Owner -ine $aOperator){throw 'Known backup provenance mismatch.'}
$aInfo=& SqlLocalDB info CosmixUat_d893653346 2>&1|Out-String
if($LASTEXITCODE -ne 0 -or [regex]::Match($aInfo,'(?im)^[\t ]*Owner:[\t ]*([^\r\n]+)\r?$').Groups[1].Value.Trim() -ine $aOperator){throw 'Existing operator-owned reader instance unavailable.'}
& SqlLocalDB start CosmixUat_d893653346|Out-Null;if($LASTEXITCODE -ne 0){throw 'Existing reader startup failed.'}
$aInfo=& SqlLocalDB info CosmixUat_d893653346 2>&1|Out-String
$aPipe=[regex]::Match($aInfo,'(?im)^[\t ]*Instance pipe name:[\t ]*([^\r\n]+)\r?$').Groups[1].Value.Trim()
if($aPipe -notmatch '^np:\\\\.\\pipe\\LOCALDB#[A-Fa-f0-9]{8}\\tsql\\query$'){throw 'Reader pipe invalid.'}
$aBuilder=[System.Data.SqlClient.SqlConnectionStringBuilder]::new();$aBuilder['Data Source']=$aPipe;$aBuilder['Initial Catalog']='master';$aBuilder['Integrated Security']=$true;$aBuilder['Encrypt']=$false;$aBuilder['Connect Timeout']=5
$aConnection=[System.Data.SqlClient.SqlConnection]::new($aBuilder.get_ConnectionString())
try{
 $aConnection.Open();$aCommand=$aConnection.CreateCommand();$aCommand.CommandTimeout=30
 $aCommand.CommandText='RESTORE HEADERONLY FROM DISK=@backup';[void]$aCommand.Parameters.AddWithValue('@backup',$aBackup)
 $aReader=$aCommand.ExecuteReader();if(-not $aReader.Read()){throw 'No backup header.'}
 $aHeader=[ordered]@{};for($aIndex=0;$aIndex -lt $aReader.FieldCount;$aIndex++){if($aReader.GetName($aIndex) -in @('DatabaseName','FamilyGUID','BackupSetGUID','BackupType','Position','BackupStartDate','BackupFinishDate','HasBackupChecksums','IsCopyOnly','BackupSize','ServerName')){$aHeader[$aReader.GetName($aIndex)]=$aReader.GetValue($aIndex)}}
 if($aReader.Read()){throw 'Multiple backup sets require explicit range review.'};$aReader.Dispose()
 if($aHeader.DatabaseName -cne 'Cosmix_Testing' -or $aHeader.BackupType -ne 1 -or -not $aHeader.HasBackupChecksums -or -not $aHeader.IsCopyOnly){throw 'Not the expected full COPY_ONLY/CHECKSUM backup.'}
 $aHeader|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $env:LOCALAPPDATA 'Cosmix/PartAContinuation/Database20261013/known-backup-header.json') -Encoding utf8
 $aCandidate=Get-Content -LiteralPath (Join-Path $env:LOCALAPPDATA 'Cosmix/PartAContinuation/Database20261013/CosmixUat_d893653346-inventory.json') -Raw|ConvertFrom-Json
 [pscustomobject]@{KnownBackupHashVerified=$true;ExpectedCatalog=$true;FullCopyOnly=$true;Checksums=$true;ExistingCandidateHasSameBackupFamily=([string]$aHeader.FamilyGUID -ieq [string]$aCandidate.FamilyId);OriginalDataChanged=$false;RestorePerformed=$false}|ConvertTo-Json
}finally{$aConnection.Dispose()}
