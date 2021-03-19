using System;
using System.Collections.Generic;
using System.Text;

namespace ITHit.FileSystem.Samples.Common.Syncronyzation
{

    /// <summary>
    /// File attributes that are not provided by .NET, but required for 
    /// detecting pinned/unpinned files and synchronization.
    /// </summary>
    /// <remarks>
    /// You can enable Attributes column in Windows File Manager to see them.
    /// Some usefull file attributes reference:
    /// 
    /// 4194304     (0x400000)	FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS    (M)
    /// 4096 	     (0x1000) 	FILE_ATTRIBUTE_OFFLINE                  (O)
    /// 1024 	      (0x400)  	FILE_ATTRIBUTE_REPARSE_POINT            (L)
    /// 16   	       (0x10)   FILE_ATTRIBUTE_DIRECTORY                (D)
    ///          (0x00080000)   FILE_ATTRIBUTE_PINNED                   (P)
    ///          (0x00100000)   FILE_ATTRIBUTE_UNPINNED                 (U)
    /// 32             (0x20)   FILE_ATTRIBUTE_ARCHIVE                  (A)
    /// 512           (0x200)   FILE_ATTRIBUTE_SPARSE_FILE
    /// </remarks>
    [Flags]
    public enum FileAttributesExt
    {
        Pinned = 0x00080000,
        Unpinned = 0x00100000,
        Offline = 0x1000
    }
}
