#!/usr/bin/env bash
# One-shot signing setup + optional build for the WebDAV Drive iOS sample.
#
# It drives fastlane (see fastlane/Fastfile) to create — or reuse/edit if they
# already exist — the App IDs, App Group, development certificate and
# provisioning profiles, then optionally builds and deploys to a device.
#
# Usage:
#   export FASTLANE_USER="you@example.com"     # your Apple ID
#   export FASTLANE_TEAM_ID="ABCD123456"       # your 10-char Team ID
#   ./setup-signing.sh                 # signing setup only
#   ./setup-signing.sh --build         # signing setup + build & deploy
#   ./setup-signing.sh --force         # regenerate/edit existing profiles
#   ./setup-signing.sh --build --force # do both
#
# Optional:
#   export FASTLANE_PASSWORD="..."     # avoids the interactive password prompt
#   export FASTLANE_SESSION="..."      # reuse a 2FA session (see `fastlane spaceauth`)

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$SCRIPT_DIR"

DO_BUILD=false
FORCE=false
for arg in "$@"; do
  case "$arg" in
    --build) DO_BUILD=true ;;
    --force) FORCE=true ;;
    *) echo "Unknown option: $arg"; exit 1 ;;
  esac
done

# --- Preconditions ---------------------------------------------------------
if ! command -v fastlane >/dev/null 2>&1; then
  echo "fastlane is not installed. Install it with one of:"
  echo "  brew install fastlane"
  echo "  gem install fastlane"
  exit 1
fi

if [ -z "${FASTLANE_USER:-}" ]; then
  echo "Please set FASTLANE_USER to your Apple ID email, e.g.:"
  echo "  export FASTLANE_USER=you@example.com"
  exit 1
fi

if [ -z "${FASTLANE_TEAM_ID:-}" ]; then
  echo "Please set FASTLANE_TEAM_ID to your 10-char Apple Developer Team ID, e.g.:"
  echo "  export FASTLANE_TEAM_ID=ABCD123456"
  echo "Find it with: security find-identity -v -p codesigning"
  exit 1
fi

# fastlane (cert / sigh) needs full Xcode, not just the Command Line Tools.
# If the active developer dir is the CLT, point DEVELOPER_DIR at Xcode so
# xcode-select/xcrun resolve to Xcode for this run without needing sudo.
XCODE_DIR="/Applications/Xcode.app/Contents/Developer"
if [ -z "${DEVELOPER_DIR:-}" ] && [ -d "$XCODE_DIR" ]; then
  case "$(xcode-select -p 2>/dev/null)" in
    *CommandLineTools*|"") export DEVELOPER_DIR="$XCODE_DIR" ;;
  esac
fi

if [ -n "${DEVELOPER_DIR:-}" ]; then
  echo "Using Xcode at: $DEVELOPER_DIR"
elif ! xcrun --find xcodebuild >/dev/null 2>&1; then
  echo "Could not locate Xcode. Install Xcode and either:"
  echo "  export DEVELOPER_DIR=$XCODE_DIR"
  echo "  or: sudo xcode-select --switch $XCODE_DIR"
  exit 1
fi

# --- Run -------------------------------------------------------------------
# Pass "FORCE:true" to fastlane only when --force was given. The
# "${arr[@]:-}" guard keeps this working under `set -u` on bash 3.2 (macOS),
# where expanding an empty array would otherwise fail with "unbound variable".
FASTLANE_ARGS=()
$FORCE && FASTLANE_ARGS+=("FORCE:true")

if $DO_BUILD; then
  echo "Setting up signing and deploying to the connected device..."
  fastlane release ${FASTLANE_ARGS[@]:-}
else
  echo "Setting up signing (App IDs, App Group, certificate, profiles)..."
  fastlane signing ${FASTLANE_ARGS[@]:-}
  echo
  echo "Signing setup complete. To build and deploy, run:"
  echo "  ./setup-signing.sh --build"
  echo "or directly:"
  echo "  TEAM_ID=$FASTLANE_TEAM_ID ./build-device.sh"
fi
