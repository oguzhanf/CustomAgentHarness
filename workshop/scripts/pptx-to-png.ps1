param(
  [Parameter(Mandatory=$true)][string]$Pptx,
  [Parameter(Mandatory=$true)][string]$OutDir,
  [int]$WidthPx = 1920,
  [int]$HeightPx = 1080
)
$ErrorActionPreference = 'Stop'
$pptx = (Resolve-Path $Pptx).Path
if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir | Out-Null }
$OutDir = (Resolve-Path $OutDir).Path

$ppt = New-Object -ComObject PowerPoint.Application
try {
  $pres = $ppt.Presentations.Open($pptx, $true, $true, $false)
  $count = $pres.Slides.Count
  for ($i = 1; $i -le $count; $i++) {
    $name = "slide-{0:D2}.png" -f $i
    $path = Join-Path $OutDir $name
    if (Test-Path $path) { Remove-Item $path -Force }
    $pres.Slides.Item($i).Export($path, 'PNG', $WidthPx, $HeightPx)
  }
  $pres.Close()
  Write-Host "Exported $count slides to $OutDir"
} finally {
  $ppt.Quit()
  [System.Runtime.InteropServices.Marshal]::ReleaseComObject($ppt) | Out-Null
}
