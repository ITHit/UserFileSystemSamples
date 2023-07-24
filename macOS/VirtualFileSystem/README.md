
<h1 class="d-xl-block d-none">Virtual File System Sample for macOS in .NET, C#</h1>
<p>This sample implements a virtual file system for macOS with synchronization support, folders on-demand listing, thumbnails and context menu support.&nbsp;It synchronizes files and folders both from remote storage to the user file system and from the user file system to remote storage. This sample is&nbsp;written in Xamarin,&nbsp;.NET/C#.&nbsp;To simulate the remote storage, this sample is using a folder in the local file system on the same machine.&nbsp;</p>
<p>The purpose of this sample is to demonstrate the major features of the IT Hit User File System for .NET on macOS and provide patterns for its programming. You will use this sample as a starting point for creating a OneDrive-like file system for macOS for your DMS/CRM/ERP and will reprogram it to publish data from your real storage instead of the local file system.</p>
<p>You can download this sample and a trial license in the&nbsp;<a title="IT Hit User File System for .NET Download" href="https://www.userfilesystem.com/download/">product download area</a>. You can also clone it and browse the code on&nbsp;<a title="Virtual File System Sample in .NET, C#" href="https://github.com/ITHit/UserFileSystemSamples/tree/master/macOS/">GitHub</a>.&nbsp;</p>
<h2>Requirements</h2>
<ul>
<li>.NET 7</li>
<li>Xcode 14.3+</li>
<li>Visual Studio Community 2022 for Mac, Stable Channel.</li>
</ul>
<h2>Solution Structure</h2>
<p>The macOS sample solution consists of 3 projects: container application, an extension project, and a common code.</p>
<p>The container application provides a Menu Bar icon to install/uninstall the file system extension. Inside the container application, you should change the hardcoded directory to replicate in your Virtual Disk manually. Consider that the extension can only access sandbox folders (including Downloads, Pictures, Music, Movies). It means that it won't be able to show the contents of the folder outside of the sandbox.</p>
<p>The extension project runs in the background and implements a virtual file system on macOS (File Provider). It processes requests from macOS applications sent via macOS file system API and lists folders content. The macOS extension can be installed only as part of a container application, you can not install the extension application by itself.</p>
<h2>Setting the License</h2>
<p>Download the license file&nbsp;<a href="https://www.userfilesystem.com/download/downloads/" rel="nofollow">here</a>. With the trial license, the product is fully functional and does not have any limitations. As soon as the trial license expires the product will stop working. Open the&nbsp;<code class="code">VirtualFilesystemMacApp/Resources/appsettings.json</code>&nbsp;file and set the&nbsp;<code class="code">UserFileSystemLicense</code>&nbsp;field.&nbsp;<span>Set the license content directly as a value (NOT as a path to the license file). Do not forget to escape quotes: \":</span></p>
<ol>
<li>
<pre class="brush:js;auto-links:false;toolbar:false">"UserFileSystemLicense": "&lt;?xml version=\"1.0\" encoding=\"utf-8\"?&gt;&lt;License…</pre>
</li>
</ol>
<p>You can also run the sample without explicitly specifying a license for 5 days. In this case, the Engine will automatically request the trial licenses from the IT Hit website https://www.userfilesystem.com. Make sure it is accessible via firewalls if any. After 5 days the Engine will stop working. To extend the trial period you will need to download trial licenses and specify them in <code class="code">appsettings.json</code></p>
<h2>Setting the Remote Storage Path</h2>
<p>Here you will set path to the folder that simulates your remote storage. Your virtual drive will mirror files and folders from this folder. Note that the extension runs as a sandboxed application and has access to a limited number of locations in the local file system. To simulate the remote storage structure you can copy the<span>&nbsp;</span><code class="code">\RemoteStorage\</code><span>&nbsp;</span>folder provided with the project under the<span>&nbsp;</span><code class="code">~/Pictures/</code><span>&nbsp;</span>folder.&nbsp;To set the remote storage directory open the&nbsp;<code class="code">VirtualFilesystemMacApp/Resources/appsettings.json</code>&nbsp;file and set<span>&nbsp;</span><code class="code">RemoteStorageRootPath</code><span>&nbsp;</span>string. Make sure to replace the ${USER} with a real user name:</p>
<pre class="brush:js;auto-links:false;toolbar:false">"RemoteStorageRootPath": "/Users/User1/Pictures/RemoteStorage"</pre>
<p>Now you are ready to compile and run the project.</p>
<h2>Running the Sample</h2>
<p><span class="warn">Note that&nbsp;this sample does NOT require&nbsp;<a title="Projects Deployment on macOS" href="https://www.userfilesystem.com/examples/apple_deployment/">Group ID, App Identifies and Provisioning Profiles configuration</a>&nbsp;for development. It is only required for production deployment.</span></p>
<p><span>To run the sample open the project in Visual Studio and run the project. The application adds an application to the macOS Status Bar. To mount the file system select the 'Install Extension' command in the Status Bar.</span></p>
<p><span><img id="__mcenew" alt="Virtual File System Mac in .NET/C#" src="https://www.userfilesystem.com/media/2117/virtulfilesystemmac.png" rel="122138"></span></p>
<p><span class="warn">Note, that every File Provider Extension runs in a sandbox, so access to the local filesystem restricted by OS except Downloads, Pictures, Music, Movies public directories.</span></p>
<h2>See also:</h2>
<ul>
<li><a title="Troubleshooting on macOS" href="https://www.userfilesystem.com/examples/mac_troubleshooting/">macOS File Provider Extension Troubleshooting</a></li>
<li><a title="Projects Deployment on macOS" href="https://www.userfilesystem.com/examples/apple_deployment/">macOS File Provider Extension Projects Deployment</a></li>
</ul>
<h3 class="para d-inline next-article-heading">Next Article:</h3>
<a title="Virtual Drive Sample for Windows" href="https://www.userfilesystem.com/examples/virtual_drive/">Virtual Drive Sample for Windows in .NET, C#</a>

