$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot 'Cosmix_Module_Integration_and_Delivery_Plan_2026_10_12.docx'
$qa = Join-Path $PSScriptRoot 'integration-plan-qa'
New-Item -ItemType Directory -Path $qa -Force | Out-Null
$word = $null
$document = $null
$initialDocuments = 0
$originalAlerts = $null
$originalVisible = $null
try {
    $word = New-Object -ComObject Word.Application
    $initialDocuments = $word.Documents.Count
    $originalAlerts = $word.DisplayAlerts
    $originalVisible = $word.Visible
    if ($initialDocuments -eq 0) { $word.Visible = $false }
    $word.DisplayAlerts = 0
    $missing = [System.Type]::Missing
    $document = $word.Documents.Open($source, $false, $false, $false, $missing, $missing, $false, $missing, $missing, $missing, $missing, $false)
    $document.Fields.Update() | Out-Null
    $document.Repaginate()
    $document.Save()
    $pdf = Join-Path $qa 'integration-plan.pdf'
    $document.ExportAsFixedFormat($pdf, 17)
    Write-Output $pdf
    Write-Output ('Pages: ' + $document.ComputeStatistics(2))
}
finally {
    if ($null -ne $document) { $document.Close(0) }
    if ($null -ne $word) {
        $word.DisplayAlerts = $originalAlerts
        if ($initialDocuments -eq 0 -and $word.Documents.Count -eq 0) { $word.Quit() }
        else { $word.Visible = $originalVisible }
    }
}
