using System;
using System.Runtime.InteropServices;

using ITHit.FileSystem.Windows.ShellExtension;

namespace VirtualDrive.ShellExtension
{
    /// <summary>
    /// Thumbnails provider Windows Shell Extension. Runs in one process with Engine.
    /// </summary>
    [ComVisible(true)]
    [ProgId("VirtualDrive.ThumbnailProvider")]
    [Guid("05CF065E-E135-4B2B-9D4D-CFB3FBAC73A4")]
    public class ThumbnailProviderIntegrated : ThumbnailProviderHandlerIntegratedBase
    {
        public ThumbnailProviderIntegrated() : base(ServiceProvider.GetService<VirtualEngine>())
        {
        }
    }
}
