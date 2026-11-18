using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Common.Core;
using CoreGraphics;
using Foundation;
using ITHit.FileSystem;
using UIKit;
using WebDAVCommon;

namespace WebDAViOSApp
{
    /// <summary>
    /// Lists configured WebDAV server URLs and mounts/unmounts the corresponding
    /// File Provider domains. Mounted drives appear in the Files app.
    /// Presents the sign-in UI when the server requires authentication.
    /// </summary>
    internal class DriveListViewController : UITableViewController
    {
        private class DriveState
        {
            public bool Mounted;
            public bool AuthRequired;
        }

        private const string CellId = "cell";

        private readonly AppDelegate appDelegate;
        private readonly List<string> serverUrls = new List<string>();
        private readonly Dictionary<string, DriveState> driveStates = new Dictionary<string, DriveState>();
        private readonly ILogger logger = new ConsoleLogger(nameof(DriveListViewController));

        public DriveListViewController(AppDelegate appDelegate, List<string> initialUrls)
            : base(UITableViewStyle.InsetGrouped)
        {
            this.appDelegate = appDelegate;
            serverUrls.AddRange(initialUrls ?? new List<string>());
            Title = "WebDAV Drive";
        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            NavigationItem.RightBarButtonItem = new UIBarButtonItem(UIBarButtonSystemItem.Add, (s, e) => ShowAddUrlDialog());

            RefreshControl = new UIRefreshControl();
            RefreshControl.ValueChanged += (s, e) => RefreshStates();

            UpdateEmptyView();
            RefreshStates();
        }

        public override void ViewWillAppear(bool animated)
        {
            base.ViewWillAppear(animated);
            if (NavigationController != null)
            {
                NavigationController.NavigationBar.PrefersLargeTitles = true;
            }
            RefreshStates();
        }

        public override nint RowsInSection(UITableView tableView, nint section) => serverUrls.Count;

        public override string TitleForHeader(UITableView tableView, nint section) => "Drives";

        public override string TitleForFooter(UITableView tableView, nint section)
            => serverUrls.Count > 0 ? "Tap a drive to mount it in the Files app. Swipe left to remove it." : null!;

        public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
        {
            UITableViewCell cell = new UITableViewCell(UITableViewCellStyle.Subtitle, CellId);

            string url = serverUrls[indexPath.Row];
            driveStates.TryGetValue(url, out DriveState? state);

            var config = UIListContentConfiguration.SubtitleCellConfiguration;
            config.Text = new Uri(url).Host;
            config.SecondaryText = url;
            config.SecondaryTextProperties.Color = UIColor.SecondaryLabel;
            config.Image = UIImage.GetSystemImage("externaldrive.fill");
            config.ImageProperties.TintColor = UIColor.SystemBlue;
            cell.ContentConfiguration = config;

            cell.AccessoryView = CreateStatusBadge(state);
            return cell;
        }

        /// <summary>
        /// Builds the trailing status badge: a colored SF Symbol plus a short status
        /// label, similar to OneDrive's per-drive status indicator.
        /// </summary>
        private static UIView CreateStatusBadge(DriveState? state)
        {
            string text;
            UIColor color;
            string? symbol = null;

            if (state == null)
            {
                text = "Connecting";
                color = UIColor.SecondaryLabel;
            }
            else if (state.AuthRequired)
            {
                text = "Sign in required";
                color = UIColor.SystemOrange;
                symbol = "exclamationmark.circle.fill";
            }
            else if (state.Mounted)
            {
                text = "Connected";
                color = UIColor.SystemGreen;
                symbol = "checkmark.circle.fill";
            }
            else
            {
                text = "Not mounted";
                color = UIColor.SecondaryLabel;
            }

            var label = new UILabel
            {
                Text = text,
                Font = UIFont.PreferredFootnote,
                TextColor = color
            };

            var badge = new UIStackView(new UIView[] { label })
            {
                Axis = UILayoutConstraintAxis.Horizontal,
                Alignment = UIStackViewAlignment.Center,
                Spacing = 4
            };

            if (symbol != null)
            {
                var icon = new UIImageView(UIImage.GetSystemImage(symbol))
                {
                    TintColor = color,
                    ContentMode = UIViewContentMode.ScaleAspectFit
                };
                badge.InsertArrangedSubview(icon, 0);
            }

            badge.SizeToFit();
            return badge;
        }

