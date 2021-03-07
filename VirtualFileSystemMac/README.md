## Requirements

macOS 11.0+
Mono 6.12.0.122+
Xamarin.Mac: 7.4.0.10+
Xcode 12.4+
Visual Studio Community 2019 for Mac v8.8.10+, Stable Channel.

## Solution Structure 

The macOS sample solution consists of 3 projects: container application, an extension project and a common code.

The container application provides Menu Bar icon to install/uninstall extension. Inside container application you should change hardcoded directory to replicate in your Virtual Disk manually. Consider that extension can only access sandbox folders (including Downloads, Pictures, Music, Movies). It means that it won't be able to show contents of folder outside of sandbox.  

The extension project runs in the background and implements a virtual file system on macOS (File Provider). It processes requests from macOS applications sent via macOS file system API and lists folders content. The macOS extension can be installed only as part of a container application, you can not install the extension application by itself.

## Running the Sample

In the following steps we will describe how to configure and run macOS sample in the development environment. You will create an Apple group ID, Apple app identifies and Apple provisioning profiles. Than you will update the sample container application project and extension project to use the created IDs and profiles.

Login to Apple developer account here: https://developer.apple.com/. To complete steps below you must have an App Manager role.

1. **Create App Group.**
    Navigate to Certificates, IDs, Profile -> Identifiers -> App Groups and create a new group.
    
2. **Create Apple macOS App IDs.** 
    Navigate to Certificates, IDs, Profile -> Identifiers -> App IDs. Create 2 identifiers that will be unique for your project. One will be used for container application another – for extension.

3. **Add app identifiers to group.** 
    Add both identifiers created in Step 2 to group created in Step 1. Select identifier and click on Edit. Than check App Groups checkbox, select Edit button and select the group created in Step 1.

4. **Create Provisioning Profiles.** 
    Navigate to Certificates, Identifiers & Profiles -> Profiles -> Development and create profile. Associate profile with extension ID and container ID respectively.

5. **Download profiles and certificates in XCode.** 
    Run XCode and go to Xcode Menu > Preferences -> Accounts tab. Select team and click on “Download Manual Profiles”. You can find more detailed instructions: [here](https://docs.microsoft.com/en-us/xamarin/mac/deploy-test/publishing-to-the-app-store/profiles)
    
6. **Set bundle identifier name in Container project.** 
    The bundle identifier is located in VirtualFilesystemMacApp/Info.plist file. You can edit it either in Visual Studio or directly in Info.plist file in CFBundleIdentifier field (by default it is set to com.ithit.virtualfilesystem.app). You must set this identifier to the value specified in Step 1.

7. **Set bundle identifier name in Extension project.** 
    The bundle identifier is located in FileProviderExtension/Info.plist file. You can edit it either in Visual Studio or directly in Info.plist file in in CFBundleIdentifier field (by default it is set to com.ithit.virtualfilesystem.app.extension). You must set this identifier to the value specified in Step 1.
    
8. **Configure macOS bundle signing in Container and Extension projects.**
    For each project in Visual Studio go to project Options. Select Mac Signing and check 'Sign the application bundle'. Select Identity and Providioning profile.

9. **Configure application permissions in Container and Extension projects.** 
    Select Entitlements.plist and select group created in Step 1 in App Groups field for each project.

10. **Set App Group ID in code.** 
    Edit AppGroupsSettings.cs in /VirtualFilesystemCommon/ specify AppGroupId. 
    
11. **Set license** 
    Download the license file [here](https://www.userfilesystem.com/download/downloads/). With the trial license the product is fully functional and does not have any limitations. As soon as the trial license expires the product will stop working.
    Open 'VirtualFilesystemMacApp/Resources/appsettings.json' file and set License string:
    `"License": "<?xml version='1.0'...",`
    
11. **Set remote directory** 
    To set directory which will ba shown in virtual filesystem open 'VirtualFilesystemMacApp/Resources/appsettings.json' file and set RemoteStorageRootPath string:
    `"RemoteStorageRootPath": "/Users/${USER}/Downloads/RemoteStorage"`
        
Now you are ready to compile and run the project.

## Using macOS File Provider Extension

In this section we describe how to use your deployed application on the end user device.

The sample is provided with an application that you will use to install/uninstall extension (the container application). To change folder that will be shown in virtual filesystem change path in 'appsettings.json' file in the Resources of the VirtualFilesystemMacApp project. 
**Note**, that every File Provider Extension runs under sandbox, so access to local filesystem restricted by OS except Downloads, Pictures, Music, Movies public directories.

Current version of the Extension can only list folders and files without ability to access its contents.

To enable File Provider Extension simply run container application and select Install Extension in application menu at macOS Status Bar.
