param(
  [Parameter(Mandatory=$true)][string]$Pptx,
  [Parameter(Mandatory=$true)][string]$Pdf
)
$ErrorActionPreference = 'Stop'
$pptx = (Resolve-Path $Pptx).Path
$pdfOut = if ([System.IO.Path]::IsPathRooted($Pdf)) { $Pdf } else { Join-Path (Get-Location) $Pdf }
if (Test-Path $pdfOut) { Remove-Item $pdfOut -Force }

$ppt = New-Object -ComObject PowerPoint.Application
try {
  # ppt is non-visible by default in modern Office; do NOT try to set Visible=$false (Office throws).
  $pres = $ppt.Presentations.Open($pptx, $true, $true, $false) # ReadOnly, Untitled, WithWindow=$false
  $pres.SaveAs($pdfOut, 32)  # ppSaveAsPDF = 32
  $pres.Close()
  Write-Host "Wrote $pdfOut"
} finally {
  $ppt.Quit()
  [System.Runtime.InteropServices.Marshal]::ReleaseComObject($ppt) | Out-Null
}
