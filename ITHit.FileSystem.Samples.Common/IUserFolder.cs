using ITHit.FileSystem;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace ITHit.FileSystem.Samples.Common
{
    public interface IUserFolder : IUserFileSystemItem
    {
        Task<string> CreateFileAsync(IFileBasicInfo fileInfo, Stream content);
        Task<string> CreateFolderAsync(IFolderBasicInfo folderInfo);
        Task<IEnumerable<FileSystemItemBasicInfo>> EnumerateChildrenAsync(string pattern);
        Task<string> UpdateAsync(IFolderBasicInfo folderInfo, ServerLockInfo lockInfo = null);
    }
}