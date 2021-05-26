using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Provider;
using System.Linq;

namespace ITHit.FileSystem.Samples.Common.Windows
{
    /// <summary>
    /// Manages custom data associated with the item. 
    /// </summary>
    /// <remarks>
    /// We can not store custom data inside placeholder because of the MS Office transactional save, 
    /// which renames and deletes the file, so all custom data is lost with it.
    /// </remarks>
    public class CustomDataManager
    {
        /// <summary>
        /// Path in user file system with which this custom data corresponds.
        /// </summary>
        private readonly string userFileSystemPath;

        /// <summary>
        /// Path to the folder that stores custom data associated with files and folders.
        /// </summary>
        private readonly string serverDataFolderPath;

        /// <summary>
        /// Virtual file system root path.
        /// </summary>
        private readonly string userFileSystemRootPath;

        /// <summary>
        /// Path to the icons folder.
        /// </summary>
        private readonly string iconsFolderPath;

        /// <summary>
        /// Logger.
        /// </summary>
        private readonly ILogger logger;

        /// <summary>
        /// Path to the file that stores custom columns data.
        /// </summary>
        private readonly string customColumnsFilePath;

        /// <summary>
        /// Custom columns file extension.
        /// </summary>
        private const string customColumnsExt = ".columns";

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        public CustomDataManager(
            string userFileSystemPath, 
            string serverDataFolderPath, 
            string userFileSystemRootPath, 
            string iconsFolderPath,
            ILogger logger)
        {
            this.userFileSystemPath = userFileSystemPath ?? throw new NullReferenceException(nameof(userFileSystemPath));
            this.serverDataFolderPath = serverDataFolderPath ?? throw new NullReferenceException(nameof(serverDataFolderPath));
            this.userFileSystemRootPath = userFileSystemRootPath ?? throw new NullReferenceException(nameof(userFileSystemRootPath));
            this.iconsFolderPath = iconsFolderPath ?? throw new NullReferenceException(nameof(iconsFolderPath));
            this.logger = logger ?? throw new NullReferenceException(nameof(logger));

            customColumnsFilePath = $"{GetColumnsFilePath(userFileSystemPath)}{customColumnsExt}";
        }

        /// <summary>
        /// Indicates if the item was saved to the remote storage.
        /// </summary>
        /// <remarks>
        /// To detect if this is a new file we must store some marker ouside of the file, 
        /// for the marker to survive MS Office transactional save, which deletes and recreates the file.
        /// </remarks>
        public bool IsNew
        {
            get 
            {
                // If ETag file exists, this means the data was succcesefully saved to the server.
                ETagManager eTagManager = new ETagManager(userFileSystemPath, serverDataFolderPath, userFileSystemRootPath, logger);
                return !eTagManager.ETagExists();
            }
            set
            {
                ETagManager eTagManager = new ETagManager(userFileSystemPath, serverDataFolderPath, userFileSystemRootPath, logger);
                if(value)
                {
                    eTagManager.DeleteETag();
                }
                else
                {
                    eTagManager.EnsureETagExists();
                }
            }
        }

        /*
        internal async Task ClearStateAsync()
        {
            if (FsPath.Exists(userFileSystemPath))
            {
                await SetIconAsync(false);
            }
        }

        internal async Task SetUploadErrorStateAsync(Exception ex)
        {
            if (FsPath.Exists(userFileSystemPath))
            {
                if (ex is ConflictException)
                {
                    await SetConflictIconAsync(true);
                }
                else
                {
                    await SetUploadPendingIconAsync(true);
                }
            }
        }

        private async Task SetDownloadErrorStateAsync(Exception ex)
        {
            if (FsPath.Exists(userFileSystemPath))
            {
                if (ex is ConflictException)
                {
                    await SetConflictIconAsync(true);
                }
                if (ex is ExistsException)
                {
                    await SetConflictIconAsync(true);
                }
                else
                {
                    // Could be BlockedException or other exception.
                    await SetDownloadPendingIconAsync(true);
                }
            }
        }

        /// <summary>
        /// Sets or removes "Download pending" icon.
        /// </summary>
        /// <param name="set">True to display the icon. False - to remove the icon.</param>
        private async Task SetDownloadPendingIconAsync(bool set)
        {
            //            await SetIconAsync(set, 2, "Down.ico", "Download from server pending");
        }

        /// <summary>
        /// Sets or removes "Upload pending" icon.
        /// </summary>
        /// <param name="set">True to display the icon. False - to remove the icon.</param>
        private async Task SetUploadPendingIconAsync(bool set)
        {
            //            await SetIconAsync(set, 2, "Up.ico", "Upload to server pending");
        }
        */

