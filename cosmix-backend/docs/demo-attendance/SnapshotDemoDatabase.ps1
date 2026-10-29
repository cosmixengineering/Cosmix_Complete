param([ValidateSet('before','after','final')][string]$Phase,
 [ValidateSet('DemoAttendance20261028','DemoAttendancePolicy20261028')][string]$EvidenceFolder='DemoAttendance20261028')
$ErrorActionPreference='Stop'
$root=Join-Path (Join-Path $env:LOCALAPPDATA 'Cosmix\LocalTesting') $EvidenceFolder
if($EvidenceFolder -eq 'DemoAttendancePolicy20261028' -and (Test-Path -LiteralPath (Join-Path $root ($Phase+'-table-hashes.json')))){throw 'Preserve existing policy evidence; choose an unused phase'}
[void](New-Item -ItemType Directory -Path $root -Force)
$conn=[System.Data.SqlClient.SqlConnection]::new('Server=np:\\.\pipe\LOCALDB#36E7B148\tsql\query;Database=Cosmix_Testing;Integrated Security=true;TrustServerCertificate=true')
$conn.Open()
try {
 $cmd=$conn.CreateCommand()
 $cmd.CommandText="SELECT QUOTENAME(s.name)+'.'+QUOTENAME(t.name) TableName, (SELECT STRING_AGG(QUOTENAME(c.name),',') WITHIN GROUP (ORDER BY ic.key_ordinal) FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE i.object_id=t.object_id AND i.is_primary_key=1) OrderColumns FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id ORDER BY s.name,t.name"
 $table=[System.Data.DataTable]::new();$da=[System.Data.SqlClient.SqlDataAdapter]::new($cmd);[void]$da.Fill($table)
 $result=@()
 foreach($row in $table.Rows){
  $name=[string]$row.TableName; $order=[string]$row.OrderColumns
  if(!$order){throw 'Missing deterministic primary key'}
  $cmd.CommandText="SELECT COUNT_BIG(*) [RowCount], CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM $name ORDER BY $order FOR JSON PATH, INCLUDE_NULL_VALUES)),2) ContentSha256 FROM $name"
  $t=[System.Data.DataTable]::new();$da=[System.Data.SqlClient.SqlDataAdapter]::new($cmd);[void]$da.Fill($t)
  $result += [pscustomobject]@{Table=$name;Count=[long]$t.Rows[0].RowCount;Hash=[string]$t.Rows[0].ContentSha256}
 }
 $result|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $root ($Phase+'-table-hashes.json'))
 if($Phase -ne 'before'){
  $before=Get-Content -LiteralPath (Join-Path $root 'before-table-hashes.json') -Raw|ConvertFrom-Json
  $differences=@(foreach($r in $result){$b=$before|Where-Object Table -eq $r.Table;if($b.Hash -cne $r.Hash){[pscustomobject]@{Table=$r.Table;Before=$b.Count;After=$r.Count;Delta=$r.Count-$b.Count}}})
  $differences|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $root ($Phase+'-table-differences.json'))
  $differences|Format-Table -AutoSize
 }else{Write-Output ('Read-only baseline saved: '+$result.Count+' tables')}
}finally{$conn.Dispose()}
