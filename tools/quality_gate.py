#!/usr/bin/env python3
"""
Master Quality Gate for KiteWin Windows 11 (BAWA Standard).
Audits WinUI 3 XAML / C# code for 4 epx grid compliance and zero emojis.
"""

import os
import sys
import re
import unicodedata

def run_grid_lint(folder: str) -> bool:
    print("=== 1. Windows 11 BAWA 4 epx Grid Lint ===")
    skip = {'Space.xaml', 'Theme.cs', 'Grid.cs', 'Tokens.xaml', 'Tokens.cs', 'SkillsData.cs'}
    xaml_attr = re.compile(r'\b(Margin|Padding|Spacing|RowSpacing|ColumnSpacing|Width|Height|MinWidth|MinHeight|MaxWidth|MaxHeight|CornerRadius|BorderThickness)=\"([^\"{]+)\"')
    code_attr = re.compile(r'(new Thickness\(([^)]*)\)|new CornerRadius\(([^)]*)\)|\.(Margin|Padding|Spacing|RowSpacing|ColumnSpacing|Width|Height|MinWidth|MinHeight|MaxWidth|MaxHeight)\s*=\s*(-?\d+(?:\.\d+)?)\b)')

    results = []
    for root, dirs, files in os.walk(folder):
        if any(s in root for s in ['obj', 'bin', '.git']):
            continue
        for fname in sorted(files):
            if fname in skip:
                continue
            ext = os.path.splitext(fname)[1].lower()
            if ext not in ('.xaml', '.cs'):
                continue
            fpath = os.path.join(root, fname)
            rel = os.path.relpath(fpath, folder)
            with open(fpath, 'r', encoding='utf-8', errors='ignore') as f:
                for idx, line in enumerate(f, 1):
                    if 'grid: ok' in line:
                        continue
                    bad = []
                    vals = []
                    if ext == '.xaml':
                        for m in xaml_attr.finditer(line):
                            vals.extend(re.split(r'[,\s]+', m.group(2).strip()))
                        if 'TranslateTransform' in line or 'Canvas.Left=' in line or 'Canvas.Top=' in line:
                            bad.append('offset')
                    else:
                        for m in code_attr.finditer(line):
                            raw = m.group(2) or m.group(3) or m.group(5)
                            if raw:
                                vals.extend(re.split(r'[,\s]+', raw.strip()))
                        if 'TranslateTransform' in line or 'Canvas.SetLeft' in line or 'Canvas.SetTop' in line:
                            bad.append('offset')
                    for v in vals:
                        try:
                            n = float(v)
                            if n in (0, 1, 2) or (n.is_integer() and int(n) % 4 == 0):
                                continue
                            bad.append(v)
                        except ValueError:
                            pass
                    if bad:
                        results.append((rel, idx, ', '.join(bad), line.strip()))

    if results:
        print(f"FAIL: Found {len(results)} off-grid violations:")
        for rel, idx, bad, line in results[:10]:
            print(f"  {rel}:{idx} [{bad}]: {line}")
        return False

    print("PASS 4 epx Grid Compliance (0 violations)")
    return True

def run_emoji_audit(folder: str) -> bool:
    print("\n=== 2. Zero Emojis Audit (100% Segoe Fluent Icons) ===")
    violations = []
    for root, dirs, files in os.walk(folder):
        if any(skip in root for skip in ['.git', 'bin', 'obj']):
            continue
        for fname in files:
            ext = os.path.splitext(fname)[1].lower()
            if ext in ('.cs', '.xaml', '.md'):
                fpath = os.path.join(root, fname)
                rel = os.path.relpath(fpath, folder)
                with open(fpath, 'r', encoding='utf-8', errors='ignore') as f:
                    for lno, line in enumerate(f, 1):
                        for ch in line:
                            code = ord(ch)
                            if (0x1F300 <= code <= 0x1F9FF) or (0x2600 <= code <= 0x27BF and code != 0x2713):
                                violations.append((rel, lno, ch, line.strip()))
    if violations:
        print(f"FAIL: Found {len(violations)} emoji violations!")
        for rel, lno, ch, line in violations[:10]:
            print(f"  {rel}:{lno} [{ch}]: {line}")
        return False

    print("PASS Zero Emojis (100% Segoe Fluent Icons)")
    return True

def main():
    root = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
    src_folder = os.path.join(root, 'src')
    
    ok1 = run_grid_lint(src_folder)
    ok2 = run_emoji_audit(src_folder)

    if ok1 and ok2:
        print("\nALL QUALITY GATES PASSED (BAWA Craftsmanship Standard Verified)")
        sys.exit(0)
    else:
        sys.exit(1)

if __name__ == '__main__':
    main()
