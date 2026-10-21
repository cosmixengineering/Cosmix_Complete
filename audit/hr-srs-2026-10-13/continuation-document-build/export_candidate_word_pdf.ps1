param(
    [Parameter(Mandatory=$true)][string]$DocumentPath,
    [Parameter(Mandatory=$true)][string]$PdfPath
)
$ErrorActionPreference = 'Stop'
$sourceDocument = (Resolve-Path -LiteralPath $DocumentPath).Path
$pdfOutput = [System.IO.Path]::GetFullPath($PdfPath)
$taskArtifactRoot = 'D:\CosmixPartAContinuation20261013\document-update-final-v1\'
if (-not $sourceDocument.StartsWith($taskArtifactRoot, [StringComparison]::OrdinalIgnoreCase) -or
    -not $pdfOutput.StartsWith($taskArtifactRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Candidate export must stay in the exact owned document artifact directory.'
}
[System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($pdfOutput)) | Out-Null
$initialWordProcessIds = @((Get-Process -Name WINWORD -ErrorAction SilentlyContinue).Id)
$wordApplication = $null
$openedDocument = $null
$ownedWordInstance = $false
try {
    $wordApplication = New-Object -ComObject Word.Application
    $newWordProcesses = @(Get-Process -Name WINWORD -ErrorAction SilentlyContinue | Where-Object { $initialWordProcessIds -notcontains $_.Id })
    if ($newWordProcesses.Count -eq 0 -or $wordApplication.Documents.Count -ne 0) {
        throw 'Cannot export through an existing user-owned Word instance.'
    }
    $ownedWordInstance = $true
    $wordApplication.Visible = $false
    $wordApplication.DisplayAlerts = 0
    $wordApplication.AutomationSecurity = 3
    $openedDocument = $wordApplication.Documents.Open($sourceDocument, $false, $true, $false)
    $openedDocument.Repaginate()
    $pageCount = $openedDocument.ComputeStatistics(2)
    $openedDocument.ExportAsFixedFormat($pdfOutput, 17)
    if (-not [System.IO.File]::Exists($pdfOutput)) { throw 'Candidate PDF export was not produced.' }
    [PSCustomObject]@{ Pdf = $pdfOutput; Pages = $pageCount; Bytes = (Get-Item -LiteralPath $pdfOutput).Length } | ConvertTo-Json -Compress
}
finally {
    if ($null -ne $openedDocument) {
        $noSave = 0
        $openedDocument.Close([ref]$noSave)
        [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($openedDocument)
    }
    if ($null -ne $wordApplication) {
        if ($ownedWordInstance) { $wordApplication.Quit() }
        [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($wordApplication)
    }
    [GC]::Collect()
    [GC]::WaitForPendingFinalizers()
}
