using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using VirtualDrive.ShellExtension.Interop;

namespace VirtualDrive.ShellExtension.Thumbnails
{
    // It is Windows Shell Extension code. We can ignore warnings about platform compatibility
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]

    [ComVisible(true)]
    [ProgId("VirtualDrive.ThumbnailProvider"), Guid(ThumbnailClass)]
    public class ThumbnailProvider : ThumbnailProviderBase
    {
        public const string ThumbnailClass = "05CF065E-E135-4B2B-9D4D-CFB3FBAC73A4";
        public static readonly Guid ThumbnailClassGuid = Guid.Parse(ThumbnailClass);


        /// <summary>
        /// Returns the thumbnail to the system.
        /// </summary>
        /// <param name="userFileSystemPath">File path in user file system to generate thumbnail for.</param>
        /// <param name="size">
        /// The maximum thumbnail size, in pixels. The Shell draws the returned bitmap at this size or smaller. 
        /// The returned bitmap should fit into a square of width and height cx, though it does not need to be 
        /// a square image. The Shell scales the bitmap to render at lower sizes. For example, if the image has 
        /// a 6:4 aspect ratio, then the returned bitmap should also have a 6:4 aspect ratio.
        /// </param>
        /// <returns>Thumbnail bitmap or null if thumbnail can not be generated.</returns>
        public override async Task<Bitmap> GetThumbnailsAsync(string userFileSystemPath, uint size)
        {
            string remoteStorageItemPath = Mapping.MapPath(userFileSystemPath);

            // In this sample we just generate a thumbnail from the remote storage file using a local 
            // platform thumbnail handler.
            // In your real life application you will replace this call with a thumbnails generation from URL.
            Bitmap thumbnail = GetThumbnailUsingPlatformThumbnailHandler(remoteStorageItemPath, size);

            return thumbnail;
        }

        /// <summary>
        /// Generates thumbnail for a file in file system using existing local registered thumbnail handler if any.
        /// </summary>
        /// <remarks>
        /// You can use this method to reduce the number of thumbnail generation requests to your remote storage, 
        /// by generating a thumbnail from a local file system for hydrated files.
        /// </remarks>
        /// <param name="filePath">File path in user file system to generate thumbnail for.</param>
        /// <param name="size">The maximum thumbnail size, in pixels.</param>
        /// <returns>Returns a thumbnail bitmap or null if the thumbnail handler is not found.</returns>
        private Bitmap GetThumbnailUsingPlatformThumbnailHandler(string filePath, uint size)
        {
            IShellItem destinationItem = null;
            IThumbnailProvider provider = null;

            try
            {
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
                if ( provider == null )
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
