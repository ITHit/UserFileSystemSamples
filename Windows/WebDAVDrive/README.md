
<h1 class="d-xl-block d-none">WebDAV Drive Sample for Windows in .NET, C#</h1>
<p>This sample implements a virtual file system for Windows that displays documents from a WebDAV server. You can edit documents, upload and download documents as well as manage folders structure using Windows File Manager. This sample provides automatic documents locking for Microsoft Office and AutoCAD documents.&nbsp;It supports synchronization, on-demand loading,&nbsp;selective offline files support as well as upload and download progress. It synchronizes files and folders both from a WebDAV server to the local user file system and from the local user file system to the WebDAV server. This sample is written in .NET, C#.&nbsp;&nbsp;</p>
<p><span>You can use this sample out-of-the-box to manage documents on a WebDAV server, or you can use it as a starting point for your custom virtual drive to create&nbsp;OneDrive-like features for your DMS/CRM/ERP and reprogram it to publish data from your storage.&nbsp;</span></p>
<p><span>This sample is supplied as part of the SDK with <a title=".NET Client" href="https://www.webdavsystem.com/client/">IT Hit WebDAV Client Library for .NET</a> and with <a title="userfilesystem.com" href="https://www.userfilesystem.com/">IT Hit User File System</a>.</span></p>
<p><span>You can download this sample and trial licenses in the&nbsp;<a title="Download" href="https://www.webdavsystem.com/client/download/">IT Hit WebDAV Client Library product download area</a> and in the&nbsp;<a title="Download" href="https://www.userfilesystem.com/download/">IT Hit User File System&nbsp;</a></span><a title="Download" href="https://www.userfilesystem.com/download/">product download area</a><span>. You can also clone it or browse the code on&nbsp;</span><a title="Virtual File System Sample in .NET, C#" href="https://github.com/ITHit/UserFileSystemSamples/tree/master/Windows/WebDAVDrive">GitHub</a><span>.&nbsp;</span><span></span></p>
<h2>Requirements</h2>
<ul>
<li>.NET 6 or later or .NET Framework 4.8.</li>
<li>Microsoft Windows 10 Creators Update or later version.</li>
<li>NTFS file system.</li>
<li>The sample supports WebDAV servers with cookies authentication, Basic, Digest, NTLM, and Kerberos authentication.</li>
<li>To connect to WebDAV servers with cookies authentication Microsoft Edge&nbsp;is required.&nbsp;</li>
</ul>
<h2>Server Requirements</h2>
<p>This sample requires WebDAV server with collections synchronization support. The following samples support collections synchronization:</p>
<ul>
<li><a href="https://github.com/ITHit/WebDAVServerSamples/tree/master/CS/WebDAVServer.FileSystemSynchronization.AspNetCore">FileSystemSynchronization</a> sample supplied with IT Hit WebDAV Server Engine for .NET.</li>
<li><a href="https://github.com/ITHit/WebDAVServerSamplesJava/tree/master/Java/jakarta/collectionsync">collectionsync</a> sample supplied with IT Hit WebDAV Server Library for Java.</li>
</ul>
<p>You can also test this sample with IT Hit demo servers:</p>
<ul>
<li><a href="https://webdavserver.net">https://webdavserver.net</a></li>
<li><a href="https://webdavserver.com">https://webdavserver.com</a></li>
</ul>
<h2>Setting License</h2>
<p><span class="warn">Note that to use the sample you need both the IT Hit WebDAV Client Library license and IT Hit User File System license.</span></p>
<p>To run the example, you will need both IT Hit WebDAV Client Library for .NET license and IT Hit User File System Engine for .NET License. You can download <span>a trial license in the&nbsp;<a title="Download" href="https://www.webdavsystem.com/client/download/">IT Hit WebDAV Client Library product download area</a>&nbsp;and in the&nbsp;<a title="Download" href="https://www.userfilesystem.com/download/">IT Hit User File System&nbsp;</a></span><a title="Download" href="https://www.userfilesystem.com/download/">product download area</a>.&nbsp;Note that this sample is fully functional with a trial license and does not have any limitations. The trial licenses are valid for one month will stop working after this. You can check the expiration date inside the license file.&nbsp;Download the licenses file and specify license strings in the&nbsp;<span><code class="code">WebDAVClientLicense</code> and&nbsp;<code class="code">UserFileSystemLicense</code></span>&nbsp;fields respectively in&nbsp;<code class="code">appsettings.json</code>&nbsp;file.&nbsp;Set the license content directly as a value (NOT as a path to the license file). Do not forget to escape quotes: \":</p>
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
<h2>Setting User File System Root Folder</h2>
<p>By default, this sample will mount the user file system under the&nbsp;<code class="code">%USERPROFILE%\DAV\</code>&nbsp;folder (typically&nbsp;<code class="code">C:\Users\&lt;username&gt;\DAV\</code>). To specify a different folder edit the&nbsp;<code class="code">"UserFileSystemRootPath"</code>&nbsp;parameter in&nbsp;<code class="code">appsettings.json</code>.</p>
<pre class="brush:html;auto-links:false;toolbar:false">"UserFileSystemRootPath": "%USERPROFILE%\\DAVv7\\",</pre>
<p>Note that this folder must be indexed, otherwise the file system may not work as expected. This is a Microsoft Windows API requirement for cloud file systems.</p>
<h2>Running the Sample</h2>
<p>To run the sample open the WebDAVDrive project in Visual Studio and run the project in debug mode.&nbsp;In the debug mode this sample provides additional support for the development and testing convenience. When starting in the debug mode, it will automatically create a folder where the virtual file system will reside, register the user file system with the platform and then open&nbsp;an instance of Windows File Manager with a mounted file system as well as will launch a default web browser navigating to the WebDAV server URL specified in your&nbsp;<code class="code">appsettings.json</code>:</p>
<p><img id="__mcenew" alt="WebDAV Drive sample launches Windows File manager with mounted virtual file system displaying content of your WebDAV server" src="https://www.userfilesystem.com/media/2103/mapdrivesample1.png" rel="121895"></p>
<p>You can start managing and editing files on your WebDAV server&nbsp;and will see all changes being propagated to the file system on the client machine. You can also edit documents and manage file structure on the client and all changes will automatically be reflected on the WebDAV server.</p>
<h2 class="heading-link" id="nav_packagingproject">Packaging Project<a class="list-link d-inline" href="https://www.userfilesystem.com/examples/virtual_drive/#nav_packagingproject"></a></h2>
<p><span class="warn">Starting with IT Hit User File System v5 Beta, the WebDAVDrive project supports identity and provides&nbsp;the same functionality as <span>WebDAVDrive</span>.Packaging project. Starting&nbsp;<span>WebDAVDrive</span> project directly registers thumbnails handler shell extension, context menu handler and custom states &amp; columns handler.</span></p>
<p>This sample provides a Windows Application Packaging Project which allows deployment of your application to the Windows Store. The&nbsp;package can be also used for direct&nbsp;deployment to users in a corporate environment or consumer environment. Start reading about various deployment scenarios in&nbsp;<a href="https://learn.microsoft.com/en-us/windows/msix/desktop/managing-your-msix-deployment-targetdevices">this article</a>.</p>
<p>To start the project with thumbnails and context menu support follow these steps:</p>
<ol>
<li>Set the packaging project as your startup project.</li>
<li>Set the VirtualDrive project under the packaging project as an Entry Point.</li>
</ol>
<p>Run the project from Visual Studio. This will automatically register COM components as well as you can start debugging COM components without additional steps.</p>
<p>The packaging project will also perform an automatic cleanup on uninstall. Your sync root registration will be automatically unregistered, folders created by the application will be deleted as well as all COM components unregistered.&nbsp;</p>
<h2>On-Demand Loading</h2>
<p>Initially, when you start the application, the user file system does not contain any file of folder placeholders, except the sync root folder. The content of the folders is populated only when any application is listing folder content. The content of files is loaded only when an application is opening a file for reading or writing.</p>
<p>After running the sample all files and folders are marked with a cloud icon<img id="__mcenew" alt="Offline File" src="https://www.userfilesystem.com/media/1988/offilefile.png" rel="116798" data-allowlink="false">, which means that the content of this file or folder is not available locally but instead resides in the remote location. Even though the file shows the correct size in the Size column, the file occupies zero bytes on the disk. You can open the file Properties dialog to check the "Size on disk" value. You can see in the console log that only root folder files and folders placeholders are being created when Windows File Manager listed the root folder content during the initial launch. Folders located deeper in the hierarchy are not loaded until their content is being requested by the platform file system calls.&nbsp;</p>
<p>When any application is accessing the file, located under the&nbsp;<code class="code">\DAV\</code>&nbsp;folder, opening it for reading or writing, the operating system redirects the call to this sample, which loads file content from the WebDAV server. The file becomes marked with a green check-mark on a white background icon<img id="__mcenew" alt="Local File" src="https://www.userfilesystem.com/media/1986/localfile.png" rel="116799" data-allowlink="false">, which means the file content is present on the local disk:</p>
<p><img id="__mcenew" alt="Local Cloud File. Green check-mark on a white background icon means the file content is present on the local disk." src="https://www.userfilesystem.com/media/1983/localcloudfile.png" rel="116801"></p>
<p>The Windows File Manager provides the "Always keep on this device" and "Free up space" context menus, which are standard menus provided by Windows OS. If you select the&nbsp;"Always keep on this device" the file or entire folder structure will be recursively loaded to the local disk, all file content will be loaded to the local disk and will become available offline. All files and folders are marked with a pinned file icon<img id="__mcenew" alt="Pinned File" src="https://www.userfilesystem.com/media/1989/pinnedfile.png" rel="116800" data-allowlink="false">. Pinned files will not be deleted from the drive even if it runs low on space.</p>
<p><img id="__mcenew" alt="Always keep on this device menu will load all files to a loacal disk and will keep them from purging on low disk space." src="https://www.userfilesystem.com/media/1982/alwayskeeponthisdevice.png" rel="116802"></p>
<p>To remove content from the local disk select the "Free up space" menu. It will restore the cloud icon<img id="__mcenew" alt="Offline File" src="https://www.userfilesystem.com/media/1988/offilefile.png" rel="116798" data-allowlink="false">.</p>
<p>When a large file is being downloaded from the WebDAV server, this sample submits progress reports to the operation system, to show a standard Windows "Downloading" dialog. At the same time, the Windows File Manager also shows progress in the files list view:</p>
<p><img id="__mcenew" alt="Large file download from the remote storage show progress reports in Windows File Manager and Files View" src="https://www.userfilesystem.com/media/1984/cloudfiledownloadprogress.png" rel="116804"></p>
<h2>Microsoft Office and AutoCAD Documents Editing</h2>
<p>This sample<span>&nbsp;supports synchronization of the Microsoft Office and AutoCAD documents, preserving all data associated with a file in your remote storage.</span></p>
<p><span>This sample automatically locks Microsoft Office and AutoCAD documents in the remote storage when a document is being opened for editing and automatically unlocks when the document is closed. When the document is opened you will see the lock icon&nbsp;<img id="__mcenew" alt="Lock icon" src="https://www.userfilesystem.com/media/2071/locked.png" rel="120785"><span>&nbsp;</span>in the Status column in Windows File Manager:</span></p>
<p><img id="__mcenew" alt="WebDAV Drive automatically locks Microsoft office document when it is being opened for editing and unlocks when closed." src="https://www.userfilesystem.com/media/2105/mapdrivesamplemsofficenarrowsmall.png" rel="121897"></p>
<p>Any temporary Microsoft Office and AutoCAD documents (~$docfile.docx, G57BURP.tmp, etc) are stored in the local file system only and are NOT synchronized to the server. Typically temporary Microsoft Office and AutoCAD documents are being automatically deleted by Microsoft office when the document editing is completed.</p>
<h2>File ETags</h2>
<p>When listing folder content the client reads file eTags and stores it as part of each placeholder. When sending modified content to the server, the client attaches the saved eTag to the request. The server compares eTags to ensure the file was not modified on the server since the file was read by the client. In the case of Microsoft Office documents, eTags provide one more level of protection against overwriting server changes in addition to locks.</p>
<h2>In-Sync Status and ETag Usage For Synchronization</h2>
<p>Synchronization in this sample is based on In-Sync file status as well as on the eTag received from the server.</p>
<p>The server to client synchronization is performed only if the file on the client is marked as In-Sync&nbsp;with the server (it is marked with<img id="__mcenew" alt="In-Sync icon" src="https://www.userfilesystem.com/media/1986/localfile.png" rel="118449">&nbsp;or&nbsp;<img id="__mcenew" alt="Pinned file" src="https://www.userfilesystem.com/media/1989/pinnedfile.png" rel="118452">&nbsp;or&nbsp;<img id="__mcenew" alt="Cloud file" src="https://www.userfilesystem.com/media/1988/offilefile.png" rel="118451">icon).</p>
<p>When any file or folder on the client is updated, it is marked as not In-Sync<img id="__mcenew" alt="Not in sync icon" src="https://www.userfilesystem.com/media/1987/notinsyncfile.png" rel="118450">, which means the content must be sent to the server. When the updated file is being sent from client to server, the server compares the eTag sent from the client with the eTag stored on the server, to avoid the server changes being overwritten. The file is updated only if eTags match. Otherwise, the file/folder is considered to be in conflict and marked with the conflict icon.</p>
<h2>Commands</h2>
<p>For debugging and development purposes this sample provides the following console commands:</p>
<ul>
<li>'Esc' - simulates app uninstall. The sync root will be unregistered and all files will be deleted.</li>
<li>'Space' - simulates machine reboot or application failure. The application exits without unregistering the sync root. All files and folders placeholders, their attributes, and states remain in the user file system.&nbsp;</li>
<li>'e' - starts/stops the Engine and all sync services.&nbsp;When the Engine is stopped, the user can still edit hydrated documents but can not hydrate files, access offline folders, or sync with remote storage.</li>
<li>'s' -&nbsp;starts/stops full synchronization service. The full synchronization service periodically syncs remote storage and user file system based on each file eTag and in-sync status.&nbsp; &nbsp;</li>
<li>'m' - starts/stops remote storage monitor. When the remote storage monitor is stopped the sample does not receive notifications from the remote storage.</li>
<li>'d' - enable/disable debug and performance logging.</li>
<li>'l' - opens a log file.</li>
<li>'b' - opens Help &amp; Support portal to submit support tickets, report bugs, suggest features.</li>
</ul>
<h2>See Also:</h2>
<ul>
<li><a title="Quick Start" href="https://www.userfilesystem.com/programming/creating_virtual_file_system/">Creating Virtual File System in .NET - Quick Start Guide</a></li>
<li><a title="Creating Virtual Drive in .NET" href="https://www.userfilesystem.com/programming/win/creating_virtual_drive/">Creating Virtual Drive in .NET - Advanced Features Implementation Guide</a><a title="Locking" href="https://www.userfilesystem.com/programming/previous_versions/v2/locking/"><br></a></li>
</ul>
<h3 class="para d-inline next-article-heading">Next Article:</h3>
<a title="WebDAV Drive Sample for macOS" href="https://www.userfilesystem.com/examples/webdav_drive_mac/">WebDAV Drive Sample for macOS in .NET, C#</a>

