$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$dist = Join-Path $root 'portable'
$assets = Join-Path $root 'assets'
$out = Join-Path $dist 'ShutdownSteam.exe'
$iconPath = Join-Path $dist 'ShutdownSteam.ico'
$emblemPath = Join-Path $assets 'Emblem.png'
$dashboardPath = Join-Path $assets 'Dashboard.png'
$manifestPath = Join-Path $root 'app.manifest'
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path -LiteralPath $csc)) { $csc = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe" }
New-Item -ItemType Directory -Force -Path $dist, $assets | Out-Null
Add-Type -AssemblyName System.Drawing
$sourceImage = [System.Drawing.Image]::FromFile((Join-Path $assets 'Emblem-Public.png'))
try {
    $bitmap = New-Object System.Drawing.Bitmap 320, 320
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.DrawImage($sourceImage, 0, 0, 320, 320)
        $bitmap.Save($emblemPath, [System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $graphics.Dispose(); $bitmap.Dispose() }
    $icoStream = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter $icoStream
    $frames = @(32, 48, 128, 256)
    $payloads = @()
    foreach ($dimension in $frames) {
        $bitmap = New-Object System.Drawing.Bitmap $dimension, $dimension
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $stream = New-Object System.IO.MemoryStream
        try {
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.DrawImage($sourceImage, 0, 0, $dimension, $dimension)
            $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
            $payloads += ,$stream.ToArray()
        } finally { $graphics.Dispose(); $bitmap.Dispose(); $stream.Dispose() }
    }
    $writer.Write([UInt16]0); $writer.Write([UInt16]1); $writer.Write([UInt16]$frames.Count)
    $offset = 6 + 16 * $frames.Count
    for ($i = 0; $i -lt $frames.Count; $i++) {
        $dimensionByte = if ($frames[$i] -eq 256) { 0 } else { $frames[$i] }
        $writer.Write([byte]$dimensionByte); $writer.Write([byte]$dimensionByte)
        $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([UInt16]1); $writer.Write([UInt16]32)
        $writer.Write([UInt32]$payloads[$i].Length); $writer.Write([UInt32]$offset)
        $offset += $payloads[$i].Length
    }
    foreach ($payload in $payloads) { $writer.Write([byte[]]$payload) }
    [System.IO.File]::WriteAllBytes($iconPath, $icoStream.ToArray())
    $writer.Dispose(); $icoStream.Dispose()
} finally { $sourceImage.Dispose() }
$sources = Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' | Select-Object -ExpandProperty FullName
& $csc /nologo /target:winexe /optimize+ /platform:anycpu "/win32manifest:$manifestPath" "/win32icon:$iconPath" "/resource:$emblemPath,Emblem.png" "/resource:$dashboardPath,Dashboard.png" "/out:$out" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll $sources
if ($LASTEXITCODE -ne 0) { throw "C# build failed with exit code $LASTEXITCODE" }
Get-Item -LiteralPath $out | Select-Object FullName, Length, LastWriteTime
