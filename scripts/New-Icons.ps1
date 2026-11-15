param([Parameter(Mandatory)][string]$Destination)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Path $Destination -Force | Out-Null
$pngs = @{}
foreach ($size in 16, 32, 48, 128, 256) {
    $bitmap = [Drawing.Bitmap]::new($size, $size)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $light = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(246,230,80))
    $dark = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(23,25,15))
    $round = [Drawing.Drawing2D.GraphicsPath]::new()
    try {
        $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.ScaleTransform($size / 40.0, $size / 40.0)
        $round.AddArc(0,0,20,20,180,90)
        $round.AddArc(20,0,20,20,270,90)
        $round.AddArc(20,20,20,20,0,90)
        $round.AddArc(0,20,20,20,90,90)
        $round.CloseFigure()
        $graphics.FillPath($light, $round)
        $graphics.TranslateTransform(7,7)
        $graphics.ScaleTransform(0.9,0.9)
        $graphics.FillPolygon($dark, [Drawing.Point[]]@(
            [Drawing.Point]::new(5,0),[Drawing.Point]::new(28,0),[Drawing.Point]::new(23,6),
            [Drawing.Point]::new(13,6),[Drawing.Point]::new(5,16),[Drawing.Point]::new(0,16)
        ))
        $graphics.FillPolygon($dark, [Drawing.Point[]]@(
            [Drawing.Point]::new(10,19),[Drawing.Point]::new(18,9),[Drawing.Point]::new(25,9),
            [Drawing.Point]::new(16,21),[Drawing.Point]::new(16,27),[Drawing.Point]::new(10,27)
        ))
        $file = Join-Path $Destination "icon-$size.png"
        $bitmap.Save($file, [Drawing.Imaging.ImageFormat]::Png)
        $pngs[$size] = [IO.File]::ReadAllBytes($file)
    }
    finally { $round.Dispose(); $light.Dispose(); $dark.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
}
$sizes = @(16,32,48,128,256)
$stream = [IO.File]::Create((Join-Path $Destination 'App.ico'))
$writer = [IO.BinaryWriter]::new($stream)
try {
    $writer.Write([UInt16]0)
    $writer.Write([UInt16]1)
    $writer.Write([UInt16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    foreach ($size in $sizes) {
        $edge = [byte]($size % 256)
        $writer.Write($edge)
        $writer.Write($edge)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]32)
        $writer.Write([UInt32]$pngs[$size].Length)
        $writer.Write([UInt32]$offset)
        $offset += $pngs[$size].Length
    }
    foreach ($size in $sizes) { $writer.Write([byte[]]$pngs[$size]) }
}
finally { $writer.Dispose() }
