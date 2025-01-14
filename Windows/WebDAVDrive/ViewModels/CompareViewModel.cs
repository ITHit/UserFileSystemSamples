using System;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;
using CommunityToolkit.Mvvm.ComponentModel;

using DavFIle = ITHit.WebDAV.Client.IFile;
using ITHit.FileSystem;
using ITHit.FileSystem.Samples.Common;
using ITHit.FileSystem.Windows;


namespace WebDAVDrive.ViewModels
{
    /// <summary>
    /// Compare view model.
    /// </summary>
    public class CompareViewModel : ObservableObject
    {
        private string localPath = string.Empty;
        private string remotePath = string.Empty;
        private string localCreationDate = string.Empty;
        private string remoteCreationDate = string.Empty;
        private string localModificationDate = string.Empty;
        private string remoteModificationDate = string.Empty;
        private string localSize = string.Empty;
        private string localContentETag = string.Empty;
        private string remoteContentETag = string.Empty;
        private string localMetadataETag = string.Empty;
        private string remoteMetadataETag = string.Empty;
        private string remoteSize = string.Empty;
        private string bytesDifferent = string.Empty;

        private bool isLoading = false;
        private bool isMergeButtonEnabled = false;
        private bool isTakeLocalVersionButtonEnabled = false;
        private bool isServerLocalVersionButtonEnabled = false;
        private bool isCloseButtonEnabled = false;

        /// <summary>
        /// File Path.
        /// </summary>
        private readonly string filePath;

        /// <summary>
        /// Engine instance.
        /// </summary>
        private readonly VirtualEngine engine;

        /// <summary>
        /// Placeholder File.
        /// </summary>
        private PlaceholderFile? placeholderFile;

        /// <summary>
        /// Remote storage file.
        /// </summary>
        private FileMetadataExt? remoteFileMetadata;

        /// <summary>
        /// Path of the local file.
        /// </summary>
        public string LocalPath
        {
            get { return localPath; }
            set
            {
                SetProperty(ref localPath, value);
            }
        }

        /// <summary>
        /// Path of the remote file.
        /// </summary>
        public string RemotePath
        {
            get { return remotePath; }
            set
            {
                SetProperty(ref remotePath, value);
            }
        }

        /// <summary>
        /// Local ETag of the metadata.
        /// </summary>
        public string LocalContentETag
        {
            get { return localContentETag; }
            set
            {
                SetProperty(ref localContentETag, value);
            }
        }

        /// <summary>
        /// Remote ETag of the metadata.
        /// </summary>
        public string RemoteContentETag
        {
            get { return remoteContentETag; }
            set
            {
                SetProperty(ref remoteContentETag, value);
            }
        }

        /// <summary>
        /// Local ETag of the metadata.
        /// </summary>
        public string LocalMetadataETag
        {
            get { return localMetadataETag; }
            set
            {
                SetProperty(ref localMetadataETag, value);
            }
        }

        /// <summary>
        /// Remote ETag of the metadata.
        /// </summary>
        public string RemoteMetadataETag
        {
            get { return remoteMetadataETag; }
            set
            {
                SetProperty(ref remoteMetadataETag, value);
            }
        }

        /// <summary>
        /// Creation date of the local file.
        /// </summary>
        public string LocalCreationDate
        {
            get { return localCreationDate; }
            set
            {
                SetProperty(ref localCreationDate, value);
            }
        }

        /// <summary>
        /// Creation date of the remote file.
        /// </summary>
        public string RemoteCreationDate
        {
            get { return remoteCreationDate; }
            set
            {
                SetProperty(ref remoteCreationDate, value);
            }
        }

        /// <summary>
        /// Size of the local file.
        /// </summary>
        public string LocalSize
        {
            get { return localSize; }
            set
            {
                SetProperty(ref localSize, value);
            }
        }


