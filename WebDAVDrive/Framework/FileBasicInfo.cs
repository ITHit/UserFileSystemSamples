using ITHit.FileSystem;
using System;
using System.Collections.Generic;
using System.Text;

namespace VirtualFileSystem
{
    ///<inheritdoc cref="IFileBasicInfo"/>
    internal class FileBasicInfo : FileSystemItemBasicInfo, IFileBasicInfo
    {
        ///<inheritdoc/>
        public long Length { get; set; }
    }
}
