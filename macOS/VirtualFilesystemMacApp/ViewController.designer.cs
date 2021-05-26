// WARNING
//
// This file has been generated automatically by Visual Studio to store outlets and
// actions made in the UI designer. If it is removed, they will be lost.
// Manual changes to this file may not be handled correctly.
//
using Foundation;
using System.CodeDom.Compiler;

namespace VirtualFilesystemMacApp
{
	[Register ("ViewController")]
	partial class ViewController
	{
		[Outlet]
		AppKit.NSButton ChooseFolderButton { get; set; }

		[Outlet]
		AppKit.NSTextField FolderPathTextField { get; set; }

		[Outlet]
		AppKit.NSButton InstallButton { get; set; }

		[Outlet]
		AppKit.NSButton UninstallButton { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (InstallButton != null) {
				InstallButton.Dispose ();
				InstallButton = null;
			}

			if (UninstallButton != null) {
				UninstallButton.Dispose ();
				UninstallButton = null;
			}

			if (ChooseFolderButton != null) {
				ChooseFolderButton.Dispose ();
				ChooseFolderButton = null;
			}

			if (FolderPathTextField != null) {
				FolderPathTextField.Dispose ();
				FolderPathTextField = null;
			}
		}
	}
}
