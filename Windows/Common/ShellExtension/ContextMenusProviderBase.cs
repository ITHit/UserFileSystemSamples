using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using log4net;
using ITHit.FileSystem.Samples.Common.Windows.ShellExtension.Interop;

namespace ITHit.FileSystem.Samples.Common.Windows.ShellExtension
{
    /// <summary>
    /// Base class which implements logic to extend Windows Explorer commands.
    /// </summary>
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

        protected ILog Log { get; }

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        public ContextMenusProviderBase()
        {
            ReferenceManager.AddObjectReference();

            Log = ShellExtensionConfiguration.GetLogger("ContextMenus.log");
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

                Log.Info($"\nGetting menu title for {string.Join(",", files)}");

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
                Log.Error(ex);
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

                Log.Info($"\nInvoke menu command for {string.Join(",", files)}");

                InvokeMenuCommandAsync(files).GetAwaiter().GetResult();

                return WinError.S_OK;
            }
            catch (NotImplementedException)
            {
                return WinError.E_NOTIMPL;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return WinError.E_FAIL;
            }
        }

        /// <inheritdoc/>
        public int GetState(IShellItemArray itemArray, bool okToBeSlow, out EXPCMDSTATE commandState)
        {
            commandState = EXPCMDSTATE.ECS_ENABLED;

            try
            {
                IEnumerable<string> files = itemArray.GetFilesPath().Where(ShellExtensionConfiguration.IsVirtualDriveFolder);
                if (!files.Any() || !files.All(File.Exists))
                    return WinError.E_NOTIMPL;

                Log.Info($"\nGetting menu state for {string.Join(",", files)}");

                commandState = GetMenuStateAsync(files).GetAwaiter().GetResult();

                return WinError.S_OK;
            }
            catch (NotImplementedException)
            {
                return WinError.E_NOTIMPL;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
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

                Log.Info($"\nGetting menu icon for {string.Join(",", files)}");

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
                Log.Error(ex);
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
