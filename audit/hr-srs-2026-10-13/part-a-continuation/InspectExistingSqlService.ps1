$ErrorActionPreference='Stop'; Set-StrictMode -Version Latest
$aOperator=[System.Security.Principal.WindowsIdentity]::GetCurrent().Name
$aService=Get-CimInstance Win32_Service -Filter "Name='MSSQLSERVER'"
if($aService.State -ne 'Running' -or $aService.PathName -notmatch 'MSSQL16\.MSSQLSERVER.+sqlservr\.exe.+-sMSSQLSERVER'){throw 'Known installed local SQL service identity does not match.'}
$aListeners=@(Get-NetTCPConnection -OwningProcess $aService.ProcessId -State Listen)
if(-not ($aListeners|Where-Object {$_.LocalAddress -eq '127.0.0.1' -and $_.LocalPort -eq 1434})){throw 'Known local SQL listener is absent.'}
$aBuilder=[System.Data.SqlClient.SqlConnectionStringBuilder]::new()
$aBuilder['Data Source']='tcp:127.0.0.1,1434';$aBuilder['Initial Catalog']='master';$aBuilder['Integrated Security']=$true
$aBuilder['Encrypt']=$false;$aBuilder['Connect Timeout']=5
$aConnection=[System.Data.SqlClient.SqlConnection]::new($aBuilder.get_ConnectionString())
try {
 $aConnection.Open();$aCommand=$aConnection.CreateCommand();$aCommand.CommandTimeout=15
 $aCommand.CommandText="SET NOCOUNT ON;SELECT ORIGINAL_LOGIN() AS Operator,CONVERT(nvarchar(128),SERVERPROPERTY('MachineName')) AS Machine;SELECT name,SUSER_SNAME(owner_sid) AS Owner,is_read_only,CONVERT(nvarchar(36),family_guid) AS FamilyId FROM sys.databases AS d LEFT JOIN sys.database_recovery_status AS r ON r.database_id=d.database_id WHERE name=N'Cosmix_Testing';"
 $aReader=$aCommand.ExecuteReader();$aReader.Read()|Out-Null
 if($aReader.GetString(0) -ine $aOperator -or $aReader.GetString(1) -ine $env:COMPUTERNAME){throw 'SQL local service/operator mismatch.'}
 $aReader.NextResult()|Out-Null
 if(-not $aReader.Read()){Write-Output 'Known SQL service has no Cosmix_Testing catalog; no other catalog data was read.';return}
 $aOwner=$aReader.GetString(1);$aFamily=$aReader.GetString(3);$aReader.Dispose()
 $aCommand.CommandText="SET NOCOUNT ON;SELECT COUNT(*) AS MigrationCount,MAX(MigrationId) AS LatestMigration FROM [Cosmix_Testing].[dbo].[__EFMigrationsHistory];SELECT (SELECT COUNT_BIG(*) FROM [Cosmix_Testing].[auth].[UserAccounts]) AS Accounts,(SELECT COUNT_BIG(*) FROM [Cosmix_Testing].[hr].[Employees]) AS Employees;"
 $aReader=$aCommand.ExecuteReader();$aReader.Read()|Out-Null;$aCount=$aReader.GetInt32(0);$aLatest=$aReader.GetString(1)
 $aReader.NextResult()|Out-Null;$aReader.Read()|Out-Null;$aAccounts=$aReader.GetInt64(0);$aEmployees=$aReader.GetInt64(1);$aReader.Dispose()
 $aPrivateRoot=Join-Path $env:LOCALAPPDATA 'Cosmix/PartAContinuation/Database20261013'
 [pscustomobject]@{Server='tcp:127.0.0.1,1434';Operator=$aOperator;Owner=$aOwner;Database='Cosmix_Testing';FamilyId=$aFamily;MigrationCount=$aCount;LatestMigration=$aLatest;Accounts=$aAccounts;Employees=$aEmployees;Mode='UnselectedReadOnlyInventory';ObservedAtUtc=[datetime]::UtcNow.ToString('O')}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $aPrivateRoot 'MSSQLSERVER-inventory.json') -Encoding utf8
 [pscustomobject]@{CatalogFound=$true;LocalServiceMatches=$true;OwnerMatches=($aOwner -ieq $aOperator);MigrationCount=$aCount;LatestMigration=$aLatest;Accounts=$aAccounts;Employees=$aEmployees;SelectedForWrites=$false;QueryOnly=$true}|ConvertTo-Json
} finally {$aConnection.Dispose()}
