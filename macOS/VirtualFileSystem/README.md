
<h1 class="d-xl-block d-none">Virtual File System Sample for macOS in .NET, C#</h1>
<p>This sample implements a virtual file system for macOS with synchronization support, folders on-demand listing, thumbnails and context menu support.&nbsp;It synchronizes files and folders both from remote storage to the user file system and from the user file system to remote storage. This sample is&nbsp;written in Xamarin,&nbsp;.NET/C#.&nbsp;To simulate the remote storage, this sample is using a folder in the local file system on the same machine.&nbsp;</p>
<p>The purpose of this sample is to demonstrate the major features of the IT Hit User File System for .NET on macOS and provide patterns for its programming. You will use this sample as a starting point for creating a OneDrive-like file system for macOS for your DMS/CRM/ERP and will reprogram it to publish data from your real storage instead of the local file system.</p>
<p>You can download this sample and a trial license in the&nbsp;<a title="IT Hit User File System for .NET Download" href="https://www.userfilesystem.com/download/">product download area</a>. You can also clone it and browse the code on&nbsp;<a title="Virtual File System Sample in .NET, C#" href="https://github.com/ITHit/UserFileSystemSamples/tree/master/macOS/">GitHub</a>.&nbsp;</p>
<h2>Requirements</h2>
<ul>
<li>.NET 10 SDK and the macOS workload.</li>
<li>Xcode compatible with the installed workload. A tested combination is Xcode 26.0.1 with workload set 10.0.100 (macOS SDK 26.0.11017).</li>
<li>A Mac running a macOS version supported by Xcode. The built sample requires macOS 12.0 or later.</li>
<li>A valid Apple development certificate with its private key installed in Keychain. Debug builds generate the team-specific app-group configuration automatically.</li>
<li>The dotnet command-line tools; Visual Studio for Mac is not required.</li>
</ul>
<h2>Build Setup for .NET 10</h2>
<h3>1. Select Xcode and install the workload</h3>
<p>Install the .NET 10 SDK and a compatible Xcode. Open Xcode once to complete setup, then run:</p>
<pre><code>sudo xcode-select --switch /Applications/Xcode.app/Contents/Developer
sudo xcodebuild -runFirstLaunch
xcodebuild -version
dotnet --version</code></pre>
<p>For Xcode 26.0.1, select the compatible workload set from the sample directory:</p>
<pre><code>sudo dotnet workload install macos --version 10.0.100
dotnet workload --version
dotnet workload list</code></pre>
<p>Do not run an unversioned workload update while keeping this Xcode version: newer workloads may require newer Xcode. For other versions, select the matching workload set using the .NET for macOS release notes. Do not disable Xcode version validation.</p>
<h3>2. Automatic Debug Team ID and signing</h3>
<p><strong>No manual Team ID replacement is needed for Debug builds.</strong> The build selects an installed development identity and obtains its actual TeamIdentifier by signing a temporary local executable, which is removed afterward. The certificate display name is not used to guess the Team ID.</p>
<p>For the app and extension, generated <code>debug-signing/Info.plist</code> and <code>debug-signing/Entitlements.plist</code> files under each project's intermediate output directory replace the template's <code>YOURTEAMID</code> with the detected team. The generated <code>SampleAppGroupId</code>, entitlement, and extension document group all use <code>TEAMID.group.com.filesystem.vfs</code>. SDK and post-build signing use these generated files; tracked templates remain unchanged. Storage reads the group ID from the current bundle's <code>SampleAppGroupId</code> entry.</p>
<p>Local macOS development using this Team-ID-prefixed group does not require provisioning profiles solely for app-group access or registration of the sample's bundle IDs in the developer portal. An installed valid development certificate and private key are still required. Distribution and iOS device deployment have separate requirements.</p>
<p>For Release builds, configure your own group in both projects' entitlements and Info.plist templates, including <code>SampleAppGroupId</code> and the extension's <code>NSExtensionFileProviderDocumentGroup</code>. Release does not generate these values automatically. Configure distribution identities and profiles as required; if changing bundle IDs, update their references consistently. Never commit private keys or profiles.</p>
<pre><code>security find-identity -v -p codesigning</code></pre>
<p>Debug builds automatically select the first valid Apple development identity reported by Keychain and use its exact fingerprint for SDK signing, embedded libraries, the app, and extension. Supported names are Apple Development, Mac Developer, and iPhone Developer. The repository contains no personal certificate name. With no installed valid development identity, the build stops with an actionable error rather than falling back to ad-hoc signing.</p>
<p>If several identities are installed, select one with <code>-p:DebugSigningIdentity=YOUR_CERTIFICATE_SHA1</code> on dotnet build, using the fingerprint reported above. The override also applies to the custom signing scripts. The Team ID and matching app-group configuration are generated automatically for this identity. Keep the extension sandboxed.</p>
<h3>3. Configure storage and build</h3>
<p>Set the license and <code>RemoteStorageRootPath</code> in <code>VirtualFileSystemMacApp/Resources/appsettings.json</code> as described below. Use storage that the sandboxed extension is permitted to access, and do not publish personal license values.</p>
<p>Run from <code>Sources/Samples/macOS/VirtualFileSystem</code>. For Intel:</p>
<pre><code>dotnet build VirtualFileSystemMacApp/VirtualFileSystemMacApp.csproj -c Debug -r osx-x64
open "VirtualFileSystemMacApp/bin/Debug/net10.0-macos/osx-x64/Virtual Filesystem Sample.app"</code></pre>
<p>For Apple Silicon, replace both occurrences of <code>osx-x64</code> with <code>osx-arm64</code>. Building the container project builds and embeds the extension and signs the bundles. Launch the container app, not the extension, and use the menu-bar Install Extension command to mount the drive.</p>
<h3>4. Verify and troubleshoot</h3>
<pre><code>codesign --verify --deep --strict --verbose=2 "VirtualFileSystemMacApp/bin/Debug/net10.0-macos/osx-x64/Virtual Filesystem Sample.app"
codesign -dvv "VirtualFileSystemMacApp/bin/Debug/net10.0-macos/osx-x64/Virtual Filesystem Sample.app/Contents/PlugIns/FileProviderExtension.appex"
codesign -d --entitlements - "VirtualFileSystemMacApp/bin/Debug/net10.0-macos/osx-x64/Virtual Filesystem Sample.app/Contents/PlugIns/FileProviderExtension.appex"</code></pre>
<ul>
<li><strong>Shared-container access denied:</strong> check the signed TeamIdentifier, matching group IDs, and provisioning authorization. Signature verification and successful compilation alone do not prove app-group access. Do not disable the sandbox or bypass container resolution with a hardcoded path.</li>
<li><strong>Extension launch failure after upgrading .NET:</strong> inspect launchd messages in Console for an obsolete <code>net8.0-macos</code> path. Quit and reopen the new app build; restart macOS if its launch job remains cached. Do not delete domain data as a first step.</li>
<li><strong>Missing icons:</strong> run <code>dotnet build VirtualFileSystemMacApp/VirtualFileSystemMacApp.csproj -t:Rebuild -c Debug -r osx-x64</code>. The app's <code>Contents/Resources</code> should contain <code>AppIcon.icns</code> and <code>Assets.car</code>. Do not include duplicate asset catalogs.</li>
<li><strong>Minimum OS error:</strong> keep <code>SupportedOSPlatformVersion</code> and <code>LSMinimumSystemVersion</code> at matching values of 12.0 or later.</li>
<li><strong>NuGet vulnerability warnings:</strong> review and update affected dependencies before distribution; do not suppress the audit checks.</li>
</ul>
<h3>Release builds</h3>
<p>The commands above build a development app, not a production installer. Release settings and <code>sign_release.sh</code> scripts must be configured for your own distribution identity, installer identity where applicable, and matching profiles. The existing <code>Mac Developer</code> selectors are legacy placeholders, not a universal distribution configuration. Developer ID distribution outside the App Store requires the appropriate signing and notarization workflow. Never commit profiles or private keys.</p>
<h2>Solution Structure</h2>
<p>The macOS sample solution consists of 3 projects: container application, an extension project, and a common code.</p>
<p>The container application provides a Menu Bar icon to install/uninstall the file system extension. Inside the container application, you should change the hardcoded directory to replicate in your Virtual Disk manually. Consider that the extension can only access sandbox folders (including Downloads, Pictures, Music, Movies). It means that it won't be able to show the contents of the folder outside of the sandbox.</p>
<p>The extension project runs in the background and implements a virtual file system on macOS (File Provider). It processes requests from macOS applications sent via macOS file system API and lists folders content. The macOS extension can be installed only as part of a container application, you can not install the extension application by itself.</p>
<h2>Setting the License</h2>
<p>Download the license file&nbsp;<a href="https://www.userfilesystem.com/download/downloads/" rel="nofollow">here</a>. With the trial license, the product is fully functional and does not have any limitations. As soon as the trial license expires the product will stop working. Open the&nbsp;<code class="code">VirtualFilesystemMacApp/Resources/appsettings.json</code>&nbsp;file and set the&nbsp;<code class="code">UserFileSystemLicense</code>&nbsp;field.&nbsp;<span>Set the license content directly as a value (NOT as a path to the license file). Do not forget to escape quotes: \":</span></p>
<ol>
<li>
<pre class="brush:js;auto-links:false;toolbar:false">"UserFileSystemLicense": "&lt;?xml version=\"1.0\" encoding=\"utf-8\"?&gt;&lt;License�</pre>
</li>
</ol>
<p>You can also run the sample without explicitly specifying a license for 5 days. In this case, the Engine will automatically request the trial licenses from the IT Hit website https://www.userfilesystem.com. Make sure it is accessible via firewalls if any. After 5 days the Engine will stop working. To extend the trial period you will need to download trial licenses and specify them in <code class="code">appsettings.json</code></p>
<h2>Setting the Remote Storage Path</h2>
<p>Here you will set path to the folder that simulates your remote storage. Your virtual drive will mirror files and folders from this folder. Note that the extension runs as a sandboxed application and has access to a limited number of locations in the local file system. To simulate the remote storage structure you can copy the<span>&nbsp;</span><code class="code">\RemoteStorage\</code><span>&nbsp;</span>folder provided with the project under the<span>&nbsp;</span><code class="code">~/Pictures/</code><span>&nbsp;</span>folder.&nbsp;To set the remote storage directory open the&nbsp;<code class="code">VirtualFilesystemMacApp/Resources/appsettings.json</code>&nbsp;file and set<span>&nbsp;</span><code class="code">RemoteStorageRootPath</code><span>&nbsp;</span>string. Make sure to replace the ${USER} with a real user name:</p>
<pre class="brush:js;auto-links:false;toolbar:false">"RemoteStorageRootPath": "/Users/User1/Pictures/RemoteStorage"</pre>
<p>Now you are ready to compile and run the project.</p>
<h2>Running the Sample</h2>
<p>Install the toolchain and development certificate described above, then build and open the container app with the documented dotnet and open commands. The application adds an icon to the macOS Status Bar. To mount the file system select the 'Install Extension' command in the Status Bar.</p>
<p><span><img id="__mcenew" alt="Virtual File System Mac in .NET/C#" src="https://www.userfilesystem.com/media/2117/virtulfilesystemmac.png" rel="122138"></span></p>
<p><span class="warn">Note, that every File Provider Extension runs in a sandbox, so access to the local filesystem restricted by OS except Downloads, Pictures, Music, Movies public directories.</span></p>
<h2>See also:</h2>
<ul>
<li><a title="Troubleshooting on macOS" href="https://www.userfilesystem.com/examples/mac_troubleshooting/">macOS File Provider Extension Troubleshooting</a></li>
<li><a title="Projects Deployment on macOS" href="https://www.userfilesystem.com/examples/apple_deployment/">macOS File Provider Extension Projects Deployment</a></li>
</ul>
<h3 class="para d-inline next-article-heading">Next Article:</h3>
<a title="Virtual Drive Sample for Windows" href="https://www.userfilesystem.com/examples/virtual_drive/">Virtual Drive Sample for Windows in .NET, C#</a>