        /// <summary>
        /// Sets or removes "Conflict" icon.
        /// </summary>
        /// <param name="set">True to display the icon. False - to remove the icon.</param>
        private async Task SetConflictIconAsync(bool set)
        {
            await SetIconAsync(set, (int)CustomColumnIds.ConflictIcon, "Error.ico", "Conflict. File is modified both on the server and on the client.");
        }

        /// <summary>
        /// Sets or removes "Lock" icon and all lock properties.
        /// </summary>
        /// <param name="lockInfo">Information about the lock. Pass null to remove lock info.</param>
        public async Task SetLockInfoAsync(ServerLockInfo lockInfo)
        {
            if (lockInfo != null)
            {
                // Add lock info columns.
                IEnumerable<FileSystemItemPropertyData> lockProps = lockInfo.GetLockProperties(Path.Combine(iconsFolderPath, "Locked.ico"));
                await SetCustomColumnsAsync(lockProps);
            }
            else
            {
                // Remove lock info columns.                
                IEnumerable<FileSystemItemPropertyData> lockProps = new ServerLockInfo().GetLockProperties(null);
                await RemoveCustomColumnsAsync(lockProps);
            }
        }

        /// <summary>
        /// Sets or removes "Lock pending" icon.
        /// </summary>
        /// <param name="set">True to display the icon. False - to remove the icon.</param>
        public async Task SetLockPendingIconAsync(bool set)
        {
            await SetIconAsync(set, (int)CustomColumnIds.LockOwnerIcon, "LockedPending.ico", "Updating lock...");
        }

        /// <summary>
        /// Sets or removes icon.
        /// </summary>
        /// <param name="set">True to display the icon. False - to remove the icon.</param>
        private async Task SetIconAsync(bool set, int id, string iconFile = null, string description = null)
        {
            if(set)
            {
                string iconFilePath = Path.Combine(iconsFolderPath, iconFile);
                FileSystemItemPropertyData propData = new FileSystemItemPropertyData((int)id, description, iconFilePath);
                await SetCustomColumnsAsync(new []{ propData });
            }
            else
            {
                FileSystemItemPropertyData propData = new FileSystemItemPropertyData((int)id, null);
                await RemoveCustomColumnsAsync(new []{ propData });
            }
        }

        /*
        /// <summary>
        /// Sets or removes icon.
        /// </summary>
        /// <param name="set">True to display the icon. False - to remove the icon.</param>
        private async Task SetIconAsync(bool set, int? id = null, string iconFile = null, string description = null)
        {
            IStorageItem storageItem = await FsPath.GetStorageItemAsync(userFileSystemPath);

            if (storageItem == null)
            {
                // This method may be called on temp files, typically created by MS Office, that exist for a short period of time.
                // StorageProviderItemProperties.SetAsync(null,) causes AccessViolationException 
                // which is not handled by .NET (or handled by HandleProcessCorruptedStateExceptions) and causes a fatal crush.
                return;
            }

            try
            {
                if (set)
                {
                    StorageProviderItemProperty propState = new StorageProviderItemProperty()
                    {
                        Id = id.Value,
                        Value = description,
                        IconResource = Path.Combine(virtualDrive.Settings.IconsFolderPath, iconFile)
                    };
                    await StorageProviderItemProperties.SetAsync(storageItem, new StorageProviderItemProperty[] { propState });
                }
                else
                {
                    await StorageProviderItemProperties.SetAsync(storageItem, new StorageProviderItemProperty[] { });
                }
            }

            // Setting status icon failes for blocked files.
            catch (FileNotFoundException)
            {

            }
            catch (COMException)
            {
                // "Error HRESULT E_FAIL has been returned from a call to a COM component."
            }
            catch (Exception ex)
            {
                if (ex.HResult == -2147024499)
                {
                    // "The operation failed due to a conflicting cloud file property lock. (0x8007018D)"
                }
                else
                {
                    // Rethrow the exception preserving stack trace of the original exception.
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
                }
            }
        }
        */
        
        /// <summary>
        /// Sets or removes read-only attribute on files.
        /// </summary>
        /// <param name="set">True to set the read-only attribute. False - to remove the read-only attribute.</param>
        public async Task<bool> SetLockedByAnotherUserAsync(bool set)
        {
            bool resultSet = false;
            // Changing read-only attribute on folders triggers folders listing. Changing it on files only.

            if (FsPath.IsFile(userFileSystemPath))
            {
                FileInfo file = new FileInfo(userFileSystemPath);
                if (set != file.IsReadOnly)
                {
                    // Set/Remove read-only attribute.
                    if (set)
                    {
                        new FileInfo(userFileSystemPath).Attributes |= System.IO.FileAttributes.ReadOnly;
                    }
                    else
                    {
                        new FileInfo(userFileSystemPath).Attributes &= ~System.IO.FileAttributes.ReadOnly;
                    }

                    resultSet = true;
                }
            }

            return resultSet;
        }

