
<h1 class="d-xl-block d-none">Virtual File System Sample for Mac in .NET, C#</h1>
<p>This sample implements a virtual file system for Mac with synchronization support, folders on-demand listing, thumbnails and context menu support.&nbsp;It synchronizes files and folders both from remote storage to the user file system and from the user file system to remote storage. This sample is&nbsp;written in Xamarin,&nbsp;.NET/C#.&nbsp;To simulate the remote storage, this sample is using a folder in the local file system on the same machine.&nbsp;</p>
<p>The purpose of this sample is to demonstrate the major features of the IT Hit User File System for .NET on macOS and provide patterns for its programming. You will use this sample as a starting point for creating a OneDrive-like file system for macOS for your DMS/CRM/ERP and will reprogram it to publish data from your real storage instead of the local file system.</p>
<p>You can download this sample and a trial license in the&nbsp;<a title="IT Hit User File System for .NET Download" href="https://www.userfilesystem.com/download/">product download area</a>. You can also clone it and browse the code on&nbsp;<a title="Virtual File System Sample in .NET, C#" href="https://github.com/ITHit/UserFileSystemSamples/tree/master/macOS/">GitHub</a>.&nbsp;</p>
<h2>Requirements</h2>
<ul>
<li>macOS 11.0+</li>
<li>Mono 6.12.0.122+</li>
<li>Xamarin.Mac: 7.4.0.10+</li>
<li>Xcode 12.4+</li>
<li>Visual Studio Community 2019 for Mac v8.8.10+, Stable Channel.</li>
</ul>
<h2>Solution Structure</h2>
<p>The macOS sample solution consists of 3 projects: container application, an extension project, and a common code.</p>
<p>The container application provides a Menu Bar icon to install/uninstall the file system extension. Inside the container application, you should change the hardcoded directory to replicate in your Virtual Disk manually. Consider that the extension can only access sandbox folders (including Downloads, Pictures, Music, Movies). It means that it won't be able to show the contents of the folder outside of the sandbox.</p>
<p>The extension project runs in the background and implements a virtual file system on macOS (File Provider). It processes requests from macOS applications sent via macOS file system API and lists folders content. The macOS extension can be installed only as part of a container application, you can not install the extension application by itself.</p>
<h2>Configuring the Sample</h2>
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
<p><strong>Set bundle identifier name in Container project.</strong>&nbsp;The bundle identifier is located in VirtualFilesystemMacApp/Info.plist file. You can edit it either in Visual Studio or directly in Info.plist file in the CFBundleIdentifier field (by default it is set to <code class="code">com.userfilesystem.vfs.app</code>). You must set this identifier to the value specified in Step 2.</p>
</li>
<li>
<p><strong>Set bundle identifier name in the Extension project.</strong>&nbsp;The bundle identifier is located in FileProviderExtension/Info.plist file. You can edit it either in Visual Studio or directly in Info.plist file in the CFBundleIdentifier field (by default it is set to <code class="code">com.userfilesystem.vfs.app.extension</code>). You must set this identifier to the value specified in Step 2.</p>
</li>
<li>
<p><strong>Configure macOS bundle signing in Container and Extension projects.</strong>&nbsp;For each project in Visual Studio go to the project Options. Select Mac Signing and check 'Sign the application bundle'. Select Identity and Provisioning profile.</p>
</li>
<li>
<p><strong>Configure application permissions in Container and Extension projects.</strong>&nbsp;Select Entitlements.plist and select group created in Step 1 in App Groups field for each project.</p>
</li>
<li>
<p><strong>Set App Group ID in code.</strong>&nbsp;Edit <code class="code">AppGroupsSettings.cs</code>&nbsp;file located in <code class="code">/VirtualFilesystemCommon/</code>&nbsp;folder. Specify AppGroupId.</p>
</li>
<li>
<p><strong>Set the license.</strong>&nbsp;Download the license file&nbsp;<a href="https://www.userfilesystem.com/download/downloads/" rel="nofollow">here</a>. With the trial license, the product is fully functional and does not have any limitations. As soon as the trial license expires the product will stop working. Open the&nbsp;<code class="code">VirtualFilesystemMacApp/Resources/appsettings.json</code>&nbsp;file and set the <code class="code">License</code> string:&nbsp;</p>
<pre class="brush:js;auto-links:false;toolbar:false">"License": "&lt;?xml version='1.0'...",</pre>
</li>
<li>
<p><strong>Set the remote storage directory.</strong> Here you must set the path to the folder that simulates your remote storage. Your virtual drive will mirror files and folders from this folder. Note that the extension runs as a sandboxed application and has access to a limited number of locations in the local file system. To simulate the remote storage structure you can copy the <code class="code">\RemoteStorage\</code> folder provided with the project under the <code class="code">~/Pictures/</code> folder.&nbsp;To set the remote storage directory open the&nbsp;<code class="code">VirtualFilesystemMacApp/Resources/appsettings.json</code>&nbsp;file and set <code class="code">RemoteStorageRootPath</code> string. Make sure to replace the ${USER} with a real user name:</p>
<pre class="brush:js;auto-links:false;toolbar:false">"RemoteStorageRootPath": "/Users/User1/Pictures/RemoteStorage"</pre>
</li>
</ol>
<p>Now you are ready to compile and run the project.</p>
<h2>Running the Sample</h2>
<p><span>To run the sample open the project in Visual Studio and run the project. The application adds an application to the macOS Status Bar. To mount the file system select the 'Install Extension' command in the Status Bar.</span></p>
<p><span><img id="__mcenew" alt="Virtual File System Mac in .NET/C#" src="https://www.userfilesystem.com/media/2117/virtulfilesystemmac.png" rel="122138"></span></p>
<p><span class="warn">Note, that every File Provider Extension runs in a sandbox, so access to the local filesystem restricted by OS except Downloads, Pictures, Music, Movies public directories.</span></p>
<h2>Troubleshooting</h2>
<p>If you experience issues on application start it may be caused by an incorrect app configuration. You can find what may be wrong using a macOS Console:</p>
<p><img id="__mcenew" alt="Virtual Drive .NET macOS Console" src="https://www.userfilesystem.com/media/2177/virtualdrivemacosconsole.png" rel="123887"></p>
<p>If your application started successfully but you experience issues with the file system you may need to filter logs to find information sent by the file system provider by using the 'ITHit' search:</p>
<p><img id="__mcenew" alt="Virtual File System macOS Console" src="https://www.userfilesystem.com/media/2176/virtualfilesystemmacosconsole.png" rel="123886"></p>
<p>You can select and copy the console output and submit it to the <a href="https://ithitcorp.atlassian.net/servicedesk/customer/portal/12">Help &amp; Support system</a>.</p>
<h3 class="para d-inline next-article-heading">Next Article:</h3>
<a title="Virtual Drive Sample in .NET, C# with thumbnail support Microsoft Office documents editing support, automatic locking and custom columns support" href="https://www.userfilesystem.com/examples/virtual_drive/">Virtual Drive Sample in .NET, C#</a>

