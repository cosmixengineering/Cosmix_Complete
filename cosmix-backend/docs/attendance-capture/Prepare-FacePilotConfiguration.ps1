[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SourceFile,
    [Parameter(Mandatory)][string]$DestinationDirectory
)
# Export only existing face/vault settings; no approvals, thresholds, keys or host selection are invented.
$ErrorActionPreference='Stop'
if(!$IsWindows){throw 'Windows owner-protected export required.'}
$sid=[Security.Principal.WindowsIdentity]::GetCurrent().User
$source=[IO.Path]::GetFullPath($SourceFile)
$destination=[IO.Path]::GetFullPath($DestinationDirectory).TrimEnd('\')
$allowedRoot=Join-Path $env:LOCALAPPDATA 'Cosmix\FaceVerification\'
if(!$destination.StartsWith($allowedRoot,[StringComparison]::OrdinalIgnoreCase) -or
    $DestinationDirectory -match '(^|[\\/])\.\.?([\\/]|$)' -or
    (Test-Path -LiteralPath $destination)){throw 'A new directory inside the existing FaceVerification root is required.'}
if(!(Test-Path -LiteralPath $allowedRoot -PathType Container)){throw 'Existing face storage root required.'}
foreach($path in @($source,$destination)){
    for($part=$path;$part;$part=[IO.Path]::GetDirectoryName($part)){
        if(Test-Path -LiteralPath $part){
            if((Get-Item -LiteralPath $part).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Linked paths forbidden.'}
        }
    }
}
$file=Get-Item -LiteralPath $source
if($file.PSIsContainer -or $file.Length -lt 1 -or $file.Length -gt 65536){throw 'Source file outside bounds.'}
$acl=Get-Acl -LiteralPath $source
if($acl.GetOwner([Security.Principal.SecurityIdentifier]).Value -cne $sid.Value){throw 'Source owner differs.'}
foreach($rule in $acl.GetAccessRules($true,$true,[Security.Principal.SecurityIdentifier])){
    if($rule.AccessControlType -eq 'Allow' -and $rule.IdentityReference.Value -cnotin @($sid.Value,'S-1-5-18','S-1-5-32-544')){
        throw 'Source file is not private.'
    }
}
$sourceHash=(Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
$bytes=[IO.File]::ReadAllBytes($source)
$sections=@('SelfHostedFace','FaceVerificationPolicy','FaceCapture','FaceStorageRetention',
    'FaceTemplateStorage','FaceStorageRetentionJournal','FaceRetirementWorker','FaceDisposal','PrivateFiles')
$settings=[ordered]@{}
$seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
try {
    $document=[Text.Json.JsonDocument]::Parse([ReadOnlyMemory[byte]]$bytes)
    try {
        if($document.RootElement.ValueKind -ne [Text.Json.JsonValueKind]::Object){throw 'Flat configuration object required.'}
        foreach($property in $document.RootElement.EnumerateObject()){
            if(!$seen.Add($property.Name)){throw 'Duplicate source configuration key.'}
            if(($property.Name -split ':',2)[0] -cnotin $sections){continue}
            if($property.Value.ValueKind -eq [Text.Json.JsonValueKind]::String){
                $settings[$property.Name]=$property.Value.GetString()
            }elseif($property.Value.ValueKind -in @([Text.Json.JsonValueKind]::True,[Text.Json.JsonValueKind]::False,[Text.Json.JsonValueKind]::Number)){
                $settings[$property.Name]=$property.Value.GetRawText()
            }else{throw 'Existing flat scalar settings required.'}
        }
    }finally{$document.Dispose()}
}finally{[Security.Cryptography.CryptographicOperations]::ZeroMemory($bytes)}
foreach($required in @('SelfHostedFace:Enabled','SelfHostedFace:CompreFace:BaseUri',
    'SelfHostedFace:CompreFace:DetectionApiKey','SelfHostedFace:CompreFace:VerificationApiKey',
    'FaceVerificationPolicy:AuthorityConfirmed','PrivateFiles:Directory','PrivateFiles:KeyFile')){
    if(!$settings.Contains($required) -or [string]::IsNullOrWhiteSpace($settings[$required])){throw 'Required existing setting missing.'}
}
if((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -cne $sourceHash){throw 'Source configuration changed.'}
$encoded=[Text.UTF8Encoding]::new($false).GetBytes(($settings|ConvertTo-Json -Depth 4))
if($encoded.Length -gt 65536){
    [Security.Cryptography.CryptographicOperations]::ZeroMemory($encoded)
    throw 'Export exceeds the private configuration reader limit.'
}
$newAcl=[Security.AccessControl.DirectorySecurity]::new()
$newAcl.SetOwner($sid);$newAcl.SetAccessRuleProtection($true,$false)
$newAcl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($sid,'FullControl','ContainerInherit,ObjectInherit','None','Allow'))
$directory=[IO.DirectoryInfo]::new($destination)
# Create with private permissions, rather than writing secrets before fixing inherited ACLs.
[IO.FileSystemAclExtensions]::Create($directory,$newAcl)
$output=Join-Path $destination 'face-pilot.json'
try{
    $stream=[IO.File]::Open($output,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try{$stream.Write($encoded,0,$encoded.Length);$stream.Flush($true)}finally{$stream.Dispose()}
}finally{[Security.Cryptography.CryptographicOperations]::ZeroMemory($encoded)}
foreach($path in @($destination,$output)){
    $saved=Get-Acl -LiteralPath $path
    if($saved.GetOwner([Security.Principal.SecurityIdentifier]).Value -cne $sid.Value){throw 'Export owner differs.'}
    foreach($rule in $saved.GetAccessRules($true,$true,[Security.Principal.SecurityIdentifier])){
        if($rule.AccessControlType -eq 'Allow' -and $rule.IdentityReference.Value -cne $sid.Value){throw 'Export access differs.'}
    }
}
if((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -cne $sourceHash){throw 'Source configuration changed.'}
[pscustomobject]@{Output=$output;SettingsCopied=$settings.Count;SourceUnchanged=$true;HostConfigurationChanged=$false;KeysGenerated=0}
