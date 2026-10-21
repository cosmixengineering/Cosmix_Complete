[CmdletBinding()]
param()

$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
if(!$IsWindows){throw 'Windows temporary ACL fixtures required.'}
$scriptPath=Join-Path $PSScriptRoot 'Prepare-FacePilotConfiguration.ps1'
$scriptHash=(Get-FileHash -LiteralPath $scriptPath -Algorithm SHA256).Hash
$tempParent=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
$fixtureRoot=Join-Path $tempParent ('Cosmix-FacePilot-Verify-'+[guid]::NewGuid().ToString('N'))
$originalLocal=[Environment]::GetEnvironmentVariable('LOCALAPPDATA','Process')
$originalCulture=[Globalization.CultureInfo]::CurrentCulture
$originalUiCulture=[Globalization.CultureInfo]::CurrentUICulture
$sid=[Security.Principal.WindowsIdentity]::GetCurrent().User
$checks=0
$failures=[Collections.Generic.List[string]]::new()
$junction=$null

function Check([bool]$ok,[string]$name){
    $script:checks++
    if(!$ok){$script:failures.Add($name);Write-Output "FAIL $name"}
}
function Private-Directory([string]$path,[switch]$AdditionalReader){
    $acl=[Security.AccessControl.DirectorySecurity]::new()
    $acl.SetOwner($sid);$acl.SetAccessRuleProtection($true,$false)
    $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new(
        $sid,'FullControl','ContainerInherit,ObjectInherit','None','Allow'))
    if($AdditionalReader){
        $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new(
            [Security.Principal.SecurityIdentifier]::new('S-1-5-32-545'),'ReadAndExecute','Allow'))
    }
    [IO.FileSystemAclExtensions]::Create([IO.DirectoryInfo]::new($path),$acl)
}
function Fixture-Source([string]$name,[string]$json){
    $path=Join-Path $sourceDirectory ($name+'.json')
    [IO.File]::WriteAllText($path,$json,[Text.UTF8Encoding]::new($false))
    $acl=[Security.AccessControl.FileSecurity]::new()
    $acl.SetOwner($sid);$acl.SetAccessRuleProtection($true,$false)
    $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($sid,'FullControl','Allow'))
    [IO.FileSystemAclExtensions]::SetAccessControl([IO.FileInfo]::new($path),$acl)
    return $path
}
function Private-Policy([string]$path){
    $acl=Get-Acl -LiteralPath $path
    $descriptor=[Security.AccessControl.RawSecurityDescriptor]::new($acl.GetSecurityDescriptorBinaryForm(),0)
    return $null -ne $descriptor.DiscretionaryAcl -and
        $acl.GetOwner([Security.Principal.SecurityIdentifier]).Value -ceq $sid.Value -and
        @($acl.GetAccessRules($true,$true,[Security.Principal.SecurityIdentifier])|Where-Object {
            $_.AccessControlType -eq 'Allow' -and $_.IdentityReference.Value -cne $sid.Value
        }).Count -eq 0
}
function Export-Fixture([string]$source,[string]$destination){
    # Both arguments and LOCALAPPDATA are confined to this invocation's fresh fixture.
    foreach($path in @($source,$destination)){
        if(![IO.Path]::GetFullPath($path).StartsWith($fixtureRoot+'\',[StringComparison]::OrdinalIgnoreCase)){
            throw 'Fixture escaped its temporary root.'
        }
    }
    if($env:LOCALAPPDATA -cne $fakeLocal){throw 'Temporary LOCALAPPDATA was not selected.'}
    if((Get-FileHash -LiteralPath $scriptPath -Algorithm SHA256).Hash -cne $scriptHash){
        throw 'Preparation source changed during verification.'
    }
    return & $scriptPath -SourceFile $source -DestinationDirectory $destination
}
function Reject-Fixture([string]$name,[string]$json){
    $source=Fixture-Source $name $json
    $destination=Join-Path $faceRoot $name
    $rejected=$false
    try{$null=Export-Fixture $source $destination}catch{$rejected=$true}
    Check $rejected ($name+': rejected')
    Check (!(Test-Path -LiteralPath $destination)) ($name+': no destination created')
}

try {
    Private-Directory $fixtureRoot
    $fakeLocal=Join-Path $fixtureRoot 'LocalAppData'
    Private-Directory $fakeLocal
    Private-Directory (Join-Path $fakeLocal 'Cosmix')
    $faceRoot=Join-Path $fakeLocal 'Cosmix\FaceVerification'
    Private-Directory $faceRoot
    $sourceDirectory=Join-Path $fixtureRoot 'SyntheticUserSecrets'
    Private-Directory $sourceDirectory -AdditionalReader
    [Environment]::SetEnvironmentVariable('LOCALAPPDATA',$fakeLocal,'Process')

    $values=[ordered]@{
        'SelfHostedFace:Enabled'=$true
        'SelfHostedFace:CompreFace:BaseUri'='https://localhost:1'
        'SelfHostedFace:CompreFace:DetectionApiKey'='synthetic-detection-not-a-secret'
        'SelfHostedFace:CompreFace:VerificationApiKey'='synthetic-verification-not-a-secret'
        'SelfHostedFace:MinimumSimilarity'=0.83
        'SelfHostedFace:SilentFace:Models:0:MinimumLiveProbability'=0.91
        'SelfHostedFace:SilentFace:Models:0:CropScale'=2.7
        'SelfHostedFace:SilentFace:Models:0:SourceRevision'='synthetic-reviewed-revision'
        'SelfHostedFace:SilentFace:Models:0:Path'=(Join-Path $fixtureRoot 'model-not-loaded.onnx')
        'FaceVerificationPolicy:AuthorityConfirmed'=$true
        'FaceVerificationPolicy:ApprovalReference'='synthetic-existing-approval'
        'FaceCapture:Enabled'=$false
        'FaceCapture:LifetimeSeconds'=120
        'FaceStorageRetention:CaptureUseLifetimeSeconds'=300
        'FaceTemplateStorage:Storage:KeyReference'='synthetic-existing-key'
        'FaceStorageRetentionJournal:Enabled'=$false
        'FaceRetirementWorker:Enabled'=$false
        'FaceDisposal:Enabled'=$false
        'PrivateFiles:Directory'=(Join-Path $fixtureRoot 'vault-not-opened')
        'PrivateFiles:KeyFile'=(Join-Path $fixtureRoot 'key-not-opened')
        'Jwt:SigningKey'='synthetic-unrelated-jwt'
        'ConnectionStrings:CosmixDatabase'='synthetic-do-not-connect'
        'Urls'='https://localhost:7240'
        'LocalTesting:Enabled'=$true
        'SelfHostedFaceExtra:Enabled'=$true
    }
    $baseJson=ConvertTo-Json -InputObject $values -Depth 8 -Compress
    $source=Fixture-Source 'native-scalars' $baseJson
    # Only this synthetic parent has an additional reader; the source file is owner-only.
    $sourceHash=(Get-FileHash -LiteralPath $source).Hash
    $sourceSddl=(Get-Acl -LiteralPath $source).Sddl
    $parentSddl=(Get-Acl -LiteralPath $sourceDirectory).Sddl
    $rootSddl=(Get-Acl -LiteralPath $faceRoot).Sddl
    $destination=Join-Path $faceRoot 'native-scalars'
    [Globalization.CultureInfo]::CurrentCulture=[Globalization.CultureInfo]::GetCultureInfo('fr-FR')
    [Globalization.CultureInfo]::CurrentUICulture=[Globalization.CultureInfo]::GetCultureInfo('fr-FR')
    $result=Export-Fixture $source $destination
    [Globalization.CultureInfo]::CurrentCulture=$originalCulture
    [Globalization.CultureInfo]::CurrentUICulture=$originalUiCulture
    $output=Join-Path $destination 'face-pilot.json'
    $saved=Get-Content -LiteralPath $output -Raw|ConvertFrom-Json -AsHashtable
    Check (@($result).Count -eq 1 -and $result.SourceUnchanged -and !$result.HostConfigurationChanged -and
        $result.KeysGenerated -eq 0 -and $result.Output -ceq $output) 'bounded result reports no runtime/key changes'
    Check ($saved.Count -eq 20 -and $result.SettingsCopied -eq 20) 'only face/vault allowlisted settings copied'
    Check (@($saved.Values|Where-Object {$_ -isnot [string]}).Count -eq 0) 'native scalars become configuration strings'
    Check ($saved['SelfHostedFace:Enabled'] -ceq 'true' -and $saved['FaceCapture:Enabled'] -ceq 'false' -and
        $saved['FaceCapture:LifetimeSeconds'] -ceq '120' -and
        $saved['SelfHostedFace:MinimumSimilarity'] -ceq '0.83' -and
        $saved['SelfHostedFace:SilentFace:Models:0:CropScale'] -ceq '2.7') 'scalar conversion is invariant under fr-FR'
    Check ($saved['FaceVerificationPolicy:ApprovalReference'] -ceq $values['FaceVerificationPolicy:ApprovalReference'] -and
        $saved['PrivateFiles:KeyFile'] -ceq $values['PrivateFiles:KeyFile'] -and
        $saved['SelfHostedFace:CompreFace:DetectionApiKey'] -ceq $values['SelfHostedFace:CompreFace:DetectionApiKey']) 'existing approval/key/credential values preserved'
    Check (!(Test-Path -LiteralPath $values['PrivateFiles:KeyFile']) -and
        !(Test-Path -LiteralPath $values['PrivateFiles:Directory']) -and
        !(Test-Path -LiteralPath $values['SelfHostedFace:SilentFace:Models:0:Path'])) 'referenced keys/vault/model never created'
    Check ((Get-FileHash -LiteralPath $source).Hash -ceq $sourceHash -and
        (Get-Acl -LiteralPath $source).Sddl -ceq $sourceSddl -and
        (Get-Acl -LiteralPath $sourceDirectory).Sddl -ceq $parentSddl -and
        (Get-Acl -LiteralPath $faceRoot).Sddl -ceq $rootSddl) 'source and pre-existing fixture ACLs remain unchanged'
    Check ((Private-Policy $destination) -and (Private-Policy $output)) 'new directory and file have owner-only explicit DACLs'
    Check (@(Get-ChildItem -LiteralPath $destination -Force).Count -eq 1) 'only the selected JSON file is created'
    $outputHash=(Get-FileHash -LiteralPath $output).Hash
    $rejected=$false
    try{$null=Export-Fixture $source $destination}catch{$rejected=$true}
    Check ($rejected -and (Get-FileHash -LiteralPath $output).Hash -ceq $outputHash) 'existing destination is never overwritten'

    Reject-Fixture 'duplicate-case' ($baseJson.TrimEnd('}')+',"selfhostedface:enabled":false}')
    Reject-Fixture 'array-setting' ($baseJson.Replace('"FaceCapture:Enabled":false','"FaceCapture:Enabled":[]'))
    Reject-Fixture 'object-setting' ($baseJson.Replace('"FaceCapture:Enabled":false','"FaceCapture:Enabled":{}'))
    Reject-Fixture 'null-setting' ($baseJson.Replace('"FaceCapture:Enabled":false','"FaceCapture:Enabled":null'))
    Reject-Fixture 'missing-approval' ($baseJson.Replace('"FaceVerificationPolicy:AuthorityConfirmed":true,',''))
    Reject-Fixture 'malformed-json' '{'
    Reject-Fixture 'oversized-source' (' '*65537)

    $outside=Join-Path $fakeLocal 'outside-face-root'
    $rejected=$false
    try{$null=Export-Fixture $source $outside}catch{$rejected=$true}
    Check ($rejected -and !(Test-Path -LiteralPath $outside)) 'destination outside face root rejected'
    $traversal=Join-Path $faceRoot 'child\..\traversal'
    $rejected=$false
    try{$null=Export-Fixture $source $traversal}catch{$rejected=$true}
    Check ($rejected -and !(Test-Path -LiteralPath (Join-Path $faceRoot 'traversal'))) 'destination traversal rejected'

    $publicSource=Fixture-Source 'nonprivate-source' $baseJson
    $publicAcl=[Security.AccessControl.FileSecurity]::new()
    $publicAcl.SetOwner($sid);$publicAcl.SetAccessRuleProtection($true,$false)
    $publicAcl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($sid,'FullControl','Allow'))
    $publicAcl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new(
        [Security.Principal.SecurityIdentifier]::new('S-1-5-32-545'),'Read','Allow'))
    [IO.FileSystemAclExtensions]::SetAccessControl([IO.FileInfo]::new($publicSource),$publicAcl)
    $destination=Join-Path $faceRoot 'nonprivate-source'
    $rejected=$false
    try{$null=Export-Fixture $publicSource $destination}catch{$rejected=$true}
    Check ($rejected -and !(Test-Path -LiteralPath $destination)) 'additional source file allow identity rejected'

    $junction=Join-Path $faceRoot 'synthetic-link'
    $linkTarget=Join-Path $fixtureRoot 'link-target'
    Private-Directory $linkTarget
    $null=New-Item -ItemType Junction -Path $junction -Target $linkTarget
    $destination=Join-Path $junction 'export'
    $rejected=$false
    try{$null=Export-Fixture $source $destination}catch{$rejected=$true}
    Check ($rejected -and !(Test-Path -LiteralPath (Join-Path $linkTarget 'export'))) 'linked destination ancestor rejected'

    $nullSource=Fixture-Source 'null-dacl-source' $baseJson
    $nullAcl=[Security.AccessControl.FileSecurity]::new()
    $nullAcl.SetSecurityDescriptorSddlForm('O:'+$sid.Value+'D:NO_ACCESS_CONTROL',
        [Security.AccessControl.AccessControlSections]::Access -bor [Security.AccessControl.AccessControlSections]::Owner)
    [IO.FileSystemAclExtensions]::SetAccessControl([IO.FileInfo]::new($nullSource),$nullAcl)
    $descriptor=[Security.AccessControl.RawSecurityDescriptor]::new(
        (Get-Acl -LiteralPath $nullSource).GetSecurityDescriptorBinaryForm(),0)
    if($null -ne $descriptor.DiscretionaryAcl){throw 'Null-DACL fixture was not installed.'}
    $destination=Join-Path $faceRoot 'null-dacl-source'
    $rejected=$false
    try{$null=Export-Fixture $nullSource $destination}catch{$rejected=$true}
    Check ($rejected -and !(Test-Path -LiteralPath $destination)) 'null source DACL rejected before export'

    # Compact input fits, but pretty-printed output grows beyond EvaluateFile's limit.
    $large=[ordered]@{}
    foreach($key in $values.Keys){$large[$key]=$values[$key]}
    for($i=0;$i -lt 1000;$i++){$large['FaceCapture:Synthetic'+$i]='x'*32}
    $largeJson=ConvertTo-Json -InputObject $large -Depth 8 -Compress
    Check ([Text.Encoding]::UTF8.GetByteCount($largeJson) -le 65536) 'output-bound fixture source fits 64 KiB'
    Reject-Fixture 'oversized-output' $largeJson
    Check ((Get-FileHash -LiteralPath $scriptPath -Algorithm SHA256).Hash -ceq $scriptHash) 'preparation source remained frozen during tests'
} finally {
    [Environment]::SetEnvironmentVariable('LOCALAPPDATA',$originalLocal,'Process')
    [Globalization.CultureInfo]::CurrentCulture=$originalCulture
    [Globalization.CultureInfo]::CurrentUICulture=$originalUiCulture
    # Remove only this verifier's fresh temporary tree, unlinking its junction first.
    $resolved=[IO.Path]::GetFullPath($fixtureRoot)
    if(!$resolved.StartsWith($tempParent+'\',[StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($resolved) -cnotmatch '^Cosmix-FacePilot-Verify-[0-9a-f]{32}$'){
        throw 'Refusing cleanup outside the owned temporary fixture.'
    }
    if($junction -and (Test-Path -LiteralPath $junction)){
        $link=[IO.Path]::GetFullPath($junction)
        if(!$link.StartsWith($resolved+'\',[StringComparison]::OrdinalIgnoreCase) -or
            !((Get-Item -LiteralPath $link).Attributes -band [IO.FileAttributes]::ReparsePoint)){
            throw 'Temporary junction identity changed.'
        }
        Remove-Item -LiteralPath $link -Force
    }
    if(Test-Path -LiteralPath $resolved){Remove-Item -LiteralPath $resolved -Recurse -Force}
}
Check ([Environment]::GetEnvironmentVariable('LOCALAPPDATA','Process') -ceq $originalLocal) 'process LOCALAPPDATA restored'
Write-Output "Face pilot preparation: $($checks-$failures.Count)/$checks passed; temporary synthetic inputs only."
if($failures.Count){throw ($failures -join '; ')}
