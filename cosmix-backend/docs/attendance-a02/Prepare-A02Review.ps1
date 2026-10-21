[CmdletBinding()]
param([switch]$Finalize,[string]$FrozenReleaseDirectory)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop' -or $PSVersionTable.PSVersion.Major -ne 5 -or -not [Environment]::Is64BitProcess) { throw 'Use 64-bit Windows PowerShell 5.1 for stable packet serialization.' }
. (Join-Path $PSScriptRoot 'Invoke-A02Setup.ps1')
if ($FrozenReleaseDirectory) { Set-A02ReleaseDirectory $FrozenReleaseDirectory }
$review=Get-FreshExecutorReview
$destination=Join-Path $PSScriptRoot $(if ($Finalize) { 'A02-Review.json' } else { 'A02-Review.draft.json' })
# Generated non-secret review artifact only. No private target path is created.
[IO.File]::WriteAllText($destination,($review | ConvertTo-Json -Depth 18),[Text.UTF8Encoding]::new($false))
[pscustomobject]@{Manifest=$destination;ManifestSha256=(Get-FileHash -LiteralPath $destination).Hash;PacketSha256=$review.PacketSha256;SqlConnections=0;PrivateWrites=0}
