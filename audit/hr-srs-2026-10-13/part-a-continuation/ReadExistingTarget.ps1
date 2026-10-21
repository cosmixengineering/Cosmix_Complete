param([string]$BindingPath = (Join-Path $env:LOCALAPPDATA 'Cosmix\PartAContinuation\Database20261013\target-binding.json'),
    [switch]$StartExisting)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$aBinding=Get-Content -LiteralPath $BindingPath -Raw | ConvertFrom-Json
$aOperator=[System.Security.Principal.WindowsIdentity]::GetCurrent().Name
if ($aBinding.InstanceName -cne 'CosmixUat_d893653346' -or $aBinding.Database -cne 'Cosmix_Testing' -or $aBinding.Operator -ine $aOperator) {
    throw 'Binding must select the previously established existing synthetic target and current operator.'
}
$aMetadata=& SqlLocalDB info $aBinding.InstanceName 2>&1 | Out-String
if ($LASTEXITCODE -ne 0) { throw 'Selected instance metadata is unavailable.' }
$aOwner=[regex]::Match($aMetadata,'(?im)^[\t ]*Owner:[\t ]*([^\r\n]+)\r?$')
if (-not $aOwner.Success -or $aOwner.Groups[1].Value.Trim() -ine $aOperator) { throw 'Selected existing instance owner differs from current operator.' }
if ($StartExisting) {
    # LocalDB can stop after an idle interval. Restart only the already selected
    # operator-owned instance, never create/adopt a replacement or default alias.
    $aStartOutput=& SqlLocalDB start $aBinding.InstanceName 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) { throw 'Selected existing instance failed to start.' }
    $aMetadata=& SqlLocalDB info $aBinding.InstanceName 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) { throw 'Live selected instance metadata is unavailable.' }
}
$aPipe=[regex]::Match($aMetadata,'(?im)^[\t ]*Instance pipe name:[\t ]*([^\r\n]+)\r?$')
if (-not $aPipe.Success -or $aPipe.Groups[1].Value.Trim() -notmatch '^np:\\\\.\\pipe\\LOCALDB#[A-Fa-f0-9]{8}\\tsql\\query$') {
    throw 'Selected live instance has no verified explicit local pipe.'
}
if ($aPipe.Groups[1].Value.Trim() -cne $aBinding.Pipe) {
    if (-not $StartExisting) { throw 'Live selected instance pipe differs from private binding.' }
    $aBinding.Pipe=$aPipe.Groups[1].Value.Trim()
    $aBinding | ConvertTo-Json | Set-Content -LiteralPath $BindingPath -Encoding utf8
}
$aBuilder=[System.Data.SqlClient.SqlConnectionStringBuilder]::new()
# PowerShell adapts this IDictionary implementation; use its canonical SQL
# keywords and verify the actual typed accessors before opening a connection.
$aBuilder['Data Source']=$aBinding.Pipe
$aBuilder['Initial Catalog']=$aBinding.Database
$aBuilder['Integrated Security']=$true
$aBuilder['Encrypt']=$false
# This selected LocalDB uses operator-protected Windows named-pipe IPC and
# does not support this client's TLS negotiation. The strict local-pipe,
# instance/owner/database guards above exclude TCP and production targets.
$aBuilder['TrustServerCertificate']=$true
$aBuilder['Connect Timeout']=5
if ($aBuilder.get_DataSource() -cne $aBinding.Pipe -or $aBuilder.get_InitialCatalog() -cne 'Cosmix_Testing' -or -not $aBuilder.get_IntegratedSecurity()) {
    throw 'Connection builder did not retain the exact selected integrated-authentication binding.'
}
$aConnection=[System.Data.SqlClient.SqlConnection]::new($aBuilder.get_ConnectionString())
try {
    $aConnection.Open()
    $aCommand=$aConnection.CreateCommand(); $aCommand.CommandTimeout=10
    $aCommand.CommandText=@'
SET NOCOUNT ON;
SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) AS ServerName, ORIGINAL_LOGIN() AS Operator,
DB_NAME() AS DatabaseName,SUSER_SNAME(owner_sid) AS Owner,is_read_only AS IsReadOnly
FROM sys.databases WHERE database_id=DB_ID();
SELECT MigrationId,ProductVersion FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;
SELECT (SELECT COUNT_BIG(*) FROM auth.UserAccounts) AS Accounts,
(SELECT COUNT_BIG(*) FROM hr.Employees) AS Employees,
(SELECT COUNT_BIG(*) FROM hr.DisciplinaryCases) AS Cases;
'@
    $aReader=$aCommand.ExecuteReader(); $aSets=[System.Collections.Generic.List[object]]::new()
    do {
        $aRows=[System.Collections.Generic.List[object]]::new()
        while($aReader.Read()) {
            $aRow=[ordered]@{}
            for($aIndex=0;$aIndex -lt $aReader.FieldCount;$aIndex++){ $aRow[$aReader.GetName($aIndex)]=$aReader.GetValue($aIndex) }
            $aRows.Add([pscustomobject]$aRow)
        }
        $aSets.Add($aRows.ToArray())
    } while($aReader.NextResult())
    $aReader.Dispose()
    $aIdentity=@($aSets[0]); $aHistory=@($aSets[1]); $aCounts=@($aSets[2])
    [pscustomobject]@{DatabaseMatches=($aIdentity.Count -eq 1 -and $aIdentity[0].DatabaseName -ceq 'Cosmix_Testing');
        OwnerMatches=($aIdentity.Count -eq 1 -and $aIdentity[0].Owner -ieq $aOperator);
        OperatorMatches=($aIdentity.Count -eq 1 -and $aIdentity[0].Operator -ieq $aOperator);
        MigrationCount=$aHistory.Count;LatestMigration=if($aHistory.Count -gt 0){$aHistory[-1].MigrationId}else{$null};
        Accounts=$aCounts[0].Accounts;Employees=$aCounts[0].Employees;QueryOnly=$true} | ConvertTo-Json
    if($aIdentity.Count -ne 1 -or $aIdentity[0].DatabaseName -cne 'Cosmix_Testing' -or
        $aIdentity[0].Owner -ine $aOperator -or $aIdentity[0].Operator -ine $aOperator -or $aHistory.Count -lt 22) {
        throw 'Live target identity/operator/history differs from the approved existing binding.'
    }
    [pscustomobject]@{Identity=$aIdentity[0];History=$aHistory;Counts=$aCounts;
        ObservedAtUtc=[datetime]::UtcNow.ToString('O');ReadOnly=$true} | ConvertTo-Json -Depth 5 |
        Set-Content -LiteralPath (Join-Path (Split-Path -Parent $BindingPath) 'read-only-preflight.json') -Encoding utf8
    [pscustomobject]@{Database='Cosmix_Testing';OwnerMatchesOperator=$true;MigrationCount=$aHistory.Count;
        LatestMigration=$aHistory[-1].MigrationId;Accounts=$aCounts[0].Accounts;Employees=$aCounts[0].Employees;
        DisciplinaryCases=$aCounts[0].Cases;IsReadOnly=$aIdentity[0].IsReadOnly;QueryOnly=$true} | ConvertTo-Json
} finally { $aConnection.Dispose() }
