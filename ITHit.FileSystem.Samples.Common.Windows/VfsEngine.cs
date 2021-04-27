using ITHit.FileSystem.Windows;
using log4net;
using System.IO;
using System.Threading.Tasks;

namespace ITHit.FileSystem.Samples.Common.Windows
{
    // In most cases you can use this class in your project without any changes.
    /// <inheritdoc/>
    internal class VfsEngine : EngineWindows
    {
        /// <summary>
        /// Virtual drive.
        /// </summary>
        private VirtualDriveBase virtualDrive;

        /// <summary>
        /// Logger.
        /// </summary>
        private ILogger logger;

        /// <summary>
        /// Enables or disables processing of changes made in IFile.CoseAsync(), IFileSystemItem.MoveToAsync(), IFileSystemItem.DeteteAsync().
        /// You will disable processing to debug synchronization service.
        /// </summary>
        internal bool ChangesProcessingEnabled = true;

        /// <summary>
        /// Creates a Windows user file system.
        /// </summary>
        /// <param name="license">A license string.</param>
        /// <param name="userFileSystemRootPath">A root folder of your user file system. Your file system tree will be located under this folder.</param>
        /// <param name="log">Logger.</param>
        internal VfsEngine(string license, string userFileSystemRootPath, VirtualDriveBase virtualDrive, ILog log) : base(license, userFileSystemRootPath)
        {
            logger = new Logger("File System Engine", log);

            this.virtualDrive = virtualDrive;

            // We want our file system to run regardless of any errors.
            // If any request to file system fails in user code or in Engine itself we continue processing.
            ThrowExceptions = false;

            StateChanged += Engine_StateChanged;
            Error += Engine_Error;
            Message += Engine_Message;
        }

        
        /// <inheritdoc/>
        public override async Task<IFileSystemItem> GetFileSystemItemAsync(string path)
        {
            if(File.Exists(path))
            {
                return new VfsFile(path, this, this, virtualDrive);
            }
            if(Directory.Exists(path))
            {
                return new VfsFolder(path, this, this, virtualDrive);
            }

            // Note that this method may be called for items that does not exists in file system,
            // for example when a a file handle is being closed during delete operation.
            // The Engine calls IFile.CloseAsync() and IFileSystemItem.DeleteCompletionAsync() methods in this case.
            return null; 
        }
        

        private void Engine_Message(IEngine sender, EngineMessageEventArgs e)
        {
            logger.LogMessage(e.Message, e.SourcePath, e.TargetPath);
        }

        private void Engine_Error(IEngine sender, EngineErrorEventArgs e)
        {
            logger.LogError(e.Message, e.SourcePath, e.TargetPath, e.Exception);
        }

        /// <summary>
        /// Show status change.
        /// </summary>
        /// <param name="engine">Engine</param>
        /// <param name="e">Contains new and old Engine state.</param>
        private void Engine_StateChanged(Engine engine, EngineWindows.StateChangeEventArgs e)
        {
            engine.LogMessage($"{e.NewState}");
        }
    }
}
