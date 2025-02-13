using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Concurrent;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.System;
using Windows.Graphics;
using WinUIEx;
using Windows.UI.WindowManagement;
using Windows.ApplicationModel.Resources;

using ITHit.FileSystem;
using ITHit.FileSystem.Samples.Common.Windows;
using ITHit.FileSystem.Windows;
using WindowManager = ITHit.FileSystem.Samples.Common.Windows.WindowManager;

using WebDAVDrive.Services;
using WebDAVDrive.ViewModels;
using WebDAVDrive.Extensions;

namespace WebDAVDrive.Dialogs
{
    /// <summary>
    /// Tray application window. Provides application commands and shows list of file system events.
    /// </summary>
    public sealed partial class Tray : Window
    {
        private const int animationDurationMs = 150; //how long Tray window animation durates after user clicks on tray icon
        private const int frameRate = 120; // frame rate of animation in FPS

        //left offset of the window set to 4/5 of screen width minus 30 pixels of right offset
        private const int windowWidth = 400;
        private const int windowHeight = 658;
        private const int maxScrollPositionToPerformAutoScroll = 5; //maximal scroll position in pixels when auto scroll to top performs on adding new events
        private int leftOffset; //left offset of the window - calculated at initialization

        /// <summary>
        /// Virtual engine instance.
        /// </summary>
        private readonly VirtualEngine engine;

        /// <summary>
        /// Domains service.
        /// </summary>
        private readonly IDrivesService drivesService;

        /// <summary>
        /// Key of Engine in Domains Dictionary.
        /// </summary>
        private readonly Guid engineKey;

        /// <summary>
        /// Log formatter.
        /// </summary>
        private readonly LogFormatter logFormatter;

        private readonly FilesEventsViewModel filesEventsViewModel = new FilesEventsViewModel();
        
        /// <summary>
        /// BlockingCollection of engine changing events.
        /// </summary>
        private BlockingCollection<ItemsChangeEventArgs> eventsQueue = new BlockingCollection<ItemsChangeEventArgs>();

        /// <summary>
        /// Task which processes events from eventsQueue collection.
        /// </summary>
        private Task? handlerEngineEventsTask;

        /// <summary>
        /// Cancellation token source used to cancel task which processes events from eventsQueue collection.
        /// </summary>
        private CancellationTokenSource engineEventsTaskCancellationTokenSource;

        /// <summary>
        /// Indicates if the window is pinned.
        /// </summary>
        public bool Pinned { get; set; } = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="Tray"/> class.
        /// </summary>
        /// <param name="engineKey">Key of Engine in Domains Dictionary.</param>
        /// <param name="engine">Engine instance.</param>
        /// <param name="drivesService">Domains Service.</param>
        public Tray(Guid engineKey, VirtualEngine engine, IDrivesService drivesService, LogFormatter logFormatter)
        {
            this.engine = engine;
            this.drivesService = drivesService;
            this.engineKey = engineKey;
            this.logFormatter = logFormatter;
            InitializeComponent();

            FilesEvents.DataContext = filesEventsViewModel;
            tbDriveName.Text = engine.RemoteStorageRootPath;

            ResourceLoader resourceLoader = ResourceLoader.GetForViewIndependentUse();
            tbSyncMessage.Text = resourceLoader.GetString("FilesSynchronized");
            HideShowLog.Text = resourceLoader.GetString("HideLog");
            EnableDisableDebugLogging.Text = logFormatter.DebugLoggingEnabled ?
                resourceLoader.GetString("DisableDebugLogging") : resourceLoader.GetString("EnableDebugLogging");
            HeaderText.Text = ServiceProvider.GetService<AppSettings>().ProductName;

            // Remove the window icon from the taskbar
            this.SetIsShownInSwitchers(false);

            AddAnimationHandlers();
            Activate();

            Closed += TrayClosed;
            Activated += TrayActivated;

            // Handle the engine state and items change events.
            engine.StateChanged += EngineStateChanged;
            engine.ItemsChanged += EngineItemsChanged;
            engine.SyncService.StateChanged += SyncServiceStateChanged;

#if DEBUG
            HideShowLog.Visibility = Visibility.Visible;
            EnableDisableDebugLogging.Visibility = Visibility.Visible;
#endif

            //Hide window on Esc key pressed.
            //This is not dialog window, so the behavior is different - so we should use separate Esc logic instead of deriving from DialogWindow.
            if (Content is UIElement rootContent)
            {
                rootContent.KeyDown += RootContent_KeyDown;
            }

            engineEventsTaskCancellationTokenSource = new CancellationTokenSource();
            handlerEngineEventsTask = CreateHandlerEventsTask(engineEventsTaskCancellationTokenSource.Token);
        }

