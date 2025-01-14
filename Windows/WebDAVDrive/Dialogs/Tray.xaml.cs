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

namespace WebDAVDrive.Dialogs
{
    /// <summary>
    /// Tray application window. Provides application commands and shows list of file system events.
    /// </summary>
    public sealed partial class Tray : Window
    {
        private const int animationDurationMs = 150; //how long Tray window animation durates after user clicks on tray icon
        private const int frameRate = 120; // frame rate of animation in FPS

        private readonly int leftOffset = DisplayArea.Primary.WorkArea.Width - 370; //left offset of the window
        private const int windowWidth = 340;
        private const int windowHeight = 658;


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
#endif

            //Hide window on Esc key pressed.
            //This is not dialog window, so the behavior is different - so we should use separate Esc logic instead of deriving from DialogWindow.
            if (Content is UIElement rootContent)
            {
                rootContent.KeyDown += (sender, e) =>
                {
                    if (e.Key == VirtualKey.Escape)
                    {
                        HideWindow();
                    }
                };
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
            AppWindow.Resize(new SizeInt32(windowWidth, windowHeight));
        }

        /// <summary>
        /// Shows the window with animation.
        /// </summary>
        public void ShowWithAnimation()
        {
            AppWindow.Move(new PointInt32(leftOffset, DisplayArea.Primary.WorkArea.Height));
            this.SetForegroundWindow();

            // Get the screen dimensions
            int screenHeight = DisplayArea.Primary.WorkArea.Height;

            // Animate the window's Y position
            int startY = screenHeight;
            int endY = screenHeight - windowHeight + 8; //add 8 pixels to force final position right above task panel
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

        private async void EngineItemsChanged(Engine sender, ItemsChangeEventArgs e)
        {
            ServiceProvider.DispatcherQueue.TryEnqueue(async () =>
            {
                if (e.Result.IsSuccess)
                {
                    foreach (ChangeEventItem item in e.Items)
                    {
                        filesEventsViewModel.LineItems.Insert(0, new FileEventViewModel()
                        {
                            FileName = Path.GetFileName(item.Path),
                            EventNameText = Enum.GetName(e.OperationType)!,
                            Thumbnail = await GetThumbnailAsync(item),
                            TimeText = DateTime.Now.ToString("h:mm tt")
                        });
                    }
                }

                svHistory.ChangeView(horizontalOffset: null, verticalOffset: 0, zoomFactor: null, disableAnimation: false);
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
            if (args.WindowActivationState == WindowActivationState.Deactivated)
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
        }

        private void OnMenuButtonClicked(object sender, RoutedEventArgs e)
        {
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
            AssignBrushToBorder(sender as Border, "HistoryItemHoveredBrush");
        }

        private void HistoryItemPointerExited(object sender, PointerRoutedEventArgs e)
        {
            AssignBrushToBorder(sender as Border, "TransparentBrush");
        }

        private void HistoryItemPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            AssignBrushToBorder(sender as Border, "HistoryItemPressedBrush");
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

        private async Task<BitmapImage> GetThumbnailAsync(ChangeEventItem item)
        {
            BitmapImage thumbnailSource = new BitmapImage(new Uri("ms-appx:///Images/Folder.svg"));
            if (Application.Current.Resources.TryGetValue("FolderSvg", out object resource) && resource is BitmapImage bitmap)
            {
                thumbnailSource = bitmap;
            }

            try
            {
                // Get StorageFile from file path
                StorageFile file = await StorageFile.GetFileFromPathAsync(item.Path);

                // Get thumbnail
                using (StorageItemThumbnail thumbnail = await file.GetThumbnailAsync(ThumbnailMode.SingleItem, 40))
                {
                    if (thumbnail != null)
                    {
                        BitmapImage bitmapImage = new BitmapImage();
                        await bitmapImage.SetSourceAsync(thumbnail);
                        thumbnailSource = bitmapImage; // Set thumbnail in LineItem
                    }
                }
            }
            catch // Handle errors (e.g., file not found, no thumbnail available)
            {
                if (item.ItemType == FileSystemItemType.File)
                {
                    if (Application.Current.Resources.TryGetValue("FilePng", out object resourceFile) && resourceFile is BitmapImage bitmapFile)
                    {
                        thumbnailSource = bitmapFile;
                    }
                    else
                    {
                        thumbnailSource = new BitmapImage(new Uri("ms-appx:///Images/File.png"));
                    }
                }
            }

            return thumbnailSource;
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
            }
            catch { }// Ignore the exception that occurs when closing the window.
        }
    }
}
