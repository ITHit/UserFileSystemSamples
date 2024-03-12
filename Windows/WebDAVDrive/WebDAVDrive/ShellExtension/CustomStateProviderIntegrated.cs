using System.Runtime.InteropServices;

using ITHit.FileSystem.Windows.ShellExtension;


namespace WebDAVDrive.ShellExtension
{
    /// <summary>
    /// Implements custom state provider for virtual drive.
    /// Displays custom colums and custom state icons in Status column in Windows Explorer.
    /// </summary>
    [ComVisible(true)]
    [ProgId("WebDAVDrive.CustomStateProvider")]
    [Guid("754F334F-095C-46CD-B033-B2C0523D2829")]
    public class CustomStateProviderIntegrated : CustomStateHandlerIntegratedBase
    {
        public CustomStateProviderIntegrated() : base(Program.Engines)
        {
        }
    }
}
