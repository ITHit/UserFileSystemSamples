using ITHit.FileSystem;
using System;
using System.Collections.Generic;
using System.Text;

namespace ITHit.FileSystem.Samples.Common
{
    ///<inheritdoc cref="IFileBasicInfo"/>
    public class FileBasicInfo : FileSystemItemBasicInfo, IFileBasicInfo
    {
        ///<inheritdoc/>
        public long Length { get; set; }
    }
}
