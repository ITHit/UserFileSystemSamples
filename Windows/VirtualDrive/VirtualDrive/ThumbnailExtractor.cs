using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

using ITHit.FileSystem.Windows.ShellExtension.Interop;

namespace VirtualDrive
{
    internal static class ThumbnailExtractor
    {
        /// <summary>
        /// Generates thumbnail for a file using existing local registered thumbnail handler if any.
        /// </summary>
        /// <param name="path">Path to get thumbnail for.</param>
        /// <param name="size">The maximum thumbnail size, in pixels.</param>
        /// <returns>Returns a thumbnail bitmap or null if the thumbnail handler is not found.</returns>
        public static byte[] GetRemoteThumbnail(string path, uint size)
        {
            using (Bitmap bitmap = GetThumbnailBitmap(path, size))
            {
                if (bitmap == null)
                {
                    return null;
                }

                using (MemoryStream stream = new MemoryStream())
                {
                    bitmap.Save(stream, ImageFormat.Bmp);
                    byte[] bitmapBytes = stream.GetBuffer();
                    return bitmapBytes;
                }
            }
        }

        private static Bitmap GetThumbnailBitmap(string filePath, uint size)
        {
            IShellItem destinationItem = null;
            IThumbnailProvider provider = null;

            try
            {
                if (filePath.StartsWith(@"\\"))
                {
                    filePath = filePath.Remove(0, 4);
                }

                uint itemResult = Shell32.SHCreateItemFromParsingName(filePath, null, typeof(IShellItem).GUID,
                    out destinationItem);

                if (itemResult != WinError.S_OK)
                {
                    return null;
                }

                // Trying to get an existing thumbnail handler for the item from this local platform.
                int bindResult = destinationItem.BindToHandler(IntPtr.Zero, Shell32.BHID_ThumbnailHandler,
                    typeof(IThumbnailProvider).GUID, out IntPtr providerPointer);

                if (bindResult != WinError.S_OK)
                {
                    // No thumbnail handler found for this item.
                    return null;
                }

                provider = Marshal.GetObjectForIUnknown(providerPointer) as IThumbnailProvider;
                if (provider == null)
                {
                    throw new ArgumentException();
                }

                provider.GetThumbnail(size, out IntPtr sourceBitmapHandle, out WTS_ALPHATYPE pdwAlpha);

                Bitmap result = Image.FromHbitmap(sourceBitmapHandle);

                return result;
            }
            finally
            {
                if (destinationItem != null)
                {
                    Marshal.ReleaseComObject(destinationItem);
                }
                if (provider != null)
                {
                    Marshal.ReleaseComObject(provider);
                }
            }
        }
    }
}
