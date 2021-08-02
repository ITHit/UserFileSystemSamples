using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using VirtualDrive.ThumbnailProvider.Interop;

namespace VirtualDrive.ThumbnailProvider
{
    public abstract class ThumbnailProviderBase : InitializedWithItem, IThumbnailProvider
    {
        public abstract Task<Bitmap> GetThumbnailsAsync(string filePath, uint size);

        private string filePath = null;

        protected ILog Log { get; }

        public ThumbnailProviderBase()
        {
            ReferenceManager.AddObjectReference();

            Log = GetLogger();
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
                Log.Info($"\nGetting thumbnail for {filePath}");

                using (Bitmap thumbnail = GetThumbnailsAsync(filePath, cx).GetAwaiter().GetResult())
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
            catch (Exception ex)
            {
                Log.Error(ex);
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

        /// <summary>
        /// Returns logger with configured repository.
        /// </summary>
        private static ILog GetLogger()
        {
            string assemblyPath = Path.GetDirectoryName(typeof(ThumbnailProviderBase).Assembly.Location);
            Hierarchy hierarchy = (Hierarchy)LogManager.GetRepository(typeof(ThumbnailProviderBase).Assembly);

            if (!hierarchy.Configured)
            {
                PatternLayout patternLayout = new PatternLayout();
                patternLayout.ConversionPattern = "%date [%thread] %-5level %logger - %message%newline";
                patternLayout.ActivateOptions();

                RollingFileAppender roller = new RollingFileAppender();
                roller.AppendToFile = true;
                if (assemblyPath.Contains("WindowsApps"))
                {
                    roller.File = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        Mapping.AppSettings.AppID), "ThumbnailProvider.log");
                }
                else
                {
                    roller.File = Path.Combine(assemblyPath, "ThumbnailProvider.log");
                }
                roller.Layout = patternLayout;
                roller.MaxSizeRollBackups = 5;
                roller.MaximumFileSize = "10MB";
                roller.RollingStyle = RollingFileAppender.RollingMode.Size;
                roller.StaticLogFileName = true;
                roller.ActivateOptions();
                hierarchy.Root.AddAppender(roller);

                hierarchy.Root.Level = Level.Info;
                hierarchy.Configured = true;
            }

            return LogManager.GetLogger(typeof(ThumbnailProviderBase));
        }
    }
}
