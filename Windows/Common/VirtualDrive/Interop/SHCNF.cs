namespace ITHit.FileSystem.Samples.Common.Windows.VirtualDrive.Interop
{
    internal enum SHCNF : uint
    {
        SHCNF_IDLIST = 0x0000,
        SHCNF_PATHA = 0x0001,
        SHCNF_PRINTERA = 0x0002,
        SHCNF_DWORD = 0x0003,
        SHCNF_PATHW = 0x0005,
        SHCNF_PRINTERW = 0x0006,
        SHCNF_TYPE = 0x00FF,
        SHCNF_FLUSH = 0x1000,
        SHCNF_FLUSHNOWAIT = 0x3000,
        SHCNF_NOTIFYRECURSIVE = 0x10000
    }
}
