# Flecha diagonal gruesa (abajo-izquierda -> arriba-derecha) en PNG transparente 1280x720.
# Uso: powershell -File flecha.ps1 <salida.png>
param([string]$Out)
Add-Type -AssemblyName System.Drawing
$bmp = New-Object System.Drawing.Bitmap 1280, 720, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)

# Eje de la flecha
$x0 = 70.0; $y0 = 690.0; $x1 = 1215.0; $y1 = 70.0
$dx = $x1 - $x0; $dy = $y1 - $y0; $len = [Math]::Sqrt($dx*$dx + $dy*$dy)
$ux = $dx / $len; $uy = $dy / $len; $nx = -$uy; $ny = $ux
$shaft = 26.0; $headW = 80.0; $headL = 150.0   # medio ancho del cuerpo, medio ancho de la punta, largo de la punta
$bx = $x1 - $ux*$headL; $by = $y1 - $uy*$headL
function P($x, $y) { New-Object System.Drawing.PointF ([single]$x), ([single]$y) }
$pts = @(
  (P ($x0 + $nx*$shaft*0.35) ($y0 + $ny*$shaft*0.35)),   # cola fina
  (P ($bx + $nx*$shaft) ($by + $ny*$shaft)),
  (P ($bx + $nx*$headW) ($by + $ny*$headW)),
  (P $x1 $y1),
  (P ($bx - $nx*$headW) ($by - $ny*$headW)),
  (P ($bx - $nx*$shaft) ($by - $ny*$shaft)),
  (P ($x0 - $nx*$shaft*0.35) ($y0 - $ny*$shaft*0.35))
)
$path = New-Object System.Drawing.Drawing2D.GraphicsPath
$path.AddPolygon([System.Drawing.PointF[]]$pts)
# relleno degradado amarillo -> rojo a lo largo de la flecha
$brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush((P $x0 $y0), (P $x1 $y1), [System.Drawing.Color]::FromArgb(255,255,216,0), [System.Drawing.Color]::FromArgb(255,235,30,30))
$pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::Black), 14
$pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
$g.DrawPath($pen, $path)
$g.FillPath($brush, $path)
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
