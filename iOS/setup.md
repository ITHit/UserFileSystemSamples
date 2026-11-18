cd /Users/myroslava/Documents/GitHub/fuse/Sources/Samples/iOS/WebDAVDrive

export FASTLANE_USER="you@example.com"      # your Apple ID
export FASTLANE_TEAM_ID="ABCD123456"        # 10-char Team ID

# 1) Install fastlane if needed
brew install fastlane            # or: gem install fastlane

# 2) Signing setup only (App IDs + group + cert + profiles)
./setup-signing.sh

# 3) Edit/regenerate existing profiles (e.g. after adding a device)
./setup-signing.sh --force

# 4) Full flow: signing setup + build & deploy to the connected device
./setup-signing.sh --build