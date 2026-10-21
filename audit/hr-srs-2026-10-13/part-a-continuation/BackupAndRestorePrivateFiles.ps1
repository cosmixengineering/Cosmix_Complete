$ErrorActionPreference='Stop'; Set-StrictMode -Version Latest
$aSettingsPath=Join-Path $env:LOCALAPPDATA 'Cosmix/LocalTesting/settings.json'
$aSettings=Get-Content -LiteralPath $aSettingsPath -Raw|ConvertFrom-Json
$aSource=[IO.Path]::GetFullPath($aSettings.PrivateFiles.Directory)
$aKey=[IO.Path]::GetFullPath($aSettings.PrivateFiles.KeyFile)
$aRoot=Join-Path $env:LOCALAPPDATA 'Cosmix/PartAContinuation/FileRestore20261013'
if(Test-Path -LiteralPath $aRoot){throw 'A prior restore evidence directory exists; preserve it and review before another run.'}
if(-not (Test-Path -LiteralPath $aSource -PathType Container) -or (Get-Item -LiteralPath $aKey).Length -ne 32){throw 'Existing private source/key is not provisioned.'}
$aIdentity=[System.Security.Principal.WindowsIdentity]::GetCurrent()
$aAllowed=@($aIdentity.User.Value,'S-1-5-18','S-1-5-32-544')
function Assert-Private([string]$aPath){
 $aItem=Get-Item -LiteralPath $aPath
 if(($aItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw 'Private evidence cannot traverse links.'}
 $aAcl=Get-Acl -LiteralPath $aPath
 if($aAcl.GetOwner([System.Security.Principal.SecurityIdentifier]).Value -notin $aAllowed){throw 'Private owner mismatch.'}
 foreach($aRule in $aAcl.GetAccessRules($true,$true,[System.Security.Principal.SecurityIdentifier])){
  if($aRule.AccessControlType -eq 'Allow' -and $aRule.IdentityReference.Value -notin $aAllowed){throw 'Private ACL is too broad.'}
 }
}
function New-PrivateDirectory([string]$aPath){
 [void][IO.Directory]::CreateDirectory($aPath)
 $aAcl=[System.Security.AccessControl.DirectorySecurity]::new();$aAcl.SetOwner($aIdentity.User);$aAcl.SetAccessRuleProtection($true,$false)
 foreach($aSid in $aAllowed){$aAcl.AddAccessRule([System.Security.AccessControl.FileSystemAccessRule]::new([System.Security.Principal.SecurityIdentifier]::new($aSid),'FullControl','ContainerInherit,ObjectInherit','None','Allow'))}
 Set-Acl -LiteralPath $aPath -AclObject $aAcl;Assert-Private $aPath
}
Assert-Private $aSource;Assert-Private $aKey
if(@(Get-ChildItem -LiteralPath $aSource -Directory).Count -ne 0){throw 'Nested object source requires explicit backup review.'}
$aObjects=@(Get-ChildItem -LiteralPath $aSource -File)
if($aObjects.Count -eq 0){throw 'No existing private evidence.'}
New-PrivateDirectory $aRoot
foreach($aStage in @('Backup','Restored')){
 New-PrivateDirectory (Join-Path $aRoot $aStage)
 New-PrivateDirectory (Join-Path $aRoot "$aStage/Objects")
 New-PrivateDirectory (Join-Path $aRoot "$aStage/Keys")
}
$aEntries=[System.Collections.Generic.List[object]]::new()
foreach($aObject in $aObjects){
 Assert-Private $aObject.FullName
 $aBackup=Join-Path $aRoot ('Backup/Objects/'+$aObject.Name)
 $aRestored=Join-Path $aRoot ('Restored/Objects/'+$aObject.Name)
 [IO.File]::Copy($aObject.FullName,$aBackup,$false);[IO.File]::Copy($aBackup,$aRestored,$false)
 Assert-Private $aBackup;Assert-Private $aRestored
 $aHash=(Get-FileHash -LiteralPath $aObject.FullName -Algorithm SHA256).Hash
 if((Get-FileHash -LiteralPath $aBackup -Algorithm SHA256).Hash -cne $aHash -or (Get-FileHash -LiteralPath $aRestored -Algorithm SHA256).Hash -cne $aHash){throw 'Ciphertext backup/restore differs from source.'}
 $aEntries.Add([pscustomobject]@{Name=$aObject.Name;Length=$aObject.Length;Sha256=$aHash})
}
$aKeys=[ordered]@{};$aKeys[$aSettings.PrivateFiles.KeyReference]=$aKey
if($aSettings.PrivateFiles.PSObject.Properties.Name -contains 'DecryptionKeyFiles'){
 foreach($aProperty in $aSettings.PrivateFiles.DecryptionKeyFiles.PSObject.Properties){
  if($aKeys.Contains($aProperty.Name) -and $aKeys[$aProperty.Name] -ine $aProperty.Value){throw 'Key reference collision.'};$aKeys[$aProperty.Name]=[IO.Path]::GetFullPath($aProperty.Value)
 }
}
$aRestoredKeys=[ordered]@{};$aKeyIndex=0
foreach($aReference in $aKeys.Keys){
 $aKeySource=$aKeys[$aReference];Assert-Private $aKeySource
 if((Get-Item -LiteralPath $aKeySource).Length -ne 32){throw 'Retained encryption key must be exactly32bytes.'}
 $aKeyIndex++;$aBackupKey=Join-Path $aRoot "Backup/Keys/key-$aKeyIndex.bin";$aRestoredKey=Join-Path $aRoot "Restored/Keys/key-$aKeyIndex.bin"
 [IO.File]::Copy($aKeySource,$aBackupKey,$false);[IO.File]::Copy($aBackupKey,$aRestoredKey,$false)
 Assert-Private $aBackupKey;Assert-Private $aRestoredKey
 if((Get-FileHash -LiteralPath $aKeySource).Hash -cne (Get-FileHash -LiteralPath $aRestoredKey).Hash){throw 'Restored key differs.'}
 $aRestoredKeys[$aReference]=$aRestoredKey
}
$aDecryptionKeys=[ordered]@{}
foreach($aReference in $aRestoredKeys.Keys){if($aReference -cne $aSettings.PrivateFiles.KeyReference){$aDecryptionKeys[$aReference]=$aRestoredKeys[$aReference]}}
$aOptions=[ordered]@{Directory=(Join-Path $aRoot 'Restored/Objects');KeyReference=$aSettings.PrivateFiles.KeyReference;KeyFile=$aRestoredKeys[$aSettings.PrivateFiles.KeyReference];DecryptionKeyFiles=$aDecryptionKeys}
$aOptions|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $aRoot 'restored-options.json') -Encoding utf8
[pscustomobject]@{AtUtc=[datetime]::UtcNow.ToString('O');SourceDirectory=$aSource;Objects=$aEntries.ToArray();RestoredKeyCount=$aKeys.Count;SourceChanged=$false}|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $aRoot 'restore-manifest.json') -Encoding utf8
[pscustomobject]@{ExistingCiphertexts=$aObjects.Count;CiphertextBackupAndRestoreVerified=$true;RetainedKeysRestored=$aKeys.Count;PrivateAclsVerified=$true;OriginalObjectsAndKeysChanged=$false;PlaintextDecryptionTestPending=$true}|ConvertTo-Json
