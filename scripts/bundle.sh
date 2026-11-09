#!/bin/bash
# builds build/Murmur.app from the release binary and signs it, with the best identity there is:
#  1. an "Apple Development" certificate (free apple id in xcode): keychain "always allow" and all
#     permissions survive rebuilds, because macos ties them to the team id
#  2. "Murmur Local Signing" (scripts/create-signing-cert.sh): mic + accessibility survive rebuilds,
#     but keychain asks again after each build (it ties self-signed apps to the exact binary)
#  3. ad hoc: every permission resets on each build
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

APPLE_DEVELOPMENT="$(security find-identity -v -p codesigning | grep -o '"Apple Development: [^"]*"' | head -n 1 | tr -d '"' || true)"
if [[ -n "$APPLE_DEVELOPMENT" ]]; then
    codesign --force --sign "$APPLE_DEVELOPMENT" "$APP"
elif security find-certificate -c "$SIGNING_IDENTITY" >/dev/null 2>&1; then
    codesign --force --sign "$SIGNING_IDENTITY" "$APP"
else
    echo "note: no signing identity found, signing ad hoc (run scripts/create-signing-cert.sh once)"
    codesign --force --sign - "$APP"
fi

echo "built $APP"
