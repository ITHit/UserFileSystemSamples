using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ITHit.FileSystem.Samples.Common.Windows
{
    public interface IVirtualFolder
    {
        Task<IEnumerable<FileSystemItemMetadataExt>> EnumerateChildrenAsync(string pattern, CancellationToken cancellationToken);
    }
}
