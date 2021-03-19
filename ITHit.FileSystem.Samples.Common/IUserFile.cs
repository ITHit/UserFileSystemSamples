using ITHit.FileSystem;
using System.IO;
using System.Threading.Tasks;

namespace ITHit.FileSystem.Samples.Common
{
    public interface IUserFile : IUserFileSystemItem
    {
        Task<byte[]> ReadAsync(long offset, long length);
        Task<string> UpdateAsync(IFileBasicInfo fileInfo, Stream content = null, ServerLockInfo lockInfo = null);
        Task<bool> ValidateDataAsync(long offset, long length);
    }
}