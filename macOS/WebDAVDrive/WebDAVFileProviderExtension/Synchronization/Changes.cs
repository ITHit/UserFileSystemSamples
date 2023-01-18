using System;
using System.Collections.Generic;
using ITHit.FileSystem;
using ITHit.FileSystem.Synchronization;

namespace WebDAVFileProviderExtension.Synchronization
{
    public class Changes : List<IChangedItem>, IChanges
    {
        public string NewSyncToken { get; set; }
    }
}
