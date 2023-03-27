using System.Runtime.InteropServices;
using ITHit.FileSystem.Windows.ShellExtension;


namespace VirtualDrive.ShellExtension
{
    /// <summary>
    /// Implements custom content URI source provider. Runs in a separate process from Engine.
    /// </summary>
    [ComVisible(true)]
    [ProgId("VirtualDrive.UriSource")]
    [Guid("6D45BC7A-D0B7-4913-8984-FD7261550C08")]
    public class UriSourceRpc : ContentUriSourceRpcBase
    {

    }
}
