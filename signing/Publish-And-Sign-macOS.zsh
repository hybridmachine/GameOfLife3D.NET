#!/bin/zsh
#
# Publishes, bundles, signs, and notarizes GameOfLife3D.NET for macOS (osx-arm64).
# Usage: ./signing/Publish-And-Sign-macOS.zsh [path-to-macos-signing-config.json]
#
# Prerequisites:
#   1. "Developer ID Application" certificate installed in login keychain
#   2. Notarization credentials stored via:
#      xcrun notarytool store-credentials "GameOfLife3D-notarize" \
#          --apple-id "your@email.com" --team-id "TEAM_ID" --password "app-specific-password"
#   3. signing/macOS/macos-signing-config.json populated from template

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
MACOS_DIR="$SCRIPT_DIR/macOS"
PROJECT_PATH="$REPO_ROOT/src/GameOfLife3D.NET/GameOfLife3D.NET.csproj"
CONFIG_PATH="${1:-$MACOS_DIR/macos-signing-config.json}"

# --- Config Loading ---

if [[ ! -f "$CONFIG_PATH" ]]; then
  echo "ERROR: Signing config not found at: $CONFIG_PATH" >&2
  echo "Copy macos-signing-config.template.json to macos-signing-config.json and fill in your values." >&2
  exit 1
fi

get_json_field() {
  local config_file="$1"
  local field_name="$2"
  python3 - "$config_file" "$field_name" <<'PY'
import json
import sys

config_path = sys.argv[1]
field_name = sys.argv[2]
with open(config_path, "r", encoding="utf-8") as f:
    data = json.load(f)
value = data.get(field_name, "")
print(value if isinstance(value, str) else "")
PY
}

TEAM_ID="$(get_json_field "$CONFIG_PATH" "TeamId")"
CERTIFICATE_IDENTITY="$(get_json_field "$CONFIG_PATH" "CertificateIdentity")"
NOTARIZATION_PROFILE="$(get_json_field "$CONFIG_PATH" "NotarizationKeychainProfile")"
BUNDLE_ID="$(get_json_field "$CONFIG_PATH" "BundleIdentifier")"

if [[ -z "$TEAM_ID" || -z "$CERTIFICATE_IDENTITY" || -z "$NOTARIZATION_PROFILE" ]]; then
  echo "ERROR: macos-signing-config.json must include TeamId, CertificateIdentity, and NotarizationKeychainProfile." >&2
  exit 1
fi

if [[ "$TEAM_ID" == "YOUR_TEAM_ID" ]]; then
  echo "ERROR: Please update macos-signing-config.json with your Apple Developer account details." >&2
  exit 1
fi

# --- Verify Certificate ---

if ! security find-identity -v -p codesigning | grep -q "$CERTIFICATE_IDENTITY"; then
  echo "ERROR: Certificate not found in keychain: $CERTIFICATE_IDENTITY" >&2
  echo "Create a 'Developer ID Application' certificate at:" >&2
  echo "https://developer.apple.com/account/resources/certificates/list" >&2
  exit 1
fi

echo "Using certificate: $CERTIFICATE_IDENTITY"

# --- Step 1: Publish ---

echo ""
echo "Publishing Release build (osx-arm64)..."
dotnet publish "$PROJECT_PATH" \
  --configuration Release \
  --runtime osx-arm64 \
  --self-contained true \
  -p:PublishSingleFile=false \
  -p:IncludeAllContentForSelfExtract=false \
  -p:EnableCompressionInSingleFile=false

PUBLISH_DIR="$REPO_ROOT/src/GameOfLife3D.NET/bin/Release/net10.0/osx-arm64/publish"

if [[ ! -d "$PUBLISH_DIR" ]]; then
  echo "ERROR: Publish output not found at: $PUBLISH_DIR" >&2
  exit 1
fi

# --- Step 2: Create .app bundle ---

