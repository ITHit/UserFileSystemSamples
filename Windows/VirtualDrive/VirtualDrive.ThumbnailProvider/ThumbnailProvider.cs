using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using VirtualDrive.ThumbnailProvider.Interop;

namespace VirtualDrive.ThumbnailProvider
{
    // It is Windows Shell Extension code. We can ignore warnings about platform compatibility
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]

    [ComVisible(true)]
    [ProgId("VirtualDrive.ThumbnailProvider"), Guid("05CF065E-E135-4B2B-9D4D-CFB3FBAC73A4")]
    public class ThumbnailProvider : ThumbnailProviderBase
    {
        public override async Task<Bitmap> GetThumbnailsAsync(string filePath, uint size)
        {
            string sourcePath = Mapping.MapPath(filePath);

            uint itemResult = Shell32.SHCreateItemFromParsingName(sourcePath, null, typeof(IShellItem).GUID,
                out IShellItem destinationItem);

            if (itemResult != WinError.S_OK)
            {
                return await Task.FromResult<Bitmap>(null);
            }

            int bindResult = destinationItem.BindToHandler(IntPtr.Zero, Shell32.BHID_ThumbnailHandler,
                typeof(IThumbnailProvider).GUID, out IntPtr providerPointer);

            if (bindResult != WinError.S_OK)
            {
                return await Task.FromResult<Bitmap>(null);
            }

            if (!(Marshal.GetObjectForIUnknown(providerPointer) is IThumbnailProvider provider))
            {
                throw new ArgumentException();
            }

            provider.GetThumbnail(size, out IntPtr sourceBitmapHandle, out WTS_ALPHATYPE pdwAlpha);

            Bitmap result = Image.FromHbitmap(sourceBitmapHandle);

            Marshal.ReleaseComObject(destinationItem);
            Marshal.ReleaseComObject(provider);

            return await Task.FromResult(result);
        }
    }
}