        private void RootContent_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Escape)
            {
                HideWindow();
                //set pinned to false and update menu points visibility, as we made window hidden by Esc
                Pinned = false;
                Pin.Visibility = Visibility.Visible;
                Unpin.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Sets default UI parameters for the window. Make as separate method, because otherwise UI look is broken.
        /// </summary>
        public void SetInitialVisualParameters()
        {
            AppWindow.Move(new PointInt32(leftOffset, -10000));
            this.SetIsResizable(false);
            this.SetIsMaximizable(false);
            this.SetIsMinimizable(false);
            WinUIEx.WindowManager.Get(this).IsTitleBarVisible = false;
            this.Resize(windowWidth, windowHeight);
            leftOffset = DisplayArea.Primary.WorkArea.Width - AppWindow.Size.Width - 30; //30 pixels right margin
        }

        /// <summary>
        /// Shows the window with animation.
        /// </summary>
        public void ShowWithAnimation()
        {
            try
            {
                int actualHeight = AppWindow.Size.Height;

                AppWindow.Move(new PointInt32(leftOffset, DisplayArea.Primary.WorkArea.Height));
                this.SetForegroundWindow();

                // Get the screen dimensions
                int screenHeight = DisplayArea.Primary.WorkArea.Height;

                // Animate the window's Y position
                int startY = screenHeight;
                int endY = screenHeight - actualHeight + 8; //add 8 pixels to force final position right above task panel
                int totalFrames = animationDurationMs * frameRate / 1000; //frame rate is per second, and duration in ms - so we divide by 1000

                for (int frame = 0; frame <= totalFrames; frame++)
                {
                    double progress = (double)frame / totalFrames;
                    int currentY = (int)(startY + (endY - startY) * progress);

                    // Set the window position
                    AppWindow.Move(new PointInt32(leftOffset, currentY));
                    // Wait for the next frame
                    Thread.Sleep(1000 / frameRate); //frame rate is per second, so to get time span between frames we take 1000 ms and divide by rate
                }

                // Ensure the window is fully visible at the end
                AppWindow.Move(new PointInt32(leftOffset, endY));
                this.SetForegroundWindow();
                //set IsAlwaysOnTop to make the window always showing behind other windows, once it's pinned
                this.SetIsAlwaysOnTop(true);
            }
            catch { } // Ignore the exception that occurs when closing the window.
        }

        private void SyncServiceStateChanged(object sender, SynchEventArgs e)
        {
            ServiceProvider.DispatcherQueue.TryEnqueue(() =>
            {
                ResourceLoader resourceLoader = ResourceLoader.GetForViewIndependentUse();
                if (e.NewState == SynchronizationState.Synchronizing)
                {
                    imgSync.Style = (Style)Application.Current.Resources["TrayHeaderBottomImageSyncStyle"];
                    tbSyncMessage.Text = resourceLoader.GetString("FilesSynching");
                }
                else if (e.NewState == SynchronizationState.Disabled)
                {
                    imgSync.Style = (Style)Application.Current.Resources["TrayHeaderBottomImagePauseStyle"];
                    tbSyncMessage.Text = resourceLoader.GetString("FilesSynchronizationPaused");
                }
                else
                {
                    imgSync.Style = (Style)Application.Current.Resources["TrayHeaderBottomImageStyle"];
                    tbSyncMessage.Text = resourceLoader.GetString("FilesSynchronized");
                }
            });
        }

        private void EngineItemsChanged(Engine sender, ItemsChangeEventArgs e)
        {            
            if (ShouldShowEventInTray(e))
            {
                eventsQueue.Add(e);
            }
        }

        private bool ShouldShowEventInTray(ItemsChangeEventArgs e)
        {
            //Move and Delete operations not showing in Tray (as well as operations with status Filtered)
            //Errors shown only of types Conflict, Failed and Exception
            return e.OperationType != OperationType.Move && e.OperationType != OperationType.Delete &&
                   (e.Result.IsSuccess || e.Result.Status == OperationStatus.Conflict || e.Result.Status == OperationStatus.Failed
                || e.Result.Status == OperationStatus.Exception);
        }

        /// <summary>
        /// Starts thread that processes engine events queue.
        /// </summary>
        /// <param name="cancelationToken">The token to monitor for cancellation requests.</param>
        /// <returns></returns>
        private Task CreateHandlerEventsTask(CancellationToken cancelationToken)
        {
            return Task.Run(() =>
            {
                try
                {
                    while (!cancelationToken.IsCancellationRequested)
                    {
                        ProcessEngineEventAsync(eventsQueue.Take(cancelationToken));
                    }
                }
                catch (OperationCanceledException)
                {
                }
            },
            cancelationToken);
        }

        /// <summary>
        /// Processes single event from engine.
        /// </summary>
        /// <param name="eventArgs">Event args representing engine event.</param>
        /// <returns></returns>
        private void ProcessEngineEventAsync(ItemsChangeEventArgs eventArgs)
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            ServiceProvider.DispatcherQueue.TryEnqueue(async () =>
            {
                string progressString = string.Empty;
                bool isCompleted = true; //for most events set it to true by default
                bool ignoreEvent = false;
                bool shouldShowEventName = true;
                //only UpdateContent or populating events currently have progress notifications
                bool isEventWithProgress = eventArgs.OperationType == OperationType.UpdateContent || eventArgs.OperationType == OperationType.Populate;
                if (isEventWithProgress)
                {
                    if (eventArgs.NotificationTime.HasFlag(NotificationTime.Progress))
                    {
                        if (eventArgs.Position == eventArgs.Length)
                        {
                            //for Incoming - ignore progress event with final position - to avoid conflict with [NotificationTime == After] event
                            ignoreEvent = eventArgs.Direction == SyncDirection.Incoming;
                            isCompleted = true;
                        }
                        else
                        {
                            progressString = eventArgs.OperationType == OperationType.UpdateContent ?
                                $"{eventArgs.Position.GetUserFriendlySize()} of {eventArgs.Length.GetUserFriendlySize()}" :
                                $" {eventArgs.Position} of {eventArgs.Length}";
                            isCompleted = false;
                            //for UpdateContent with progress we don't show event name on UI - just progress, to save place
                            shouldShowEventName = eventArgs.OperationType != OperationType.UpdateContent;
                        }
                    }
                    //if progress event gives error or download/populate is finished - mark it as completed
                    if (eventArgs.NotificationTime == NotificationTime.After || !eventArgs.Result.IsSuccess)
                    {
                        isCompleted = true;
                    }
                }

                if (!ignoreEvent)
                {
                    //if Populate event we process only Parent item (folder); for other events we process Items array (usually contains a single item)
                    ChangeEventItem[] eventItems = eventArgs.OperationType == OperationType.Populate ? [eventArgs.Parent] : eventArgs.Items;
                    foreach (ChangeEventItem item in eventItems)
                    {
                        string actualPath = item.NewPath ?? item.Path;
                        string fileNameShowing = Path.GetFileName(actualPath);
                        //if the item is a root folder - we show remote URL without protocol (like it is showing in Windows Explorer)
                        if (actualPath == engine.Path)
                        {
                            Uri rootUri = new Uri(engine.RemoteStorageRootPath);
                            fileNameShowing = (rootUri.Authority + rootUri.PathAndQuery + rootUri.Fragment).TrimEnd('/');
                        }
                        //we modify existing UI row only in case the event is UpdateContent or Populate
                        FileEventViewModel? existingItem = null;
                        if (isEventWithProgress)
                        {
                            existingItem = filesEventsViewModel.LineItems.
                                FirstOrDefault(li => li.OperationType == eventArgs.OperationType && li.Path == actualPath && eventArgs.Direction == li.Direction &&
                                    (!li.IsCompleted || (DateTime.Now - li.Time).TotalSeconds <= 1));
                        }
                        if (existingItem == null)
                        {
                            await AddNewEventRow(eventArgs, item, fileNameShowing, progressString, isCompleted, shouldShowEventName);
                        }
                        else
                        {
                            await ModifyExistingEventRow(eventArgs, existingItem, progressString, isCompleted, shouldShowEventName);
                        }
                    }
                }

                if (svHistory.VerticalOffset <= maxScrollPositionToPerformAutoScroll)
                {
                    svHistory.ChangeView(horizontalOffset: null, verticalOffset: 0, zoomFactor: null, disableAnimation: false);
                }

                tcs.SetResult(true);
            });
            // Wait for UI thread to finish processing event.
            tcs.Task.Wait();
        }

