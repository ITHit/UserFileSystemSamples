using System.Runtime.InteropServices;
using ITHit.FileSystem.Windows.ShellExtension.Thumbnails;


namespace WebDAVDrive.ShellExtension
{
    
    /// <summary>
    /// Thumbnails provider Windows Shell Extension.
    /// </summary>
    [ComVisible(true)]
    [ProgId("WebDAVDrive.ThumbnailProvider")]
    [Guid("A5B0C82F-50AA-445C-A404-66DEB510E84B")]
    public class ThumbnailProvider : ThumbnailProviderHandlerRpcBase
    {

    }
    
}
