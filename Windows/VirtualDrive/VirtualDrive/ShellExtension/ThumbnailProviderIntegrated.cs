using ITHit.FileSystem.Windows.ShellExtension;
using System;
using System.Runtime.InteropServices;


namespace VirtualDrive.ShellExtension
{
    /// <summary>
    /// Thumbnails provider Windows Shell Extension.
    /// </summary>
    [ComVisible(true)]
    [ProgId("VirtualDrive.ThumbnailProvider")]
    [Guid("05CF065E-E135-4B2B-9D4D-CFB3FBAC73A4")]
    public class ThumbnailProviderIntegrated : ThumbnailProviderIntegratedBase
    {
        public ThumbnailProviderIntegrated() : base(Program.Engine)
        {
        }
    }
}
