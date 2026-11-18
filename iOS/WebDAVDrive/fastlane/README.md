fastlane documentation
----

# Installation

Make sure you have the latest version of the Xcode command line tools installed:

```sh
xcode-select --install
```

For _fastlane_ installation instructions, see [Installing _fastlane_](https://docs.fastlane.tools/#installing-fastlane)

# Available Actions

## iOS

### ios app_ids

```sh
[bundle exec] fastlane ios app_ids
```

Create or reuse both App IDs with the App Group capability enabled

### ios app_group

```sh
[bundle exec] fastlane ios app_group
```

Create or reuse the shared App Group and associate it with both App IDs

### ios certificates

```sh
[bundle exec] fastlane ios certificates
```

Create or reuse a development signing certificate

### ios add_device

```sh
[bundle exec] fastlane ios add_device
```

Register a new device in the Apple Developer Portal

### ios profiles

```sh
[bundle exec] fastlane ios profiles
```

Create or edit a development provisioning profile for each App ID

### ios signing

```sh
[bundle exec] fastlane ios signing
```

Full signing setup: App IDs, App Group, certificate and profiles

### ios build

```sh
[bundle exec] fastlane ios build
```

Build and deploy to the connected device via build-device.sh

### ios release

```sh
[bundle exec] fastlane ios release
```

Set up signing, then build and deploy

----

This README.md is auto-generated and will be re-generated every time [_fastlane_](https://fastlane.tools) is run.

More information about _fastlane_ can be found on [fastlane.tools](https://fastlane.tools).

The documentation of _fastlane_ can be found on [docs.fastlane.tools](https://docs.fastlane.tools).
