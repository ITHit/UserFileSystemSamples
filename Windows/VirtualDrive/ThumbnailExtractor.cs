using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;

using ITHit.FileSystem;

namespace VirtualDrive
{
    internal static class ThumbnailExtractor
    {
        /// <summary>
        /// Generates thumbnail for a file using existing local registered thumbnail handler if any.
        /// </summary>
        /// <param name="path">Path to get thumbnail for.</param>
        /// <param name="size">The maximum thumbnail size, in pixels.</param>
        /// <param name="itemType">FileSystemItemType enum value - file or folder.</param>
        /// <returns>Returns a thumbnail bitmap or null if the thumbnail handler is not found.</returns>
        public static byte[] GetRemoteThumbnail(string path, uint size, FileSystemItemType itemType)
        {
            using (Bitmap bitmap = GetThumbnailBitmap(path, size, itemType))
            {
                if (bitmap == null)
                {
                    return null;
                }

                using (MemoryStream stream = new MemoryStream())
                {
                    bitmap.Save(stream, ImageFormat.Png);
                    byte[] bitmapBytes = stream.GetBuffer();
                    return bitmapBytes;
                }
            }
        }

        //taken from Tray.xaml.cs, modified to match different return types and non-async mode
        private static Bitmap GetThumbnailBitmap(string actualPath, uint size, FileSystemItemType itemType)
        {
            Bitmap thumbnailSource;

            try
            {
                StorageItemThumbnail? storageItemThumbnail = null;
                if (actualPath.StartsWith(@"\\"))
                {
                    actualPath = actualPath.Remove(0, 4);
                }

                if (itemType == FileSystemItemType.File)
                {
                    try
                    {
                        StorageFile file = StorageFile.GetFileFromPathAsync(actualPath).GetAwaiter().GetResult();
                        storageItemThumbnail = file.GetThumbnailAsync(ThumbnailMode.SingleItem, size).GetAwaiter().GetResult();
                    }
                    catch (FileNotFoundException)
                    {
                        // File is missing, use a default thumbnail.
                        storageItemThumbnail = GetDefaultThumbnailForFileType(actualPath, size).GetAwaiter().GetResult();
                    }
                }
                else
                {
                    try
                    {
                        StorageFolder folder = StorageFolder.GetFolderFromPathAsync(actualPath).GetAwaiter().GetResult();
                        storageItemThumbnail = folder.GetThumbnailAsync(ThumbnailMode.SingleItem, size).GetAwaiter().GetResult();
                    }
                    catch (FileNotFoundException)
                    {
                        // Folder is missing, use a generic folder icon
                        storageItemThumbnail = GetDefaultFolderThumbnail(size).GetAwaiter().GetResult();
                    }
                }

                // Create a bitmap image from the thumbnail
                Bitmap bitmapImage = new Bitmap(storageItemThumbnail.AsStreamForRead());
                thumbnailSource = bitmapImage;
            }
            catch (Exception)
            {
                Uri defaultUri = itemType == FileSystemItemType.File ? new Uri("ms-appx:///Images/File.png") : new Uri("ms-appx:///Images/FolderLight.png");
                StorageFile file = StorageFile.GetFileFromApplicationUriAsync(defaultUri).GetAwaiter().GetResult();

                using (IRandomAccessStreamWithContentType fileStream = file.OpenReadAsync().GetAwaiter().GetResult())
                using (Stream netStream = fileStream.AsStreamForRead())
                {
                    thumbnailSource = new Bitmap(netStream);
                }
            }
            return thumbnailSource;
        }

        private static async Task<StorageItemThumbnail> GetDefaultThumbnailForFileType(string filePath, uint size)
        {
            // Create a temporary file with the given extension to get the system icon.
            StorageFolder tempFolder = ApplicationData.Current.TemporaryFolder;
            StorageFile fakeFile = await tempFolder.CreateFileAsync($"fake{Path.GetExtension(filePath)}", CreationCollisionOption.ReplaceExisting);

            // Request a system-generated thumbnail.
            StorageItemThumbnail thumbnail = await fakeFile.GetThumbnailAsync(ThumbnailMode.SingleItem, size);

            return thumbnail;
        }

        private static async Task<StorageItemThumbnail> GetDefaultFolderThumbnail(uint size)
        {
            // Create a temporary folder to get the system icon.
            StorageFolder tempFolder = ApplicationData.Current.TemporaryFolder;
            StorageFolder fakeFolder = await tempFolder.CreateFolderAsync($"fake", CreationCollisionOption.ReplaceExisting);

            // Request a system-generated thumbnail
            StorageItemThumbnail thumbnail = await fakeFolder.GetThumbnailAsync(ThumbnailMode.SingleItem, size);

            return thumbnail;
        }
    }
}
