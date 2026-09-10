$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

$projectRoot = Split-Path -Parent $PSScriptRoot
$output = Join-Path $projectRoot 'src\ClipPull\Assets\clippull.ico'
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $output) | Out-Null

$size = 256
$bitmap = New-Object System.Drawing.Bitmap $size, $size
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::Transparent)

$blue = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(15, 108, 189))
$white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
$pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), 18
$pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round

$graphics.FillEllipse($blue, 8, 8, 240, 240)

# Download arrow.
$arrow = [System.Drawing.Point[]]@(
    (New-Object System.Drawing.Point 108, 62),
    (New-Object System.Drawing.Point 148, 62),
    (New-Object System.Drawing.Point 148, 128),
    (New-Object System.Drawing.Point 180, 128),
    (New-Object System.Drawing.Point 128, 180),
    (New-Object System.Drawing.Point 76, 128),
    (New-Object System.Drawing.Point 108, 128)
)
$graphics.FillPolygon($white, $arrow)

# Download tray.
$graphics.DrawLine($pen, 76, 194, 76, 206)
$graphics.DrawLine($pen, 76, 206, 180, 206)
$graphics.DrawLine($pen, 180, 206, 180, 194)

$hIcon = $bitmap.GetHicon()
$icon = [System.Drawing.Icon]::FromHandle($hIcon)
$stream = [System.IO.File]::Create($output)
try {
    $icon.Save($stream)
}
finally {
    $stream.Dispose()
    $icon.Dispose()
    $pen.Dispose()
    $white.Dispose()
    $blue.Dispose()
    $graphics.Dispose()
    $bitmap.Dispose()
}

Write-Host "Generated $output"
