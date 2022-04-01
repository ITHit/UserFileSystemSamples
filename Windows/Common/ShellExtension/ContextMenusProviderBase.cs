using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using log4net;
using ITHit.FileSystem.Samples.Common.Windows.ShellExtension.Interop;
using ITHit.FileSystem.Samples.Common.Windows.Rpc;
using ITHit.FileSystem.Samples.Common.Windows.Rpc.Generated;
using System.Xml.Serialization;

namespace ITHit.FileSystem.Samples.Common.Windows.ShellExtension
{
    /// <summary>
    /// Common code to extend Windows Explorer commands.
    /// </summary>
    /// <remarks>
    /// You will derive your class from this class to implement your Windows Explorer context menu.
    /// Typically you do not need to make any changes in this class.
    /// </remarks>
    public abstract class ContextMenusProviderBase : IExplorerCommand
    {
        /// <summary>
        /// Method should be overridden to return menu title.
        /// </summary>
        /// <param name="filesPath">List of selected items.</param>
        public abstract Task<string> GetMenuTitleAsync(IEnumerable<string> filesPath);

        /// <summary>
        /// Method should be overridden to handle menu item call.
        /// </summary>
        /// <param name="filesPath">List of selected items.</param>
        public abstract Task InvokeMenuCommandAsync(IEnumerable<string> filesPath);

        /// <summary>
        /// Method should be overridden to return menu state.
        /// </summary>
        /// <param name="filesPath">List of selected items.</param>
        public abstract Task<EXPCMDSTATE> GetMenuStateAsync(IEnumerable<string> filesPath);

        /// <summary>
        /// Method should be overridden to return menu item icon.
        /// </summary>
        /// <param name="filesPath">List of selected items.</param>
        public abstract Task<string> GetIconAsync(IEnumerable<string> filesPath);

        protected ILogger Log { get; }

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        public ContextMenusProviderBase()
        {
            ReferenceManager.AddObjectReference();

            Log = new GrpcLogger("Context Menu Provider");
        }

        ~ContextMenusProviderBase()
        {
            ReferenceManager.ReleaseObjectReference();
        }

        /// <inheritdoc/>
        public int GetTitle(IShellItemArray itemArray, out string title)
        {
            title = null;

            try
            {
                IEnumerable<string> files = itemArray.GetFilesPath().Where(ShellExtensionConfiguration.IsVirtualDriveFolder);
                if (!files.Any() || !files.All(File.Exists))
                    return WinError.E_NOTIMPL;

                Log.LogMessage($"{nameof(ContextMenusProviderBase)}.{nameof(GetTitle)}()", string.Join(", ", files));

                title = GetMenuTitleAsync(files).GetAwaiter().GetResult();

                if (string.IsNullOrEmpty(title))
                    return WinError.E_NOTIMPL;

                return WinError.S_OK;
            }
            catch (NotImplementedException)
            {
                return WinError.E_NOTIMPL;
            }
            catch (Exception ex)
            {
                Log.LogError("", null, null, ex);
                return WinError.E_FAIL;
            }
        }

        /// <inheritdoc/>
        public int Invoke(IShellItemArray itemArray, object bindCtx)
        {
            try
            {
                IEnumerable<string> files = itemArray.GetFilesPath().Where(ShellExtensionConfiguration.IsVirtualDriveFolder);
                if (!files.Any() || !files.All(File.Exists))
                    return WinError.E_NOTIMPL;

                Log.LogMessage($"{nameof(ContextMenusProviderBase)}.{nameof(Invoke)}()", string.Join(", ", files));

                InvokeMenuCommandAsync(files).GetAwaiter().GetResult();

                return WinError.S_OK;
            }
            catch (NotImplementedException)
            {
                return WinError.E_NOTIMPL;
            }
            catch (Exception ex)
            {
                Log.LogError("", null, null, ex);
                return WinError.E_FAIL;
            }
        }

        /// <inheritdoc/>
        public int GetState(IShellItemArray itemArray, bool okToBeSlow, out EXPCMDSTATE commandState)
        {
            commandState = EXPCMDSTATE.ECS_ENABLED;

            try
            {
                if (itemArray is null)
                {
                    return WinError.E_NOTIMPL;
                }

                IEnumerable<string> files = itemArray.GetFilesPath().Where(ShellExtensionConfiguration.IsVirtualDriveFolder);
                if (!files.Any() || !files.All(File.Exists))
                {
                    return WinError.E_NOTIMPL;
                }

                Log.LogMessage($"{nameof(ContextMenusProviderBase)}.{nameof(GetState)}()", string.Join(", ", files));

                commandState = GetMenuStateAsync(files).GetAwaiter().GetResult();

                return WinError.S_OK;
            }
            catch (NotImplementedException)
            {
                return WinError.E_NOTIMPL;
            }
            catch (Exception ex)
            {
                Log.LogError("", null, null, ex);
                return WinError.E_FAIL;
            }
        }

        /// <inheritdoc/>
        public int GetFlags(out EXPCMDFLAGS flags)
        {
            flags = EXPCMDFLAGS.ECF_DEFAULT;
            return WinError.S_OK;
        }

        /// <inheritdoc/>
        public int GetIcon(IShellItemArray itemArray, out string resourceString)
        {
            resourceString = null;

            try
            {
                IEnumerable<string> files = itemArray.GetFilesPath().Where(ShellExtensionConfiguration.IsVirtualDriveFolder);
                if (!files.Any() || !files.All(File.Exists))
                    return WinError.E_NOTIMPL;

                Log.LogMessage($"{nameof(ContextMenusProviderBase)}.{nameof(GetIcon)}()", string.Join(", ", files));

                resourceString = GetIconAsync(files).GetAwaiter().GetResult();

                if (string.IsNullOrEmpty(resourceString))
                    return WinError.E_NOTIMPL;

                return WinError.S_OK;
            }
            catch (NotImplementedException)
            {
                return WinError.E_NOTIMPL;
            }
            catch (Exception ex)
            {
                Log.LogError("", null, null, ex);
                return WinError.E_FAIL;
            }
        }

        /// <inheritdoc/>
        public int GetToolTip(IShellItemArray itemArray, out string tooltip)
        {
            tooltip = null;
            return WinError.E_NOTIMPL;
        }

        /// <inheritdoc/>
        public int GetCanonicalName(out Guid guid)
        {
            guid = Guid.Empty;
            return WinError.E_NOTIMPL;
        }

        /// <inheritdoc/>
        public int EnumSubCommands(out IEnumExplorerCommand commandEnum)
        {
            commandEnum = null;
            return WinError.E_NOTIMPL;
        }
    }
}
