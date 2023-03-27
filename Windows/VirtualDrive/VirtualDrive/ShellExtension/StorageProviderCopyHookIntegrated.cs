using ITHit.FileSystem.Windows.ShellExtension;
using System;
using System.Runtime.InteropServices;


namespace VirtualDrive.ShellExtension
{
    /// <summary>
    /// Determines whether the Shell will allow to move, copy, delete, rename, hydrate and dehydrate a folder. 
    /// Runs in one process with Engine.
    /// </summary>
    [ComVisible(true)]
    [ProgId("VirtualDrive.StorageProviderCopyHook")]
    [Guid("4E813313-2227-42AE-BDC9-53C17A9CF812")]
    internal class StorageProviderCopyHookIntegrated : StorageProviderCopyHookIntegratedBase
    {
        public StorageProviderCopyHookIntegrated() : base(Program.Engine)
          {
        }
    }
}