        private async Task ModifyExistingEventRow(ItemsChangeEventArgs e, FileEventViewModel existingItem, string progressString, bool isCompleted,
            bool showEventName)
        {
            existingItem.ProgressText = progressString;
            existingItem.ProgressPercent = e.Length > 0 ? e.Position * 100 / e.Length : 0;
            existingItem.IsCompleted = isCompleted;
            existingItem.Time = DateTime.Now;
            existingItem.ProgressVisibility = isCompleted ? Visibility.Collapsed : Visibility.Visible;
            existingItem.EventNameVisibility = showEventName ? Visibility.Visible : Visibility.Collapsed;
            existingItem.ErrorMessage = e.Result.IsSuccess ? null : $"{e.Result.Status}. {e.Result.Exception?.Message ?? e.Result.Message}".Trim();
            //if it's operation with error - changing icon to error one
            if (!e.Result.IsSuccess)
            {
                existingItem.MainOverlayIcon = await GetMainOverlayIconAsync(e.OperationType, e.Direction, e.Result.Status);
            }
        }

        private async Task AddNewEventRow(ItemsChangeEventArgs e, ChangeEventItem engineItem, string fileNameShowing, string progressString, bool isCompleted,
            bool showEventName)
        {
            string actualPath = engineItem.NewPath ?? engineItem.Path;
            filesEventsViewModel.LineItems.Insert(0, new FileEventViewModel()
            {
                Path = actualPath,
                FileName = fileNameShowing,
                OperationType = e.OperationType,
                ProgressText = progressString,
                ProgressPercent = e.Length > 0 ? e.Position * 100 / e.Length : 0,
                Time = DateTime.Now,
                IsCompleted = isCompleted,
                ProgressVisibility = isCompleted ? Visibility.Collapsed : Visibility.Visible,
                EventNameVisibility = showEventName ? Visibility.Visible : Visibility.Collapsed,
                ErrorMessage = e.Result.IsSuccess ? null : $"{e.Result.Status}. {e.Result.Message}".Trim(),
                Direction = e.Direction,
                Thumbnail = await GetThumbnailAsync(engineItem),
                MainOverlayIcon = await GetMainOverlayIconAsync(e.OperationType, e.Direction, e.Result.Status),
                SyncTypeOverlayIcon = await GetSyncTypeOverlayIconAsync(e.Direction)
            });
        }

