using System;

namespace ITHit.FileSystem.Samples.Common.Windows.ShellExtension.Interop
{
    [Flags]
    public enum EXPCMDFLAGS : uint
    {
        ECF_DEFAULT = 0,
        ECF_HASSUBCOMMANDS = 0x1,
        ECF_HASSPLITBUTTON = 0x2,
        ECF_HIDELABEL = 0x4,
        ECF_ISSEPARATOR = 0x8,
        ECF_HASLUASHIELD = 0x10,
        ECF_SEPARATORBEFORE = 0x20,
        ECF_SEPARATORAFTER = 0x40,
        ECF_ISDROPDOWN = 0x80,
        ECF_TOGGLEABLE = 0x100,
        ECF_AUTOMENUICONS = 0x200
    }
}
