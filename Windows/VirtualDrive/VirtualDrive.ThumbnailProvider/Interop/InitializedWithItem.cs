
namespace VirtualDrive.ThumbnailProvider.Interop
{
    public abstract class InitializedWithItem : IInitializedWithItem
    {
        public virtual int Initialize(IShellItem shellItem, STGM accessMode)
        {
            SelectedShellItem = shellItem;
            SelectedShellItemAccessMode = accessMode;
            return WinError.S_OK;
        }

        public IShellItem SelectedShellItem { get; private set; }
        public STGM SelectedShellItemAccessMode { get; private set; }
    }
}