        private void EngineStateChanged(Engine engine, EngineWindows.StateChangeEventArgs e)
        {
            ServiceProvider.DispatcherQueue.TryEnqueue(() =>
            {
                if (e.NewState == EngineState.Stopped)
                {
                    StopSynchronization.Visibility = Visibility.Collapsed;
                    StartSynchronization.Visibility = Visibility.Visible;
                }
                else
                {
                    StartSynchronization.Visibility = Visibility.Collapsed;
                    StopSynchronization.Visibility = Visibility.Visible;
                }
            });
        }

        private void TrayActivated(object sender, WindowActivatedEventArgs args)
        {
            //hide window on losing focus only in case it is not pinned
            if (!Pinned && args.WindowActivationState == WindowActivationState.Deactivated)
            {
                HideWindow();
            }
        }

        //Event handlers for clicking on settings icon at top right (to make icon animation)
        private void AddAnimationHandlers()
        {
            ShowMenu.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(ShowMenuPointerPressed), true);
            ShowMenu.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(ShowMenuPointerReleased), true);
        }

        private void TrayClosed(object sender, WindowEventArgs args)
        {
            ShowMenu.RemoveHandler(UIElement.PointerPressedEvent, (PointerEventHandler)ShowMenuPointerPressed);
            ShowMenu.RemoveHandler(UIElement.PointerReleasedEvent, (PointerEventHandler)ShowMenuPointerReleased);
            engineEventsTaskCancellationTokenSource.Cancel();
            handlerEngineEventsTask?.Wait();
        }

        private void OnMenuButtonClicked(object sender, RoutedEventArgs e)
        {
            ResourceLoader resourceLoader = ResourceLoader.GetForViewIndependentUse();
            EnableDisableDebugLogging.Text = logFormatter.DebugLoggingEnabled ?
                resourceLoader.GetString("DisableDebugLogging") : resourceLoader.GetString("EnableDebugLogging");
            MainMenu.ShowAt(ShowMenu);
        }

        // Handle the click event of the expandable menu item
        private async void StartStopSynchronizationClicked(object sender, RoutedEventArgs e)
        {
            await engine.Commands.StartStopEngineAsync();
        }

        private void ShowMenuPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            AnimatedIcon.SetState(FindChildByName(ShowMenu, "AnimatedSettingsIcon"), "Normal");
        }

        private void ShowMenuPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            AnimatedIcon.SetState(FindChildByName(ShowMenu, "AnimatedSettingsIcon"), "Pressed");
        }

        private void HistoryItemPointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Border border)
            {
                AssignBrushToBorder(border, "HistoryItemHoveredBrush");
                //show menu button only in case it's not Delete or DeleteCompletion event (item is not deleted)
                if (border.Tag is FileEventViewModel fileEvent && FindChildByName(border, "ShowItemMenu") is Button button &&
                    fileEvent.OperationType != OperationType.Delete && fileEvent.OperationType != OperationType.DeleteCompletion)
                {
                    button.Visibility = Visibility.Visible;
                }
            }
        }

        private void HistoryItemPointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Border border)
            {
                AssignBrushToBorder(border, "TransparentBrush");
                if (FindChildByName(border, "ShowItemMenu") is Button button)
                {
                    button.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void HistoryItemPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Border border)
            {
                AssignBrushToBorder(border, "HistoryItemPressedBrush");
                //show menu button only in case it's not Delete or DeleteCompletion event (item is not deleted)
                if (border.Tag is FileEventViewModel fileEvent && FindChildByName(border, "ShowItemMenu") is Button button &&
                    fileEvent.OperationType != OperationType.Delete && fileEvent.OperationType != OperationType.DeleteCompletion)
                {
                    button.Visibility = Visibility.Visible;
                }
            }
        }

        /// <summary>
        /// Opens a window to mount a new drive.
        /// </summary>
        private void MountNewDriveClicked(object sender, RoutedEventArgs e)
        {
            new MountNewDrive().Show();
        }

        /// <summary>
        /// Sends feedback.
        /// </summary>
        private async void FeedbackMenuClicked(object sender, RoutedEventArgs e)
        {
            await Commands.OpenSupportPortalAsync();
        }

        /// <summary>
        /// Unmounts WebDAV drive.
        /// </summary>
        private async void UnmountClicked(object sender, RoutedEventArgs e)
        {
            await engine.Commands.StopEngineAsync();
            await drivesService.UnMountAsync(engineKey, engine.RemoteStorageRootPath);
        }

        /// <summary>
        /// Opens Log File.
        /// </summary>
        private void OpenLogFileClicked(object sender, RoutedEventArgs e)
        {
            if (logFormatter?.LogFilePath != null && File.Exists(logFormatter?.LogFilePath))
            {
                // Open log file.
                Commands.Open(logFormatter.LogFilePath);
            }
        }

        /// <summary>
        /// Hides/Shows console log.
        /// </summary>
        private void HideShowLogClicked(object sender, RoutedEventArgs e)
        {
            WindowManager.SetConsoleWindowVisibility(!WindowManager.ConsoleVisible);

            ResourceLoader resourceLoader = ResourceLoader.GetForViewIndependentUse();
            HideShowLog.Text = WindowManager.ConsoleVisible ? resourceLoader.GetString("HideLog") : resourceLoader.GetString("ShowLog");
        }

        /// <summary>
        /// Pins/Unpins Tray window.
        /// </summary>
        private void PinUnpinClicked(object sender, RoutedEventArgs e)
        {
            Pinned = !Pinned;
            Pin.Visibility = Pinned ? Visibility.Collapsed : Visibility.Visible;
            Unpin.Visibility = Pinned ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// Enables/Disables debug logging.
        /// </summary>
        private void EnableDisableDebugLoggingClicked(object sender, RoutedEventArgs e)
        {
            logFormatter.DebugLoggingEnabled = !logFormatter.DebugLoggingEnabled;
            ResourceLoader resourceLoader = ResourceLoader.GetForViewIndependentUse();
            EnableDisableDebugLogging.Text = logFormatter.DebugLoggingEnabled ?
                resourceLoader.GetString("DisableDebugLogging") : resourceLoader.GetString("EnableDebugLogging");
        }        

        /// <summary>
        /// Exit application.
        /// </summary>
        private void ExitClicked(object sender, RoutedEventArgs e)
        {
            // Close the window.
            Close();

            Application.Current.Exit();
        }

        /// <summary>
        /// Opens the folder in WebDAV Drive.
        /// </summary>
        private void OpenFolderClicked(object sender, RoutedEventArgs e)
        {
            Commands.Open(engine.Path);
        }

        /// <summary>
        /// Opens the remote storage folder.
        /// </summary>
        private void OpenRemoteStorageClicked(object sender, RoutedEventArgs e)
        {
            Commands.Open(engine.RemoteStorageRootPath);
        }

        private void ViewItemOnlineMenuClicked(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem)
            {
                if (menuItem.Tag is FileEventViewModel fileEvent)
                {
                    string url = engine.Mapping.MapPath(fileEvent.Path);
                    Commands.Open(url);
                }
            }
        }

        private void OpenItemMenuClicked(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem)
            {
                if (menuItem.Tag is FileEventViewModel fileEvent)
                {
                    engine.ClientNotifications(fileEvent.Path).ExecVerb(Verb.Open);
                }
            }
        }

        private void ShowInFolderMenuClicked(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem)
            {
                if (menuItem.Tag is FileEventViewModel fileEvent)
                {
                    engine.ClientNotifications(fileEvent.Path).ExecVerb(Verb.ShowInFolder);
                }
            }
        }

        private async Task<BitmapImage> GetThumbnailAsync(ChangeEventItem item)
        {
            BitmapImage thumbnailSource;

            try
            {
                string actualPath = item.NewPath ?? item.Path;
                StorageItemThumbnail? storageItemThumbnail = null;

                if (item.ItemType == FileSystemItemType.File)
                {
                    try
                    {
                        StorageFile file = await StorageFile.GetFileFromPathAsync(actualPath);
                        storageItemThumbnail = await file.GetThumbnailAsync(ThumbnailMode.SingleItem, 48);
                    }
                    catch (FileNotFoundException)
                    {
                        // File is missing, use a default thumbnail.
                        storageItemThumbnail = await GetDefaultThumbnailForFileType(actualPath);
                    }
                }
                else
                {
                    try
                    {
                        StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(actualPath);
                        storageItemThumbnail = await folder.GetThumbnailAsync(ThumbnailMode.SingleItem, 48);
                    }
                    catch (FileNotFoundException)
                    {
                        // Folder is missing, use a generic folder icon
                        storageItemThumbnail = await GetDefaultFolderThumbnail();
                    }
                }

                // Create a bitmap image from the thumbnail
                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.SetSource(storageItemThumbnail);
                thumbnailSource = bitmapImage;
            }
            catch (Exception)
            {
                thumbnailSource = item.ItemType == FileSystemItemType.File ? new BitmapImage(new Uri("ms-appx:///Images/File.png")) :
                    new BitmapImage(new Uri("ms-appx:///Images/FolderLight.png"));
            }

            return thumbnailSource;
        }

        private async Task<StorageItemThumbnail> GetDefaultThumbnailForFileType(string filePath)
        {
            // Create a temporary file with the given extension to get the system icon.
            StorageFolder tempFolder = ApplicationData.Current.TemporaryFolder;
            StorageFile fakeFile = await tempFolder.CreateFileAsync($"fake{Path.GetExtension(filePath)}", CreationCollisionOption.ReplaceExisting);

            // Request a system-generated thumbnail.
            StorageItemThumbnail thumbnail = await fakeFile.GetThumbnailAsync(ThumbnailMode.SingleItem, 48);

            return thumbnail;
        }

        private async Task<StorageItemThumbnail> GetDefaultFolderThumbnail()
        {
            // Create a temporary folder to get the system icon.
            StorageFolder tempFolder = ApplicationData.Current.TemporaryFolder;
            StorageFolder fakeFolder = await tempFolder.CreateFolderAsync($"fake", CreationCollisionOption.ReplaceExisting);

            // Request a system-generated thumbnail
            StorageItemThumbnail thumbnail = await fakeFolder.GetThumbnailAsync(ThumbnailMode.SingleItem, 48);

            return thumbnail;
        }
        private async Task<BitmapImage> GetMainOverlayIconAsync(OperationType type, SyncDirection direction, OperationStatus status)
        {
            BitmapImage result = null;

            string resourceName = string.Empty;
            //for non-Success status show error icon instead
            if (status != OperationStatus.Success)
            {
                resourceName = "ErrorPng";
            }
            else if (type == OperationType.Create || type == OperationType.CreateCompletion)
            {
                resourceName = "PlusPng";
            }
            else if (type == OperationType.Lock)
            {
                resourceName = "LockPng";
            }
            else if (type == OperationType.Unlock)
            {
                resourceName = "UnlockPng";
            }
            else if (type == OperationType.UpdateContent)
            {
                resourceName = direction == SyncDirection.Incoming ? "CloudDownloadPng" : "CloudUploadPng";
            }

            if (Application.Current.Resources.TryGetValue(resourceName, out object resource) && resource is BitmapImage bitmap)
            {
                result = bitmap;
            }

            return result;
        }

        private async Task<BitmapImage> GetSyncTypeOverlayIconAsync(SyncDirection direction)
        {
            BitmapImage result = null;

            string resourceName = string.Empty;
            if (direction == SyncDirection.Incoming)
            {
                resourceName = "DownloadPng";
            }
            else if (direction == SyncDirection.Outgoing)
            {
                resourceName = "UploadPng";
            }

            if (Application.Current.Resources.TryGetValue(resourceName, out object resource) && resource is BitmapImage bitmap)
            {
                result = bitmap;
            }

            return result;
        }

        private void AssignBrushToBorder(Border? border, string brushResourceName)
        {
            if (border != null && Application.Current.Resources.TryGetValue(brushResourceName, out object resource) && resource is SolidColorBrush brush)
            {
                border.Background = brush;
            }
        }

        /// <summary>
        /// Finds the child of UI element with given name. If not found - returns null.
        /// </summary>
        /// <param name="parent">Parent UI element for which we should find the child.</param>
        /// <param name="controlName">Name of desired child.</param>

        private static DependencyObject? FindChildByName(DependencyObject parent, string controlName)
        {
            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);

            for (int i = 0; i < childrenCount; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is FrameworkElement && ((FrameworkElement)child).Name == controlName)
                    return child;

                DependencyObject? result = FindChildByName(child, controlName);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        /// <summary>
        /// Hides the window from screen.
        /// </summary>
        private void HideWindow()
        {
            try
            {
                // Hide the window when it loses focus
                AppWindow.Move(new PointInt32(leftOffset, -10000));
                //unset IsAlwaysOnTop to make further animation showing behind taskbar
                this.SetIsAlwaysOnTop(false);
            }
            catch { }// Ignore the exception that occurs when closing the window.
        }
    }
}