        public override void RowSelected(UITableView tableView, NSIndexPath indexPath)
        {
            string url = serverUrls[indexPath.Row];
            tableView.DeselectRow(indexPath, true);

            driveStates.TryGetValue(url, out DriveState? state);
            bool mounted = state?.Mounted ?? false;
            bool authRequired = state?.AuthRequired ?? false;

            var sheet = UIAlertController.Create(new Uri(url).Host, url, UIAlertControllerStyle.ActionSheet);

            if (authRequired)
            {
                sheet.AddAction(UIAlertAction.Create("Sign In", UIAlertActionStyle.Default, _ => ShowLogin(url)));
            }

            sheet.AddAction(UIAlertAction.Create(mounted ? "Unmount" : "Mount", UIAlertActionStyle.Default,
                action => { _ = ToggleMount(url); }));

            sheet.AddAction(UIAlertAction.Create("Remove Drive", UIAlertActionStyle.Destructive,
                action => { _ = RemoveServer(url, indexPath); }));

            sheet.AddAction(UIAlertAction.Create("Cancel", UIAlertActionStyle.Cancel, null));

            // Action sheets must be presented from a popover on iPad.
            if (sheet.PopoverPresentationController != null)
            {
                UITableViewCell? cell = tableView.CellAt(indexPath);
                sheet.PopoverPresentationController.SourceView = cell as UIView ?? tableView;
                sheet.PopoverPresentationController.SourceRect = cell != null ? cell.Bounds : tableView.Bounds;
            }

            PresentViewController(sheet, true, null);
        }

        public override async void CommitEditingStyle(UITableView tableView, UITableViewCellEditingStyle editingStyle, NSIndexPath indexPath)
        {
            if (editingStyle == UITableViewCellEditingStyle.Delete)
            {
                await RemoveServer(serverUrls[indexPath.Row], indexPath);
            }
        }

        private async Task RemoveServer(string url, NSIndexPath indexPath)
        {
            try
            {
                if (await appDelegate.IsMountedAsync(url))
                {
                    await appDelegate.UnmountAsync(url);
                }

                serverUrls.Remove(url);
                driveStates.Remove(url);
                await appDelegate.SecureStorage.SetAsync("WebDAVServerURLs", serverUrls);
                TableView.DeleteRows(new[] { indexPath }, UITableViewRowAnimation.Automatic);
                UpdateEmptyView();
            }
            catch (Exception ex)
            {
                logger.LogError("Delete failed.", ex: ex);
                ShowError(ex.Message);
            }
        }

