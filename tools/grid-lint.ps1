# Flags spacing numbers off the 4 epx grid, views nudged with transforms or canvas positions,
# and a page column with MaxWidth placed straight in a ScrollViewer.
# Usage: tools/grid-lint.ps1 [folder]      (default: src)
# Allowed numbers: 0, 1, 2, and multiples of 4. Better: a token ({StaticResource SpaceLg}, Space.Lg).
# The token files (Space.xaml, Theme.cs, Grid.cs, and a design kit's Tokens.*) are skipped.
# Silence one line with a reason: <!-- grid: ok <why> --> or // grid: ok <why>
param([string]$Folder = (Join-Path (Split-Path $PSScriptRoot -Parent) 'src'))
$skip = 'Space.xaml', 'Theme.cs', 'Grid.cs', 'Tokens.xaml', 'Tokens.cs'
$xamlAttr = '\b(Margin|Padding|Spacing|RowSpacing|ColumnSpacing|Width|Height|MinWidth|MinHeight|MaxWidth|MaxHeight|CornerRadius|BorderThickness)="([^"{]+)"'
$codeAttr = '(new Thickness\(([^)]*)\)|new CornerRadius\(([^)]*)\)|\.(Margin|Padding|Spacing|RowSpacing|ColumnSpacing|Width|Height|MinWidth|MinHeight|MaxWidth|MaxHeight)\s*=\s*(-?\d+(?:\.\d+)?)\b)'
$count = 0
Get-ChildItem $Folder -Recurse -Include *.xaml, *.cs | Where-Object { $skip -notcontains $_.Name -and $_.FullName -notmatch '\\(obj|bin)\\' } | ForEach-Object {
    $file = $_
    $lineNo = 0
    foreach ($line in Get-Content $file.FullName) {
        $lineNo++
        if ($line -match 'grid: ok') { continue }
        $bad = @()
        $values = @()
        if ($file.Extension -eq '.xaml') {
            foreach ($m in [regex]::Matches($line, $xamlAttr)) { $values += $m.Groups[2].Value -split '[,\s]+' }
            if ($line -match 'TranslateTransform|Canvas\.(Left|Top)=') { $bad += 'offset' }
        } else {
            foreach ($m in [regex]::Matches($line, $codeAttr)) {
                $raw = if ($m.Groups[2].Success) { $m.Groups[2].Value } elseif ($m.Groups[3].Success) { $m.Groups[3].Value } else { $m.Groups[5].Value }
                $values += $raw -split '[,\s]+'
            }
            if ($line -match 'TranslateTransform|Canvas\.Set(Left|Top)') { $bad += 'offset' }
        }
        foreach ($v in $values) {
            $n = 0.0
            if (-not [double]::TryParse($v, [Globalization.NumberStyles]::Float, [Globalization.CultureInfo]::InvariantCulture, [ref]$n)) { continue }
            if ($n -eq 0 -or $n -eq 1 -or $n -eq 2 -or ($n -eq [math]::Floor($n) -and $n % 4 -eq 0)) { continue }
            $bad += $v
        }
        if ($bad.Count -gt 0) {
            $count++
            $rel = $file.FullName.Substring((Split-Path $Folder -Parent).Length + 1)
            Write-Output "${rel}:${lineNo}: off grid ($($bad -join ', ')): $($line.Trim())"
        }
    }
    # A ScrollViewer places a child with MaxWidth by its desired width: in a wide window the column slides off the right edge.
    if ($file.Extension -eq '.xaml') {
        $text = [IO.File]::ReadAllText($file.FullName)
        foreach ($m in [regex]::Matches($text, '<ScrollViewer\b[^>]*>\s*(?:<!--.*?-->\s*)*<[\w:.]+\b[^>]*\bMaxWidth=', 'Singleline')) {
            $count++
            $rel = $file.FullName.Substring((Split-Path $Folder -Parent).Length + 1)
            $at = $text.Substring(0, $m.Index).Split("`n").Count
            Write-Output "${rel}:${at}: the ScrollViewer's child sets MaxWidth, so a wide window pushes it off center. Put a <Grid> between them."
        }
    }
}
if ($count -gt 0) { Write-Output "FAIL grid-lint: $count lines to fix"; exit 1 }
Write-Output 'PASS grid-lint'
