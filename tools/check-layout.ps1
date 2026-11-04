# Checks the layout files snapshot mode writes next to every PNG.
# Usage: tools/check-layout.ps1 [files]     (default: %TEMP%\KiteWin-shots\*-layout.json)
# Every probe must:
#   1. start on a whole physical pixel, so its edges stay sharp
#   2. start a multiple of 4 epx from its area's edge
#   3. stay inside its area's inset on both sides
#   4. share one leading edge with the other probes on its edge
#   5. sit a multiple of 4 epx below the probe above it on its edge
#   6. never overlap another probe in its area
# At 125% or 150% display scale, edges snap to physical pixels (0.8 or 0.67 epx), so every
# comparison allows one physical pixel of rounding. A real misalignment is larger than that.
param([string[]]$Files = @(Join-Path $env:TEMP "KiteWin-shots-$(Split-Path (Split-Path $PSScriptRoot -Parent) -Leaf)\*-layout.json"))
$script:tolerance = 0.5
function OnGrid([double]$v) { $r = [math]::Abs($v) % 4; return ($r -lt $script:tolerance -or (4 - $r) -lt $script:tolerance) }
$failed = 0
$paths = $Files | ForEach-Object { Get-ChildItem $_ -ErrorAction SilentlyContinue }
if (-not $paths) { Write-Output "no layout files: $Files (run tools/snap.ps1)"; exit 1 }
foreach ($path in $paths) {
    $layout = Get-Content $path.FullName -Raw | ConvertFrom-Json
    $scale = if ($layout.Scale) { [double]$layout.Scale } else { 1.0 }
    $script:tolerance = [math]::Max(0.5, 1 / $scale) + 0.01
    $tolerance = $script:tolerance
    $errors = @()
    $areas = @{}
    foreach ($a in $layout.Areas) { $areas[$a.Name] = $a }
    foreach ($p in $layout.Probes) {
        $px = $p.X * $scale; $py = $p.Y * $scale
        if ([math]::Abs($px - [math]::Round($px)) -gt 0.02 -or [math]::Abs($py - [math]::Round($py)) -gt 0.02) {
            $errors += "$($p.Name) sits between physical pixels: x $([math]::Round($p.X, 2)), y $([math]::Round($p.Y, 2)) at scale $scale"
        }
        $a = $areas[$p.Area]
        if (-not $a) { $errors += "$($p.Name) names area '$($p.Area)', which is not on screen"; continue }
        $dx = $p.X - $a.X
        if (-not (OnGrid $dx)) { $errors += "$($p.Name) starts $([math]::Round($dx, 2)) epx into $($a.Name)" }
        if ($p.X -lt $a.X + $a.Inset - $tolerance -or $p.X + $p.Width -gt $a.X + $a.Width - $a.Inset + $tolerance) {
            $errors += "$($p.Name) is not inside the $($a.Inset) epx inset of $($a.Name)"
        }
    }
    foreach ($group in ($layout.Probes | Where-Object { $_.Edge } | Group-Object Area, Edge)) {
        $xs = $group.Group | ForEach-Object { [math]::Round($_.X, 1) } | Sort-Object -Unique
        if (@($xs).Count -gt 1) {
            $list = ($group.Group | ForEach-Object { "$($_.Name) $([math]::Round($_.X, 1))" }) -join ', '
            $errors += "edge $($group.Name -replace ', ', '/') has $(@($xs).Count) leading edges: $list"
        }
        $sorted = @($group.Group | Sort-Object Y)
        for ($i = 1; $i -lt $sorted.Count; $i++) {
            $gap = $sorted[$i].Y - ($sorted[$i - 1].Y + $sorted[$i - 1].Height)
            if (-not (OnGrid $gap)) { $errors += "gap $([math]::Round($gap, 2)) epx between $($sorted[$i - 1].Name) and $($sorted[$i].Name)" }
        }
    }
    foreach ($group in ($layout.Probes | Group-Object Area)) {
        $g = @($group.Group)
        for ($i = 0; $i -lt $g.Count; $i++) {
            for ($j = $i + 1; $j -lt $g.Count; $j++) {
                $a = $g[$i]; $b = $g[$j]
                if ($a.X + $tolerance -lt $b.X + $b.Width -and $b.X + $tolerance -lt $a.X + $a.Width -and
                    $a.Y + $tolerance -lt $b.Y + $b.Height -and $b.Y + $tolerance -lt $a.Y + $a.Height) {
                    $errors += "$($a.Name) overlaps $($b.Name)"
                }
            }
        }
    }
    if ($errors.Count -gt 0) {
        $failed++
        Write-Output "FAIL $($path.Name)"
        $errors | ForEach-Object { Write-Output "  - $_" }
    } else {
        Write-Output "PASS $($path.Name): $(@($layout.Probes).Count) probes in $(@($layout.Areas).Count) areas"
    }
}
if ($failed -gt 0) { exit 1 }
