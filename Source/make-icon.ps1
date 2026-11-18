Add-Type -AssemblyName System.Drawing

$out = Join-Path $PSScriptRoot "app.ico"
$sizes = @(16, 20, 24, 32, 48, 64, 128, 256)
$frames = @()

foreach ($sz in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap($sz, $sz, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $navy = [System.Drawing.Color]::FromArgb(15, 27, 45)
    $gold = [System.Drawing.Color]::FromArgb(198, 160, 48)
    $white = [System.Drawing.Color]::White
    $blue = [System.Drawing.Color]::FromArgb(74, 123, 184)

    $r = [float]($sz * 0.16)
    $d = 2 * $r
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc(0, 0, $d, $d, 180, 90)
    $path.AddArc($sz - $d, 0, $d, $d, 270, 90)
    $path.AddArc($sz - $d, $sz - $d, $d, $d, 0, 90)
    $path.AddArc(0, $sz - $d, $d, $d, 90, 90)
    $path.CloseFigure()

    $navyBrush = New-Object System.Drawing.SolidBrush($navy)
    $goldBrush = New-Object System.Drawing.SolidBrush($gold)
    $whiteBrush = New-Object System.Drawing.SolidBrush($white)
    $blueBrush = New-Object System.Drawing.SolidBrush($blue)

    $g.FillPath($navyBrush, $path)
    $g.FillRectangle($goldBrush, [float]($sz * 0.22), [float]($sz * 0.25), [float]($sz * 0.19), [float]($sz * 0.50))
    $g.FillRectangle($whiteBrush, [float]($sz * 0.47), [float]($sz * 0.25), [float]($sz * 0.31), [float]($sz * 0.22))
    $g.FillRectangle($blueBrush, [float]($sz * 0.47), [float]($sz * 0.53), [float]($sz * 0.31), [float]($sz * 0.22))

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $frames += , @($sz, $ms.ToArray())

    $navyBrush.Dispose(); $goldBrush.Dispose(); $whiteBrush.Dispose(); $blueBrush.Dispose()
    $path.Dispose(); $g.Dispose(); $bmp.Dispose()
}

$fs = [System.IO.File]::Create($out)
$bw = New-Object System.IO.BinaryWriter($fs)
$bw.Write([UInt16]0)
$bw.Write([UInt16]1)
$bw.Write([UInt16]$frames.Count)

$offset = 6 + 16 * $frames.Count
foreach ($f in $frames) {
    $sz = [int]$f[0]
    $data = [byte[]]$f[1]
    $dim = 0
    if ($sz -lt 256) { $dim = $sz }
    $bw.Write([byte]$dim)
    $bw.Write([byte]$dim)
    $bw.Write([byte]0)
    $bw.Write([byte]0)
    $bw.Write([UInt16]1)
    $bw.Write([UInt16]32)
    $bw.Write([UInt32]$data.Length)
    $bw.Write([UInt32]$offset)
    $offset += $data.Length
}
foreach ($f in $frames) {
    $bw.Write([byte[]]$f[1])
}
$bw.Flush()
$bw.Close()
$fs.Close()

Write-Output ("icon written: " + $out + " (" + (Get-Item $out).Length + " bytes)")
