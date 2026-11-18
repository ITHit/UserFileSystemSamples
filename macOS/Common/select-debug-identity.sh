#!/usr/bin/env bash
set -euo pipefail

identity="$(/usr/bin/security find-identity -v -p codesigning | /usr/bin/awk '/"Apple Development:|"Mac Developer:|"iPhone Developer:/ && !selected { selected = $2 } END { print selected }')"
if [ -z "$identity" ]; then
  echo "No valid Apple development signing identity found. Install a development certificate and its private key in Keychain before building the macOS samples." >&2
  exit 1
fi
printf '%s\n' "$identity"
