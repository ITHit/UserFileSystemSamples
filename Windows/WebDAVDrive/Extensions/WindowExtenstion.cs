using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace WebDAVDrive.Extensions
{
    /// <summary>
    /// Provides extension methods for Window
    /// </summary>
    public static class WindowExtenstion
    {
        /// <summary>
        /// Resizes the window to desired width and height, taking in count corrent scale
        /// </summary>
        /// <param name="width">Desired width</param>
        /// <param name="height">Desired height</param>
        public static void Resize(this Window window, int width, int height)
        {
            int scale = ServiceProvider.Scale;
            int usedWidth = width * scale / 100 <= DisplayArea.Primary.WorkArea.Width ? width * scale / 100 : DisplayArea.Primary.WorkArea.Width;
            int usedHeight = height * scale / 100 <= DisplayArea.Primary.WorkArea.Height ? height * scale / 100 : DisplayArea.Primary.WorkArea.Height;
            window.AppWindow.Resize(new SizeInt32(usedWidth, usedHeight));
        }
    }
}
