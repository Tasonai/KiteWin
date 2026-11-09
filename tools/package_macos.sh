#!/bin/zsh
# Master macOS Packaging and Distribution Script for Kite (BAMA Standard)
# Builds release DMG, ZIP archive, Sparkle 2 appcast feed, and updates Homebrew Cask.
set -euo pipefail

here=${0:A:h}
root=${here:h}
name="Kite"
app="$HOME/Applications/$name.app"
version="${1:-1.4.0}"
rebuild="${2:-false}"

echo "=== 1. Checking macOS Application Bundle ==="
if [[ "$rebuild" == "--rebuild" || ! -d "$app" ]]; then
    echo "Building release $name.app..."
    "$root/build.sh"
else
    echo "Found existing bundle at $app"
fi

dist="$root/dist"
mkdir -p "$dist"

dmg_name="$name-$version-macOS.dmg"
dmg_path="$dist/$dmg_name"
zip_name="$name-$version-macOS.zip"
zip_path="$dist/$zip_name"
tmp_dmg="$dist/tmp.dmg"
stage_dir=$(mktemp -d /tmp/kite_dmg_stage.XXXXXX)

trap 'rm -rf "$stage_dir" "$tmp_dmg"' EXIT

echo ""
echo "=== 2. Staging and Creating DMG Installer ==="
cp -R "$app" "$stage_dir/"
ln -s /Applications "$stage_dir/Applications"

rm -f "$dmg_path" "$tmp_dmg" "$zip_path"
hdiutil create -volname "$name" -srcfolder "$stage_dir" -ov -format UDRW "$tmp_dmg" -quiet
hdiutil convert "$tmp_dmg" -format UDZO -imagekey zlib-level=9 -o "$dmg_path" -quiet
rm -f "$tmp_dmg"

echo "PASS DMG generated at: $dmg_path"

echo ""
echo "=== 3. Creating Release ZIP Archive ==="
ditto -c -k --keepParent "$app" "$zip_path"
echo "PASS ZIP generated at: $zip_path"

echo ""
echo "=== 4. Computing Checksums ==="
dmg_sha=$(shasum -a 256 "$dmg_path" | awk '{print $1}')
zip_sha=$(shasum -a 256 "$zip_path" | awk '{print $1}')
dmg_size=$(stat -f%z "$dmg_path")
zip_size=$(stat -f%z "$zip_path")

echo "$dmg_sha  $dmg_name" > "$dist/checksums.txt"
echo "$zip_sha  $zip_name" >> "$dist/checksums.txt"
echo "PASS Checksums saved to $dist/checksums.txt"

echo ""
echo "=== 5. Updating Homebrew Cask ==="
cask_file="$root/packaging/homebrew/kite.rb"
if [[ -f "$cask_file" ]]; then
    python3 -c "
import re
path = '$cask_file'
with open(path, 'r', encoding='utf-8') as f:
    c = f.read()
c = re.sub(r'version\s+\"[^\"]+\"', f'version \"$version\"', c)
c = re.sub(r'sha256\s+\"[a-f0-9]+\"', f'sha256 \"$dmg_sha\"', c)
with open(path, 'w', encoding='utf-8') as f:
    f.write(c)
print('PASS Homebrew Cask updated to version $version')
"
fi

echo ""
echo "=== 6. Updating Sparkle 2 Appcast Feed ==="
appcast_file="$root/packaging/appcast.xml"
python3 -c "
import os, datetime
path = '$appcast_file'
now = datetime.datetime.now(datetime.timezone.utc).strftime('%a, %d %b %Y %H:%M:%S +0000')
xml = f'''<?xml version=\"1.0\" encoding=\"utf-8\"?>
<rss version=\"2.0\" xmlns:sparkle=\"http://www.andymatuschak.org/xml-namespaces/sparkle\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\">
  <channel>
    <title>Kite Changelog</title>
    <link>https://raw.githubusercontent.com/Tasonai/Kite/main/packaging/appcast.xml</link>
    <description>Most recent changes with links to updates.</description>
    <language>en</language>
    <item>
      <title>Version $version</title>
      <sparkle:releaseNotesLink>https://github.com/Tasonai/Kite/releases/tag/v$version</sparkle:releaseNotesLink>
      <pubDate>{now}</pubDate>
      <enclosure url=\"https://github.com/Tasonai/Kite/releases/download/v$version/$dmg_name\"
                 sparkle:version=\"$version\"
                 sparkle:shortVersionString=\"$version\"
                 length=\"$dmg_size\"
                 type=\"application/x-apple-diskimage\" />
    </item>
  </channel>
</rss>
'''
os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
with open(path, 'w', encoding='utf-8') as f:
    f.write(xml)
print('PASS Sparkle 2 appcast feed generated at $appcast_file')
"

echo ""
echo "=== 7. Verifying DMG Mount ==="
mount_point="/Volumes/$name"
hdiutil attach "$dmg_path" -nobrowse -quiet
if [[ -d "$mount_point/$name.app" ]]; then
    echo "PASS DMG mount verified successfully ($mount_point/$name.app found)"
else
    echo "FAIL DMG mount check failed"
    hdiutil detach "$mount_point" -force -quiet || true
    exit 1
fi
hdiutil detach "$mount_point" -force -quiet

echo ""
echo "=== macOS Release Package Build Complete ==="
echo "Artifacts in $dist:"
ls -lh "$dist"
