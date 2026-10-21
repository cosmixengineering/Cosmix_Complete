<#!
Source-only preparation. The responsible operator must review the frozen SQL,
manifest, hashes, and this runner before invoking it. This creates a new backup
and isolated clone; it never applies schema changes to Cosmix_Testing.
!#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ReviewedSqlPath,
    [Parameter(Mandatory=$true)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$ExpectedSqlSha256,
    [Parameter(Mandatory=$true)][string]$FrozenMigrationManifestPath,
    [Parameter(Mandatory=$true)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$ExpectedManifestSha256,
    [string]$BindingPath = (Join-Path $env:LOCALAPPDATA 'Cosmix/PartAContinuation/Database20261013/target-binding.json'),
    [string]$FixturePath = (Join-Path $env:LOCALAPPDATA 'Cosmix/LocalTesting/Test-Accounts.json'),
    [switch]$StartExisting,
    [switch]$CloneOnly
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$aPrivateRoot=[IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Cosmix/PartAContinuation/Database20261013'))
$aOperator=[Security.Principal.WindowsIdentity]::GetCurrent()
$aOperatorName=$aOperator.Name
$aAllowedSids=@($aOperator.User.Value,'S-1-5-18','S-1-5-32-544')
$aInstance='CosmixUat_d893653346'
$aSource='Cosmix_Testing'
$aClone='Cosmix_PartA_Acceptance_20261013'
$aBaseline='20261008074438_AddEmployeeChangeJournal'
$aFinal='20261013121357_CompleteHrFoundationPartA'
$aStage='source preflight'
$aGuardFailure=$null
$aAttemptRoot=$null
$aConnections=[Collections.Generic.List[IDisposable]]::new()
$aClock=[Diagnostics.Stopwatch]::StartNew()

function Require([bool]$Condition,[string]$Message) {
    if(-not $Condition){$script:aGuardFailure=$Message;throw [InvalidOperationException]::new($Message)}
}
function Check-NoLinks([string]$Path) {
    $aWalk=[IO.Path]::GetFullPath($Path)
    while($aWalk){
        if(Test-Path -LiteralPath $aWalk){
            Require (((Get-Item -LiteralPath $aWalk -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -eq 0) 'Linked path ancestry is forbidden.'
        }
        $aWalk=[IO.Path]::GetDirectoryName($aWalk)
    }
}
function Check-Private([string]$Path,[bool]$Directory=$false) {
    Check-NoLinks $Path
    Require (Test-Path -LiteralPath $Path) 'Required private evidence is missing.'
    $aAcl=Get-Acl -LiteralPath $Path
    Require ($aAcl.GetOwner([Security.Principal.SecurityIdentifier]).Value -ceq $aOperator.User.Value) 'Private evidence owner differs from the operator.'
    foreach($aRule in $aAcl.GetAccessRules($true,$true,[Security.Principal.SecurityIdentifier])){
        Require ($aRule.AccessControlType -ne [Security.AccessControl.AccessControlType]::Allow -or $aAllowedSids -ccontains $aRule.IdentityReference.Value) 'Private evidence has a non-private allow rule.'
    }
    if($Directory){
        Require ((Get-Item -LiteralPath $Path).PSIsContainer -and $aAcl.AreAccessRulesProtected) 'Private root must be a protected owned directory.'
    } else { Require (-not (Get-Item -LiteralPath $Path).PSIsContainer) 'Expected a private file.' }
}
function Protect-NewDirectory([string]$Path) {
    Require (-not (Test-Path -LiteralPath $Path)) 'Attempt directory already exists; no overwrite or adoption.'
    Require ([IO.Path]::GetFullPath($Path).StartsWith($aPrivateRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) 'Attempt path escaped its private root.'
    Check-NoLinks $Path
    [void][IO.Directory]::CreateDirectory($Path)
    $aAcl=[Security.AccessControl.DirectorySecurity]::new()
    $aAcl.SetOwner($aOperator.User)
    $aAcl.SetAccessRuleProtection($true,$false)
    foreach($aSid in $aAllowedSids){
        $aAcl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new(
            [Security.Principal.SecurityIdentifier]::new($aSid),[Security.AccessControl.FileSystemRights]::FullControl,
            ([Security.AccessControl.InheritanceFlags]::ContainerInherit -bor [Security.AccessControl.InheritanceFlags]::ObjectInherit),
            [Security.AccessControl.PropagationFlags]::None,[Security.AccessControl.AccessControlType]::Allow))
    }
    Set-Acl -LiteralPath $Path -AclObject $aAcl
    Check-Private $Path $true
}
function New-PrivateJson([string]$Name,$Value) {
    Require ($null -ne $aAttemptRoot) 'No owned attempt directory.'
    $aPath=Join-Path $aAttemptRoot $Name
    Require ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($aPath)) -ieq $aAttemptRoot) 'Evidence filename escaped the attempt directory.'
    $aBytes=[Text.UTF8Encoding]::new($false).GetBytes(($Value | ConvertTo-Json -Depth 12))
    $aFile=[IO.FileStream]::new($aPath,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try{$aFile.Write($aBytes,0,$aBytes.Length);$aFile.Flush($true)}finally{$aFile.Dispose()}
    Check-Private $aPath
}
function Open-BoundConnection([string]$Catalog) {
    Require ($Catalog -cin @('master',$aSource,$aClone)) 'Unapproved connection catalog.'
    $aBuilder=[Data.SqlClient.SqlConnectionStringBuilder]::new()
    # Use IDictionary SQL keywords; PowerShell property adaptation can misbind setters.
    $aBuilder['Data Source']=$aLivePipe
    $aBuilder['Initial Catalog']=$Catalog
    $aBuilder['Integrated Security']=$true
    $aBuilder['Encrypt']=$false
    # This client cannot negotiate TLS with the exact owner-verified Windows
    # LocalDB pipe. The local-pipe/IsLocalDB/owner guards exclude TCP targets.
    $aBuilder['TrustServerCertificate']=$true
    $aBuilder['Connect Timeout']=5
    $aBuilder['Pooling']=$false
    $aBuilder['Application Name']='Cosmix Part A guarded clone acceptance'
    Require ($aBuilder.get_DataSource() -ceq $aLivePipe -and $aBuilder.get_InitialCatalog() -ceq $Catalog -and $aBuilder.get_IntegratedSecurity() -and -not $aBuilder.get_Encrypt()) 'Canonical connection binding failed.'
    $aConnection=[Data.SqlClient.SqlConnection]::new($aBuilder.get_ConnectionString())
    $aConnections.Add($aConnection)
    $aConnection.Open()
    return $aConnection
}
function New-Command($Connection,[string]$Text,[hashtable]$Parameters=@{},[int]$Timeout=180) {
    Require ($aClock.Elapsed.TotalMinutes -lt 20) 'The bounded acceptance command-dispatch window expired.'
    $aCommand=$Connection.CreateCommand();$aCommand.CommandText=$Text;$aCommand.CommandTimeout=$Timeout
    foreach($aKey in $Parameters.Keys){
        [void]$aCommand.Parameters.Add('@'+$aKey,[Data.SqlDbType]::NVarChar,4000)
        $aCommand.Parameters['@'+$aKey].Value=$Parameters[$aKey]
    }
    return $aCommand
}
function Read-Rows($Connection,[string]$Text,[hashtable]$Parameters=@{}) {
    $aCommand=New-Command $Connection $Text $Parameters
    try{
        $aReader=$aCommand.ExecuteReader();$aRows=[Collections.Generic.List[object]]::new()
        try{
            while($aReader.Read()){
                $aRow=[ordered]@{}
                for($aIndex=0;$aIndex -lt $aReader.FieldCount;$aIndex++){ $aRow[$aReader.GetName($aIndex)]=$aReader.GetValue($aIndex) }
                $aRows.Add([pscustomobject]$aRow)
            }
        }finally{$aReader.Dispose()}
        return $aRows.ToArray()
    }finally{$aCommand.Dispose()}
}
function Execute-Sql($Connection,[string]$Text,[hashtable]$Parameters=@{}) {
    $aCommand=New-Command $Connection $Text $Parameters
    try{[void]$aCommand.ExecuteNonQuery()}finally{$aCommand.Dispose()}
}
function Quote-Name([string]$Value){ return '['+$Value.Replace(']',']]')+']' }
function Check-Database($Connection,[string]$Catalog) {
    $aRows=@(Read-Rows $Connection @'
SET NOCOUNT ON;
SELECT DB_NAME() AS Catalog,ORIGINAL_LOGIN() AS OperatorName,
 CONVERT(int,SERVERPROPERTY('IsLocalDB')) AS IsLocalDb,
 CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) AS ServerName,
 SUSER_SNAME(d.owner_sid) AS OwnerName,d.is_read_only AS ReadOnly,d.state_desc AS State,
 CONVERT(nvarchar(36),r.family_guid) AS FamilyId,
 (SELECT COUNT(*) FROM sys.synonyms) AS Synonyms,
 (SELECT COUNT(*) FROM sys.sql_expression_dependencies WHERE referenced_server_name IS NOT NULL OR referenced_database_name IS NOT NULL) AS ExternalDependencies
FROM sys.databases d JOIN sys.database_recovery_status r ON r.database_id=d.database_id WHERE d.database_id=DB_ID();
'@)
    Require ($aRows.Count -eq 1 -and $aRows[0].Catalog -ceq $Catalog -and $aRows[0].OperatorName -ieq $aOperatorName -and $aRows[0].OwnerName -ieq $aOperatorName -and $aRows[0].IsLocalDb -eq 1 -and -not $aRows[0].ReadOnly -and $aRows[0].State -ceq 'ONLINE' -and $aRows[0].Synonyms -eq 0 -and $aRows[0].ExternalDependencies -eq 0 -and [guid]$aRows[0].FamilyId -eq $aExpectedFamily) 'Live catalog/local/operator/family/dependency guard failed.'
    return $aRows[0]
}
function Read-History($Connection) { return @(Read-Rows $Connection 'SELECT MigrationId,ProductVersion FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;') }
function Check-History($History,[int]$ExpectedCount) {
    Require ($History.Count -eq $ExpectedCount) 'Unexpected migration history length.'
    for($aIndex=0;$aIndex -lt $ExpectedCount;$aIndex++){
        Require ($History[$aIndex].MigrationId -ceq $aManifest[$aIndex].MigrationId -and $History[$aIndex].ProductVersion -ceq $aManifest[$aIndex].ProductVersion) 'History is not the exact frozen graph prefix.'
    }
}
function Hash-Bytes([byte[]]$Bytes) {
    $aHash=[Security.Cryptography.SHA256]::Create()
    try{ return ([BitConverter]::ToString($aHash.ComputeHash($Bytes))).Replace('-','') }finally{$aHash.Dispose()}
}
function Read-BoundText([string]$Path,[string]$ExpectedSha256) {
    $aBytes=[IO.File]::ReadAllBytes([IO.Path]::GetFullPath($Path))
    Require ((Hash-Bytes $aBytes) -ieq $ExpectedSha256) 'The actual input bytes differ from the reviewed hash.'
    $aMemory=[IO.MemoryStream]::new($aBytes,$false)
    $aReader=[IO.StreamReader]::new($aMemory,[Text.UTF8Encoding]::new($false,$true),$true)
    try{return $aReader.ReadToEnd()}finally{$aReader.Dispose();$aMemory.Dispose()}
}
function Write-CanonicalValue($Writer,$Value) {
    if($Value -is [DBNull]){ $Writer.Write([byte]0);return }
    $Writer.Write([byte]1)
    $Writer.Write($Value.GetType().FullName)
    if($Value -is [byte[]]){ $Writer.Write($Value.Length);$Writer.Write($Value);return }
    $aCulture=[Globalization.CultureInfo]::InvariantCulture
    if($Value -is [decimal]){$aText=$Value.ToString('G29',$aCulture)}
    elseif($Value -is [datetime]){$aText=$Value.ToString('O',$aCulture)}
    elseif($Value -is [datetimeoffset]){$aText=$Value.ToString('O',$aCulture)}
    elseif($Value -is [double]){$aText=$Value.ToString('G17',$aCulture)}
    elseif($Value -is [single]){$aText=$Value.ToString('G9',$aCulture)}
    elseif($Value -is [IFormattable]){$aText=$Value.ToString($null,$aCulture)}
    else{$aText=$Value.ToString()}
    $Writer.Write([string]$aText)
}
function Read-PreservationSnapshot($Connection,$Baseline=$null) {
    $aTables=@(Read-Rows $Connection 'SELECT s.name AS SchemaName,t.name AS TableName FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE t.is_ms_shipped=0 ORDER BY s.name,t.name;')
    $aItems=[Collections.Generic.List[object]]::new();[long]$aTotal=0
    foreach($aTable in $aTables){
        $aKey=$aTable.SchemaName+'.'+$aTable.TableName
        $aQuoted=(Quote-Name $aTable.SchemaName)+'.'+(Quote-Name $aTable.TableName)
        $aCountRows=@(Read-Rows $Connection ('SELECT COUNT_BIG(*) AS Rows FROM '+$aQuoted+';'))
        [long]$aCount=$aCountRows[0].Rows;$aTotal+=$aCount
        Require ($aCount -le 100000 -and $aTotal -le 200000) 'Database exceeds this bounded local acceptance inventory.'
        $aOriginal=@(if($null -ne $Baseline){$Baseline | Where-Object Key -CEQ $aKey})
        if($null -ne $Baseline -and $aOriginal.Count -eq 0){
            $aItems.Add([pscustomobject]@{Key=$aKey;Rows=$aCount;Columns=@();Digest=$null});continue
        }
        # Capture every original source column except SQL rowversion/timestamp.
        # Newly introduced projections are intentionally outside this source-fact proof.
        $aColumns=@(if($aOriginal.Count -eq 1){$aOriginal[0].Columns}else{Read-Rows $Connection 'SELECT c.name AS Name FROM sys.columns c WHERE c.object_id=OBJECT_ID(@name) AND c.system_type_id<>189 ORDER BY c.column_id;' @{name=$aQuoted} | ForEach-Object Name})
        Require ($aColumns.Count -gt 0) 'A source table has no retained comparison columns.'
        $aCommand=New-Command $Connection ('SELECT '+(($aColumns | ForEach-Object {Quote-Name $_}) -join ',')+' FROM '+$aQuoted+';')
        $aRowHashes=[Collections.Generic.List[string]]::new()
        try{
            $aReader=$aCommand.ExecuteReader()
            try{
                while($aReader.Read()){
                    Require ($aRowHashes.Count -lt 100000) 'Source grew beyond the bounded row inventory.'
                    $aMemory=[IO.MemoryStream]::new();$aWriter=[IO.BinaryWriter]::new($aMemory,[Text.UTF8Encoding]::new($false),$true)
                    try{
                        for($aIndex=0;$aIndex -lt $aReader.FieldCount;$aIndex++){ Write-CanonicalValue $aWriter ($aReader.GetValue($aIndex)) }
                        $aWriter.Flush();$aRowHashes.Add((Hash-Bytes $aMemory.ToArray()))
                    }finally{$aWriter.Dispose();$aMemory.Dispose()}
                }
            }finally{$aReader.Dispose()}
        }finally{$aCommand.Dispose()}
        Require ($aRowHashes.Count -eq $aCount) 'Rows changed while inventory was being captured.'
        [string[]]$aSorted=$aRowHashes.ToArray();[Array]::Sort($aSorted,[StringComparer]::Ordinal)
        $aDigest=Hash-Bytes ([Text.Encoding]::UTF8.GetBytes(($aSorted -join "`n")))
        $aItems.Add([pscustomobject]@{Key=$aKey;Rows=$aCount;Columns=$aColumns;Digest=$aDigest})
    }
    return $aItems.ToArray()
}
function Check-Preserved($Before,$After,[bool]$AfterMigration=$false) {
    foreach($aOriginal in $Before){
        $aCurrent=@($After | Where-Object Key -CEQ $aOriginal.Key)
        Require ($aCurrent.Count -eq 1) 'An original table is missing.'
        if($AfterMigration -and $aOriginal.Key -ceq 'dbo.__EFMigrationsHistory'){continue}
        Require ($aCurrent[0].Rows -eq $aOriginal.Rows -and $aCurrent[0].Digest -ceq $aOriginal.Digest) 'Retained row count/ID/source facts changed; review is required.'
    }
    $aOldKeys=@($Before | ForEach-Object Key)
    foreach($aNew in $After){
        if($aOldKeys -cnotcontains $aNew.Key){Require ($AfterMigration -and $aNew.Rows -eq 0) 'Unexpected new or seeded business table.'}
    }
    if(-not $AfterMigration){Require ($Before.Count -eq $After.Count) 'Clone table inventory differs.'}
}
function Split-ReviewedSql([string]$Sql) {
    # GO is recognized only on a separator line outside strings, quoted names,
    # comments, and nested block comments. GO n / SQLCMD directives are rejected.
    $aBatches=[Collections.Generic.List[string]]::new();$aBuffer=[Text.StringBuilder]::new()
    $aState='normal';$aBlockDepth=0
    foreach($aLine in [regex]::Split($Sql,"\r\n|\n|\r")){
        if($aState -ceq 'normal' -and $aLine -match '^\s*GO\s*(?:--.*)?$'){
            if(-not [string]::IsNullOrWhiteSpace($aBuffer.ToString())){$aBatches.Add($aBuffer.ToString())}
            [void]$aBuffer.Clear();continue
        }
        Require (-not ($aState -ceq 'normal' -and $aLine -match '^\s*(?:GO\b|:|!)')) 'Unsupported GO repetition or SQLCMD command.'
        [void]$aBuffer.AppendLine($aLine)
        for($aIndex=0;$aIndex -lt $aLine.Length;$aIndex++){
            $aChar=$aLine[$aIndex];$aNext=if($aIndex+1 -lt $aLine.Length){$aLine[$aIndex+1]}else{[char]0}
            if($aState -ceq 'normal'){
                if($aChar -eq '-' -and $aNext -eq '-') {break}
                if($aChar -eq '/' -and $aNext -eq '*'){$aState='block';$aBlockDepth=1;$aIndex++;continue}
                if($aChar -eq "'"){$aState='string';continue}
                if($aChar -eq '['){$aState='bracket';continue}
                if($aChar -eq '"'){$aState='quoted';continue}
            }elseif($aState -ceq 'block'){
                if($aChar -eq '/' -and $aNext -eq '*'){$aBlockDepth++;$aIndex++}
                elseif($aChar -eq '*' -and $aNext -eq '/'){$aBlockDepth--;$aIndex++;if($aBlockDepth -eq 0){$aState='normal'}}
            }elseif($aState -ceq 'string' -and $aChar -eq "'"){
                if($aNext -eq "'"){$aIndex++}else{$aState='normal'}
            }elseif($aState -ceq 'bracket' -and $aChar -eq ']'){
                if($aNext -eq ']'){$aIndex++}else{$aState='normal'}
            }elseif($aState -ceq 'quoted' -and $aChar -eq '"'){
                if($aNext -eq '"'){$aIndex++}else{$aState='normal'}
            }
        }
    }
    Require ($aState -ceq 'normal') 'Unterminated SQL lexical construct.'
    if(-not [string]::IsNullOrWhiteSpace($aBuffer.ToString())){$aBatches.Add($aBuffer.ToString())}
    Require ($aBatches.Count -gt 0 -and $aBatches.Count -le 2000) 'Invalid or unbounded SQL batch count.'
    return $aBatches.ToArray()
}

try {
    Require ($env:OS -ceq 'Windows_NT') 'Windows LocalDB acceptance only.'
    Check-Private $aPrivateRoot $true
    Require ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($BindingPath)) -ieq $aPrivateRoot) 'Binding is outside the private selection directory.'
    Check-Private $BindingPath;Check-Private $FixturePath
    $aBinding=Get-Content -LiteralPath $BindingPath -Raw | ConvertFrom-Json
    Require ($aBinding.InstanceName -ceq $aInstance -and $aBinding.Database -ceq $aSource -and $aBinding.Operator -ieq $aOperatorName) 'Private selection does not match the approved existing target.'
    # Selection/profile and historic pipe are provenance, never current identity proof.
    $aKnownHeaderPath=Join-Path $aPrivateRoot 'known-backup-header.json'
    $aInventoryPath=Join-Path $aPrivateRoot ($aInstance+'-inventory.json')
    $aKnownIntentPath=Join-Path $env:LOCALAPPDATA 'Cosmix/LocalTesting/Attendance-Office-Migration-20261013-B05-intent.json'
    Check-Private $aKnownHeaderPath;Check-Private $aInventoryPath;Check-Private $aKnownIntentPath
    $aKnownHeader=Get-Content -LiteralPath $aKnownHeaderPath -Raw | ConvertFrom-Json
    $aInventory=Get-Content -LiteralPath $aInventoryPath -Raw | ConvertFrom-Json
    $aKnownIntent=Get-Content -LiteralPath $aKnownIntentPath -Raw | ConvertFrom-Json
    Require ($aKnownIntent.Owner -ieq $aOperatorName -and $aKnownIntent.Database -ceq $aSource -and $aKnownIntent.RestoreVerified -and $aKnownIntent.BackupSha256 -match '^[A-Fa-f0-9]{64}$') 'Known backup operator/provenance is invalid.'
    Check-Private $aKnownIntent.Backup
    Require ((Get-FileHash -LiteralPath $aKnownIntent.Backup -Algorithm SHA256).Hash -ieq $aKnownIntent.BackupSha256) 'Known backup hash changed.'
    $aExpectedFamily=[guid]$aKnownHeader.FamilyGUID
    Require ($aExpectedFamily -ne [guid]::Empty -and $aKnownHeader.DatabaseName -ceq $aSource -and $aKnownHeader.BackupType -eq 1 -and $aKnownHeader.IsCopyOnly -and $aKnownHeader.HasBackupChecksums -and $aInventory.InstanceName -ceq $aInstance -and $aInventory.Database -ceq $aSource -and $aInventory.Operator -ieq $aOperatorName -and [guid]$aInventory.FamilyId -eq $aExpectedFamily) 'Known family/inventory provenance failed.'

    Check-NoLinks $ReviewedSqlPath;Check-NoLinks $FrozenMigrationManifestPath
    Require ((Get-FileHash -LiteralPath $ReviewedSqlPath -Algorithm SHA256).Hash -ieq $ExpectedSqlSha256 -and (Get-FileHash -LiteralPath $FrozenMigrationManifestPath -Algorithm SHA256).Hash -ieq $ExpectedManifestSha256) 'Reviewed frozen input hashes differ.'
    $aManifest=@((Read-BoundText $FrozenMigrationManifestPath $ExpectedManifestSha256) | ConvertFrom-Json)
    Require ($aManifest.Count -eq 23 -and $aManifest[11].MigrationId -ceq $aBaseline -and $aManifest[22].MigrationId -ceq $aFinal -and @($aManifest | ForEach-Object MigrationId | Select-Object -Unique).Count -eq 23) 'Unexpected frozen migration graph.'
    for($aIndex=0;$aIndex -lt $aManifest.Count;$aIndex++){
        Require ($aManifest[$aIndex].MigrationId -match '^\d{14}_[A-Za-z0-9_]+$' -and $aManifest[$aIndex].ProductVersion -match '^\d+\.\d+\.\d+(?:[-+][A-Za-z0-9.-]+)?$') 'Invalid frozen manifest record.'
        if($aIndex -gt 0){Require ([string]::CompareOrdinal($aManifest[$aIndex-1].MigrationId,$aManifest[$aIndex].MigrationId) -lt 0) 'Frozen graph is not ordered.'}
    }
    $aSql=Read-BoundText $ReviewedSqlPath $ExpectedSqlSha256
    Require ($aSql.Length -gt 0 -and $aSql.Length -le 10000000) 'SQL size exceeds bounded acceptance.'
    Require (-not [regex]::IsMatch($aSql,'(?im)^\s*(?:USE\b|DELETE\b|:|!)|\b(?:CREATE|ALTER|DROP)\s+DATABASE\b|\b(?:BACKUP|RESTORE|TRUNCATE)\b|\bDELETE\s+(?:FROM|TOP)\b|\bEXECUTE\s+AS\b')) 'SQL contains target switching or a prohibited destructive operation.'
    Require (-not [regex]::IsMatch($aSql,'(?i)\bDROP\s+(?:TABLE|COLUMN)\b')) 'SQL removes retained schema facts.'
    $aBatches=@(Split-ReviewedSql $aSql)
    # Exactly the eleven pending migration-history INSERTs are required; the
    # reviewed hash binds all other SQL, including any legitimate date projections.
    $aInsertedIds=@([regex]::Matches($aSql,"(?is)INSERT\s+INTO\s+\[__EFMigrationsHistory\]\s*\([^)]*\)\s*VALUES\s*\(N?'([^']+)'\s*,\s*N?'([^']+)'\s*\)") | ForEach-Object { [pscustomobject]@{MigrationId=$_.Groups[1].Value;ProductVersion=$_.Groups[2].Value} })
    Require ($aInsertedIds.Count -eq 11) 'SQL is not exactly the reviewed pending 12-to-A23 range.'
    for($aIndex=0;$aIndex -lt 11;$aIndex++){Require ($aInsertedIds[$aIndex].MigrationId -ceq $aManifest[$aIndex+12].MigrationId -and $aInsertedIds[$aIndex].ProductVersion -ceq $aManifest[$aIndex+12].ProductVersion) 'SQL history inserts differ from the frozen manifest.'}

    $aFixtures=@(Get-Content -LiteralPath $FixturePath -Raw | ConvertFrom-Json)
    $aFixtureEmails=@($aFixtures | ForEach-Object {[string]$_.Email})
    Require ($aFixtureEmails.Count -eq 3 -and @($aFixtureEmails | Select-Object -Unique).Count -eq 3 -and @($aFixtureEmails | Where-Object {[string]::IsNullOrWhiteSpace($_)}).Count -eq 0) 'Expected exactly three private fixture email identities.'
    $aFixtures=$null # Password is never used for SQL inventory.
    $aInfo=& SqlLocalDB info $aInstance 2>&1 | Out-String
    Require ($LASTEXITCODE -eq 0) 'The already selected LocalDB instance is unavailable.'
    $aOwner=[regex]::Match($aInfo,'(?im)^[\t ]*Owner:[\t ]*([^\r\n]+)\r?$')
    Require ($aOwner.Success -and $aOwner.Groups[1].Value.Trim() -ieq $aOperatorName) 'Existing LocalDB owner differs from the operator.'
    if($StartExisting){
        $aStart=& SqlLocalDB start $aInstance 2>&1 | Out-String
        Require ($LASTEXITCODE -eq 0) 'Exact owner-verified existing instance failed to start.'
        $aInfo=& SqlLocalDB info $aInstance 2>&1 | Out-String
        Require ($LASTEXITCODE -eq 0) 'Existing LocalDB metadata could not be refreshed.'
    }
    $aLiveOwner=[regex]::Match($aInfo,'(?im)^[\t ]*Owner:[\t ]*([^\r\n]+)\r?$')
    Require ($aLiveOwner.Success -and $aLiveOwner.Groups[1].Value.Trim() -ieq $aOperatorName) 'Refreshed existing LocalDB owner differs from the operator.'
    $aPipeMatch=[regex]::Match($aInfo,'(?im)^[\t ]*Instance pipe name:[\t ]*([^\r\n]+)\r?$')
    Require ($aPipeMatch.Success -and $aPipeMatch.Groups[1].Value.Trim() -match '^np:\\\\.\\pipe\\LOCALDB#[A-Fa-f0-9]{8}\\tsql\\query$') 'Exact local named pipe is unavailable.'
    $aLivePipe=$aPipeMatch.Groups[1].Value.Trim()
    $aMaster=Open-BoundConnection 'master';$aSourceConnection=Open-BoundConnection $aSource
    $aSourceIdentity=Check-Database $aSourceConnection $aSource
    Require ($aSourceIdentity.ServerName.Split('\')[0] -ieq $env:COMPUTERNAME -and $aKnownHeader.ServerName.Split('\')[0] -ieq $env:COMPUTERNAME) 'Current or known backup host differs from this operator host.'
    $aHistory=@(Read-History $aSourceConnection);Check-History $aHistory 12
    $aMatches=@(Read-Rows $aSourceConnection 'SELECT (SELECT COUNT_BIG(*) FROM auth.UserAccounts WHERE LoginName=@e0) AS Match0,(SELECT COUNT_BIG(*) FROM auth.UserAccounts WHERE LoginName=@e1) AS Match1,(SELECT COUNT_BIG(*) FROM auth.UserAccounts WHERE LoginName=@e2) AS Match2;' @{e0=$aFixtureEmails[0];e1=$aFixtureEmails[1];e2=$aFixtureEmails[2]})
    Require ($aMatches.Count -eq 1 -and $aMatches[0].Match0 -eq 1 -and $aMatches[0].Match1 -eq 1 -and $aMatches[0].Match2 -eq 1) 'All three private expected fixture accounts must exist uniquely.'
    $aCounts=@(Read-Rows $aSourceConnection 'SELECT (SELECT COUNT_BIG(*) FROM auth.UserAccounts) AS Accounts,(SELECT COUNT_BIG(*) FROM hr.Employees) AS Employees;')
    Require ($aCounts[0].Accounts -eq 5 -and $aCounts[0].Employees -eq 14) 'Source inventory differs from the reviewed current target.'
    $aFiles=@(Read-Rows $aSourceConnection 'SELECT name,type,physical_name,size FROM sys.database_files ORDER BY file_id;')
    Require ($aFiles.Count -eq 2 -and @($aFiles | Where-Object type -EQ 0).Count -eq 1 -and @($aFiles | Where-Object type -EQ 1).Count -eq 1 -and ($aFiles | Measure-Object size -Sum).Sum -le 1310720) 'Expected one bounded data file and one log file.'
    $aAbsent=@(Read-Rows $aMaster 'SELECT DB_ID(@catalog) AS ExistingId;' @{catalog=$aClone})
    Require ($aAbsent[0].ExistingId -is [DBNull]) 'Acceptance catalog already exists; no adoption, replace, or retry.'
    $aKnownActual=@(Read-Rows $aMaster 'RESTORE HEADERONLY FROM DISK=@path;' @{path=$aKnownIntent.Backup})
    Require ($aKnownActual.Count -eq 1 -and $aKnownActual[0].DatabaseName -ceq $aSource -and [guid]$aKnownActual[0].FamilyGUID -eq $aExpectedFamily -and [guid]$aKnownActual[0].BackupSetGUID -eq [guid]$aKnownHeader.BackupSetGUID -and $aKnownActual[0].ServerName -ceq $aKnownHeader.ServerName -and $aKnownActual[0].BackupType -eq 1 -and $aKnownActual[0].IsCopyOnly -and $aKnownActual[0].HasBackupChecksums -and -not $aKnownActual[0].IsDamaged) 'Known backup readback provenance differs.'
    $aBefore=@(Read-PreservationSnapshot $aSourceConnection)
    foreach($aKey in @('auth.UserAccounts','hr.Employees')){Require (@($aBefore | Where-Object Key -CEQ $aKey)[0].Columns -ccontains 'Id') 'Core identity columns are missing from preservation proof.'}

    $aAttemptRoot=Join-Path $aPrivateRoot ('clone-'+[guid]::NewGuid().ToString('N'))
    Protect-NewDirectory $aAttemptRoot
    $aRestoreRoot=Join-Path $aAttemptRoot 'restore';Protect-NewDirectory $aRestoreRoot
    $aBackup=Join-Path $aAttemptRoot 'Current12-CopyOnly-Checksum.bak'
    $aDataPath=Join-Path $aRestoreRoot ($aClone+'.mdf');$aLogPath=Join-Path $aRestoreRoot ($aClone+'_log.ldf')
    foreach($aPath in @($aBackup,$aDataPath,$aLogPath)){Check-NoLinks $aPath;Require (-not (Test-Path -LiteralPath $aPath)) 'A new target file already exists.'}
    New-PrivateJson 'intent.json' ([ordered]@{Instance=$aInstance;Source=$aSource;Clone=$aClone;Operator=$aOperatorName;Server=$aSourceIdentity.ServerName;Pipe=$aLivePipe;Family=$aExpectedFamily;OriginalHistory=$aHistory;SourceSnapshot=$aBefore;Backup=$aBackup;DataPath=$aDataPath;LogPath=$aLogPath;SqlSha256=$ExpectedSqlSha256;ManifestSha256=$ExpectedManifestSha256;ExpectedFixtureMatches=3;Accounts=5;Employees=14;AtUtc=[datetime]::UtcNow.ToString('O');OriginalSchemaWritesAllowed=$false})
    $aStage='fresh checksummed backup'
    [void](Check-Database $aSourceConnection $aSource);Check-History @(Read-History $aSourceConnection) 12
    Execute-Sql $aMaster 'BACKUP DATABASE [Cosmix_Testing] TO DISK=@path WITH COPY_ONLY,CHECKSUM,NOFORMAT,NOINIT,NAME=N''Cosmix current12 Part A clone acceptance 20261013'';' @{path=$aBackup}
    Check-Private $aBackup
    $aBackupSha=(Get-FileHash -LiteralPath $aBackup -Algorithm SHA256).Hash
    $aHeader=@(Read-Rows $aMaster 'RESTORE HEADERONLY FROM DISK=@path;' @{path=$aBackup})
    Require ($aHeader.Count -eq 1 -and $aHeader[0].Position -eq 1 -and $aHeader[0].DatabaseName -ceq $aSource -and [guid]$aHeader[0].FamilyGUID -eq $aExpectedFamily -and $aHeader[0].BackupType -eq 1 -and $aHeader[0].IsCopyOnly -and $aHeader[0].HasBackupChecksums -and -not $aHeader[0].IsDamaged) 'Fresh backup header verification failed.'
    Execute-Sql $aMaster 'RESTORE VERIFYONLY FROM DISK=@path WITH FILE=1,CHECKSUM;' @{path=$aBackup}
    Check-Preserved $aBefore @(Read-PreservationSnapshot $aSourceConnection $aBefore)
    Check-History @(Read-History $aSourceConnection) 12
    New-PrivateJson 'backup-verified.json' ([ordered]@{Backup=$aBackup;Sha256=$aBackupSha;Header=$aHeader[0];VerifyOnly=$true;SourceUnchanged=$true;AtUtc=[datetime]::UtcNow.ToString('O')})

    $aStage='isolated true restore'
    $aFileList=@(Read-Rows $aMaster 'RESTORE FILELISTONLY FROM DISK=@path WITH FILE=1;' @{path=$aBackup})
    Require ($aFileList.Count -eq 2 -and @($aFileList | Where-Object Type -CEQ 'D').Count -eq 1 -and @($aFileList | Where-Object Type -CEQ 'L').Count -eq 1 -and @($aFileList | Where-Object {$_.IsPresent -ne $true}).Count -eq 0) 'Backup must contain exactly one present data and one log file.'
    $aDataName=@($aFileList | Where-Object Type -CEQ 'D')[0].LogicalName;$aLogName=@($aFileList | Where-Object Type -CEQ 'L')[0].LogicalName
    Require ((Get-FileHash -LiteralPath $aBackup -Algorithm SHA256).Hash -ceq $aBackupSha) 'Fresh backup hash changed before restore.'
    Check-Private $aRestoreRoot $true
    foreach($aPath in @($aDataPath,$aLogPath)){Check-NoLinks $aPath;Require (-not (Test-Path -LiteralPath $aPath)) 'Restore destination became occupied.'}
    $aAbsent=@(Read-Rows $aMaster 'SELECT DB_ID(@catalog) AS ExistingId,(SELECT COUNT(*) FROM sys.master_files WHERE physical_name IN (@data,@log)) AS FileConflicts;' @{catalog=$aClone;data=$aDataPath;log=$aLogPath})
    Require ($aAbsent[0].ExistingId -is [DBNull] -and $aAbsent[0].FileConflicts -eq 0) 'Clone catalog/files became occupied.'
    Execute-Sql $aMaster 'RESTORE DATABASE [Cosmix_PartA_Acceptance_20261013] FROM DISK=@path WITH FILE=1,CHECKSUM,MOVE @dataName TO @dataPath,MOVE @logName TO @logPath,RECOVERY;' @{path=$aBackup;dataName=$aDataName;dataPath=$aDataPath;logName=$aLogName;logPath=$aLogPath}
    $aCloneConnection=Open-BoundConnection $aClone
    [void](Check-Database $aCloneConnection $aClone);Check-History @(Read-History $aCloneConnection) 12
    $aCloneFiles=@(Read-Rows $aCloneConnection 'SELECT type,physical_name FROM sys.database_files ORDER BY file_id;')
    Require ($aCloneFiles.Count -eq 2 -and @($aCloneFiles | Where-Object {$_.type -eq 0 -and $_.physical_name -ieq $aDataPath}).Count -eq 1 -and @($aCloneFiles | Where-Object {$_.type -eq 1 -and $_.physical_name -ieq $aLogPath}).Count -eq 1) 'Restored physical file paths differ from the owned destinations.'
    Check-Private $aDataPath;Check-Private $aLogPath
    $aCloneBefore=@(Read-PreservationSnapshot $aCloneConnection $aBefore);Check-Preserved $aBefore $aCloneBefore
    Check-Preserved $aBefore @(Read-PreservationSnapshot $aSourceConnection $aBefore)
    Check-History @(Read-History $aSourceConnection) 12
    New-PrivateJson 'restore-verified.json' ([ordered]@{Source=$aSource;Clone=$aClone;BackupSha256=$aBackupSha;TrueRestorePerformed=$true;ExactOriginalColumnsCompared=$true;RowVersionExcluded=$true;CloneSnapshot=$aCloneBefore;SourceUnchanged=$true;AtUtc=[datetime]::UtcNow.ToString('O')})
    Write-Output 'Fresh COPY_ONLY/CHECKSUM backup and true isolated restore verified; original row counts, IDs, and source facts match.'
    if($CloneOnly){
        Write-Output ([pscustomobject]@{CloneRestored=$true;MigrationCount=12;Accounts=5;Employees=14;FixtureMatches=3;OriginalUnchanged=$true;SqlApplied=$false}|ConvertTo-Json -Compress)
        return
    }

    $aStage='reviewed frozen A23 clone migration'
    Require ((Get-FileHash -LiteralPath $ReviewedSqlPath -Algorithm SHA256).Hash -ieq $ExpectedSqlSha256 -and (Get-FileHash -LiteralPath $FrozenMigrationManifestPath -Algorithm SHA256).Hash -ieq $ExpectedManifestSha256) 'Reviewed input hash changed before application.'
    Check-History @(Read-History $aCloneConnection) 12
    $aCompleted=0
    foreach($aBatch in $aBatches){
        [void](Check-Database $aCloneConnection $aClone)
        Execute-Sql $aCloneConnection $aBatch
        $aCompleted++
    }
    $aStage='post-A23 preservation verification'
    [void](Check-Database $aCloneConnection $aClone)
    $aCompletedHistory=@(Read-History $aCloneConnection);Check-History $aCompletedHistory 23
    $aCloneAfter=@(Read-PreservationSnapshot $aCloneConnection $aBefore);Check-Preserved $aBefore $aCloneAfter $true
    [void](Check-Database $aSourceConnection $aSource);Check-History @(Read-History $aSourceConnection) 12
    $aSourceAfter=@(Read-PreservationSnapshot $aSourceConnection $aBefore);Check-Preserved $aBefore $aSourceAfter
    Require ((Get-FileHash -LiteralPath $aBackup -Algorithm SHA256).Hash -ceq $aBackupSha) 'Fresh backup hash changed after acceptance.'
    New-PrivateJson 'frozen-a23-result.json' ([ordered]@{Source=$aSource;Clone=$aClone;Backup=$aBackup;BackupSha256=$aBackupSha;History=$aCompletedHistory;TableSnapshot=$aCloneAfter;OriginalSourceSnapshot=$aSourceAfter;SourceUnchanged=$true;RetainedRowsIdsAndSourceFactsPreserved=$true;RowVersionExcluded=$true;SqlSha256=$ExpectedSqlSha256;ManifestSha256=$ExpectedManifestSha256;CompletedBatches=$aCompleted;FixtureMatches=3;Accounts=5;Employees=14;OriginalSchemaApplicationAllowed=$false;AtUtc=[datetime]::UtcNow.ToString('O')})
    Write-Output ([pscustomobject]@{CloneRestored=$true;MigrationCount=23;Accounts=5;Employees=14;FixtureMatches=3;RetainedRowsIdsAndSourceFactsPreserved=$true;OriginalUnchanged=$true;SqlAppliedOnlyToClone=$true;CompletedBatches=$aCompleted}|ConvertTo-Json -Compress)
} catch {
    $aFailureDetails=$_.Exception.ToString()
    $aFailureStack=$_.ScriptStackTrace
    $aFailureLine=$_.InvocationInfo.ScriptLineNumber
    if($null -eq $aAttemptRoot -and (Test-Path -LiteralPath $aPrivateRoot)){
        try {
            Check-Private $aPrivateRoot $true
            $aAttemptRoot=Join-Path $aPrivateRoot ('preflight-'+[guid]::NewGuid().ToString('N'))
            Protect-NewDirectory $aAttemptRoot
        } catch { $aAttemptRoot=$null }
    }
    if($null -ne $aAttemptRoot -and (Test-Path -LiteralPath $aAttemptRoot)){
        try{New-PrivateJson 'failure.json' ([ordered]@{Stage=$aStage;Details=$aFailureDetails;ScriptStack=$aFailureStack;ScriptLine=$aFailureLine;AtUtc=[datetime]::UtcNow.ToString('O');AutomaticCleanupOrRetry=$false})}catch{}
    }
    # Do not emit SQL exceptions, private emails, pipe names, family IDs, or paths.
    $aSafeReason=if($null -ne $aGuardFailure){' '+$aGuardFailure}else{''}
    throw [InvalidOperationException]::new("Guarded clone acceptance stopped during $aStage.$aSafeReason No automatic cleanup/retry; review private evidence and retained state.")
} finally {
    foreach($aConnection in $aConnections){$aConnection.Dispose()}
    $aFixtureEmails=$null
}
