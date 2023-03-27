
<h1 class="d-xl-block d-none">WebDAV Drive Sample for macOS in .NET, C#</h1>
<p>This sample implements a virtual file system for macOS that displays documents from a WebDAV server. You can edit documents, upload and download documents as well as manage folders structure using macOS Finder. This sample supports synchronization, on-demand loading and selective offline files support. It synchronizes files and folders both from a WebDAV server to the local user file system and from the local user file system to the WebDAV server. This sample is written in .NET, C#.&nbsp;&nbsp;</p>
<p>You can use this sample out-of-the-box to manage documents on a WebDAV server, or you can use it as a starting point for your custom virtual drive to create&nbsp;OneDrive-like features for your DMS/CRM/ERP and reprogram it to publish data from your storage.&nbsp;</p>
<p>This sample is supplied as part of the SDK with&nbsp;<a title=".NET Client" href="https://www.webdavsystem.com/client/">IT Hit WebDAV Client Library for .NET</a>&nbsp;and with&nbsp;<a title="userfilesystem.com" href="https://www.userfilesystem.com/">IT Hit User File System</a>.</p>
<p>You can download this sample and trial licenses in the&nbsp;<a title="Download" href="https://www.webdavsystem.com/client/download/">IT Hit WebDAV Client Library product download area</a>&nbsp;and in the&nbsp;<a title="Download" href="https://www.userfilesystem.com/download/">IT Hit User File System&nbsp;</a><a title="Download" href="https://www.userfilesystem.com/download/">product download area</a>. You can also clone it or browse the code on&nbsp;<a title="WebDAV Drive Sample for macOS in .NET, C#" href="https://github.com/ITHit/UserFileSystemSamples/tree/master/macOS/WebDAVDrive">GitHub</a>.&nbsp;</p>
<p><span class="warn">This sample is provided with IT Hit User File System v6.2 and later versions.</span></p>
<h2 class="heading-link" id="nav_requirements">Requirements<a class="list-link d-inline" href="https://www.userfilesystem.com/examples/virtual_file_system_mac/#nav_requirements"></a></h2>
<ul>
<li>macOS 11.0+</li>
<li>Mono 6.12.0.122+</li>
<li>Xamarin.Mac: 7.4.0.10+</li>
<li>Xcode 12.4+</li>
<li>Visual Studio Community 2019 for Mac v8.8.10+, Stable Channel.</li>
</ul>
<h2>Solution Structure</h2>
<p>The macOS sample solution consists of 3 projects: container application, an extension project, and a common code.</p>
<p>The container application provides a Menu Bar icon to install/uninstall the file system extension.&nbsp;</p>
<p>The extension project runs in the background and implements a virtual file system on macOS (File Provider). It processes requests from macOS applications sent via macOS file system API and lists folders content. The macOS extension can be installed only as part of a container application, you can not install the extension application by itself.</p>
<h2 class="heading-link" id="nav_creatinggroupidappidentifiesandprovisioningprofiles"><span>Creating Group ID, App Identifies and Provisioning Profiles</span><a class="list-link d-inline" href="https://www.userfilesystem.com/examples/virtual_file_system_mac/#nav_creatinggroupidappidentifiesandprovisioningprofiles"></a></h2>
<p>In the following steps, we will describe how to configure and run this sample in the development environment. You will create an Apple group ID, Apple app identifies, and Apple provisioning profiles. Then you will update the sample container application project and extension project to use the created IDs and profiles.</p>
<p>Log-in to the Apple developer account here:&nbsp;<a href="https://developer.apple.com/" rel="nofollow">https://developer.apple.com/</a>. To complete the steps below you must have an App Manager role.</p>
<ol>
<li>
<p><strong>Create App Group.</strong>&nbsp;Navigate to Certificates, IDs, Profiles -&gt; Identifiers -&gt; App Groups and create a new group.</p>
</li>
<li>
<p><strong>Create Apple macOS App IDs.</strong>&nbsp;Navigate to Certificates, IDs, Profile -&gt; Identifiers -&gt; App IDs. Create 2 identifiers that will be unique for your project. One will be used for container application another – for the extension.</p>
</li>
<li>
<p><strong>Add app identifiers to the group.</strong>&nbsp;Add both identifiers created in Step 2 to the group created in Step 1. Select identifier and click on Edit. Then check the App Groups checkbox, select the Edit button and select the group created in Step 1.</p>
</li>
<li>
<p><span><strong>Create Provisioning Profiles</strong>.</span>&nbsp;Navigate to Certificates, Identifiers &amp; Profiles -&gt; Profiles -&gt; Development and create profile. Associate profile with extension ID and container ID respectively.</p>
</li>
<li>
<p><strong>Download profiles and certificates in XCode.</strong>&nbsp;Run XCode and go to Xcode Menu &gt; Preferences -&gt; Accounts tab. Select team and click on “Download Manual Profiles”. You can find more detailed instructions:&nbsp;<a href="https://docs.microsoft.com/en-us/xamarin/mac/deploy-test/publishing-to-the-app-store/profiles" rel="nofollow">here</a></p>
</li>
<li>
<p><strong>Set bundle identifier name in Container project.</strong>&nbsp;The bundle identifier is located in WebDAVMacApp/Info.plist file. You can edit it either in Visual Studio or directly in Info.plist file in the CFBundleIdentifier field (by default it is set to<span>&nbsp;</span><code class="code">com.userfilesystem.vfs.app</code>). You must set this identifier to the value specified in Step 2.</p>
</li>
<li>
<p><strong>Set bundle identifier name in the Extension project.</strong>&nbsp;The bundle identifier is located in WebDAVFileProviderExtension/Info.plist file. You can edit it either in Visual Studio or directly in Info.plist file in the CFBundleIdentifier field (by default it is set to<span>&nbsp;</span><code class="code">com.userfilesystem.vfs.app.extension</code>). You must set this identifier to the value specified in Step 2.</p>
</li>
<li>
<p><strong>Configure macOS bundle signing in Container and Extension projects.</strong>&nbsp;For each project in Visual Studio go to the project Options. Select Mac Signing and check 'Sign the application bundle'. Select Identity and Provisioning profile.</p>
</li>
<li>
<p><strong>Configure application permissions in Container and Extension projects.</strong>&nbsp;Select Entitlements.plist and select group created in Step 1 in App Groups field for each project.</p>
</li>
<li>
<p><strong>Set App Group ID in code.</strong>&nbsp;Edit<span>&nbsp;</span><code class="code">AppGroupsSettings.cs</code>&nbsp;file located in<span>&nbsp;</span><code class="code">/WebDAVCommon/</code>&nbsp;folder. Specify AppGroupId.</p>
</li>
</ol>
<h2>Setting the License</h2>
<p><span class="warn">Note that to use the sample you need both the IT Hit WebDAV Client Library license and IT Hit User File System license.</span></p>
<p>To run the example, you will need both IT Hit WebDAV Client Library for .NET license and IT Hit User File System Engine for .NET License. You can download&nbsp;a WebDAV Client for .NET trial license in the&nbsp;<a title="Download" href="https://www.webdavsystem.com/client/download/">IT Hit WebDAV Client Library product download area</a>&nbsp;and the User File System trial license in the&nbsp;<a title="Download" href="https://www.userfilesystem.com/download/">IT Hit User File System&nbsp;</a><a title="Download" href="https://www.userfilesystem.com/download/">product download area</a>.&nbsp;Note that this sample is fully functional with a trial licenses and does not have any limitations. The trial licenses are valid for one month will stop working after this. You can check the expiration date inside the license file.&nbsp;Download the license files and specify license strings in the&nbsp;<code class="code">WebDAVClientLicense</code>&nbsp;and&nbsp;<code class="code">UserFileSystemLicense</code>&nbsp;fields respectively in&nbsp;<code class="code">WebDAVMacApp\Resources\appsettings.json</code>&nbsp;file.&nbsp;Set the license content directly as a value (NOT as a path to the license file). Do not forget to escape quotes: \":</p>
<pre class="brush:xml;auto-links:false;toolbar:false">"UserFileSystemLicense": "&lt;?xml version=\"1.0\" encoding=\"utf-8\"?&gt;&lt;License…</pre>
<p>You can also run the sample&nbsp;without explicitly specifying a license&nbsp;for 5 days. In this case,&nbsp;the&nbsp;Engine will automatically request the trial licenses from the IT Hit website https://www.userfilesystem.com. Make sure it is accessible via firewalls if any. After 5 days the Engine will stop working. To extend the trial period you will need to download trial licenses&nbsp;and specify them in&nbsp;<code class="code">appsettings.json</code></p>
<h2>Setting WebDAV Server URL</h2>
<p>To specify the WebDAV server URL edit the&nbsp;<code class="code">"WebDAVServerUrl"</code>&nbsp;parameter in&nbsp;<code class="code">appsettings.json</code>. This could be either a server root path (https://server/) or a WebDAV folder on your server (https://server/dav/).</p>
<p>For testing and demo purposes you can use one of the IT Hit demo servers. Navigate to https://webdavserver.net or to https://webdavserver.com in a web browser. Copy the URL or your test folder, that looks like https://webdavserver.net/User123456/ and specify it in the&nbsp;<code class="code">WebDAVServerUrl</code>&nbsp;parameter.</p>
<h2 class="heading-link" id="nav_runningthesample">Running the Sample<a class="list-link d-inline" href="https://www.userfilesystem.com/examples/virtual_file_system_mac/#nav_runningthesample"></a></h2>
<p>To run the sample&nbsp;</p>
<ol>
<li>Open the project in Visual Studio and run the project. The application is added the macOS Status Bar.</li>
<li>Select 'Install Extension' command in the Status Bar.&nbsp;This will mount your WebDAV file system.</li>
</ol>
<p>Now you can manage documents using Finder, command prompt or by any other means. You can find the new file system in the 'Locations' sections in Finder.&nbsp;</p>

