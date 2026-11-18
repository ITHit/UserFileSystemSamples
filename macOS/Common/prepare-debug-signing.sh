#!/usr/bin/env bash
set -euo pipefail

identity="$1"
manifest="$2"
entitlements="$3"
output_dir="$4"
mkdir -p "$output_dir"
probe_dir="$(mktemp -d "$output_dir/probe.XXXXXX")"
trap 'rm -rf "$probe_dir"' EXIT
cp /usr/bin/true "$probe_dir/signing-probe"
if ! signing_output="$(/usr/bin/codesign --force --sign "$identity" --timestamp=none "$probe_dir/signing-probe" 2>&1)"; then
  printf '%s\n' "$signing_output" >&2
  exit 1
fi
team_id="$(/usr/bin/codesign -dvv "$probe_dir/signing-probe" 2>&1 | /usr/bin/awk -F= '/^TeamIdentifier=/ { print $2 }')"
if [[ ! "$team_id" =~ ^[A-Z0-9]{10}$ ]]; then
  echo "The selected development identity does not have a valid Apple Team ID." >&2
  exit 1
fi

/usr/bin/sed "s/YOURTEAMID/$team_id/g" "$entitlements" | /usr/bin/perl -pe 's/\r\n/\n/g' > "$output_dir/Entitlements.plist.tmp"
/usr/bin/sed "s/YOURTEAMID/$team_id/g" "$manifest" > "$output_dir/Info.plist.tmp"
app_group="$(/usr/libexec/PlistBuddy -c 'Print :com.apple.security.application-groups:0' "$output_dir/Entitlements.plist.tmp")"
if ! /usr/libexec/PlistBuddy -c "Set :SampleAppGroupId $app_group" "$output_dir/Info.plist.tmp" 2>/dev/null; then
  /usr/libexec/PlistBuddy -c "Add :SampleAppGroupId string $app_group" "$output_dir/Info.plist.tmp"
fi
for name in Info.plist Entitlements.plist; do
  if cmp -s "$output_dir/$name.tmp" "$output_dir/$name"; then
    rm "$output_dir/$name.tmp"
  else
    mv "$output_dir/$name.tmp" "$output_dir/$name"
  fi
done
printf '%s\n' "$team_id"
