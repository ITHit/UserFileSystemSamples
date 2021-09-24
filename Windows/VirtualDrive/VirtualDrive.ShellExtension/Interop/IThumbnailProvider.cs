using System;
using System.Runtime.InteropServices;

namespace VirtualDrive.ShellExtension.Interop
{
    /// <summary>
    /// Exposes a method for getting a thumbnail image and is intended to be 
    /// implemented for thumbnail handlers. 
    /// </summary>
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("E357FCCD-A995-4576-B01F-234630154E96")]
    public interface IThumbnailProvider
    {
        /// <summary>
        /// Gets a thumbnail image and alpha type.
        /// </summary>
        /// <param name="cx">
        /// The maximum thumbnail size, in pixels. The Shell draws the returned bitmap at this size or smaller. 
        /// The returned bitmap should fit into a square of width and height cx, though it does not need to be 
        /// a square image. The Shell scales the bitmap to render at lower sizes. For example, if the image has 
        /// a 6:4 aspect ratio, then the returned bitmap should also have a 6:4 aspect ratio.
        /// </param>
        /// <param name="phbmp">
        /// When this method returns, contains a pointer to the thumbnail image handle. The image must be a DIB 
        /// section and 32 bits per pixel. The Shell scales down the bitmap if its width or height is larger 
        /// than the size specified by cx. The Shell always respects the aspect ratio and never scales a bitmap 
        /// larger than its original size.
        /// </param>
        /// <param name="pdwAlpha">
        /// 
        /// </param>
        /// <returns></returns>
        [PreserveSig]
        int GetThumbnail(UInt32 cx, out IntPtr phbmp, out WTS_ALPHATYPE pdwAlpha);
    }
}
