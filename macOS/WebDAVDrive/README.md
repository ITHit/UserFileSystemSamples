
<h1 class="d-xl-block d-none">WebDAV Drive Sample for macOS in .NET, C#</h1>
<p>This sample implements a virtual file system for macOS that displays documents from a WebDAV server. You can edit documents, upload and download documents as well as manage folders structure using macOS Finder. This sample supports synchronization, on-demand loading and selective offline files support. It synchronizes files and folders both from a WebDAV server to the local user file system and from the local user file system to the WebDAV server. This sample is written in .NET, C#.&nbsp;&nbsp;</p>
<p>You can use this sample out-of-the-box to manage documents on a WebDAV server, or you can use it as a starting point for your custom virtual drive to create&nbsp;OneDrive-like features for your DMS/CRM/ERP and reprogram it to publish data from your storage.&nbsp;</p>
<p>This sample is supplied as part of the SDK with&nbsp;<a title=".NET Client" href="https://www.webdavsystem.com/client/">IT Hit WebDAV Client Library for .NET</a>&nbsp;and with&nbsp;<a title="userfilesystem.com" href="https://www.userfilesystem.com/">IT Hit User File System</a>.</p>
<p>You can download this sample and trial licenses in the&nbsp;<a title="Download" href="https://www.webdavsystem.com/client/download/">IT Hit WebDAV Client Library product download area</a>&nbsp;and in the&nbsp;<a title="Download" href="https://www.userfilesystem.com/download/">IT Hit User File System&nbsp;</a><a title="Download" href="https://www.userfilesystem.com/download/">product download area</a>. You can also clone it or browse the code on&nbsp;<a title="WebDAV Drive Sample for macOS in .NET, C#" href="https://github.com/ITHit/UserFileSystemSamples/tree/master/macOS/WebDAVDrive">GitHub</a>.&nbsp;</p>
<p><span class="warn">This sample is provided with IT Hit User File System v6.2 and later versions.</span></p>
<h2 class="heading-link" id="nav_requirements">Requirements<a class="list-link d-inline" href="https://www.userfilesystem.com/examples/virtual_file_system_mac/#nav_requirements"></a></h2>
<ul>
<li>.NET 7</li>
<li>Xcode 14.3+</li>
<li>Visual Studio Community 2022 for Mac, Stable Channel.</li>
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
<pre class="brush:xml;auto-links:false;toolbar:false">"UserFileSystemLicense": "&lt;?xml version=\"1.0\" encoding=\"utf-8\"?&gt;&lt;License…</pre>
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
<p><span class="warn">Note that&nbsp;this sample does NOT require <a title="Projects Deployment on macOS" href="https://www.userfilesystem.com/examples/apple_deployment/">Group ID, App Identifies and Provisioning Profiles configuration</a> for development. It is required only required for production deployment.</span></p>
<p>To run the sample:</p>
<ol>
<li>Open the project in Visual Studio and run the project. The application is added to the macOS Status Bar.</li>
<li>Select 'Install Extension' command in the Status Bar.&nbsp;This will mount your WebDAV file system.</li>
</ol>
<p>Now you can manage documents using Finder, command prompt or by any other means. You can find the new file system in the 'Locations' sections in Finder.&nbsp;</p>
<p>For the development and testing convenience, when installing the extension, it will automatically open an instance of Finder with a mounted file system as well as will launch a default web browser navigating to the WebDAV server URL specified in your appsettings.json:</p>
<p><img id="__mcenew" alt="WebDAV Drive for macOS sample" src="https://www.userfilesystem.com/media/2196/webdavdrivemac.png" rel="125365"></p>
<h2 class="heading-link" id="nav_packagingthesample">Packaging the Sample</h2>
<p>To create installer for testing purposes and install the sample to /Application folder follow this steps:</p>
<ol>
<li>You need Mac Developer certificate to sign app and 3rd Party Mac Developer Installer certificate to sign pkg. To get them use this <a href="https://developer.apple.com/help/account/create-certificates/create-developer-id-certificates/">guide</a>.</li>
<li>Start Release build.</li>
<li>Then open Release output folder and find WebDAV Drive signed.pkg and use this pkg to install the Sample on the same host.</li>
</ol>
<p>For production environment you need to create&nbsp;Group ID, App Identifies and Provisioning Profiles configuration as described in <a title="Projects Deployment on macOS" href="https://www.userfilesystem.com/examples/apple_deployment/">this article</a>.</p>
<h2>See also:</h2>
<ul>
<li><a title="Troubleshooting on macOS" href="https://www.userfilesystem.com/examples/mac_troubleshooting/">macOS File Provider Extension Troubleshooting</a></li>
<li><a title="Projects Deployment on macOS" href="https://www.userfilesystem.com/examples/apple_deployment/">macOS File Provider Extension Projects Deployment</a></li>
</ul>
<h3 class="para d-inline next-article-heading">Next Article:</h3>
<a title="WebDAV Drive Sample for iOS in .NET, C#" href="https://www.userfilesystem.com/examples/webdav_drive_ios/">WebDAV Drive Sample for iOS in .NET, C#</a>

