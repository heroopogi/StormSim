$ErrorActionPreference = 'Stop'
$phaseRoot = Join-Path $PSScriptRoot '../../Captures/Aftermath/Phase1_20261012'
$grid = Import-Csv (Join-Path $phaseRoot 'ground-grid.csv')
$route = Import-Csv (Join-Path $phaseRoot 'route.csv')
$dressing = Import-Csv (Join-Path $phaseRoot 'existing-dressing.csv')
$zones = Import-Csv (Join-Path $phaseRoot 'work-zones.csv')
$cameras = Import-Csv (Join-Path $phaseRoot 'camera-manifest.csv')
$svg = [System.Text.StringBuilder]::new()
function Add-Svg([string]$value) { [void]$svg.AppendLine($value) }
function Px([double]$x) { return [math]::Round(80 + ($x + 60) * 10, 2) }
function Py([double]$z) { return [math]::Round(100 + (698 - $z) * 10, 2) }
Add-Svg '<svg xmlns="http://www.w3.org/2000/svg" width="1120" height="880" viewBox="0 0 1120 880">'
Add-Svg '<rect width="1120" height="880" fill="#f5f6f4"/><g font-family="Arial,sans-serif" fill="#22313b">'
Add-Svg '<text x="60" y="40" font-size="25" font-weight="bold">Sibucao Aftermath Phase 1 Work Area</text>'
Add-Svg '<text x="60" y="68" font-size="15">Unity world coordinates in metres. Measured terrain and route; proposed dressing zones.</text>'
foreach ($p in $grid) {
    $shade = if ([double]$p.ground_y -lt -1.4) {'#aac9d4'} elseif ([double]$p.ground_y -lt -0.6) {'#d2e1df'} else {'#e5e4cf'}
    Add-Svg "<rect x='$(Px ([double]$p.x - 1))' y='$(Py ([double]$p.z + 1))' width='20' height='20' fill='$shade'/>"
}
foreach ($zone in $zones | Where-Object { $_.id -notin 'P1','C1' }) {
    $color = if ($zone.id.StartsWith('D')) {'#b25b34'} elseif ($zone.id.StartsWith('A')) {'#897131'} elseif ($zone.id -eq 'R1') {'#296b9a'} else {'#497e94'}
    $x=Px ([double]$zone.x_min); $y=Py ([double]$zone.z_max)
    $w=([double]$zone.x_max-[double]$zone.x_min)*10; $h=([double]$zone.z_max-[double]$zone.z_min)*10
    Add-Svg "<rect x='$x' y='$y' width='$w' height='$h' fill='none' stroke='$color' stroke-width='2' stroke-dasharray='6 4'/><text x='$($x+5)' y='$($y+19)' font-size='15' font-weight='bold' fill='$color'>$($zone.id)</text>"
}
$points = ($route | ForEach-Object { "$(Px ([double]$_.x)),$(Py ([double]$_.z))" }) -join ' '
Add-Svg "<polyline points='$points' fill='none' stroke='#92c9a5' stroke-width='30' stroke-opacity='.6' stroke-linejoin='round'/><polyline points='$points' fill='none' stroke='#27683c' stroke-width='3'/>"
foreach ($p in $dressing) {
    if ([double]$p.x -lt -58 -or [double]$p.x -gt 18 -or [double]$p.z -lt 642 -or [double]$p.z -gt 696) { continue }
    $name = [System.Security.SecurityElement]::Escape($p.name)
    Add-Svg "<circle cx='$(Px ([double]$p.x))' cy='$(Py ([double]$p.z))' r='2' fill='#805f49' fill-opacity='.65'><title>$name</title></circle>"
}
foreach ($c in $cameras) {
    $x=Px ([double]$c.x); $y=Py ([double]$c.z)
    Add-Svg "<circle cx='$x' cy='$y' r='6' fill='#fff' stroke='#254665' stroke-width='2'/><text x='$($x+9)' y='$($y-9)' font-size='13' fill='#254665'>$($c.name)</text>"
}
for($x=-60;$x -le 20;$x+=10){Add-Svg "<text x='$(Px $x)' y='697' text-anchor='middle' font-size='12'>$x</text>"}
for($z=640;$z -le 690;$z+=10){Add-Svg "<text x='65' y='$(Py $z)' text-anchor='end' font-size='12'>$z</text>"}
Add-Svg '<text x="460" y="725" font-size="14">World X</text><text x="22" y="375" font-size="14" transform="rotate(-90 22 375)">World Z</text>'
Add-Svg '<text x="900" y="130" font-size="16" font-weight="bold">Map key</text>'
Add-Svg '<text x="900" y="160" font-size="14">Green: protected route</text><text x="900" y="181" font-size="12">Proposed 3 m clear band</text>'
Add-Svg '<text x="900" y="216" font-size="14">Dots: existing dressing</text><text x="900" y="250" font-size="14">D1 / D2: damage focus</text><text x="900" y="284" font-size="14">A1 / A2 / A3: debris</text><text x="900" y="318" font-size="14">R1: relief station</text><text x="900" y="352" font-size="14">L1: low ground candidate</text>'
Add-Svg '<text x="900" y="402" font-size="14" font-weight="bold">Terrain height Y</text><rect x="900" y="420" width="16" height="16" fill="#aac9d4"/><text x="926" y="434" font-size="13">Below -1.4 m</text><rect x="900" y="448" width="16" height="16" fill="#d2e1df"/><text x="926" y="462" font-size="13">-1.4 to -0.6 m</text><rect x="900" y="476" width="16" height="16" fill="#e5e4cf"/><text x="926" y="490" font-size="13">Above -0.6 m</text>'
Add-Svg '<text x="60" y="772" font-size="14">Use the route polyline, not the corridor bounding box, to protect movement space.</text><text x="60" y="797" font-size="14">Terrain samples include ground beneath buildings. Low elevation alone does not establish a puddle location.</text><text x="60" y="822" font-size="14">Verify exposed ground, drainage, collision, and sightlines before placing new assets.</text></g></svg>'
[System.IO.File]::WriteAllText((Join-Path $phaseRoot 'work-area-map.svg'), $svg.ToString())
Write-Output 'Saved work-area-map.svg'
