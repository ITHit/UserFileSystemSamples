
<h1 class="d-xl-block d-none">WebDAV Drive Sample for macOS in .NET, C#</h1>
<p>This sample implements a virtual file system for macOS that displays documents from a WebDAV server. You can edit documents, upload and download documents as well as manage folders structure using macOS Finder. This sample supports synchronization, on-demand loading and selective offline files support. It synchronizes files and folders both from a WebDAV server to the local user file system and from the local user file system to the WebDAV server. This sample is written in .NET, C#.&nbsp;&nbsp;</p>
<p>You can use this sample out-of-the-box to manage documents on a WebDAV server, or you can use it as a starting point for your custom virtual drive to create&nbsp;OneDrive-like features for your DMS/CRM/ERP and reprogram it to publish data from your storage.&nbsp;</p>
<p>This sample is supplied as part of the SDK with&nbsp;<a title=".NET Client" href="https://www.webdavsystem.com/client/">IT Hit WebDAV Client Library for .NET</a>&nbsp;and with&nbsp;<a title="userfilesystem.com" href="https://www.userfilesystem.com/">IT Hit User File System</a>.</p>
<p>You can download this sample and trial licenses in the&nbsp;<a title="Download" href="https://www.webdavsystem.com/client/download/">IT Hit WebDAV Client Library product download area</a>&nbsp;and in the&nbsp;<a title="Download" href="https://www.userfilesystem.com/download/">IT Hit User File System&nbsp;</a><a title="Download" href="https://www.userfilesystem.com/download/">product download area</a>. You can also clone it or browse the code on&nbsp;<a title="WebDAV Drive Sample for macOS in .NET, C#" href="https://github.com/ITHit/UserFileSystemSamples/tree/master/macOS/WebDAVDrive">GitHub</a>.&nbsp;</p>
<p><span class="warn">This sample is provided with IT Hit User File System v6.2 and later versions.</span></p>
<h2 class="heading-link" id="nav_requirements">Requirements<a class="list-link d-inline" href="https://www.userfilesystem.com/examples/virtual_file_system_mac/#nav_requirements"></a></h2>
<ul>
<li>.NET 10 SDK and the macOS workload.</li>
<li>Xcode compatible with the installed workload, including its command-line tools. A tested combination is Xcode 26.0.1 with workload set 10.0.100 (macOS SDK 26.0.11017).</li>
<li>A Mac running a macOS version supported by that Xcode release. The built sample targets macOS 12.0 or later.</li>
<li>A valid Apple development signing certificate with its private key installed in Keychain. Debug builds generate the team-specific app-group configuration automatically.</li>
<li>The dotnet command-line tools; Visual Studio for Mac is not required.</li>
</ul>
<h2>Build Setup for .NET 10</h2>
<h3>1. Select Xcode and install the workload</h3>
<p>Install the .NET 10 SDK and a compatible Xcode first. Open Xcode once to finish setup, then select it for command-line builds:</p>
<pre><code>sudo xcode-select --switch /Applications/Xcode.app/Contents/Developer
sudo xcodebuild -runFirstLaunch
xcodebuild -version
dotnet --version</code></pre>
<p>For Xcode 26.0.1, run the following from the sample directory. This selects workload set 10.0.100 rather than the newest Apple SDK, which may require a newer Xcode:</p>
<pre><code>sudo dotnet workload install macos --version 10.0.100
dotnet workload --version
dotnet workload list</code></pre>
<p>Do not run an unversioned workload update when keeping this Xcode version. For a different Xcode release, choose the matching workload set using the .NET for macOS release notes. Do not disable Xcode version validation.</p>
<h3>2. Automatic Debug Team ID and app group</h3>
<p><strong>No manual Team ID replacement is needed for Debug builds.</strong> The build selects an installed development identity and signs a temporary local executable to obtain its actual TeamIdentifier. It does not infer the Team ID from the certificate's display name. The temporary executable is removed afterward.</p>
<p>For each app and extension project, the build generates <code>debug-signing/Info.plist</code> and <code>debug-signing/Entitlements.plist</code> under the project's intermediate output directory. It replaces <code>YOURTEAMID</code> in those generated copies with the detected team, sets <code>SampleAppGroupId</code>, and uses the copies for SDK and post-build signing. Tracked templates remain unchanged. The common storage code reads <code>SampleAppGroupId</code> from the running bundle rather than hardcoding a Team ID.</p>
<p>The app and both extensions therefore use the same <code>TEAMID.group.com.test.webdavdrive</code> group. Local macOS development with this Team-ID-prefixed group does not require creating provisioning profiles solely for the app-group entitlement or registering the sample's bundle IDs in the developer portal. A valid installed development certificate with its private key is still required. This does not apply to iOS device deployment or distribution-specific requirements. The iOS group ID and authentication configuration are unchanged.</p>
<p>Release builds do not perform this automatic configuration. Before distributing, configure your own group consistently in all three projects' entitlements and Info.plist templates (including <code>SampleAppGroupId</code> and the extensions' <code>NSExtensionFileProviderDocumentGroup</code>), signing identities, and any required profiles. If changing bundle IDs, update their references in code and project settings too. Do not commit profiles or private keys.</p>
<h3>3. Check signing and configure the server</h3>
<pre><code>security find-identity -v -p codesigning</code></pre>
<p>Debug builds automatically select the first valid Apple development identity reported by Keychain and use its exact certificate fingerprint for SDK signing, embedded libraries, the app, and both extensions. Supported names are Apple Development, Mac Developer, and iPhone Developer. No personal certificate name is stored in the sample. If no valid identity is installed, the build stops with an actionable error; it does not silently fall back to ad-hoc signing.</p>
<p>To select a different installed identity, pass <code>-p:DebugSigningIdentity=YOUR_CERTIFICATE_SHA1</code> to dotnet build, using the fingerprint reported by the command above. This single override also applies to the custom post-build signing steps. The Team ID and matching app-group configuration are generated automatically for this identity. Keep extensions sandboxed.</p>
<p>Configure the licenses, WebDAV URL, and optional WebSocket URL in <code>WebDAVMacApp/Resources/appsettings.json</code> as described below. Do not publish credentials or personal license values.</p>
<h3>4. Build and run</h3>
<p>Run these commands from <code>Sources/Samples/macOS/WebDAVDrive</code>. Use <code>osx-x64</code> on Intel or <code>osx-arm64</code> on Apple Silicon:</p>
<pre><code>dotnet build WebDAVMacApp/WebDAVMacApp.csproj -c Debug -r osx-x64
open "WebDAVMacApp/bin/Debug/net10.0-macos/osx-x64/WebDAV Drive.app"</code></pre>
<p>Replace both occurrences of <code>osx-x64</code> when building for Apple Silicon. Building the container project also builds and embeds both extensions and signs the bundles. Do not launch an extension independently. Use the app's menu-bar commands to install/mount the drive.</p>
<h3>5. Verify and troubleshoot</h3>
<pre><code>codesign --verify --deep --strict --verbose=2 "WebDAVMacApp/bin/Debug/net10.0-macos/osx-x64/WebDAV Drive.app"
codesign -dvv "WebDAVMacApp/bin/Debug/net10.0-macos/osx-x64/WebDAV Drive.app/Contents/PlugIns/WebDAVFileProviderExtension.appex"
codesign -d --entitlements - "WebDAVMacApp/bin/Debug/net10.0-macos/osx-x64/WebDAV Drive.app/Contents/PlugIns/WebDAVFileProviderExtension.appex"</code></pre>
<ul>
<li><strong>Shared-container access denied:</strong> check the signed TeamIdentifier, all group ID values, and provisioning authorization. Successful compilation or signature verification alone does not prove app-group access. Do not disable the sandbox or hardcode a container filesystem path.</li>
<li><strong>Launch fails after upgrading .NET:</strong> check Console's launchd messages for an obsolete <code>net8.0-macos</code> path. Quit the app and reopen the new build; restart macOS if its launch job remains cached. Do not delete domain data as a first troubleshooting step.</li>
<li><strong>Missing app/context-menu icon:</strong> rebuild with <code>dotnet build WebDAVMacApp/WebDAVMacApp.csproj -t:Rebuild -c Debug -r osx-x64</code>. Verify that the app's <code>Contents/Resources</code> contains <code>AppIcon.icns</code> and <code>Assets.car</code>. Keep only one asset catalog in compilation.</li>
<li><strong>Minimum OS error:</strong> both <code>SupportedOSPlatformVersion</code> and the manifest's <code>LSMinimumSystemVersion</code> must be at least 12.0 and must match.</li>
<li><strong>NuGet vulnerability warnings:</strong> review and update the affected dependencies before distribution; do not suppress audit checks to make the build appear clean.</li>
</ul>
<h2>Server Requirements</h2>
<p>This sample requires WebDAV server with collections synchronization support. The following samples support collections synchronization:</p>
<ul>
<li><a href="https://github.com/ITHit/WebDAVServerSamples/tree/master/CS/WebDAVServer.FileSystemSynchronization.AspNetCore">FileSystemSynchronization</a>&nbsp;sample supplied with IT Hit WebDAV Server Engine for .NET.</li>
<li><a href="https://github.com/ITHit/WebDAVServerSamplesJava/tree/master/Java/jakarta/collectionsync">collectionsync</a>&nbsp;sample supplied with IT Hit WebDAV Server Library for Java.</li>
</ul>
<p>You can also test this sample with IT Hit demo servers:</p>
<ul>
<li><a href="https://webdavserver.net">https://webdavserver.net</a></li>
<li><a href="https://webdavserver.com">https://webdavserver.com</a></li>
</ul>
<h2>Solution Structure</h2>
<p>The macOS sample solution consists of 3 projects: container application, an extension project, and a common code.</p>
<p>The container application provides a Menu Bar icon to install/uninstall the file system extension.&nbsp;</p>
<p>The extension project runs in the background and implements a virtual file system on macOS (File Provider). It processes requests from macOS applications sent via macOS file system API and lists folders content. The macOS extension can be installed only as part of a container application, you can not install the extension application separately.</p>
<h2>Setting License</h2>
<p><span class="warn">Note that to use the sample you need both the IT Hit WebDAV Client Library license and IT Hit User File System license.</span></p>
<p>To run the example, you will need both IT Hit WebDAV Client Library for .NET license and IT Hit User File System Engine for .NET License. You can download&nbsp;a WebDAV Client for .NET trial license in the&nbsp;<a title="Download" href="https://www.webdavsystem.com/client/download/">IT Hit WebDAV Client Library product download area</a>&nbsp;and the User File System trial license in the&nbsp;<a title="Download" href="https://www.userfilesystem.com/download/">IT Hit User File System&nbsp;</a><a title="Download" href="https://www.userfilesystem.com/download/">product download area</a>.&nbsp;Note that this sample is fully functional with a trial licenses and does not have any limitations. The trial licenses are valid for one month will stop working after this. You can check the expiration date inside the license file.&nbsp;Download the license files and specify license strings in the&nbsp;<code class="code">WebDAVClientLicense</code>&nbsp;and&nbsp;<code class="code">UserFileSystemLicense</code>&nbsp;fields respectively in&nbsp;<code class="code">WebDAVMacApp\Resources\appsettings.json</code>&nbsp;file.&nbsp;Set the license content directly as a value (NOT as a path to the license file). Do not forget to escape quotes: \":</p>
<pre class="brush:xml;auto-links:false;toolbar:false">"UserFileSystemLicense": "&lt;?xml version=\"1.0\" encoding=\"utf-8\"?&gt;&lt;License�</pre>
<p>You can also run the sample&nbsp;without explicitly specifying a license&nbsp;for 5 days. In this case,&nbsp;the&nbsp;Engine will automatically request the trial licenses from the IT Hit website https://www.userfilesystem.com. Make sure it is accessible via firewalls if any. After 5 days the Engine will stop working. To extend the trial period you will need to download trial licenses&nbsp;and specify them in&nbsp;<code class="code">appsettings.json</code></p>
<h2>Setting WebDAV Server URL</h2>
<p>To specify the WebDAV server URL edit the&nbsp;<code class="code">"WebDAVServerUrl"</code>&nbsp;parameter in&nbsp;<code class="code">appsettings.json</code>. This could be either a server root path (https://server/) or a WebDAV folder on your server (https://server/dav/).</p>
<p>For testing and demo purposes you can use one of the IT Hit demo servers. Navigate to https://webdavserver.net or to https://webdavserver.com in a web browser. Copy the URL or your test folder, that looks like https://webdavserver.net/User123456/ and specify it in the&nbsp;<code class="code">WebDAVServerUrl</code>&nbsp;parameter.</p>
<pre class="brush:html;auto-links:false;toolbar:false">"WebDAVServerUrl": "https://webdavserver.net/User123456",</pre>
<h2>Setting Web Sockets Server URL</h2>
<p>The client application receives notifications from server about changes via web sockets. To specify your web sockets server URL&nbsp;edit the&nbsp;<code class="code">"WebSocketServerUrl"</code>&nbsp;parameter in&nbsp;<code class="code">appsettings.json</code>.</p>
<p>In case you are using IT Hit demo servers, you will specify the demo server root, without user folder: wss://webdavserver.net or wss://webdavserver.com.&nbsp;</p>
<pre class="brush:html;auto-links:false;toolbar:false">"WebSocketServerUrl": "wss://webdavserver.net"</pre>
<h2 class="heading-link" id="nav_runningthesample">Running the Sample<a class="list-link d-inline" href="https://www.userfilesystem.com/examples/virtual_file_system_mac/#nav_runningthesample"></a></h2>
<p>Install the toolchain and development certificate described above before running. Debug builds automatically resolve the Team ID and app group; source templates do not need manual editing.</p>
<p>To run the sample:</p>
<ol>
<li>Build the container project with dotnet and open the resulting app bundle as shown above. The application is added to the macOS Status Bar.</li>
<li>Select 'Install Extension' command in the Status Bar.&nbsp;This will mount your WebDAV file system.</li>
</ol>
<p>Now you can manage documents using Finder, command prompt or by any other means. You can find the new file system in the 'Locations' sections in Finder.&nbsp;</p>
<p>For the development and testing convenience, when installing the extension, it will automatically open an instance of Finder with a mounted file system as well as will launch a default web browser navigating to the WebDAV server URL specified in your appsettings.json:</p>
<p><img id="__mcenew" alt="WebDAV Drive for macOS sample" src="https://www.userfilesystem.com/media/2196/webdavdrivemac.png" rel="125365"></p>
<h2 class="heading-link" id="nav_packagingthesample">Packaging the Sample</h2>
<p>The Debug commands above build a development app, not a distributable installer. Release signing and packaging targets are templates that must be configured for your distribution method. Review the Release project settings, signing targets, and <code>sign_release.sh</code> scripts; select your distribution/installer identities and provide the matching provisioning profiles where required. The WebDAV Release targets expect a local <code>embedded.provisionprofile</code> in the app and each extension project directory. Never commit those profiles or signing private keys.</p>
<p>After configuring all Release targets, build with <code>dotnet build WebDAVMacApp/WebDAVMacApp.csproj -c Release -r osx-x64</code>. Developer ID distribution outside the App Store also requires the appropriate signing/notarization workflow; a successful Debug build is not a production-deployment validation.</p>
<p>For production environment you need to create&nbsp;Group ID, App Identifies and Provisioning Profiles configuration as described in <a title="Projects Deployment on macOS" href="https://www.userfilesystem.com/examples/apple_deployment/">this article</a>.</p>
<h2>See also:</h2>
<ul>
<li><a title="Troubleshooting on macOS" href="https://www.userfilesystem.com/examples/mac_troubleshooting/">macOS File Provider Extension Troubleshooting</a></li>
<li><a title="Projects Deployment on macOS" href="https://www.userfilesystem.com/examples/apple_deployment/">macOS File Provider Extension Projects Deployment</a></li>
</ul>
<h3 class="para d-inline next-article-heading">Next Article:</h3>
<a title="WebDAV Drive Sample for iOS in .NET, C#" href="https://www.userfilesystem.com/examples/webdav_drive_ios/">WebDAV Drive Sample for iOS in .NET, C#</a>