APP_NAME="Game of Life 3D.app"
BUILD_DIR="$REPO_ROOT/build/macOS"
APP_DIR="$BUILD_DIR/$APP_NAME"
CONTENTS_DIR="$APP_DIR/Contents"
MACOS_BIN_DIR="$CONTENTS_DIR/MacOS"
RESOURCES_DIR="$CONTENTS_DIR/Resources"

echo ""
echo "Creating app bundle: $APP_NAME"

rm -rf "$APP_DIR"
mkdir -p "$MACOS_BIN_DIR"
mkdir -p "$RESOURCES_DIR"

# Copy all published files into Contents/MacOS/
cp -R "$PUBLISH_DIR/"* "$MACOS_BIN_DIR/"

# Relocate bundled ffmpeg into Contents/Resources/ (FfmpegEncoder.LocateBinary
# checks ../Resources/ffmpeg when running from inside a .app bundle).
if [[ -f "$MACOS_BIN_DIR/ffmpeg" ]]; then
  echo "Relocating bundled ffmpeg → Contents/Resources/"
  mv "$MACOS_BIN_DIR/ffmpeg" "$RESOURCES_DIR/ffmpeg"
  chmod +x "$RESOURCES_DIR/ffmpeg"
fi

# Copy Info.plist
cp "$MACOS_DIR/Info.plist" "$CONTENTS_DIR/Info.plist"

# --- Step 3: Generate .icns from PNG ---

ICON_SRC="$REPO_ROOT/logo.png"

if [[ -f "$ICON_SRC" ]]; then
  echo "Generating app icon..."
  ICONSET_DIR="$BUILD_DIR/AppIcon.iconset"
  rm -rf "$ICONSET_DIR"
  mkdir -p "$ICONSET_DIR"

  sips -z 16 16     "$ICON_SRC" --out "$ICONSET_DIR/icon_16x16.png"     > /dev/null 2>&1
  sips -z 32 32     "$ICON_SRC" --out "$ICONSET_DIR/icon_16x16@2x.png"  > /dev/null 2>&1
  sips -z 32 32     "$ICON_SRC" --out "$ICONSET_DIR/icon_32x32.png"     > /dev/null 2>&1
  sips -z 64 64     "$ICON_SRC" --out "$ICONSET_DIR/icon_32x32@2x.png"  > /dev/null 2>&1
  sips -z 128 128   "$ICON_SRC" --out "$ICONSET_DIR/icon_128x128.png"   > /dev/null 2>&1
  sips -z 256 256   "$ICON_SRC" --out "$ICONSET_DIR/icon_128x128@2x.png" > /dev/null 2>&1
  sips -z 256 256   "$ICON_SRC" --out "$ICONSET_DIR/icon_256x256.png"   > /dev/null 2>&1
  sips -z 512 512   "$ICON_SRC" --out "$ICONSET_DIR/icon_256x256@2x.png" > /dev/null 2>&1
  sips -z 512 512   "$ICON_SRC" --out "$ICONSET_DIR/icon_512x512.png"   > /dev/null 2>&1
  sips -z 1024 1024 "$ICON_SRC" --out "$ICONSET_DIR/icon_512x512@2x.png" > /dev/null 2>&1

  iconutil -c icns "$ICONSET_DIR" -o "$RESOURCES_DIR/AppIcon.icns"
  rm -rf "$ICONSET_DIR"
  echo "App icon generated."
else
  echo "WARNING: Icon source not found at $ICON_SRC — app will have no custom icon." >&2
fi

# --- Step 4: Sign (inner-to-outer) ---

ENTITLEMENTS="$MACOS_DIR/GameOfLife3D.entitlements"

echo ""
echo "Signing native libraries..."

# Sign all .dylib files
find "$MACOS_BIN_DIR" -name "*.dylib" -print0 | while IFS= read -r -d '' dylib; do
  echo "  Signing $(basename "$dylib")"
  codesign --force --options runtime \
    --entitlements "$ENTITLEMENTS" \
    --sign "$CERTIFICATE_IDENTITY" \
    --timestamp \
    "$dylib"
