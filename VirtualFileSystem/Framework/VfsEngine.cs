using ITHit.FileSystem;
using ITHit.FileSystem.Windows;
using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.Provider;

namespace VirtualFileSystem
{
    /// <inheritdoc/>
    internal class VfsEngine : EngineWindows
    {
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
        /// <param name="path">A root folder of your user file system. Your file system tree will be located under this folder.</param>
        /// <param name="log">Logger.</param>
        internal VfsEngine(string license, string path, ILog log) : base(license, path)
        {
            logger = new Logger("File System Engine", log);

            // We want our file system to run regardless of any errors.
            // If any request to file system fails in user code or in Engine itself we continue processing.
            ThrowExceptions = false;

            StateChanged += Engine_StateChanged;
            Error += Engine_Error;
            Message += Engine_Message;
        }

        //$<Engine.GetFileSystemItemAsync
        /// <inheritdoc/>
        public override async Task<IFileSystemItem> GetFileSystemItemAsync(string path)
        {
            if(File.Exists(path))
            {
                return new VfsFile(path, this, this);
            }
            if(Directory.Exists(path))
            {
                return new VfsFolder(path, this, this);
            }

            // When a file handle is being closed during delete, the file does not exist, return null.
            return null; 
        }
        //$>

        /// <summary>
        /// Keeps the last logged message, to minimize number of messages being logged.
        /// </summary>
        //private static string lastMessage = null;

        private void Engine_Message(Engine sender, Engine.MessageEventArgs e)
        {
            logger.LogMessage(e.Message, e.SourcePath, e.TargetPath);

            /*
            // Because the applications may make alot of identical calls to file system, 
            // to make logs more readable we just log "." instead of a duplicate message.
            if (lastMessage != e.Message)
            {
                log.Debug($"\n{DateTime.Now} [{Thread.CurrentThread.ManagedThreadId,2}] {"File System Engine: ",-26}{e.Message}");
                lastMessage = e.Message;
            }
            else
            {
                log.Debug(".");
            }
            */
        }

        private void Engine_Error(Engine sender, Engine.ErrorEventArgs e)
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
