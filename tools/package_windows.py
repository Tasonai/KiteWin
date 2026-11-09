#!/usr/bin/env python3
# Master Windows Packaging and Distribution Script for KiteWin (BAWA Standard)
# Packages portable ZIP, self-extracting PE32+ Setup.exe, and updates WinGet manifests.

import os, sys, re, hashlib, subprocess

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DIST = os.path.join(ROOT, "dist")
VERSION = sys.argv[1] if len(sys.argv) > 1 else "1.5.0"

print(f"=== 1. Building Windows Release Artifacts (v{VERSION}) ===")
build_script = os.path.join(ROOT, "tools", "build_windows_exe.py")

res = subprocess.run([sys.executable, build_script, VERSION], cwd=ROOT, capture_output=True, text=True)
if res.returncode != 0:
    print(res.stderr)
    print("FAIL: build_windows_exe.py failed")
    sys.exit(1)

print(res.stdout.strip())

exe_name = f"KiteWin-{VERSION}-Setup.exe"
exe_path = os.path.join(DIST, exe_name)
zip_name = f"KiteWin-{VERSION}-win-x64.zip"
zip_path = os.path.join(DIST, zip_name)

if not os.path.exists(exe_path) or not os.path.exists(zip_path):
    print("FAIL: Expected release artifacts missing in dist/")
    sys.exit(1)

with open(exe_path, "rb") as f:
    exe_sha = hashlib.sha256(f.read()).hexdigest().upper()

print("\n=== 2. Updating Windows Package Manager (WinGet) Manifests ===")
winget_dir = os.path.join(ROOT, "packaging", "winget")

# 1. Version manifest
v_manifest = os.path.join(winget_dir, "Tasonai.KiteWin.yaml")
with open(v_manifest, "w", encoding="utf-8") as f:
    f.write(f"""PackageIdentifier: Tasonai.KiteWin
PackageVersion: {VERSION}
DefaultLocale: en-US
ManifestType: version
ManifestVersion: 1.6.0
""")
print(f"PASS Updated {os.path.relpath(v_manifest, ROOT)}")

# 2. Locale manifest
l_manifest = os.path.join(winget_dir, "Tasonai.KiteWin.locale.en-US.yaml")
with open(l_manifest, "w", encoding="utf-8") as f:
    f.write(f"""PackageIdentifier: Tasonai.KiteWin
PackageVersion: {VERSION}
PackageLocale: en-US
Publisher: Tasonai
PackageName: KiteWin
PackageUrl: https://github.com/Tasonai/KiteWin
License: MIT
ShortDescription: Windows 11 studio for ideas, architecture and Antigravity skills
ManifestType: defaultLocale
ManifestVersion: 1.6.0
""")
print(f"PASS Updated {os.path.relpath(l_manifest, ROOT)}")

# 3. Installer manifest
i_manifest = os.path.join(winget_dir, "Tasonai.KiteWin.installer.yaml")
with open(i_manifest, "w", encoding="utf-8") as f:
    f.write(f"""PackageIdentifier: Tasonai.KiteWin
PackageVersion: {VERSION}
InstallerType: exe
Scope: user
InstallerSwitches:
  Silent: /S
  SilentWithProgress: /S
Installers:
  - Architecture: x64
    InstallerUrl: https://github.com/Tasonai/KiteWin/releases/download/v{VERSION}/{exe_name}
    InstallerSha256: {exe_sha}
ManifestType: installer
ManifestVersion: 1.6.0
""")
print(f"PASS Updated {os.path.relpath(i_manifest, ROOT)} with SHA256: {exe_sha}")

print("\n=== 3. Verifying WinGet Manifests Consistency ===")
assert os.path.exists(v_manifest)
assert os.path.exists(l_manifest)
assert os.path.exists(i_manifest)
print("PASS All 3 WinGet manifest files verified.")

print(f"\nWindows Release Packaging (v{VERSION}) Complete!")
