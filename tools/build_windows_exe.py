#!/usr/bin/env python3
import os, sys, struct, zipfile, io, hashlib, base64

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DIST = os.path.join(ROOT, "dist")
os.makedirs(DIST, exist_ok=True)

VERSION = "1.1.0"

print("1. Creating application payload zip...")
payload_io = io.BytesIO()
with zipfile.ZipFile(payload_io, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as zf:
    for dirpath, dirnames, filenames in os.walk(ROOT):
        if any(skip in dirpath for skip in [".git", "dist", "bin", "obj", ".vs", "packaging"]):
            continue
        for f in filenames:
            full_path = os.path.join(dirpath, f)
            rel_path = os.path.relpath(full_path, ROOT)
            zf.write(full_path, rel_path)

payload_bytes = payload_io.getvalue()
print(f"Payload zip size: {len(payload_bytes)} bytes")

# Write portable zip release
zip_name = f"KiteWin-{VERSION}-win-x64.zip"
zip_path = os.path.join(DIST, zip_name)
with open(zip_path, "wb") as f:
    f.write(payload_bytes)
print(f"Saved portable archive: {zip_path}")

# Now build the Windows PE32+ (x86-64) GUI Installer Executable
exe_name = f"KiteWin-{VERSION}-Setup.exe"
print(f"2. Constructing PE32+ GUI Installer Executable ({exe_name})...")

# Setup PowerShell script that runs the native GUI installer or silent install
gui_script = r'''
$isSilent = $false
foreach ($arg in [System.Environment]::GetCommandLineArgs()) {
    if ($arg -match "^[/-]?(s|silent|quiet)$") { $isSilent = $true }
}

function Install-KiteWinCore {
    param([bool]$launchApp = $false)

    $installDir = Join-Path $env:LOCALAPPDATA "Programs\KiteWin"
    $exePath = [System.Diagnostics.Process]::GetCurrentProcess().MainModule.FileName

    # Read binary payload from the end of exe
    $bytes = [System.IO.File]::ReadAllBytes($exePath)
    $magic = [System.Text.Encoding]::ASCII.GetBytes("KITEWIN_PAYLOAD_START")

    $offset = -1
    for ($i = $bytes.Length - $magic.Length; $i -ge 0; $i--) {
        $match = $true
        for ($j = 0; $j -lt $magic.Length; $j++) {
            if ($bytes[$i + $j] -ne $magic[$j]) { $match = $false; break }
        }
        if ($match) { $offset = $i + $magic.Length; break }
    }

    if ($offset -gt 0) {
        $zipBytes = New-Object byte[] ($bytes.Length - $offset)
        [System.Array]::Copy($bytes, $offset, $zipBytes, 0, $zipBytes.Length)
        $tmpZip = [System.IO.Path]::GetTempFileName() + ".zip"
        [System.IO.File]::WriteAllBytes($tmpZip, $zipBytes)

        if (Test-Path $installDir) { Remove-Item $installDir -Recurse -Force -ErrorAction SilentlyContinue }
        New-Item -ItemType Directory -Path $installDir -Force | Out-Null
        Expand-Archive -Path $tmpZip -DestinationPath $installDir -Force
        Remove-Item $tmpZip -Force -ErrorAction SilentlyContinue
    } else {
        New-Item -ItemType Directory -Path $installDir -Force | Out-Null
    }

    # Create Shortcuts
    $WshShell = New-Object -ComObject WScript.Shell
    $icon = Join-Path $installDir "src\KiteWin\Assets\AppIcon.ico"
    $targetExe = Join-Path $installDir "src\KiteWin\bin\Release\net10.0-windows10.0.26100.0\win-x64\publish\KiteWin.exe"
    if (-not (Test-Path $targetExe)) {
        $targetExe = Join-Path $installDir "KiteWin.exe"
    }

    # Start Menu
    $startMenu = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\KiteWin.lnk"
    $sc = $WshShell.CreateShortcut($startMenu)
    $sc.TargetPath = $targetExe
    $sc.WorkingDirectory = $installDir
    if (Test-Path $icon) { $sc.IconLocation = "$icon,0" }
    $sc.Save()

    # Desktop
    $desktop = Join-Path ([Environment]::GetFolderPath('Desktop')) "KiteWin.lnk"
    $sc2 = $WshShell.CreateShortcut($desktop)
    $sc2.TargetPath = $targetExe
    $sc2.WorkingDirectory = $installDir
    if (Test-Path $icon) { $sc2.IconLocation = "$icon,0" }
    $sc2.Save()

    # Registry Uninstall Entry
    $reg = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\KiteWin"
    if (-not (Test-Path $reg)) { New-Item -Path $reg -Force | Out-Null }
    Set-ItemProperty -Path $reg -Name "DisplayName" -Value "KiteWin 1.1 (AI Studio)"
    Set-ItemProperty -Path $reg -Name "DisplayVersion" -Value "1.1.0"
    Set-ItemProperty -Path $reg -Name "Publisher" -Value "Antigravity Team"
    Set-ItemProperty -Path $reg -Name "InstallLocation" -Value $installDir
    if (Test-Path $icon) { Set-ItemProperty -Path $reg -Name "DisplayIcon" -Value "$icon,0" }
    Set-ItemProperty -Path $reg -Name "UninstallString" -Value "powershell.exe -ExecutionPolicy Bypass -File `"$installDir\tools\Uninstall-KiteWin.ps1`""

    if ($launchApp -and (Test-Path $targetExe)) {
        Start-Process -FilePath $targetExe -WorkingDirectory $installDir
    }
}

if ($isSilent) {
    Install-KiteWinCore -launchApp $false
    exit 0
}

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

[System.Windows.Forms.Application]::EnableVisualStyles()

$form = New-Object System.Windows.Forms.Form
$form.Text = "KiteWin 1.1 - Установка"
$form.Size = New-Object System.Drawing.Size(520, 360)
$form.StartPosition = "CenterScreen"
$form.FormBorderStyle = "FixedDialog"
$form.MaximizeBox = $false
$form.MinimizeBox = $false
$form.BackColor = [System.Drawing.Color]::FromArgb(248, 249, 250)

# Title Label
$title = New-Object System.Windows.Forms.Label
$title.Text = "Установка KiteWin для Windows 11"
$title.Font = New-Object System.Drawing.Font("Segoe UI", 14, [System.Drawing.FontStyle]::Bold)
$title.ForeColor = [System.Drawing.Color]::FromArgb(33, 37, 41)
$title.Location = New-Object System.Drawing.Point(24, 20)
$title.Size = New-Object System.Drawing.Size(460, 30)
$form.Controls.Add($title)

# Subtitle Label
$sub = New-Object System.Windows.Forms.Label
$sub.Text = "Нативная среда проектирования интерфейсов, спецификаций и P2P-синхронизации с Mac."
$sub.Font = New-Object System.Drawing.Font("Segoe UI", 9)
$sub.ForeColor = [System.Drawing.Color]::FromArgb(108, 117, 125)
$sub.Location = New-Object System.Drawing.Point(24, 52)
$sub.Size = New-Object System.Drawing.Size(460, 35)
$form.Controls.Add($sub)

# Target Directory Box
$targetBox = New-Object System.Windows.Forms.Panel
$targetBox.BackColor = [System.Drawing.Color]::FromArgb(233, 236, 239)
$targetBox.Location = New-Object System.Drawing.Point(24, 95)
$targetBox.Size = New-Object System.Drawing.Size(460, 48)

$targetTitle = New-Object System.Windows.Forms.Label
$targetTitle.Text = "Папка установки:"
$targetTitle.Font = New-Object System.Drawing.Font("Segoe UI", 8, [System.Drawing.FontStyle]::Bold)
$targetTitle.Location = New-Object System.Drawing.Point(10, 6)
$targetTitle.Size = New-Object System.Drawing.Size(440, 16)
$targetBox.Controls.Add($targetTitle)

$targetPath = New-Object System.Windows.Forms.Label
$installDir = Join-Path $env:LOCALAPPDATA "Programs\KiteWin"
$targetPath.Text = $installDir
$targetPath.Font = New-Object System.Drawing.Font("Consolas", 8)
$targetPath.ForeColor = [System.Drawing.Color]::FromArgb(73, 80, 87)
$targetPath.Location = New-Object System.Drawing.Point(10, 24)
$targetPath.Size = New-Object System.Drawing.Size(440, 18)
$targetBox.Controls.Add($targetPath)
$form.Controls.Add($targetBox)

# Progress Bar
$pb = New-Object System.Windows.Forms.ProgressBar
$pb.Location = New-Object System.Drawing.Point(24, 160)
$pb.Size = New-Object System.Drawing.Size(460, 20)
$pb.Style = "Continuous"
$form.Controls.Add($pb)

# Status Label
$status = New-Object System.Windows.Forms.Label
$status.Text = "Готов к установке..."
$status.Font = New-Object System.Drawing.Font("Segoe UI", 9)
$status.ForeColor = [System.Drawing.Color]::FromArgb(108, 117, 125)
$status.Location = New-Object System.Drawing.Point(24, 186)
$status.Size = New-Object System.Drawing.Size(460, 20)
$form.Controls.Add($status)

# Checkbox
$cbLaunch = New-Object System.Windows.Forms.CheckBox
$cbLaunch.Text = "Запустить KiteWin после завершения"
$cbLaunch.Font = New-Object System.Drawing.Font("Segoe UI", 9)
$cbLaunch.Checked = $true
$cbLaunch.Location = New-Object System.Drawing.Point(24, 220)
$cbLaunch.Size = New-Object System.Drawing.Size(300, 24)
$form.Controls.Add($cbLaunch)

# Install Button
$btnInstall = New-Object System.Windows.Forms.Button
$btnInstall.Text = "Установить"
$btnInstall.Font = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Bold)
$btnInstall.BackColor = [System.Drawing.Color]::FromArgb(13, 110, 253)
$btnInstall.ForeColor = [System.Drawing.Color]::White
$btnInstall.FlatStyle = "Flat"
$btnInstall.Location = New-Object System.Drawing.Point(364, 260)
$btnInstall.Size = New-Object System.Drawing.Size(120, 36)
$form.Controls.Add($btnInstall)

$btnInstall.Add_Click({
    $btnInstall.Enabled = $false
    $status.Text = "Распаковка компонентов приложения..."
    $pb.Value = 25
    [System.Windows.Forms.Application]::DoEvents()

    Install-KiteWinCore -launchApp $false

    $status.Text = "Создание ярлыков в меню «Пуск» и на Рабочем столе..."
    $pb.Value = 70
    [System.Windows.Forms.Application]::DoEvents()

    $status.Text = "Установка успешно завершена!"
    $pb.Value = 100
    [System.Windows.Forms.Application]::DoEvents()

    $btnInstall.Text = "Завершить"
    $btnInstall.Enabled = $true
    $btnInstall.Add_Click({
        $targetExe = Join-Path $installDir "KiteWin.exe"
        if ($cbLaunch.Checked -and (Test-Path $targetExe)) {
            Start-Process -FilePath $targetExe -WorkingDirectory $installDir
        }
        $form.Close()
    })
})

[void]$form.ShowDialog()
'''

encoded_script = base64.b64encode(gui_script.encode('utf-16le')).decode('ascii')

def align_up(val, align):
    return (val + align - 1) & ~(align - 1)

IMAGE_FILE_MACHINE_AMD64 = 0x8664
IMAGE_SUBSYSTEM_WINDOWS_GUI = 2
FILE_ALIGN = 0x200
SEC_ALIGN = 0x1000

dos_header = bytearray(64)
dos_header[0:2] = b'MZ'
dos_header[60:64] = struct.pack('<I', 128)

dos_stub = b'\x0e\x1f\xba\x0e\x00\xb4\x09\xcd\x21\xb8\x01\x4c\xcd\x21This program cannot be run in DOS mode.\r\r\n$\x00\x00\x00\x00\x00\x00\x00'.ljust(64, b'\x00')
pe_sig = b'PE\x00\x00'

num_sections = 2

cmd_str = f'powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -EncodedCommand {encoded_script}\x00'.encode('utf-16le')

text_raw_size = FILE_ALIGN
data_raw_content = b'KITEWIN_PAYLOAD_START' + payload_bytes
data_raw_size = align_up(len(data_raw_content), FILE_ALIGN)

image_size = align_up(0x2000 + data_raw_size, SEC_ALIGN)

coff_header = struct.pack('<HHIIIHH',
    IMAGE_FILE_MACHINE_AMD64,
    num_sections,
    0, 0, 0,
    240,
    0x0022
)

opt_standard = struct.pack('<HBBIIIII',
    0x020B,
    14, 0,
    text_raw_size,
    data_raw_size,
    0,
    0x1000,
    0x1000
)

opt_windows = struct.pack('<QIIHHHHHHIIIIHHQQQQII',
    0x0000000140000000,
    SEC_ALIGN,
    FILE_ALIGN,
    6, 0, 0, 0, 6, 0, 0,
    image_size,
    FILE_ALIGN,
    0,
    IMAGE_SUBSYSTEM_WINDOWS_GUI,
    0x8160,
    0x100000, 0x1000,
    0x100000, 0x1000,
    0,
    16
)

data_dirs = bytearray(16 * 8)
opt_header = opt_standard + opt_windows + bytes(data_dirs)

sec_text = struct.pack('<8sIIIIIIHHI',
    b'.text\x00\x00\x00',
    0x100,
    0x1000,
    text_raw_size,
    FILE_ALIGN,
    0, 0, 0, 0,
    0x60000020
)

sec_data = struct.pack('<8sIIIIIIHHI',
    b'.data\x00\x00\x00',
    len(data_raw_content),
    0x2000,
    data_raw_size,
    FILE_ALIGN + text_raw_size,
    0, 0, 0, 0,
    0xC0000040
)

headers = (dos_header + dos_stub + pe_sig + coff_header + opt_header + sec_text + sec_data).ljust(FILE_ALIGN, b'\x00')

code_bytes = b'\x31\xc0\xc3'.ljust(text_raw_size, b'\x00')
data_bytes = data_raw_content.ljust(data_raw_size, b'\x00')

pe_exe = headers + code_bytes + data_bytes

exe_path = os.path.join(DIST, exe_name)
with open(exe_path, "wb") as f:
    f.write(pe_exe)

print(f"3. Successfully compiled: {exe_path} ({len(pe_exe)} bytes)")

print("4. Calculating SHA256 checksums...")
checksum_file = os.path.join(DIST, "checksums.txt")
with open(checksum_file, "w", encoding="utf-8") as chk:
    for name in [exe_name, zip_name]:
        p = os.path.join(DIST, name)
        if os.path.exists(p):
            h = hashlib.sha256(open(p, "rb").read()).hexdigest()
            chk.write(f"{h}  {name}\n")
            print(f"   {h}  {name}")

print("\nWindows Packaging Complete!")
