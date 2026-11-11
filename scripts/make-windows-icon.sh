#!/bin/bash
# builds windows/src/Murmur.App/Murmur.ico from the mac icon (Resources/AppIcon.icns), so both apps
# share one icon. run after scripts/make-icon.sh.
set -euo pipefail
cd "$(dirname "$0")/.."

ICNS="Resources/AppIcon.icns"
OUT="windows/src/Murmur.App/Murmur.ico"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

for size in 16 24 32 48 64 256; do
    sips -s format png -z "$size" "$size" "$ICNS" --out "$WORK/$size.png" >/dev/null
done

# an .ico is a small header plus one png per size
python3 -I - "$WORK" "$OUT" <<'EOF'
import os
import struct
import sys

work, out = sys.argv[1], sys.argv[2]
sizes = [16, 24, 32, 48, 64, 256]
images = [open(os.path.join(work, f"{size}.png"), "rb").read() for size in sizes]
header = struct.pack("<HHH", 0, 1, len(sizes))
offset = 6 + 16 * len(sizes)
entries = b""
for size, data in zip(sizes, images):
    dimension = 0 if size == 256 else size  # 0 means 256 in the ico format
    entries += struct.pack("<BBBBHHII", dimension, dimension, 0, 0, 1, 32, len(data), offset)
    offset += len(data)
with open(out, "wb") as file:
    file.write(header + entries + b"".join(images))
EOF

echo "built $OUT"