        private async Task ToggleMount(string url)
        {
            try
            {
                if (await appDelegate.IsMountedAsync(url))
                {
                    await appDelegate.UnmountAsync(url);
                }
                else
                {
                    bool mounted = await appDelegate.MountAsync(url);
                    if (!mounted)
                    {
                        ShowError($"Failed to register File Provider domain for {url}.");
                        return;
                    }

                    // The extension only discovers that auth is required after it starts
                    // and its first request fails (401 / 302 redirect). Poll for the flag
                    // so the sign-in UI appears on the first mount, not just on remount.
                    string domainIdentifier = new Uri(url).Host;
                    if (await WaitForAuthRequirementAsync(domainIdentifier))
                    {
                        ShowLogin(url);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError("Mount/Unmount failed.", ex: ex);
                ShowError(ex.Message);
            }
            finally
            {
                RefreshStates();
            }
        }

        /// <summary>
        /// Polls the domain secure storage until the extension reports that authentication
        /// is required or the timeout expires. Also refreshes the row badge along the way.
        /// </summary>
        private async Task<bool> WaitForAuthRequirementAsync(string domainIdentifier)
        {
            var timeout = TimeSpan.FromSeconds(10);
            var started = DateTime.UtcNow;
            while (DateTime.UtcNow - started < timeout)
            {
                if (await Authentication.GetRequiredLoginTypeAsync(domainIdentifier) != null)
                {
                    return true;
                }

                InvokeOnMainThread(RefreshStates);
                await Task.Delay(700);
            }
            return false;
        }

        private void ShowLogin(string url)
        {
            string domainIdentifier = new Uri(url).Host;
            _ = Authentication.PresentAsync(this, domainIdentifier, RefreshStates);
        }

        private void RefreshStates()
        {
            _ = Task.Run(async () =>
            {
                foreach (string url in serverUrls.ToArray())
                {
                    var state = new DriveState
                    {
                        Mounted = await appDelegate.IsMountedAsync(url),
                        AuthRequired = await Authentication.GetRequiredLoginTypeAsync(new Uri(url).Host) != null
                    };

                    InvokeOnMainThread(() =>
                    {
                        driveStates[url] = state;
                        TableView.ReloadData();
                    });
                }

                InvokeOnMainThread(() => RefreshControl?.EndRefreshing());
            });
        }

        /// <summary>
        /// Shows a friendly empty state when no drives are configured.
        /// </summary>
        private void UpdateEmptyView()
        {
            if (serverUrls.Count > 0)
            {
                TableView.BackgroundView = null;
                return;
            }

            var icon = new UIImageView(UIImage.GetSystemImage("externaldrive"))
            {
                TintColor = UIColor.TertiaryLabel,
                ContentMode = UIViewContentMode.ScaleAspectFit,
                TranslatesAutoresizingMaskIntoConstraints = false
            };

            var title = new UILabel
            {
                Text = "No drives yet",
                Font = UIFont.PreferredTitle2,
                TextColor = UIColor.Label,
                TextAlignment = UITextAlignment.Center
            };

            var subtitle = new UILabel
            {
                Text = "Tap + to connect a WebDAV server.\nIt will show up in the Files app.",
                Font = UIFont.PreferredSubheadline,
                TextColor = UIColor.SecondaryLabel,
                TextAlignment = UITextAlignment.Center,
                Lines = 0
            };

            var stack = new UIStackView(new UIView[] { icon, title, subtitle })
            {
                Axis = UILayoutConstraintAxis.Vertical,
                Alignment = UIStackViewAlignment.Center,
                Spacing = 12,
                TranslatesAutoresizingMaskIntoConstraints = false
            };

            var background = new UIView();
            background.AddSubview(stack);
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                icon.HeightAnchor.ConstraintEqualTo(72),
                icon.WidthAnchor.ConstraintEqualTo(72),
                stack.CenterXAnchor.ConstraintEqualTo(background.CenterXAnchor),
                stack.CenterYAnchor.ConstraintEqualTo(background.CenterYAnchor, -60),
                stack.LeadingAnchor.ConstraintGreaterThanOrEqualTo(background.LeadingAnchor, 32),
                stack.TrailingAnchor.ConstraintLessThanOrEqualTo(background.TrailingAnchor, -32)
            });

            TableView.BackgroundView = background;
        }

        private void ShowAddUrlDialog()
        {
            var alert = UIAlertController.Create("Add WebDAV Server", "e.g. https://webdavserver.net/User123456", UIAlertControllerStyle.Alert);
            alert.AddTextField(field =>
            {
                field.Placeholder = "https://server/dav/";
                field.KeyboardType = UIKeyboardType.Url;
                field.AutocapitalizationType = UITextAutocapitalizationType.None;
                field.AutocorrectionType = UITextAutocorrectionType.No;
            });
            alert.AddAction(UIAlertAction.Create("Cancel", UIAlertActionStyle.Cancel, null));
            alert.AddAction(UIAlertAction.Create("Add", UIAlertActionStyle.Default, async _action =>
            {
                string? url = alert.TextFields[0].Text?.Trim();
                if (string.IsNullOrEmpty(url) || !Uri.TryCreate(url, UriKind.Absolute, out Uri? _))
                {
                    return;
                }

                if (!url.EndsWith('/'))
                {
                    url += "/";
                }

                serverUrls.Add(url);
                await appDelegate.SecureStorage.SetAsync("WebDAVServerURLs", serverUrls);
                UpdateEmptyView();
                RefreshStates();
            }));
            PresentViewController(alert, true, null);
        }

        private void ShowError(string message)
        {
            InvokeOnMainThread(() =>
            {
                var alert = UIAlertController.Create("Error", message, UIAlertControllerStyle.Alert);
                alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, null));
                PresentViewController(alert, true, null);
            });
        }
    }
}
