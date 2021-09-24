using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using log4net;
using VirtualDrive.ShellExtension.Interop;

namespace VirtualDrive.ShellExtension
{
    /// <summary>
    /// Base class which implements logic to extend explorer.
    /// </summary>
    public abstract class ContextMenusProviderBase : IExplorerCommand
    {
        /// <summary>
        /// Method should be overridden to return menu title.
        /// </summary>
        public abstract Task<string> GetMenuTitleAsync(IEnumerable<string> filesPath);

        /// <summary>
        /// Method should be overridden to handle menu item call.
        /// </summary>
        public abstract Task InvokeMenuCommandAsync(IEnumerable<string> filesPath);

        /// <summary>
        /// Method should be overridden to return menu state.
        /// </summary>
        public abstract Task<EXPCMDSTATE> GetMenuStateAsync(IEnumerable<string> filesPath);

        protected ILog Log { get; }

        public ContextMenusProviderBase()
        {
            ReferenceManager.AddObjectReference();

            Log = ShelIExtensionModule.GetLogger("ContextMenus.log");
        }

        ~ContextMenusProviderBase()
        {
            ReferenceManager.ReleaseObjectReference();
        }

        /// <summary>
        /// Method is called CloudAPI to get menu title
        /// </summary>
        public int GetTitle(IShellItemArray itemArray, out string title)
        {
            title = null;

            try
            {
                IEnumerable<string> files = itemArray.GetFilesPath();
                if (!files.All(File.Exists))
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

        /// <summary>
        /// Method is called CloudAPI to handle menu call
        /// </summary>
        public int Invoke(IShellItemArray itemArray, object bindCtx)
        {
            try
            {
                IEnumerable<string> files = itemArray.GetFilesPath();

                if (!files.All(File.Exists))
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

        /// <summary>
        /// Method is called CloudAPI to get menu item state
        /// </summary>
        public int GetState(IShellItemArray itemArray, bool okToBeShow, out EXPCMDSTATE commandState)
        {
            commandState = EXPCMDSTATE.ECS_ENABLED;

            try
            {
                IEnumerable<string> files = itemArray.GetFilesPath();

                if (!files.All(File.Exists))
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

        public int GetFlags(out EXPCMDFLAGS flags)
        {
            flags = EXPCMDFLAGS.ECF_DEFAULT;
            return WinError.S_OK;
        }

        public int GetIcon(IShellItemArray itemArray, out string resourceString)
        {
            resourceString = null;
            return WinError.E_NOTIMPL;
        }

        public int GetToolTip(IShellItemArray itemArray, out string tooltip)
        {
            tooltip = null;
            return WinError.E_NOTIMPL;
        }

        public int GetCanonicalName(out Guid guid)
        {
            guid = Guid.Empty;
            return WinError.E_NOTIMPL;
        }

        public int EnumSubCommands(out IEnumExplorerCommand commandEnum)
        {
            commandEnum = null;
            return WinError.E_NOTIMPL;
        }

    }
}
