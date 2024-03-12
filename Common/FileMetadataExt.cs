using ITHit.FileSystem;
using System;
using System.Collections.Generic;
using System.Text;

namespace ITHit.FileSystem.Samples.Common
{
    ///<inheritdoc cref="IFileMetadata"/>
    public class FileMetadataExt : FileSystemItemMetadataExt, IFileMetadata
    {
        ///<inheritdoc/>
        public long Length { get; set; }

        ///<inheritdoc/>
        public string ContentETag { get; set; }
    }
}
