rm -R "$1/root"
mkdir "$1/root"
cp -R "$1/WebDAV Drive.app" "$1/root"

cat <<EOF > $1/root/pkg.plist
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<array>
	<dict>
		<key>BundleHasStrictIdentifier</key>
		<true/>
		<key>BundleIsRelocatable</key>
		<false/>
		<key>BundleIsVersionChecked</key>
		<true/>
		<key>BundleOverwriteAction</key>
		<string>upgrade</string>
		<key>ChildBundles</key>
		<array>
			<dict>
				<key>BundleOverwriteAction</key>
				<string></string>
				<key>RootRelativeBundlePath</key>
				<string>WebDAV Drive.app/Contents/PlugIns/WebDAVFileProviderExtension.appex</string>
			</dict>
			<dict>
				<key>BundleOverwriteAction</key>
				<string></string>
				<key>RootRelativeBundlePath</key>
				<string>WebDAV Drive.app/Contents/PlugIns/WebDAVFileProviderUIExtension.appex</string>
			</dict>
		</array>
		<key>RootRelativeBundlePath</key>
		<string>WebDAV Drive.app</string>
	</dict>
</array>
</plist>
EOF

pkgbuild --root "$1/root" --version 1 --install-location /Applications  --component-plist "$1/root/pkg.plist" "$1/WebDAV Drive.pkg"
productsign --sign  "3rd Party Mac Developer Installer" "$1/WebDAV Drive.pkg" "$1/WebDAV Drive signed.pkg"
