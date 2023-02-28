
<h1 class="d-xl-block d-none">Virtual Drive Sample for Windows in .NET, C#</h1>
<p>This is a virtual drive implementation for Windows with thumbnail support, custom context menu and custom states &amp; columns support. It also demonstrates automatic Microsoft Office/AutoCAD documents locking.&nbsp;</p>
<p>To simulate the remote storage, this sample is using a folder in the local file system on the same machine.&nbsp;This sample supports all basic synchronization features provided by the <a title="Virtual File System Sample for Windows" href="https://www.userfilesystem.com/examples/virtual_file_system/">Virtual File System</a> sample: folders&nbsp;on-demand listing, files on-demand content loading, selective offline files support, hydration progress. The sample is written in C#/.NET.</p>
<p><span>This sample supports application identity (sparce package) and provides a packaging project for deployment to Windows store.</span></p>
<p><span>You can download this sample and a trial license in the&nbsp;</span><a title="Download" href="https://www.userfilesystem.com/download/">product download area</a>&nbsp;as well as you can clone it from<span>&nbsp;</span><a title="Virtual Drive Sample in .NET, C#" href="https://github.com/ITHit/UserFileSystemSamples/tree/master/Windows/VirtualDrive">GitHub</a><span>.&nbsp;</span></p>
<p><span class="warn">This sample is provided with IT Hit User File System v3 Beta and later versions.</span></p>
<h2 class="heading-link" id="nav_requirements">Requirements<a class="list-link d-inline" href="https://www.userfilesystem.com/examples/virtual_file_system/#nav_requirements"></a></h2>
<ul>
<li>.NET 6 or later or .NET Framework 4.8.</li>
<li>Microsoft Windows 10 Creators Update or later version.</li>
<li>NTFS file system.</li>
</ul>
<h2 class="heading-link" id="nav_configuringthesample">Configuring the Sample<a class="list-link d-inline" href="https://www.userfilesystem.com/examples/virtual_file_system/#nav_configuringthesample"></a></h2>
<p>By default, the sample will use the&nbsp;<code class="code">\RemoteStorage\</code>&nbsp;folder, located under the project root, to simulate the remote storage file structure. It will mount the user file system under the&nbsp;<code class="code">%USERPROFILE%\VFS\</code>&nbsp;folder (typically&nbsp;<code class="code">C:\Users\&lt;username&gt;\VirtualDrive\</code>).</p>
<p>To specify the folder that will be used for remote storage simulation edit the&nbsp;<code class="code">"RemoteStorageRootPath"</code>&nbsp;parameter in&nbsp;<code class="code">appsettings.json</code>. This could be either an absolute path or a path relative to the application root.</p>
<p>To specify the user file system folder edit the&nbsp;<code class="code">"UserFileSystemRootPath"</code>&nbsp;parameter&nbsp;in&nbsp;<code class="code">appsettings.json</code>.</p>
<h2 class="heading-link" id="nav_settingthelicense">Setting the License<a class="list-link d-inline" href="https://www.userfilesystem.com/examples/virtual_file_system/#nav_settingthelicense"></a></h2>
<p>To run the example, you will need a valid IT Hit User File System Engine for .NET License. You can download the license in&nbsp;the&nbsp;<a title="IT Hit User File System for .NET Download" href="https://www.userfilesystem.com/download/">product download area</a>.&nbsp;Note that the Engine is fully functional with a trial license and does not have any limitations. The trial license is valid for one month and the engine will stop working after this. You can check the expiration date inside the license file.&nbsp;Download the license file and specify its content in the&nbsp;<code class="code">UserFileSystemLicense</code>&nbsp;field in&nbsp;<code class="code">appsettings.json</code>&nbsp;file.&nbsp;Set the license content directly as a value (NOT as a path to the license file). Do not forget to escape quotes: \":</p>
<pre class="brush:xml;auto-links:false;toolbar:false">"UserFileSystemLicense": "&lt;?xml version=\"1.0\" encoding=\"utf-8\"?&gt;&lt;License…</pre>
<p>You can also run the sample&nbsp;without explicitly specifying a license&nbsp;for 5 days. In this case,&nbsp;the&nbsp;Engine will automatically request the trial license from the IT Hit website https://www.userfilesystem.com. Make sure it is accessible via firewalls if any. After 5 days the Engine will stop working. To extend the trial period you will need to download a license in a&nbsp;<a title="IT Hit User File System for .NET Download" href="https://www.userfilesystem.com/download/">product download area</a>&nbsp;and specify it in&nbsp;<code class="code">appsettings.json</code></p>
<h2 class="heading-link" id="nav_runningthesample">Running the Sample<a class="list-link d-inline" href="https://www.userfilesystem.com/examples/virtual_file_system/#nav_runningthesample"></a></h2>
<p>To run the sample open the project in Visual Studio and run the project in debug mode. When starting in the debug mode, it will automatically create a folder in which the virtual file system will reside, register the virtual drive with the platform and then open&nbsp;two instances of Windows File Manager, one of which will show a virtual drive and another a folder simulating remote storage.&nbsp;</p>
<p>You can find more about running and stopping the sample as well as about basic synchronization features in the&nbsp;<a title="Virtual File System Sample for Windows" href="https://www.userfilesystem.com/examples/virtual_file_system/">Virtual File System</a>&nbsp;sample description.&nbsp;</p>
<h2><span>Shell Extensions Support</span></h2>
<p>This sample provides thumbnails handler, context menu handler and custom states and properties handler.&nbsp;<span>All handlers are registered as an application extension by the packing project provided with the sample or as a sparse package manifest of the main application. To register handlers you will simply run either VirtualDrive project directly or the VirtualDrive.Package project. You do NOT need to register the handlers using regsrv32 or any using any other COM registration technique.</span></p>
<p><span>The COM handlers are automatically unregistered on package uninstall, you do not need to unregister them manually.</span></p>
<h3>Thumbnails Support</h3>
<p><span>The Virtual Drive sample provides <a title="Creating Thumbnails Provider" href="https://www.userfilesystem.com/programming/creating_thumbnails_provider/">thumbnail provider shell extension implementation</a> in the COM object. It loads thumbnails from files located in the remote storage simulation folder and displays them in Windows Explorer. You will adapt the code to load thumbnails from your real remote storage.</span></p>
<p><img id="__mcenew" alt="Virtual Drive thumbnails support in Windows Explorer" src="https://www.userfilesystem.com/media/2147/windowsexplorerthumbnailsmode.png" rel="123092"></p>
<h3>Context Menu Support</h3>
<p>This sample provides a context menu provider that implements manual locking and unlocking:</p>
<p><img id="__mcenew" alt="Virtual drive context menu handler provider" src="https://www.userfilesystem.com/media/2182/virtualdrivecustomcontextmenuhandler.png" rel="123964"></p>
<p><span class="warn">Note that context menu support on Window 11 requires application or package identity. The sample is provided with a test developer certificate for this purpose. You must replace the certificate with your own certificate prior to deployment.</span></p>
<p>See more details on on implementing and registering context menu in the&nbsp;<a title="Creating Context Menu" href="https://www.userfilesystem.com/programming/creating_context_menu/">Creating Custom Windows Explorer Context Menu Shell Extension</a> article.</p>
<h3>Window File Manger Custom States &amp; Columns Support</h3>
<p><span>This sample registers and displays custom states &amp; columns in Windows File Manager. For demo purposes the Registrar class adds ETag column as well as columns that show information about the lock: Lock Owner, Lock Scope, Lock Expires:</span></p>
<p><span>&nbsp;&nbsp;<img id="__mcenew" alt="Virtual Drive custom columns being displayed in Windows File Manger" src="https://www.userfilesystem.com/media/2132/customcolumnswindowsfilemanager.png" rel="122440"></span></p>
<p>See more information about how to program and register the custom states and columns handler in&nbsp;<a title="Creating States &amp; Columns Provider" href="https://www.userfilesystem.com/programming/creating_custom_states_columns_provider/">Creating Custom States and Columns Provider Shell Extension for Virtual Drive</a><span>&nbsp;</span>article.</p>
<h2>Automatic Locking of Microsoft Office and AutoCAD Files</h2>
<p><span>This sample automatically locks the Microsoft Office and AutoCAD documents in the remote storage when a document is being opened for editing and automatically unlocks the document when the file is closed. When the document is opened you will see the lock icon&nbsp;<img id="__mcenew" alt="Lock icon" src="https://www.userfilesystem.com/media/2071/locked.png" rel="120785"> in the Status column in Windows File Manager:</span></p>
<p><span><img id="__mcenew" alt="Virtual Drive sample shows lock icon for Microsoft Office documents" src="https://www.userfilesystem.com/media/2133/virtualdrivemsoffice.png" rel="122441"></span></p>
<p><span>The information about the lock (lock-token, etc.) is being saved on the client machine when the document is locked.</span>&nbsp;When a document is modified on the client,&nbsp;all changes are sent to the remote storage, together with the lock-token and eTag.&nbsp;</p>
<p>You can find more more about locking programming <a title="Creating Virtual Drive in .NET" href="https://www.userfilesystem.com/programming/creating_virtual_drive/#nav_locking">in this section</a>.&nbsp;</p>
<h2>Packaging Project</h2>
<p><span class="warn">Starting with IT Hit User File System v5 Beta, the VirtualDrive project supports identity and provides&nbsp;the same functionality as VirtualDrive.Packaging project. Starting VirtualDrive project directly registers thumbnails handler shell extension, context menu handler and custom states &amp; columns handler.</span></p>
<p>This sample provides a Windows Application Packaging Project which allows deployment of your application to the Windows Store. The&nbsp;package can be also used for direct&nbsp;deployment to users in a corporate environment or consumer environment. Start reading about various deployment scenarios in <a href="https://learn.microsoft.com/en-us/windows/msix/desktop/managing-your-msix-deployment-targetdevices">this article</a>.</p>
<p>To start the project with thumbnails and context menu support follow these steps:</p>
<ol>
<li>Set the packaging project as your startup project.</li>
<li>Set the VirtualDrive project under the packaging project as an Entry Point.</li>
</ol>
<p>Run the project from Visual Studio. This will automatically register COM components.</p>
<p>The packaging project will perform an automatic cleanup on uninstall. Your sync root registration will be automatically unregistered, folders created by the application will be deleted as well as all COM components unregistered.&nbsp;</p>
<h2>See also:</h2>
<ul>
<li><a title="Creating Thumbnails Provider" href="https://www.userfilesystem.com/programming/creating_thumbnails_provider/">Creating Thumbnails Provider Shell Extension</a></li>
<li><a title="Creating Context Menu" href="https://www.userfilesystem.com/programming/creating_context_menu/">Creating Custom Windows Explorer Context Menu Shell Extension</a></li>
<li><a title="Creating States &amp; Columns Provider" href="https://www.userfilesystem.com/programming/creating_custom_states_columns_provider/">Creating Custom States and Columns Provider Shell Extension</a></li>
</ul>
<p>&nbsp;</p>
<h3 class="para d-inline next-article-heading">Next Article:</h3>
<a title="WebDAV Drive Sample for Windows in .NET, C#" href="https://www.userfilesystem.com/examples/webdav_drive/">WebDAV Drive Sample for Windows in .NET, C#</a>

