[CmdletBinding()]
param([ValidateSet('Prepare','Verify','Execute')][string]$Mode = 'Verify')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.Data
$repairRoot = 'C:\Users\Huma.Khan\AppData\Local\Cosmix\TestDataRepairs\OctoberAttendance20261028A01'
$server = 'np:\\.\pipe\LOCALDB#36E7B148\tsql\query'
$actor = 'COSMIXRND\Huma.Khan'
$planPath = Join-Path $PSScriptRoot 'Inspect-October-Test-Repair-20261028.sql'
# GetFinalPathNameByHandle confirmed this is the same private directory's physical
# backing path under Windows packaged-app virtualization. SQL Server runs outside
# that virtualization and must receive the physical path. Never use a public path.
$physicalRepairRoot = 'C:\Users\Huma.Khan\AppData\Local\Packages\OpenAI.Codex_2p2nqsd0c76g0\LocalCache\Local\Cosmix\TestDataRepairs\OctoberAttendance20261028A01'
$backupPath = Join-Path $physicalRepairRoot 'Cosmix_Testing_before_October_fixture_repair.bak'
$manifestPath = Join-Path $repairRoot 'manifest-reviewed-v2.json'
$script:committed = $false
$order = @('hr.SalaryPayments','hr.StatutoryContributions','hr.PayrollJournalEntries','hr.PayrollObligations','hr.PayrollHandoffLines','hr.PayrollHandoffBatches','attendance.AttendancePeriodLockEvents','attendance.AttendancePeriodLocks')
$expected = @{
 'hr.SalaryPayments' = @(123,'669A87B3A0BFFDE08841978F91A15D8741F85F49ED3B53012031D9B06EABEA41')
 'hr.StatutoryContributions' = @(2,'D6490469126D7D3E354B6DA86838C66BDAE3B83A26831C9C2A68D04E5E13B0D8')
 'hr.PayrollJournalEntries' = @(327,'EC0CC4B466702F73A27BD86D5E54291EF21D505150122C5CF57CB5DFBC5F60A2')
 'hr.PayrollObligations' = @(64,'A9F730362260556CBC59BBE48DD4A0E707EA903E1577A61FEBC96BB2302A37DA')
 'hr.PayrollHandoffLines' = @(190,'789B9A4574DE013A887E22279B998FE43E3B0CB5BEA68C60605E8315FF1F0E7E')
 'hr.PayrollHandoffBatches' = @(70,'98276677808FF399BD0E0B32A824CC51D548101D071C7DEC378B65002EC4D39B')
 'attendance.AttendancePeriodLockEvents' = @(1,'0C1714D26873455CCCF65F58A55DEDFFA71C1B31B2D16EFE02A7D0123DDBF1CC')
 'attendance.AttendancePeriodLocks' = @(1,'1389E69B900393D5F03C552983F3FF0F65D0672623BC9C06DCFE59F01FB814A6')
}
$cn = [System.Data.SqlClient.SqlConnection]::new("Server=$server;Database=Cosmix_Testing;Integrated Security=True;Encrypt=False;Pooling=False;Connect Timeout=10;Application Name=CosmixOctoberRepair20261028")
$script:tx = $null
function Query([string]$sql) {
 $cmd=$cn.CreateCommand(); $cmd.CommandText=$sql; $cmd.CommandTimeout=60
 if ($null -ne $script:tx) { $cmd.Transaction=$script:tx }
 $table=[System.Data.DataTable]::new()
 try { $reader=$cmd.ExecuteReader(); try { $table.Load($reader) } finally { $reader.Dispose() } } finally { $cmd.Dispose() }
 return ,$table
}
function Run([string]$sql) {
 $cmd=$cn.CreateCommand(); $cmd.CommandText=$sql; $cmd.CommandTimeout=120
 if ($null -ne $script:tx) { $cmd.Transaction=$script:tx }
 try { return $cmd.ExecuteNonQuery() } finally { $cmd.Dispose() }
}
function Hash([string]$value) {
 $sha=[Security.Cryptography.SHA256]::Create()
 try { return [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($value))).Replace('-','') } finally { $sha.Dispose() }
}
function QName([string]$table) { return (($table.Split('.') | ForEach-Object { '[' + $_.Replace(']',']]') + ']' }) -join '.') }
function Assert-Identity {
 $r=(Query @'
SELECT @@SERVERNAME ServerName,DB_NAME() DbName,SUSER_SNAME() Actor,d.state_desc State,
 SUSER_SNAME(d.owner_sid) Owner,r.database_guid DbGuid,r.family_guid Family
FROM sys.databases d JOIN sys.database_recovery_status r ON r.database_id=d.database_id WHERE d.database_id=DB_ID();
'@).Rows[0]
 if ($r.ServerName -cne 'Software-PC1\LOCALDB#36E7B148' -or $r.DbName -cne 'Cosmix_Testing' -or $r.Actor -ine $actor -or $r.Owner -ine $actor -or $r.State -ne 'ONLINE' -or [string]$r.DbGuid -ine '631714b1-1cb2-43ca-9a04-4b2c04e3d1a7' -or [string]$r.Family -ine 'd9c34627-18a2-41cf-a804-821e39516303') { throw 'Target identity mismatch.' }
 $files=Query 'SELECT physical_name FROM sys.database_files ORDER BY file_id;'
 if ($files.Rows.Count -ne 2 -or $files.Rows[0].physical_name -ine 'C:\Users\Huma.Khan\Cosmix_Testing.mdf' -or $files.Rows[1].physical_name -ine 'C:\Users\Huma.Khan\Cosmix_Testing_log.ldf') { throw 'Database file identity mismatch.' }
 $m=(Query 'SELECT COUNT(*) N,MAX(MigrationId) Latest FROM dbo.__EFMigrationsHistory;').Rows[0]
 if ($m.N -ne 31 -or $m.Latest -cne '20261028114154_ProtectPayrollPaymentSettlementAndBankExports') { throw 'Migration baseline differs.' }
}
function Assert-NoWriters {
 # Only an idle SSMS Object Explorer connection in master is permitted beside this executor.
 $other=Query "SELECT session_id FROM sys.dm_exec_sessions WHERE is_user_process=1 AND session_id<>@@SPID AND NOT(database_id=DB_ID('master') AND status='sleeping' AND program_name='SQL Server Management Studio');"
 if ($other.Rows.Count -ne 0) { throw 'Other user SQL sessions present; cannot freeze repair safely.' }
}
function Assert-Selection {
 $rows=Query ([IO.File]::ReadAllText($planPath))
 if ($rows.Rows.Count -ne 8) { throw 'Selection table count mismatch.' }
 foreach ($r in $rows.Rows) {
  if (!$expected.ContainsKey([string]$r.TableName) -or $r.PlannedRows -ne $expected[$r.TableName][0] -or $r.IdSetSha256 -cne $expected[$r.TableName][1]) { throw "ID manifest mismatch: $($r.TableName)" }
 }
}
function Create-Selection {
 [void](Run @'
CREATE TABLE #RepairIds(TableName nvarchar(128) NOT NULL, Id uniqueidentifier NOT NULL, PRIMARY KEY(TableName,Id));
INSERT #RepairIds SELECT 'attendance.AttendancePeriodLocks',Id FROM attendance.AttendancePeriodLocks WHERE Id='4EB6E47C-91C6-482F-914E-D5060BD63D97';
INSERT #RepairIds SELECT 'attendance.AttendancePeriodLockEvents',Id FROM attendance.AttendancePeriodLockEvents WHERE Id='6E56463D-2802-478D-B917-2E8FD615F635';
INSERT #RepairIds SELECT DISTINCT 'hr.PayrollHandoffBatches',PayrollHandoffBatchId FROM hr.PayrollHandoffLines WHERE AttendancePeriodLockEventId='6E56463D-2802-478D-B917-2E8FD615F635';
INSERT #RepairIds SELECT 'hr.PayrollHandoffLines',Id FROM hr.PayrollHandoffLines WHERE PayrollHandoffBatchId IN(SELECT Id FROM #RepairIds WHERE TableName='hr.PayrollHandoffBatches');
INSERT #RepairIds SELECT 'hr.PayrollObligations',Id FROM hr.PayrollObligations WHERE BatchId IN(SELECT Id FROM #RepairIds WHERE TableName='hr.PayrollHandoffBatches');
INSERT #RepairIds SELECT 'hr.PayrollJournalEntries',Id FROM hr.PayrollJournalEntries WHERE ObligationId IN(SELECT Id FROM #RepairIds WHERE TableName='hr.PayrollObligations');
INSERT #RepairIds SELECT 'hr.SalaryPayments',Id FROM hr.SalaryPayments WHERE ObligationId IN(SELECT Id FROM #RepairIds WHERE TableName='hr.PayrollObligations');
INSERT #RepairIds SELECT 'hr.StatutoryContributions',Id FROM hr.StatutoryContributions WHERE ObligationId IN(SELECT Id FROM #RepairIds WHERE TableName='hr.PayrollObligations') OR JournalEntryId IN(SELECT Id FROM #RepairIds WHERE TableName='hr.PayrollJournalEntries');
'@)
}
function Assert-Dependencies {
 [void](Run @'
IF (SELECT COUNT(*) FROM #RepairIds)<>778 THROW 51000,'Wrong selected total',1;
IF EXISTS(SELECT 1 FROM hr.PayrollHandoffBatches b JOIN #RepairIds r ON r.TableName='hr.PayrollHandoffBatches' AND r.Id=b.Id WHERE b.BatchReference NOT LIKE 'SYNTH-BATCH%') THROW 51000,'Non-synthetic batch',1;
IF EXISTS(SELECT 1 FROM hr.PayrollHandoffLines l JOIN #RepairIds r ON r.TableName='hr.PayrollHandoffLines' AND r.Id=l.Id WHERE l.AttendancePeriodLockEventId IS NULL OR l.AttendancePeriodLockEventId<>'6E56463D-2802-478D-B917-2E8FD615F635') THROW 51000,'Mixed source batch',1;
IF EXISTS(SELECT 1 FROM hr.PayrollJournalEntries j WHERE j.StatutoryContributionId IN(SELECT Id FROM #RepairIds WHERE TableName='hr.StatutoryContributions') AND j.Id NOT IN(SELECT Id FROM #RepairIds WHERE TableName='hr.PayrollJournalEntries')) THROW 51000,'Outside statutory journal',1;
IF EXISTS(SELECT 1 FROM hr.StatutoryContributions s WHERE (s.JournalEntryId IN(SELECT Id FROM #RepairIds WHERE TableName='hr.PayrollJournalEntries') OR s.ObligationId IN(SELECT Id FROM #RepairIds WHERE TableName='hr.PayrollObligations')) AND s.Id NOT IN(SELECT Id FROM #RepairIds WHERE TableName='hr.StatutoryContributions')) THROW 51000,'Outside statutory dependency',1;
IF EXISTS(SELECT 1 FROM hr.SalaryPayments WHERE Id IN(SELECT Id FROM #RepairIds WHERE TableName='hr.SalaryPayments') AND BankExportId IS NOT NULL) THROW 51000,'Bank export reference',1;
IF EXISTS(SELECT 1 FROM hr.RepaymentLedgerEntries e JOIN #RepairIds r ON r.TableName='hr.PayrollHandoffLines' AND e.SourceReference=N'payroll-line:'+LOWER(REPLACE(CONVERT(nvarchar(36),r.Id),N'-',N''))) THROW 51000,'Loan ledger references selected payroll line',1;
IF EXISTS(SELECT 1 FROM jobs.OutboxMessages WHERE State<>2 OR MessageType NOT IN('hr.report-export.v1','notification.in-app.v1')) THROW 51000,'Active or unexpected outbox work',1;
IF (SELECT COUNT(*) FROM jobs.OutboxMessages)<>2 THROW 51000,'Outbox changed',1;
IF EXISTS(SELECT 1 FROM workflow.IdempotencyReceipts WHERE Operation LIKE '%payroll%' OR Operation LIKE '%salary%' OR Operation LIKE '%month%' OR Operation LIKE '%lock%' OR Operation LIKE '%statutory%' OR ResultCode='payroll-finance-v1') THROW 51000,'Financial replay receipt',1;
IF EXISTS(SELECT 1 FROM sys.triggers WHERE parent_class=1 AND is_disabled=0) THROW 51000,'Unexpected table trigger',1;
IF EXISTS(SELECT 1 FROM sys.foreign_keys WHERE is_disabled=1) THROW 51000,'Disabled foreign key',1;
'@)
 # Every live FK edge into the selection must remain wholly inside the selection.
 $edges=Query @'
SELECT f.object_id FkId,OBJECT_SCHEMA_NAME(f.parent_object_id)+'.'+OBJECT_NAME(f.parent_object_id) Child,
 OBJECT_SCHEMA_NAME(f.referenced_object_id)+'.'+OBJECT_NAME(f.referenced_object_id) Parent,
 pc.name ChildColumn,rc.name ParentColumn
FROM sys.foreign_keys f JOIN sys.foreign_key_columns fc ON f.object_id=fc.constraint_object_id
JOIN sys.columns pc ON pc.object_id=fc.parent_object_id AND pc.column_id=fc.parent_column_id
JOIN sys.columns rc ON rc.object_id=fc.referenced_object_id AND rc.column_id=fc.referenced_column_id
ORDER BY f.object_id,fc.constraint_column_id;
'@
 foreach ($g in ($edges.Rows | Group-Object FkId)) {
  $e=$g.Group[0]; if (!$expected.ContainsKey([string]$e.Parent)) { continue }
  $join=($g.Group | ForEach-Object { 'c.['+$_.ChildColumn+']=p.['+$_.ParentColumn+']' }) -join ' AND '
  $except=''
  if ($expected.ContainsKey([string]$e.Child)) { $except=" AND NOT EXISTS(SELECT 1 FROM #RepairIds x WHERE x.TableName='$($e.Child)' AND x.Id=c.Id)" }
  $count=(Query "SELECT COUNT_BIG(*) N FROM $(QName $e.Child) c JOIN $(QName $e.Parent) p ON $join JOIN #RepairIds r ON r.TableName='$($e.Parent)' AND r.Id=p.Id WHERE 1=1 $except;").Rows[0].N
  if ($count -ne 0) { throw "External FK dependency: $($e.Child) -> $($e.Parent)" }
 }
}
function Tables { return @((Query "SELECT s.name+'.'+t.name Name FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id ORDER BY s.name,t.name;").Rows | ForEach-Object { [string]$_.Name }) }
function Snapshot([string[]]$tables,[bool]$excludeSelected) {
 $result=@()
 foreach ($t in $tables) {
  $where=''
  if ($excludeSelected -and $expected.ContainsKey($t)) { $where=" WHERE NOT EXISTS(SELECT 1 FROM #RepairIds r WHERE r.TableName='$t' AND r.Id=t.Id)" }
  $data=Query "SELECT (SELECT t.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) RowJson FROM $(QName $t) t$where;"
  $hashes=[string[]]@($data.Rows | ForEach-Object { Hash ([string]$_.RowJson) })
  [Array]::Sort($hashes,[StringComparer]::Ordinal)
  $result+= [pscustomobject]@{Table=$t;Rows=$data.Rows.Count;Sha256=(Hash ($hashes -join "`n"))}
 }
 return $result
}
function SchemaHash {
 $parts=@()
 foreach ($v in @('sys.objects','sys.columns','sys.indexes','sys.index_columns','sys.foreign_keys','sys.foreign_key_columns','sys.check_constraints','sys.default_constraints','sys.sql_modules')) {
  $r=Query "SELECT (SELECT t.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) RowJson FROM $v t;"
  $hashes=[string[]]@($r.Rows | ForEach-Object { Hash ([string]$_.RowJson) }); [Array]::Sort($hashes,[StringComparer]::Ordinal)
  $parts+= "$v|$(Hash ($hashes -join "`n"))"
 }
 return Hash ($parts -join "`n")
}
function CompareSnapshot($before,$after) {
 if ($before.Count -ne $after.Count) { throw 'Snapshot table count changed.' }
 for ($i=0;$i -lt $before.Count;$i++) {
  if ($before[$i].Table -cne $after[$i].Table -or $before[$i].Rows -ne $after[$i].Rows -or $before[$i].Sha256 -cne $after[$i].Sha256) { throw "Content drift: $($before[$i].Table)" }
 }
}
function SaveNew([string]$path,$value) {
 $json=$value | ConvertTo-Json -Depth 15
 $stream=[IO.File]::Open($path,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
 try { $bytes=[Text.UTF8Encoding]::new($false).GetBytes($json); $stream.Write($bytes,0,$bytes.Length); $stream.Flush($true) } finally { $stream.Dispose() }
}
try {
 if ([Security.Principal.WindowsIdentity]::GetCurrent().Name -ine $actor) { throw 'Wrong Windows user.' }
 if ((Get-PSDrive C).Free -lt 1GB) { throw 'At least 1 GB free space required.' }
 $cn.Open(); Assert-Identity; Assert-NoWriters; Assert-Selection; Create-Selection; Assert-Dependencies
 $tables=Tables
 $all=@(Snapshot $tables $false); $preserved=@(Snapshot $tables $true); $schema=SchemaHash
 $selfHash=(Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash
 $selectorHash=(Get-FileHash -LiteralPath $planPath -Algorithm SHA256).Hash
 if ($Mode -eq 'Prepare') {
  if (Test-Path -LiteralPath $repairRoot) {
   # Upgrade the review packet only; preserve the first private manifest verbatim.
   $original=Get-Content -LiteralPath (Join-Path $repairRoot 'manifest.json') -Raw | ConvertFrom-Json
   if (@(Get-ChildItem -LiteralPath $repairRoot -Force).Count -ne 2 -or !(Test-Path -LiteralPath (Join-Path $repairRoot 'selected-rows.private.json'))) { throw 'Unexpected existing repair files; no automatic retry.' }
   CompareSnapshot $original.All $all; CompareSnapshot $original.Preserved $preserved
   if ($original.SchemaSha256 -cne $schema -or $original.SelectorSha256 -cne $selectorHash) { throw 'Original review changed.' }
   $physicalManifest=Join-Path $physicalRepairRoot 'manifest.json'
   if ((Get-FileHash $physicalManifest).Hash -cne (Get-FileHash (Join-Path $repairRoot 'manifest.json')).Hash) { throw 'Private path resolution mismatch.' }
   SaveNew $manifestPath ([ordered]@{PreparedAt=[DateTimeOffset]::UtcNow.ToString('o');Actor=$actor;Server=$server;Database='Cosmix_Testing';ScriptSha256=$selfHash;SelectorSha256=$selectorHash;SchemaSha256=$schema;All=$all;Preserved=$preserved;DeleteRows=778;PhysicalPrivatePath=$physicalRepairRoot;OriginalManifestSha256=(Get-FileHash $physicalManifest).Hash;Approval='User explicitly requested execute the disposable October test-data repair on 2026-10-28.'})
   Write-Output "PREPARED V2: 778 rows; $($tables.Count) tables; original evidence retained. No business writes."
   return
  }
  [void][IO.Directory]::CreateDirectory($repairRoot)
  $acl=[Security.AccessControl.DirectorySecurity]::new(); $acl.SetAccessRuleProtection($true,$false)
  foreach ($sid in @([Security.Principal.WindowsIdentity]::GetCurrent().User,[Security.Principal.SecurityIdentifier]::new('S-1-5-18'))) {
   $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($sid,'FullControl','ContainerInherit,ObjectInherit','None','Allow'))
  }
  Set-Acl -LiteralPath $repairRoot -AclObject $acl
  $selected=@()
  foreach($t in $order) {
   $r=Query "SELECT (SELECT t.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) RowJson FROM $(QName $t) t JOIN #RepairIds r ON r.TableName='$t' AND r.Id=t.Id ORDER BY t.Id;"
   $selected+=[pscustomobject]@{Table=$t;Rows=@($r.Rows | ForEach-Object { ([string]$_.RowJson | ConvertFrom-Json) })}
  }
  SaveNew (Join-Path $repairRoot 'selected-rows.private.json') $selected
  SaveNew $manifestPath ([ordered]@{PreparedAt=[DateTimeOffset]::UtcNow.ToString('o');Actor=$actor;Server=$server;Database='Cosmix_Testing';ScriptSha256=$selfHash;SelectorSha256=$selectorHash;SchemaSha256=$schema;All=$all;Preserved=$preserved;DeleteRows=778;Approval='User explicitly requested execute the disposable October test-data repair on 2026-10-28.'})
  Write-Output "PREPARED: 778 selected rows; $($tables.Count) table fingerprints; private manifest created. No business writes."
  return
 }
 if (!(Test-Path -LiteralPath $manifestPath)) { throw 'Prepared manifest required.' }
 $baseline=Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
 if ($baseline.ScriptSha256 -cne $selfHash -or $baseline.SelectorSha256 -cne $selectorHash -or $baseline.SchemaSha256 -cne $schema -or $baseline.Server -cne $server) { throw 'Executor/selector/schema binding changed.' }
 CompareSnapshot $baseline.All $all; CompareSnapshot $baseline.Preserved $preserved
 if ($Mode -eq 'Verify') { Write-Output "VERIFIED: exact identity, 778 IDs, source contents, dependencies and $($tables.Count) preserved table fingerprints match. No writes."; return }
 if (Test-Path -LiteralPath $backupPath) { throw 'Backup already exists; no automatic retry.' }
 $visible=(Query "EXEC master.dbo.xp_fileexist N'$physicalRepairRoot';").Rows[0]
 if ($visible[1] -ne 1) { throw 'SQL cannot see the verified physical private backup folder.' }
 SaveNew (Join-Path $repairRoot 'execution-started.json') ([ordered]@{At=[DateTimeOffset]::UtcNow.ToString('o');ManifestSha256=(Get-FileHash $manifestPath).Hash;Actor=$actor;State='Started; not committed'})
 Assert-NoWriters
 [void](Run "BACKUP DATABASE [Cosmix_Testing] TO DISK=N'$backupPath' WITH COPY_ONLY,CHECKSUM,NOFORMAT,NOINIT,NAME=N'Cosmix October disposable fixture repair 20261028A01';")
 [void](Run "RESTORE VERIFYONLY FROM DISK=N'$backupPath' WITH CHECKSUM;")
 $header=(Query "RESTORE HEADERONLY FROM DISK=N'$backupPath';").Rows
 if ($header.Count -ne 1 -or $header[0].DatabaseName -cne 'Cosmix_Testing' -or !$header[0].IsCopyOnly -or !$header[0].HasBackupChecksums -or $header[0].IsDamaged -or [string]$header[0].FamilyGUID -ine 'd9c34627-18a2-41cf-a804-821e39516303') { throw 'Backup header identity/checksum mismatch.' }
 $backupFiles=(Query "RESTORE FILELISTONLY FROM DISK=N'$backupPath';").Rows
 if ($backupFiles.Count -ne 2 -or @($backupFiles | Where-Object { $_.PhysicalName -notin @('C:\Users\Huma.Khan\Cosmix_Testing.mdf','C:\Users\Huma.Khan\Cosmix_Testing_log.ldf') }).Count -ne 0) { throw 'Backup file list mismatch.' }
 $backupHash=(Get-FileHash -LiteralPath $backupPath -Algorithm SHA256).Hash
 SaveNew (Join-Path $repairRoot 'backup-verified.json') ([ordered]@{At=[DateTimeOffset]::UtcNow.ToString('o');Sha256=$backupHash;Database='Cosmix_Testing';VerifyOnly='PASS WITH CHECKSUM';RestoreTested=$false;Bytes=(Get-Item $backupPath).Length})
 Write-Output 'BACKUP VERIFIED WITH CHECKSUM. Starting serializable, ID-bound repair.'
 Assert-NoWriters
 [void](Run 'SET XACT_ABORT ON; SET LOCK_TIMEOUT 10000;')
 $script:tx=$cn.BeginTransaction([Data.IsolationLevel]::Serializable)
 foreach($t in $tables) { [void](Query "SELECT COUNT_BIG(*) N FROM $(QName $t) WITH(TABLOCKX,HOLDLOCK);") }
 Assert-Identity; Assert-NoWriters; Assert-Selection; Assert-Dependencies
 CompareSnapshot $baseline.All @(Snapshot $tables $false)
 if ((SchemaHash) -cne $baseline.SchemaSha256) { throw 'Schema changed before deletion.' }
 $deleted=@()
 foreach($t in $order) {
  $n=(Query "DELETE t FROM $(QName $t) t JOIN #RepairIds r ON r.TableName='$t' AND r.Id=t.Id; SELECT @@ROWCOUNT N;").Rows[0].N
  if ($n -ne $expected[$t][0]) { throw "Unexpected delete count: $t" }
  $deleted += [pscustomobject]@{Table=$t;Rows=$n}
 }
 foreach($t in $order) {
  if ((Query "SELECT COUNT_BIG(*) N FROM $(QName $t) t JOIN #RepairIds r ON r.TableName='$t' AND r.Id=t.Id;").Rows[0].N -ne 0) { throw "Selected rows remain: $t" }
 }
 CompareSnapshot $baseline.Preserved @(Snapshot $tables $false)
 if ((SchemaHash) -cne $baseline.SchemaSha256) { throw 'Schema changed during repair.' }
 SaveNew (Join-Path $repairRoot 'precommit-verified.json') ([ordered]@{At=[DateTimeOffset]::UtcNow.ToString('o');Deleted=$deleted;PreservedTables=$tables.Count;Schema='Unchanged';BackupSha256=$backupHash;State='All checks passed, commit not yet acknowledged'})
 $script:tx.Commit(); $script:committed=$true; $script:tx.Dispose(); $script:tx=$null
 SaveNew (Join-Path $repairRoot 'committed.json') ([ordered]@{At=[DateTimeOffset]::UtcNow.ToString('o');Actor=$actor;Deleted=$deleted;PreservedTables=$tables.Count;Schema='Unchanged';Migrations=31;BackupSha256=$backupHash;State='COMMITTED'})
 Write-Output "COMMITTED: exactly 778 disposable rows removed. $($tables.Count) table fingerprints match expected remaining data; schema/migrations unchanged."
} catch {
 if ($script:committed) { Write-Output 'DATABASE COMMITTED; subsequent evidence write failed. Do not retry. Inspect the committed database and precommit ledger.' }
 if ($null -ne $script:tx) { try { $script:tx.Rollback(); Write-Output 'TRANSACTION ROLLED BACK.' } catch { Write-Output 'Rollback acknowledgement unavailable; inspect state before any retry.' } }
 throw
} finally { if ($null -ne $script:tx) { $script:tx.Dispose() }; $cn.Dispose() }