        /// <summary>
        /// Removes columns.
        /// </summary>
        /// <param name="customColumnsIDs">List of columns to remove.</param>
        public async Task RemoveCustomColumnsAsync(IEnumerable<FileSystemItemPropertyData> customColumnsData)
        {
            // All cutom columns must be set togather, otherwise columns data will be wiped.

            // Read custom columns.
            List<FileSystemItemPropertyData> allColumns = (await ReadCustomColumnsAsync()).ToList();

            // Remove columns.
            foreach (var removeColumn in customColumnsData)
            {
                var column = allColumns.FirstOrDefault(x => x.Id == removeColumn.Id);
                if (column != null)
                {
                    allColumns.Remove(column);
                }
            }

            // Save custom columns
            await WriteCustomColumnsAsync(allColumns);

            // Display in Windows File Manager.
            await ShowCustomColumnsAsync(allColumns);

        }

        /// <summary>
        /// Adds or updates custom columns data, preserving existing columns.
        /// </summary>
        /// <param name="customColumnsData">List of columns to add or update.</param>
        public async Task SetCustomColumnsAsync(IEnumerable<FileSystemItemPropertyData> customColumnsData)
        {
            // All cutom columns must be set togather, otherwise columns data will be wiped.

            // Read custom columns.
            IEnumerable<FileSystemItemPropertyData> allColumns = await ReadCustomColumnsAsync();

            // Merge existing colums with columns that must be set.
            IList<FileSystemItemPropertyData> newColumns = new List<FileSystemItemPropertyData>(customColumnsData);
            foreach (var curColumn in allColumns)
            {
                var column = customColumnsData.FirstOrDefault(x => x.Id == curColumn.Id);
                if(column == null)
                {
                    newColumns.Add(curColumn);
                }
            }

            // Save custom columns
            await WriteCustomColumnsAsync(newColumns);

            // Display in Windows File Manager.
            await ShowCustomColumnsAsync(newColumns);
        }

        /// <summary>
        /// Reads custom columns data from persistent media.
        /// </summary>
        private async Task<IEnumerable<FileSystemItemPropertyData>> ReadCustomColumnsAsync()
        {
            if (!File.Exists(customColumnsFilePath))
            {
                return new List<FileSystemItemPropertyData>();
            }
            await using (FileStream stream = File.Open(customColumnsFilePath, FileMode.Open, FileAccess.Read, FileShare.Delete | FileShare.Read))
            {
                stream.Seek(0, SeekOrigin.Begin);
                return await JsonSerializer.DeserializeAsync<IEnumerable<FileSystemItemPropertyData>>(stream);
            }
        }

        /// <summary>
        /// Saves custom columns data to persistent media.
        /// </summary>
        private async Task WriteCustomColumnsAsync(IEnumerable<FileSystemItemPropertyData> customColumnsData)
        {
            if (customColumnsData.Any())
            {
                await using (FileStream stream = File.Open(customColumnsFilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Delete))
                {
                    stream.Seek(0, SeekOrigin.Begin);
                    await JsonSerializer.SerializeAsync(stream, customColumnsData);
                    stream.SetLength(stream.Position);
                }
            }
            else
            {
                File.Delete(customColumnsFilePath);
            }
        }

        /// <summary>
        /// Loads data saved for custom columns and shows it in Windows File Manager.
        /// </summary>
        /// <returns></returns>
        public async Task RefreshCustomColumnsAsync()
        {
            await ShowCustomColumnsAsync(await ReadCustomColumnsAsync());
        }

