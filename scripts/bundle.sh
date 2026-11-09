#!/bin/bash
# builds build/Murmur.app from the release binary and signs it.
# signs with the "Murmur Local Signing" identity when it exists (macos then keeps mic and
# accessibility permissions across rebuilds), otherwise ad hoc (permissions reset each build).
set -euo pipefail
cd "$(dirname "$0")/.."

PLIST="Resources/Info.plist"
NAME="$(/usr/libexec/PlistBuddy -c "Print :CFBundleName" "$PLIST")"
EXECUTABLE="$(/usr/libexec/PlistBuddy -c "Print :CFBundleExecutable" "$PLIST")"
SIGNING_IDENTITY="Murmur Local Signing"
APP="build/$NAME.app"

swift build -c release
BIN_DIR="$(swift build -c release --show-bin-path)"

rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp "$BIN_DIR/$EXECUTABLE" "$APP/Contents/MacOS/$EXECUTABLE"
cp "$PLIST" "$APP/Contents/Info.plist"
cp Resources/AppIcon.icns "$APP/Contents/Resources/AppIcon.icns"

if security find-certificate -c "$SIGNING_IDENTITY" >/dev/null 2>&1; then
    codesign --force --sign "$SIGNING_IDENTITY" "$APP"
else
    echo "note: '$SIGNING_IDENTITY' not found, signing ad hoc (run scripts/create-signing-cert.sh once)"
    codesign --force --sign - "$APP"
fi

echo "built $APP"
