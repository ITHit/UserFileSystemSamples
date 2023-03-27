using ITHit.FileSystem.Windows.ShellExtension;
using System;
using System.Runtime.InteropServices;

namespace VirtualDrive.ShellExtension
{
    /// <summary>
    /// Implements custom content URI source provider. Runs in one process with Engine.
    /// </summary>
    [ComVisible(true)]
    [ProgId("VirtualDrive.UriSource")]
    [Guid("6D45BC7A-D0B7-4913-8984-FD7261550C08")]
    public class UriSourceIntegrated : ContentUriSourceIntegratedBase
    {
        public UriSourceIntegrated() : base(Program.Engine)
        {
        }
    }
}
