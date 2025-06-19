using System;
using System.Runtime.InteropServices;

using ITHit.FileSystem.Windows.ShellExtension;

namespace VirtualDrive.ShellExtension
{
    
    /// <summary>
    /// Implements custom state provider for virtual drive. 
    /// Displays custom colums and custom state icons in Status column in Windows Explorer.
    /// Runs in one process with Engine.
    /// </summary>
    [ComVisible(true)]
    [ProgId("VirtualDrive.CustomStateProvider")]
    [Guid("000562AA-2879-4CF1-89E8-0AEC9596FE19")]
    public class CustomStateProviderIntegrated : CustomStateHandlerIntegratedBase
    {
        public CustomStateProviderIntegrated() : base(ServiceProvider.GetService<VirtualEngine>())
        {
        }
    }
    
}
