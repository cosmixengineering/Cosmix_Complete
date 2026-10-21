$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$source = Join-Path $root 'Cosmix_New_Code_Review_and_SRS_Gap_Assessment.docx'
$qa = Join-Path $root 'review-qa'
New-Item -ItemType Directory -Path $qa -Force | Out-Null
$word = $null
$document = $null
try {
    $word = New-Object -ComObject Word.Application
    $word.Visible = $false
    $word.DisplayAlerts = 0
    $document = $word.Documents.Open($source, $false, $false)
    $document.Fields.Update() | Out-Null
    $document.Repaginate()
    $document.Save()
    $pdf = Join-Path $qa 'review.pdf'
    $document.ExportAsFixedFormat($pdf, 17)
    Write-Output $pdf
    Write-Output ('Pages: ' + $document.ComputeStatistics(2))
}
finally {
    if ($null -ne $document) { $document.Close(0) }
    if ($null -ne $word) { $word.Quit() }
}
