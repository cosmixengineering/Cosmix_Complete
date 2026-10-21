param([Parameter(Mandatory=$true)][ValidateSet('MSSQLLocalDB','CosmixUat_d893653346')][string]$InstanceName)
$ErrorActionPreference='Stop'; Set-StrictMode -Version Latest
$aOperator=[System.Security.Principal.WindowsIdentity]::GetCurrent().Name
$aInfo=& SqlLocalDB info $InstanceName 2>&1 | Out-String
if($LASTEXITCODE -ne 0){throw 'Only an existing instance may be inspected.'}
$aOwner=[regex]::Match($aInfo,'(?im)^[\t ]*Owner:[\t ]*([^\r\n]+)\r?$')
if(-not $aOwner.Success -or $aOwner.Groups[1].Value.Trim() -ine $aOperator){throw 'Instance owner mismatch.'}
$aStart=& SqlLocalDB start $InstanceName 2>&1 | Out-String
if($LASTEXITCODE -ne 0){throw 'Existing instance startup failed; no replacement is created.'}
$aInfo=& SqlLocalDB info $InstanceName 2>&1 | Out-String
if($LASTEXITCODE -ne 0){throw 'Instance metadata unavailable.'}
$aPipe=[regex]::Match($aInfo,'(?im)^[\t ]*Instance pipe name:[\t ]*([^\r\n]+)\r?$')
if(-not $aPipe.Success -or $aPipe.Groups[1].Value.Trim() -notmatch '^np:\\\\.\\pipe\\LOCALDB#[A-Fa-f0-9]{8}\\tsql\\query$'){throw 'No exact local pipe.'}
$aBuilder=[System.Data.SqlClient.SqlConnectionStringBuilder]::new()
$aBuilder['Data Source']=$aPipe.Groups[1].Value.Trim(); $aBuilder['Initial Catalog']='master'
$aBuilder['Integrated Security']=$true; $aBuilder['Encrypt']=$false; $aBuilder['Connect Timeout']=5
if($aBuilder.get_DataSource() -cne $aPipe.Groups[1].Value.Trim() -or -not $aBuilder.get_IntegratedSecurity()){throw 'Connection not exactly bound.'}
$aConnection=[System.Data.SqlClient.SqlConnection]::new($aBuilder.get_ConnectionString())
try {
 $aConnection.Open(); $aCommand=$aConnection.CreateCommand(); $aCommand.CommandTimeout=10
 $aCommand.CommandText="SET NOCOUNT ON; SELECT CONVERT(int,SERVERPROPERTY('IsLocalDB')) AS IsLocalDb,ORIGINAL_LOGIN() AS Operator; SELECT name,SUSER_SNAME(owner_sid) AS Owner,is_read_only,CONVERT(nvarchar(36),family_guid) AS FamilyId FROM sys.databases AS d LEFT JOIN sys.database_recovery_status AS r ON r.database_id=d.database_id WHERE name=N'Cosmix_Testing';"
 $aReader=$aCommand.ExecuteReader(); $aReader.Read() | Out-Null
 if($aReader.GetInt32(0) -ne 1 -or $aReader.GetString(1) -ine $aOperator){throw 'Instance identity is not local/operator-owned.'}
 $aReader.NextResult() | Out-Null
 if(-not $aReader.Read()){Write-Output "No Cosmix_Testing catalog found in existing instance $InstanceName; no data was inspected.";return}
 if($aReader.GetString(1) -ine $aOperator){throw 'Testing catalog owner does not match the operator.'}
 $aCatalogFamily=$aReader.GetString(3); $aReader.Dispose()
 $aCommand.CommandText="SET NOCOUNT ON; SELECT COUNT(*) AS MigrationCount,MAX(MigrationId) AS LatestMigration FROM [Cosmix_Testing].[dbo].[__EFMigrationsHistory]; SELECT (SELECT COUNT_BIG(*) FROM [Cosmix_Testing].[auth].[UserAccounts]) AS Accounts,(SELECT COUNT_BIG(*) FROM [Cosmix_Testing].[hr].[Employees]) AS Employees;"
 $aReader=$aCommand.ExecuteReader(); $aReader.Read() | Out-Null
 $aMigrationCount=$aReader.GetInt32(0); $aLatest=$aReader.GetString(1)
 $aReader.NextResult() | Out-Null; $aReader.Read() | Out-Null
 $aAccountCount=$aReader.GetInt64(0); $aEmployeeCount=$aReader.GetInt64(1); $aReader.Dispose()
 $aPrivateRoot=Join-Path $env:LOCALAPPDATA 'Cosmix\PartAContinuation\Database20261013'
 [pscustomobject]@{InstanceName=$InstanceName;Pipe=$aPipe.Groups[1].Value.Trim();Operator=$aOperator;Database='Cosmix_Testing';
  FamilyId=$aCatalogFamily;MigrationCount=$aMigrationCount;LatestMigration=$aLatest;Accounts=$aAccountCount;Employees=$aEmployeeCount;
  Mode='UnselectedReadOnlyInventory';ObservedAtUtc=[datetime]::UtcNow.ToString('O')} | ConvertTo-Json |
  Set-Content -LiteralPath (Join-Path $aPrivateRoot ($InstanceName+'-inventory.json')) -Encoding utf8
 [pscustomobject]@{InstanceName=$InstanceName;CatalogFound=$true;OwnerMatches=$true;IsLocalDb=$true;MigrationCount=$aMigrationCount;
  LatestMigration=$aLatest;Accounts=$aAccountCount;Employees=$aEmployeeCount;SelectedForWrites=$false;QueryOnly=$true} | ConvertTo-Json
} finally {$aConnection.Dispose()}
