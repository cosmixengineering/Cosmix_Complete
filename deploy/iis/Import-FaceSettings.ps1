#requires -Version 5.1
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)][string]$ConfigurationPath,
    [ValidatePattern('^[A-Za-z0-9_-]+$')][string]$AppPoolName = 'CosmixAppPool'
)
$ErrorActionPreference = 'Stop'
$configuration = Get-Content -LiteralPath $ConfigurationPath -Raw | ConvertFrom-Json
$values = @{}
foreach ($entry in $configuration.PSObject.Properties) {
    if ($entry.Name -notmatch '^SelfHostedFace:(?:[A-Za-z0-9]+:)*[A-Za-z0-9]+$' -or
        $null -eq $entry.Value -or $entry.Value -is [pscustomobject] -or $entry.Value -is [array]) {
        throw 'Expected a flat, face-only configuration file.'
    }
    $values[$entry.Name] = [string]$entry.Value
}
if ($values['SelfHostedFace:CompreFace:BaseUri'].TrimEnd('/') -ne 'https://192.168.1.50:8000' -or
    -not $values['SelfHostedFace:CompreFace:DetectionApiKey'] -or
    -not $values['SelfHostedFace:CompreFace:VerificationApiKey']) {
    throw 'Expected the approved HTTPS face server and both private API keys.'
}
$certificate = $values['SelfHostedFace:CompreFace:TrustedRootCertificatePath']
if ((Get-FileHash -LiteralPath $certificate -Algorithm SHA256).Hash -ne $values['SelfHostedFace:CompreFace:TrustedRootCertificateSha256']) {
    throw 'Face CA file checksum mismatch.'
}
foreach ($index in 0,1) {
    if ((Get-FileHash -LiteralPath $values["SelfHostedFace:SilentFace:Models:${index}:Path"] -Algorithm SHA256).Hash -ne
        $values["SelfHostedFace:SilentFace:Models:${index}:Sha256"]) { throw 'Liveness model checksum mismatch.' }
}
Import-Module WebAdministration -ErrorAction Stop
if (-not (Test-Path "IIS:\AppPools\$AppPoolName")) { throw 'Create the dedicated IIS application pool first.' }
$filter = "system.applicationHost/applicationPools/add[@name='$AppPoolName']/environmentVariables"
if ($PSCmdlet.ShouldProcess($AppPoolName, 'Import private face settings into protected IIS configuration')) {
    foreach ($entry in $values.GetEnumerator()) {
        $name = $entry.Key.Replace(':', '__')
        $existing = @(Get-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Filter $filter -Name '.' | Select-Object -ExpandProperty Collection | Where-Object { $_.name -eq $name })
        if ($existing.Count) {
            Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Filter "$filter/add[@name='$name']" -Name value -Value $entry.Value | Out-Null
        } else {
            Add-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Filter $filter -Name '.' -Value @{name=$name;value=$entry.Value} | Out-Null
        }
    }
    Write-Output 'Face settings imported without displaying values. Recycle the pool in the maintenance window, then verify face readiness.'
}
