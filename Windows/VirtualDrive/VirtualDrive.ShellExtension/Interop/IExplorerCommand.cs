using System;
using System.Runtime.InteropServices;

namespace VirtualDrive.ShellExtension.Interop
{
    /// <summary>
    /// Represents Windows Explorer context menu. 
    /// </summary>
    /// <remarks>
    /// None of the methods of this interface should communicate with network resources. 
    /// These methods are called on the UI thread, so communication with network resources 
    /// could cause the UI to stop responding.
    /// </remarks>
    [ComImport]
    [Guid("A08CE4D0-FA25-44AB-B57C-C7B1C323E0B9")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IExplorerCommand
    {
        /// <summary>
        /// Gets the title text of the button or menu item that launches a specified Windows Explorer command item.
        /// </summary>
        /// <param name="itemArray">Shell Item array.</param>
        /// <param name="title">Title string.</param>
        /// <returns>If this method succeeds, it returns S_OK. Otherwise, it returns an HRESULT error code.</returns>
        [PreserveSig]
        int GetTitle(
            IShellItemArray itemArray,
            [MarshalAs(UnmanagedType.LPWStr)] out string title);

        /// <summary>
        /// Gets an icon resource string of the icon associated with the specified Windows Explorer command item.
        /// </summary>
        /// <param name="itemArray">Shell Item array.</param>
        /// <param name="resourceString">resource string that identifies the icon source.</param>
        /// <returns>If this method succeeds, it returns S_OK. Otherwise, it returns an HRESULT error code.</returns>
        [PreserveSig]
        int GetIcon(IShellItemArray itemArray,
            [MarshalAs(UnmanagedType.LPWStr)] out string resourceString);

        /// <summary>
        /// Gets the tooltip string associated with a specified Windows Explorer command item.
        /// </summary>
        /// <param name="itemArray">Shell Item array.</param>
        /// <param name="tooltip">Tooltip string.</param>
        /// <returns>If this method succeeds, it returns S_OK. Otherwise, it returns an HRESULT error code.</returns>
        [PreserveSig]
        int GetToolTip(IShellItemArray itemArray,
            [MarshalAs(UnmanagedType.LPWStr)] out string tooltip);

        /// <summary>
        /// Gets the GUID of a Windows Explorer command.
        /// </summary>
        /// <param name="guid">Command's GUID, under which it is declared in the registry.</param>
        /// <returns>If this method succeeds, it returns S_OK. Otherwise, it returns an HRESULT error code.</returns>
        [PreserveSig]
        int GetCanonicalName(out Guid guid);

        /// <summary>
        /// Gets state information associated with a specified Windows Explorer command item.
        /// </summary>
        /// <param name="itemArray">Shell Item array.</param>
        /// <param name="okToBeSlow">FALSE if a verb object should not perform any memory intensive computations that could cause the UI thread to stop responding. The verb object should return E_PENDING in that case. If TRUE, those computations can be completed.</param>
        /// <param name="commandState">One or more Windows Explorer command states.</param>
        /// <returns>If this method succeeds, it returns S_OK. Otherwise, it returns an HRESULT error code.</returns>
        [PreserveSig]
        int GetState(IShellItemArray itemArray,
            [MarshalAs(UnmanagedType.Bool)] bool okToBeSlow,
            out EXPCMDSTATE commandState);

        /// <summary>
        /// Invokes a Windows Explorer command.
        /// </summary>
        /// <param name="itemArray">Shell Item array.</param>
        /// <param name="bindCtx">A pointer to an IBindCtx interface, which provides access to a bind context. This value can be NULL if no bind context is needed.</param>
        /// <returns>If this method succeeds, it returns S_OK. Otherwise, it returns an HRESULT error code.</returns>
        [PreserveSig]
        int Invoke(IShellItemArray itemArray,
            [MarshalAs(UnmanagedType.Interface)] object bindCtx);

        /// <summary>
        /// Gets the flags associated with a Windows Explorer command.
        /// </summary>
        /// <param name="flags">Item flags.</param>
        /// <returns>If this method succeeds, it returns S_OK. Otherwise, it returns an HRESULT error code.</returns>
        [PreserveSig]
        int GetFlags(out EXPCMDFLAGS flags);

        /// <summary>
        /// Retrieves an enumerator for a command's subcommands.
        /// </summary>
        /// <param name="commandEnum">Contains an IEnumExplorerCommand interface pointer that can be used to walk the set of subcommands.</param>
        /// <returns>If this method succeeds, it returns S_OK. Otherwise, it returns an HRESULT error code.</returns>
        [PreserveSig]
        int EnumSubCommands(out IEnumExplorerCommand commandEnum);
    }
}
