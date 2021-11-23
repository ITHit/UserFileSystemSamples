using System;

namespace ITHit.FileSystem.Samples.Common.Windows.ShellExtension.Interop
{
    [Flags]
    public enum EXPCMDSTATE : uint
    {
        ECS_ENABLED = 0,
        ECS_DISABLED = 0x1,
        ECS_HIDDEN = 0x2,
        ECS_CHECKBOX = 0x4,
        ECS_CHECKED = 0x8,
        ECS_RADIOCHECK = 0x10
    }
}
