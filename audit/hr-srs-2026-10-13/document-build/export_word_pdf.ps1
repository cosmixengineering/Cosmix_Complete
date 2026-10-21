param(
    [Parameter(Mandatory=$true)][string]$DocumentPath,
    [Parameter(Mandatory=$true)][string]$PdfPath,
    [switch]$RecoverOnly
)
$ErrorActionPreference = 'Stop'
$sourceDocument = (Resolve-Path -LiteralPath $DocumentPath).Path
$pdfOutput = [System.IO.Path]::GetFullPath($PdfPath)
$exportWorkspace = 'C:\Users\Huma.Khan\Downloads\Cosmix_Erp_software\'
if (-not $sourceDocument.StartsWith($exportWorkspace, [StringComparison]::OrdinalIgnoreCase) -or
    -not $pdfOutput.StartsWith($exportWorkspace, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Document export must stay in the named workspace.'
}
$pdfDirectory = [System.IO.Path]::GetDirectoryName($pdfOutput)
[System.IO.Directory]::CreateDirectory($pdfDirectory) | Out-Null
if ($RecoverOnly) {
    $previousWord = [Runtime.InteropServices.Marshal]::GetActiveObject('Word.Application')
    if ($previousWord.Visible -or $previousWord.Documents.Count -ne 1) {
        throw 'Recovery cannot act on a user-owned Word session.'
    }
    $previousDocument = $previousWord.Documents.Item(1)
    if (-not $previousDocument.ReadOnly -or $previousDocument.FullName -ne $sourceDocument) {
        throw 'Recovery document is not the exact generated read-only artifact.'
    }
    $noSave = 0
    $previousDocument.Close([ref]$noSave)
    $previousWord.Quit()
    [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($previousDocument)
    [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($previousWord)
    Write-Output 'Closed the exact generated read-only document and its hidden automation session.'
    return
}
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
    if (-not [System.IO.File]::Exists($pdfOutput)) { throw 'PDF export was not produced.' }
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