        /// <summary>
        /// Size of the remote file.
        /// </summary>
        public string RemoteSize
        {
            get { return remoteSize; }
            set
            {
                SetProperty(ref remoteSize, value);
            }
        }

        /// <summary>
        /// Modification date of the remote file.
        /// </summary>
        public string LocalModificationDate
        {
            get { return localModificationDate; }
            set
            {
                SetProperty(ref localModificationDate, value);
            }
        }

        /// <summary>
        /// Modification date of the remote file.
        /// </summary>
        public string RemoteModificationDate
        {
            get { return remoteModificationDate; }
            set
            {
                SetProperty(ref remoteModificationDate, value);
            }
        }

        public string BytesDifferent
        {
            get { return bytesDifferent; }
            set
            {
                SetProperty(ref bytesDifferent, value);
            }
        }

        /// <summary>
        /// Indicates whether data is being loaded from the server.
        /// </summary>
        public bool IsLoading
        {
            get { return isLoading; }
            set
            {
                SetProperty(ref isLoading, value);
            }
        }

        /// <summary>
        /// Indicates whether the merge button is enabled.
        /// </summary>
        public bool EnableMergeButton
        {
            get { return isMergeButtonEnabled; }
            set
            {
                SetProperty(ref isMergeButtonEnabled, value);
            }
        }

        /// <summary>
        /// Indicates whether the take server version button is enabled.
        /// </summary>
        public bool EnableTakeLocalVersionButton
        {
            get { return isTakeLocalVersionButtonEnabled; }
            set
            {
                SetProperty(ref isTakeLocalVersionButtonEnabled, value);
            }
        }

        /// <summary>
        /// Indicates whether the take local version button is enabled.
        /// </summary>
        public bool EnableTakeRemoteVersionButton
        {
            get { return isServerLocalVersionButtonEnabled; }
            set
            {
                SetProperty(ref isServerLocalVersionButtonEnabled, value);
            }
        }

