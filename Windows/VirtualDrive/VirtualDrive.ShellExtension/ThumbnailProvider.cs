using System.Runtime.InteropServices;
using ITHit.FileSystem.Windows.ShellExtension.Thumbnails;


namespace VirtualDrive.ShellExtension
{
    /// <summary>
    /// Thumbnails provider Windows Shell Extension.
    /// </summary>
    [ComVisible(true)]
    [ProgId("VirtualDrive.ThumbnailProvider")]
    [Guid("05CF065E-E135-4B2B-9D4D-CFB3FBAC73A4")]
    public class ThumbnailProvider : ThumbnailProviderHandlerRpcBase
    {

    }
}
