#!/usr/bin/env bash
# Build and deploy the WebDAV Drive iOS sample to a connected device.
# This requires an Apple Developer account, a valid iPhone Developer certificate,
# and a development provisioning profile that includes the App Group
# group.com.test.webdavdrive for both com.test.webdavdrive.app and com.test.webdavdrive.app.extension.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT="$SCRIPT_DIR/WebDAViOSApp/WebDAViOSApp.csproj"
CONFIG="${CONFIG:-Release}"
RUNTIME_ID="${RUNTIME_ID:-ios-arm64}"
CODESIGN_KEY="${CODESIGN_KEY:-iPhone Developer}"
CODESIGN_PROVISION="${CODESIGN_PROVISION:-Automatic}"

if [ -z "${TEAM_ID:-}" ]; then
  echo "Please set TEAM_ID to your Apple Developer Team ID, e.g.:"
  echo "  export TEAM_ID=ABCD123456"
  echo "If you do not know your Team ID, run: security find-identity -v -p codesigning"
  exit 1
fi

echo "Building WebDAV Drive for device ($RUNTIME_ID) in $CONFIG configuration..."

# Incremental device builds can keep stale *.aotdata.* files that no longer match the
# rebuilt assemblies, which makes the app abort on launch ("Failed to load AOT module").
# Always start from clean outputs; set CLEAN=false to skip.
if [ "${CLEAN:-true}" = "true" ]; then
  rm -rf "$SCRIPT_DIR/WebDAViOSApp/bin/$CONFIG/net8.0-ios/$RUNTIME_ID" \
         "$SCRIPT_DIR/WebDAViOSApp/obj/$CONFIG/net8.0-ios/$RUNTIME_ID" \
         "$SCRIPT_DIR/WebDAVFileProviderExtension/bin/$CONFIG" \
         "$SCRIPT_DIR/WebDAVFileProviderExtension/obj/$CONFIG"
fi

dotnet build "$PROJECT" \
  -c "$CONFIG" \
  -p:RuntimeIdentifier="$RUNTIME_ID" \
  -p:CodesignKey="$CODESIGN_KEY" \
  -p:CodesignProvision="$CODESIGN_PROVISION" \
  -p:CodesignRequireProvisioningProfile=true \
  "$@"

APP_BUNDLE="$SCRIPT_DIR/WebDAViOSApp/bin/$CONFIG/net8.0-ios/$RUNTIME_ID/WebDAV Drive.app"

if [ ! -d "$APP_BUNDLE" ]; then
  echo "App bundle not found at: $APP_BUNDLE"
  exit 1
fi

# --- Re-sign embedded extension + app for device ---------------------------
# The app csproj embeds the .appex AFTER the .NET build signs the app (the
# EmbedFileProviderExtension target runs AfterTargets="Build"), so on device the
# embedded extension ends up with no code signature. Re-sign the extension (and
# then re-seal the app) using the developer identity + installed profiles.
APPEX="$APP_BUNDLE/PlugIns/WebDAVFileProviderExtension.appex"
APP_BUNDLE_ID="${APP_BUNDLE_ID:-com.test.webdavdrive.app}"
EXT_BUNDLE_ID="${EXT_BUNDLE_ID:-com.test.webdavdrive.app.extension}"

# Provisioning profiles downloaded by fastlane (sigh) into this directory.
APP_PROFILE="${APP_PROFILE:-$(ls "$SCRIPT_DIR"/*"${APP_BUNDLE_ID}".mobileprovision 2>/dev/null | head -1)}"
EXT_PROFILE="${EXT_PROFILE:-$(ls "$SCRIPT_DIR"/*"${EXT_BUNDLE_ID}".mobileprovision 2>/dev/null | head -1)}"

# Developer signing identity (SHA-1). Overridable via CODESIGN_IDENTITY.
SIGN_ID="${CODESIGN_IDENTITY:-$(security find-identity -v -p codesigning \
  | grep -E "Apple Development|iPhone Developer" | head -1 | awk '{print $2}')}"

extract_entitlements() {
  # $1 = .mobileprovision, $2 = output entitlements plist
  security cms -D -i "$1" > "$2.full" 2>/dev/null
  /usr/libexec/PlistBuddy -x -c 'Print :Entitlements' "$2.full" > "$2"
  rm -f "$2.full"
}

if [ -d "$APPEX" ] && [ -n "$SIGN_ID" ] && [ -n "$APP_PROFILE" ] && [ -n "$EXT_PROFILE" ]; then
  echo "Re-signing embedded extension and app for device (identity $SIGN_ID)..."
  echo "  app profile: $APP_PROFILE"
  echo "  ext profile: $EXT_PROFILE"

  cp "$EXT_PROFILE" "$APPEX/embedded.mobileprovision"
  cp "$APP_PROFILE" "$APP_BUNDLE/embedded.mobileprovision"

  extract_entitlements "$EXT_PROFILE" "/tmp/webdav_ext_entitlements.plist"
  extract_entitlements "$APP_PROFILE" "/tmp/webdav_app_entitlements.plist"

  # Sign nested native code inside the extension first, then the extension.
  find "$APPEX" -type f -name '*.dylib' -exec \
    codesign --force --sign "$SIGN_ID" --timestamp=none {} \;
  codesign --force --sign "$SIGN_ID" --timestamp=none --generate-entitlement-der \
    --entitlements "/tmp/webdav_ext_entitlements.plist" "$APPEX"

  # Re-seal the app so its signature covers the freshly signed extension.
  codesign --force --sign "$SIGN_ID" --timestamp=none --generate-entitlement-der \
    --entitlements "/tmp/webdav_app_entitlements.plist" "$APP_BUNDLE"

  echo "Verifying signatures..."
  codesign --verify --deep --strict --verbose=2 "$APP_BUNDLE" || true
else
  echo "WARNING: skipping device re-sign (missing identity or profiles)."
  echo "  SIGN_ID=${SIGN_ID:-<none>}"
  echo "  APP_PROFILE=${APP_PROFILE:-<none>}"
  echo "  EXT_PROFILE=${EXT_PROFILE:-<none>}"
fi

# Try to deploy with devicectl (macOS 14+). Fall back to just printing the path otherwise.
if command -v xcrun >/dev/null 2>&1 && xcrun --find devicectl >/dev/null 2>&1; then
  # devicectl requires a target device. Use DEVICE_ID if provided, otherwise
  # auto-pick the first connected physical device.
  if [ -z "${DEVICE_ID:-}" ]; then
    # Pick the identifier (UUID) of the first connected iPhone/iPad.
    DEVICE_ID="$(xcrun devicectl list devices 2>/dev/null \
      | grep -iE 'iPhone|iPad' \
      | grep -oE '[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}' \
      | head -1)"
  fi

  if [ -z "${DEVICE_ID:-}" ]; then
    echo "No connected device found. Connect/unlock your device (and trust this Mac), then:"
    echo "  xcrun devicectl list devices"
    echo "  DEVICE_ID=<identifier> ./build-device.sh"
    echo "Or install manually:"
    echo "  $APP_BUNDLE"
    exit 1
  fi

  echo "Installing $APP_BUNDLE on device $DEVICE_ID..."
  xcrun devicectl device install app --device "$DEVICE_ID" "$APP_BUNDLE"
  echo "Done. Open the WebDAV Drive app on the device and tap the server URL to mount it."
else
  echo "Build complete. Install manually with Xcode or a tool such as ios-deploy:"
  echo "  $APP_BUNDLE"
fi
