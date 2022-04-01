using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using log4net;
using ITHit.FileSystem.Samples.Common.Windows.ShellExtension.Interop;

namespace ITHit.FileSystem.Samples.Common.Windows.ShellExtension.Thumbnails
{

    /// <summary>
    /// Common code for a thumbnails provider.
    /// </summary>
    /// <remarks>
    /// You will derive your class from this class to implement your Thumbnails provider Windows Shell Extension
    /// Typically you do not need to make any changes in this class.
    /// </remarks>
    public abstract class ThumbnailProviderBase : InitializedWithItem, IThumbnailProvider
    {
        public abstract Task<byte[]> GetThumbnailsAsync(string filePath, uint size);

        private string filePath = null;

        protected ILogger Log { get; }

        public ThumbnailProviderBase()
        {
            ReferenceManager.AddObjectReference();

            Log = new GrpcLogger("Thumbnail Provider");
        }

        ~ThumbnailProviderBase()
        {
            ReferenceManager.ReleaseObjectReference();
        }

        public override int Initialize(IShellItem shellItem, STGM accessMode)
        {
            if (shellItem.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out string path) != WinError.S_OK)
            {
                return WinError.E_UNEXPECTED;
            }

            if (!ShellExtensionConfiguration.IsVirtualDriveFolder(path))
            {
                return WinError.E_UNEXPECTED;
            }

            // Show thumbnails only for files (Dont show thumbnails for directories)
            if (!File.Exists(path))
            {
                return WinError.E_UNEXPECTED;
            }
            filePath = path;

            return base.Initialize(shellItem, accessMode);
        }

        public int GetThumbnail(uint cx, out IntPtr phbmp, out WTS_ALPHATYPE pdwAlpha)
        {
            phbmp = IntPtr.Zero;
            pdwAlpha = WTS_ALPHATYPE.WTSAT_UNKNOWN;

            try
            {
                //Log.LogMessage($"{nameof(ThumbnailProviderBase)}.{nameof(GetThumbnail)}()", filePath);

                byte[] bitmapData = GetThumbnailsAsync(filePath, cx).GetAwaiter().GetResult();

                using (MemoryStream stream = new MemoryStream(bitmapData))
                using (Bitmap thumbnail = new Bitmap(stream))
                {
                    if (thumbnail == null)
                    {
                        return WinError.E_FAIL;
                    }

                    phbmp = thumbnail.GetHbitmap();
                    pdwAlpha = GetAlphaType(thumbnail);
                }

                return WinError.S_OK;
            }
            catch (NotImplementedException)
            {
                return WinError.E_FAIL;
            }
            catch (Exception ex)
            {
                Log.LogError("", null, null, ex);
                return WinError.E_FAIL;
            }
        }

        private static WTS_ALPHATYPE GetAlphaType(Bitmap image)
        {
            PixelFormat pixelFormat = image.PixelFormat;

            if (Bitmap.IsAlphaPixelFormat(pixelFormat) || Bitmap.IsCanonicalPixelFormat(pixelFormat))
            {
                return WTS_ALPHATYPE.WTSAT_ARGB;
            }
            else if (pixelFormat != PixelFormat.Undefined)
            {
                return WTS_ALPHATYPE.WTSAT_RGB;
            }
            else
            {
                return WTS_ALPHATYPE.WTSAT_UNKNOWN;
            }
        }
    }
}