        /// <summary>
        /// Displays custom columns in Windows file mnager.
        /// </summary>
        /// <param name="customColumnsData">list of columns to display.</param>
        private async Task ShowCustomColumnsAsync(IEnumerable<FileSystemItemPropertyData> customColumnsData)
        {
            List<StorageProviderItemProperty> customColumns = new List<StorageProviderItemProperty>();

            if (customColumnsData != null)
            {
                foreach (FileSystemItemPropertyData column in customColumnsData)
                {
                    customColumns.Add(new StorageProviderItemProperty()
                    {
                        Id = column.Id,
                        // If value is empty Windows File Manager crushes.
                        Value = string.IsNullOrEmpty(column.Value) ? "-" : column.Value,
                        // If icon is not set Windows File Manager crushes.
                        IconResource = column.IconResource ?? Path.Combine(iconsFolderPath, "Blank.ico")
                    });
                }
            }

            // This method may be called on temp files, typically created by MS Office, that exist for a short period of time.          
            IStorageItem storageItem = await FsPath.GetStorageItemAsync(userFileSystemPath);
            if (storageItem == null)
            {
                // This method may be called on temp files, typically created by MS Office, that exist for a short period of time.
                // StorageProviderItemProperties.SetAsync(null,) causes AccessViolationException 
                // which is not handled by .NET (nor handled by HandleProcessCorruptedStateExceptions) and causes a fatal crush.
                return;
            }

            FileInfo file = new FileInfo(userFileSystemPath);

            // Can not set provider properties on read-only files.
            // Changing read-only attribute on folders triggers folders listing. Changing on files only.
            bool readOnly = file.IsReadOnly;

            // Remove read-only attribute.
            if (readOnly && ((file.Attributes & System.IO.FileAttributes.Directory) == 0))
            {
                file.IsReadOnly = false;
                //new FileInfo(userFileSystemPath).Attributes &= ~System.IO.FileAttributes.ReadOnly;
            }

            // Update columns data.
            await StorageProviderItemProperties.SetAsync(storageItem, customColumns);

            // Set read-only attribute.
            if (readOnly && ((file.Attributes & System.IO.FileAttributes.Directory) == 0))
            {
                file.IsReadOnly = true;
                //new FileInfo(userFileSystemPath).Attributes |= System.IO.FileAttributes.ReadOnly;
            }
        }


        /// <summary>
        /// Provides methods for reading and writing ETags.
        /// </summary>
        public ETagManager ETagManager
        {
            get { return new ETagManager(userFileSystemPath, serverDataFolderPath, userFileSystemRootPath, logger); }
        }

        /// <summary>
        /// Manages lock-info and lock-mode files that correspond with the file in the user file system. 
        /// </summary>
        public LockManager LockManager
        {
            get { return new LockManager(userFileSystemPath, serverDataFolderPath, userFileSystemRootPath, logger); }
        }

        /// <summary>
        /// Moves all custom data to a new location.
        /// </summary>
        /// <param name="userFileSystemNewPath">Path of the file in the user file system to move this data to.</param>
        public async Task MoveToAsync(string userFileSystemNewPath)
        {
            await CustomColumnsMoveToAsync(userFileSystemNewPath);
            await ETagManager.MoveToAsync(userFileSystemNewPath);
            await LockManager.MoveToAsync(userFileSystemNewPath);            
        }

        /// <summary>
        /// Gets custom columns file path (without extension).
        /// </summary>
        /// <param name="userFileSystemPath">Path of the file in user file system to get the path for.</param>
        private string GetColumnsFilePath(string userFileSystemPath)
        {
            // Get path relative to the virtual root.
            string relativePath = userFileSystemPath.TrimEnd(Path.DirectorySeparatorChar).Substring(
                userFileSystemRootPath.TrimEnd(Path.DirectorySeparatorChar).Length);

            return $"{serverDataFolderPath.TrimEnd(Path.DirectorySeparatorChar)}{relativePath}";
        }


        /// <summary>
        /// Moves custom columns to a new location.
        /// </summary>
        /// <param name="userFileSystemNewPath">Path of the file in the user file system to move custom columns to.</param>
        private async Task CustomColumnsMoveToAsync(string userFileSystemNewPath)
        {
            // Move custom columns file.
            string customColumnsTargetPath = GetColumnsFilePath(userFileSystemNewPath);
            string customColumnsFileTargetPath = $"{customColumnsTargetPath}{customColumnsExt}";

            // Ensure the target directory exisit, in case we are moving into empty folder or which is offline.
            new FileInfo(customColumnsFileTargetPath).Directory.Create();

            if (File.Exists(customColumnsFilePath))
            {
                File.Move(customColumnsFilePath, customColumnsFileTargetPath);
            }

            // If this is a folder, move all data in this folder.
            string customColumnsSourceFolderPath = GetColumnsFilePath(userFileSystemPath);
            if (Directory.Exists(customColumnsSourceFolderPath))
            {
                Directory.Move(customColumnsSourceFolderPath, customColumnsTargetPath);
            }
        }

        /// <summary>
        /// Deletes all custom data associated with the item.
        /// </summary>
        public void Delete()
        {
            DeleteCustomColumns();
            ETagManager.DeleteETag();
            LockManager.DeleteLock();
        }

        /// <summary>
        /// Deletes custom columns associated with a file.
        /// </summary>
        private void DeleteCustomColumns()
        {
            File.Delete(customColumnsFilePath);

            // If this is a folder, delete all custom columns in this folder.
            string customColumnsFolderPath = GetColumnsFilePath(userFileSystemPath);
            if (Directory.Exists(customColumnsFolderPath))
            {
                Directory.Delete(customColumnsFolderPath, true);
            }
        }
    }
}