done

# Sign all .dll files (managed assemblies — codesign treats these as code objects)
echo ""
echo "Signing managed assemblies..."
find "$MACOS_BIN_DIR" -name "*.dll" -print0 | while IFS= read -r -d '' dll; do
  echo "  Signing $(basename "$dll")"
  codesign --force --options runtime \
    --entitlements "$ENTITLEMENTS" \
    --sign "$CERTIFICATE_IDENTITY" \
    --timestamp \
    "$dll"
done

# Sign all remaining non-executable files (json, pdb, cfg, etc.)
# Must come before the main executable since codesign treats them as subcomponents
echo ""
echo "Signing remaining files..."
MAIN_EXE="$MACOS_BIN_DIR/GameOfLife3D.NET"
find "$MACOS_BIN_DIR" -type f -print0 | while IFS= read -r -d '' file; do
  case "$file" in
    *.dylib|*.dll) continue ;;          # already signed above
    "$MAIN_EXE") continue ;;            # sign last
  esac
  echo "  Signing $(basename "$file")"
  codesign --force --options runtime \
    --entitlements "$ENTITLEMENTS" \
    --sign "$CERTIFICATE_IDENTITY" \
    --timestamp \
    "$file"
done

# Sign bundled ffmpeg (lives in Resources/, not picked up by the MACOS_BIN_DIR find)
if [[ -f "$RESOURCES_DIR/ffmpeg" ]]; then
  echo ""
  echo "Signing bundled ffmpeg..."
  codesign --force --options runtime \
    --entitlements "$ENTITLEMENTS" \
    --sign "$CERTIFICATE_IDENTITY" \
    --timestamp \
    "$RESOURCES_DIR/ffmpeg"
fi

# Sign the main executable after all subcomponents are signed
echo ""
echo "Signing main executable..."
codesign --force --options runtime \
  --entitlements "$ENTITLEMENTS" \
  --sign "$CERTIFICATE_IDENTITY" \
  --timestamp \
  "$MAIN_EXE"

echo ""
echo "Signing app bundle..."
codesign --force --options runtime \
  --entitlements "$ENTITLEMENTS" \
  --sign "$CERTIFICATE_IDENTITY" \
  --timestamp \
  "$APP_DIR"

# --- Step 5: Verify ---

echo ""
echo "Verifying code signature..."
codesign --verify --deep --strict --verbose=2 "$APP_DIR"

echo ""
echo "Checking Gatekeeper assessment..."
if spctl --assess --type execute --verbose=2 "$APP_DIR" 2>&1; then
  echo "Gatekeeper assessment passed."
else
  echo "WARNING: Gatekeeper assessment failed (expected before notarization)." >&2
fi

# --- Step 6: Create zip for notarization ---

ZIP_PATH="$BUILD_DIR/GameOfLife3D.NET-macOS-arm64.zip"
rm -f "$ZIP_PATH"

echo ""
echo "Creating zip for notarization..."
ditto -c -k --keepParent "$APP_DIR" "$ZIP_PATH"

# --- Step 7: Notarize ---

echo ""
echo "Submitting for notarization (this may take several minutes)..."
xcrun notarytool submit "$ZIP_PATH" \
  --keychain-profile "$NOTARIZATION_PROFILE" \
  --wait

# --- Step 8: Staple ---

echo ""
echo "Stapling notarization ticket..."
xcrun stapler staple "$APP_DIR"

# --- Step 9: Recreate distributable zip with stapled app ---

rm -f "$ZIP_PATH"
ditto -c -k --keepParent "$APP_DIR" "$ZIP_PATH"

echo ""
echo "Done!"
echo "  App bundle: $APP_DIR"
echo "  Distributable zip: $ZIP_PATH"
