$ErrorActionPreference = 'Stop'
$outDir = $PSScriptRoot
$cells = [System.Collections.Generic.List[string]]::new()
$svg = [System.Collections.Generic.List[string]]::new()
function Escape([string]$s) { [System.Security.SecurityElement]::Escape($s) }
function Box($id, $x, $y, $w, $h, $fill, $stroke, $lines, $size = 18, $bold = $false) {
    $label = Escape ($lines -join '<br>')
    $weight = if ($bold) { '1' } else { '0' }
    $cells.Add("<mxCell id='$id' value='$label' style='rounded=1;whiteSpace=wrap;html=1;arcSize=12;fillColor=$fill;strokeColor=$stroke;strokeWidth=1.5;fontColor=#18324B;fontFamily=Arial;fontSize=$size;fontStyle=$weight;' vertex='1' parent='1'><mxGeometry x='$x' y='$y' width='$w' height='$h' as='geometry'/></mxCell>")
    $svg.Add("<rect x='$x' y='$y' width='$w' height='$h' rx='14' fill='$fill' stroke='$stroke' stroke-width='1.5'/>")
    $fw = if ($bold) { '700' } else { '400' }
    $cy = $y + $h / 2 - (($lines.Count - 1) * ($size + 9) / 2) + $size * 0.35
    foreach ($line in $lines) {
        $svg.Add("<text x='$($x + $w / 2)' y='$cy' text-anchor='middle' font-family='Arial, sans-serif' font-size='$size' font-weight='$fw' fill='#18324B'>$(Escape $line)</text>")
        $cy += $size + 9
    }
}
function Text($id, $x, $y, $w, $h, $value, $size = 18, $color = '#18324B') {
    $cells.Add("<mxCell id='$id' value='$(Escape $value)' style='text;html=1;align=center;verticalAlign=middle;whiteSpace=wrap;fontFamily=Arial;fontSize=$size;fontColor=$color;fontStyle=1;' vertex='1' parent='1'><mxGeometry x='$x' y='$y' width='$w' height='$h' as='geometry'/></mxCell>")
    $svg.Add("<text x='$($x+$w/2)' y='$($y+$h/2+$size*0.35)' text-anchor='middle' font-family='Arial, sans-serif' font-size='$size' font-weight='700' fill='$color'>$(Escape $value)</text>")
}
Box 'canvas' 10 10 1260 880 '#F7FAFD' '#E2E8F0' @('')
Text 'title' 40 30 1200 44 'AIVES | SYSTEM ARCHITECTURE' 29
Text 'subtitle' 40 78 1200 28 'ASP.NET Core · Three-layer architecture' 16 '#64748B'
Box 'users' 35 170 180 240 '#EAF2FF' '#9FBDE8' @('USERS'; 'Admin'; 'Lecturer · Student'; 'Web Browser'; 'SignalR Client') 17
Box 'presentation' 370 140 640 230 '#EAF2FF' '#9FBDE8' @('')
Text 'pTitle' 385 154 610 30 '1. PRESENTATION LAYER' 21
Text 'pProject' 385 187 610 24 'AssignmentPRN.Presentation' 14 '#64748B'
Box 'razor' 390 227 185 59 '#FFFFFF' '#C6D8EF' @('Razor Views / Pages') 15
Box 'mvc' 590 227 195 59 '#FFFFFF' '#C6D8EF' @('MVC Controllers') 15
Box 'session' 800 227 190 59 '#FFFFFF' '#C6D8EF' @('Session / Roles') 15
Box 'ui' 390 302 290 48 '#FFFFFF' '#C6D8EF' @('Bootstrap · jQuery') 15
Box 'hub' 695 302 295 48 '#FFFFFF' '#C6D8EF' @('SignalR Hub') 15
Box 'business' 370 395 640 190 '#E9F7F3' '#8FC9B6' @('')
Text 'bTitle' 385 408 610 30 '2. BUSINESS LOGIC LAYER' 21
Text 'bProject' 385 440 610 24 'AssignmentPRN.Business' 14 '#52766B'
Box 'auth' 390 482 185 77 '#FFFFFF' '#B7DCD1' @('Auth Service') 16
Box 'exam' 590 482 195 77 '#FFFFFF' '#B7DCD1' @('Exam Session'; 'Service') 16
Box 'classes' 800 482 190 77 '#FFFFFF' '#B7DCD1' @('Class Management'; 'Service') 16
Box 'dal' 370 610 640 170 '#F0ECFB' '#BBAADB' @('')
Text 'dTitle' 385 623 610 30 '3. DATA ACCESS LAYER' 21
Text 'dProject' 385 655 610 24 'AssignmentPRN.DataAccess' 14 '#6E5E8E'
Box 'repo' 390 697 185 62 '#FFFFFF' '#D1C6E8' @('Repositories') 16
Box 'context' 590 697 195 62 '#FFFFFF' '#D1C6E8' @('AivesDbContext') 16
Box 'ef' 800 697 190 62 '#FFFFFF' '#D1C6E8' @('EF Core · Pomelo') 16
Box 'db' 515 805 350 60 '#E6F2FA' '#A4C4DC' @('DATABASE · MySQL 8.4') 19 $true
Box 'thirdparty' 1040 140 200 230 '#FFF4E5' '#E6C28D' @('')
Text 'tTitle' 1048 157 184 32 'THIRD-PARTY' 19
Box 'cdn' 1055 213 170 80 '#FFFFFF' '#E6C28D' @('jsDelivr CDN'; 'SignalR JavaScript') 15
Text 'tNote' 1050 310 180 28 'Browser library' 13 '#8B6C42'
Text 'frontNote' 35 430 180 30 'FRONT-END' 16 '#64748B'
Text 'serverNote' 1040 450 200 30 'BACK-END' 16 '#64748B'
Text 'serverNote2' 1040 481 200 25 'Server-side layers' 13 '#64748B'
foreach ($edge in @(@('request',215,255,370,255,'Request',0), @('response',370,310,215,310,'Response',1))) {
    $id,$x1,$y1,$x2,$y2,$label,$dashed = $edge
    $cells.Add("<mxCell id='$id' value='$label' style='edgeStyle=none;rounded=0;html=1;endArrow=block;endFill=1;strokeColor=#456480;strokeWidth=2;dashed=$dashed;fontFamily=Arial;fontSize=16;fontColor=#18324B;labelBackgroundColor=#F7FAFD;' edge='1' parent='1'><mxGeometry relative='1' as='geometry'><mxPoint x='$x1' y='$y1' as='sourcePoint'/><mxPoint x='$x2' y='$y2' as='targetPoint'/></mxGeometry></mxCell>")
    $dash = if ($dashed) { "stroke-dasharray='7 5'" } else { '' }
    $svg.Add("<line x1='$x1' y1='$y1' x2='$x2' y2='$y2' stroke='#456480' stroke-width='2' $dash marker-end='url(#arrow)'/>")
    $svg.Add("<rect x='245' y='$($y1-12)' width='96' height='24' fill='#F7FAFD'/><text x='293' y='$($y1+5)' text-anchor='middle' font-family='Arial, sans-serif' font-size='16' fill='#18324B'>$label</text>")
}
Text 'footer' 30 800 320 48 'Solid: Request · Dashed: Response' 12 '#64748B'
$xml = "<?xml version='1.0' encoding='UTF-8'?><mxfile host='app.diagrams.net'><diagram id='aives-overview' name='System Architecture'><mxGraphModel dx='1280' dy='900' grid='1' gridSize='10' page='1' pageScale='1' pageWidth='1280' pageHeight='900' background='#FFFFFF'><root><mxCell id='0'/><mxCell id='1' parent='0'/>$($cells -join '')</root></mxGraphModel></diagram></mxfile>"
$xml | Set-Content -LiteralPath (Join-Path $outDir 'AIVES-Architecture.drawio') -Encoding utf8
"<svg xmlns='http://www.w3.org/2000/svg' width='1280' height='900' viewBox='0 0 1280 900'><defs><marker id='arrow' viewBox='0 0 10 10' refX='9' refY='5' markerWidth='7' markerHeight='7' orient='auto-start-reverse'><path d='M 0 0 L 10 5 L 0 10 z' fill='#456480'/></marker></defs>$($svg -join '')</svg>" | Set-Content -LiteralPath (Join-Path $outDir 'AIVES-Architecture.svg') -Encoding utf8
[xml]$verified = Get-Content -Raw (Join-Path $outDir 'AIVES-Architecture.drawio')
$edges = @($verified.SelectNodes('//mxCell[@edge="1"]'))
if ($edges.Count -ne 2) { throw 'Expected exactly two arrows.' }
Write-Output "Created draw.io and SVG; XML valid; exactly $($edges.Count) arrows."

