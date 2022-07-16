using ITHit.FileSystem.Windows.ShellExtension;
using System;
using System.Runtime.InteropServices;

namespace VirtualDrive.ShellExtension
{
    
    /// <summary>
    /// Implements custom state provider for virtual drive. 
    /// Displays custom colums and custom state icons in Status column in Windows Explorer.
    /// </summary>
    [ComVisible(true)]
    [ProgId("VirtualDrive.CustomStateProvider")]
    [Guid("000562AA-2879-4CF1-89E8-0AEC9596FE19")]
    public class CustomStateProviderIntegrated : CustomStateHandlerIntegratedBase
    {
        public CustomStateProviderIntegrated() : base(Program.Engine)
        {
        }
    }
    
}