        /// <summary>
        /// Indicates whether buttons in general are enabled.
        /// </summary>
        public bool EnableCloseButton
        {
            get { return isCloseButtonEnabled; }
            set
            {
                SetProperty(ref isCloseButtonEnabled, value);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CompareViewModel"/> class.
        /// </summary>
        public CompareViewModel(string filePath, VirtualEngine engine)
        {
            this.filePath = filePath;
            this.engine = engine;
        }

        /// <summary>
        /// Loads data from the server asynchronously.
        /// </summary>
        public async Task LoadFileLastServerVersionAsync()
        {
            IsLoading = true;

            if (engine != null && engine.Placeholders.TryGetItem(filePath, out PlaceholderItem placeholder))
            {
                ResourceLoader resourceLoader = ResourceLoader.GetForViewIndependentUse();
                string bytesLabel = resourceLoader.GetString("Bytes");
                string bytesDifferentLabel = resourceLoader.GetString("BytesDifferent");

                FileInfo fileInfo = new FileInfo(placeholder.Path);
                DavFIle? remoteStorageFile = (await engine.DavClient.GetFileAsync(new Uri(engine.Mapping.MapPath(placeholder.Path)), Mapping.GetDavProperties(), null)).WebDavResponse;
                remoteFileMetadata = (FileMetadataExt)Mapping.GetUserFileSystemItemMetadata(remoteStorageFile);

                //assuming max size of paths shown is 25 symbols - if more, it shortens with "..." at beginning
                string localPath = EllipsisAtStart(placeholder.Path, 25, '\\');
                string remotePath = EllipsisAtStart(remoteStorageFile.Href.ToString(), 25, '/');

                LocalPath = localPath;
                LocalContentETag = placeholder.Properties.GetContentETag();
                LocalMetadataETag = placeholder.Properties.GetMetadataETag();
                LocalCreationDate = fileInfo.CreationTime.ToString();
                LocalModificationDate = fileInfo.LastWriteTime.ToString();
                LocalSize = $"{fileInfo.Length} {bytesLabel}";

                RemotePath = remotePath;
                RemoteContentETag = remoteFileMetadata.ContentETag;
                RemoteMetadataETag = remoteFileMetadata.MetadataETag;
                RemoteCreationDate = remoteStorageFile.CreationDate.ToString();
                RemoteModificationDate = remoteStorageFile.LastModified.ToString();
                RemoteSize = $"{remoteFileMetadata.Length} {bytesLabel}";

                BytesDifferent = $"{bytesDifferentLabel} {Math.Abs(fileInfo.Length - remoteFileMetadata.Length ?? 0)}";

                placeholderFile = placeholder as PlaceholderFile;
                EnableButtons();
            }
            IsLoading = false;
        }

        /// <summary>
        /// Disables the buttons.
        /// </summary>
        public void DisableButtons()
        {
            EnableMergeButton = EnableTakeLocalVersionButton = EnableTakeRemoteVersionButton = false;
            EnableCloseButton = false;
        }

        public void EnableButtons()
        {
            EnableMergeButton = engine.Settings.Compare.ContainsKey(Path.GetExtension(filePath)) || Path.GetExtension(filePath) == ".docx";
            EnableTakeLocalVersionButton = EnableTakeRemoteVersionButton = new FileInfo(placeholderFile?.Path!).Length != remoteFileMetadata?.Length ||
                                     LocalContentETag != RemoteContentETag || LocalMetadataETag != RemoteMetadataETag;
            EnableCloseButton = true;
        }

        /// <summary>
        /// Resolves the conflict by taking the local version.
        /// </summary>
        public async Task ResolveConflictTakeServerVersionAsync()
        {
            if (placeholderFile != null && remoteFileMetadata != null)
            {
                placeholderFile.SetInSync(true);
                await engine.ServerNotifications(placeholderFile.Path).UpdateAsync(remoteFileMetadata);
            }
        }

        /// <summary>
        /// Resolves the conflict by taking the server version.
        /// </summary>
        public async Task ResolveConflictTakeLocalVersionAsync()
        {
            if (placeholderFile != null && remoteFileMetadata != null)
            {
                placeholderFile.SetContentETag(remoteFileMetadata.ContentETag);
                placeholderFile.SetInSync(false);
            }
        }


        public async Task ResolveConflictMergeAsync()
        {
            if (placeholderFile != null && remoteFileMetadata != null)
            {
                OperationResult res = await placeholderFile.TryShadowDownloadAsync(remoteFileMetadata.Length ?? 0, default);
                if (Path.GetExtension(filePath) == ".docx")
                {
                    ITHit.FileSystem.Windows.AppHelper.Utilities.TryCompare(placeholderFile.Path, res.ShadowFilePath);
                }
                else
                {
                    string extension = Path.GetExtension(filePath);
                    string compareTool = engine.Settings.Compare[extension];
                    string localPath = placeholderFile.Path;
                    string command = string.Format(compareTool, localPath, res.ShadowFilePath);

                    // Open the compare tool.
                    System.Diagnostics.Process.Start(command);
                }

            }
        }

        /// <summary>
        /// Trims long path string at start, if it exceeds given length. Trimmed string will start from '...' followed by passed kind of slash character.
        /// </summary>
        /// <param name="path">Full path string.</param>
        /// <param name="maxCharsToShow">Max characters count to show.</param>
        /// <param name="kindOfSlashUsed">Character used as slash in given path (e.g. '\\' in local path, but '/' in remote path).</param>
        /// <returns></returns>
        private string EllipsisAtStart(string path, int maxCharsToShow, char kindOfSlashUsed)
        {
            string result = path;
            if (result.Length > maxCharsToShow)
            {
                int usedSlashIndex = result.IndexOf(kindOfSlashUsed, result.Length - maxCharsToShow);
                result = "..." + result.Substring(usedSlashIndex == -1 ? result.Length - maxCharsToShow : usedSlashIndex);
            }
            return result;
        }
    }
}
