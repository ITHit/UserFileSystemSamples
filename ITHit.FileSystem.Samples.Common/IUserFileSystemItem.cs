using System.Threading.Tasks;

namespace ITHit.FileSystem.Samples.Common
{
    public interface IUserFileSystemItem
    {
        Task DeleteAsync();
        Task<ServerLockInfo> LockAsync();
        Task MoveToAsync(string userFileSystemNewPath);
        Task UnlockAsync(string lockToken);
    }
}