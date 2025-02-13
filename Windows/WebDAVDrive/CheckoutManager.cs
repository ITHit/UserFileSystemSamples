using ITHit.FileSystem;
using ITHit.FileSystem.Samples.Common;
using ITHit.WebDAV.Client;
using ITHit.WebDAV.Client.Exceptions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using WebDAVDrive.Dialogs;
using WebDAVDrive.Soap;
using Windows.ApplicationModel.Resources;
using WinUIEx;
using IFile = ITHit.WebDAV.Client.IFile;

namespace WebDAVDrive
{
    /// <summary>
    /// Manage checkout/checkin operations.
    /// </summary>
    public class CheckoutManager
    {
        /// <summary>
        /// Engine.
        /// </summary>
        internal readonly VirtualEngine Engine;
        /// <summary>
        /// WebDav client to operate with webdav commands.
        /// </summary>
        internal readonly WebDavSession DavClient;
        /// <summary>
        /// Soap client to operate with SPS commands.
        /// </summary>
        private readonly SoapSession SoapClient;
        /// <summary>
        /// Incapsulate webdav commands.
        /// </summary>
        private readonly WebDavCheckoutHelper WebDavHelper;
        /// <summary>
        /// Load translation.
        /// </summary>
        private readonly ResourceLoader resourceLoader;
        /// <summary>
        /// Not show UI for given extensions, do silent checkout/checkin
        /// </summary>
        private readonly List<string> ExtensionsNoUI = new List<string>() { ".psd" };
        /// <summary>
        // 3-state bool, when null, need to ask server first
        /// </summary>
        internal bool? isCheckoutSupported = null; 
        /// <summary>
        /// Is working with sharepoint server
        /// </summary>
        private bool isSpsServer = false;

        public CheckoutManager(VirtualEngine engine) {
            Engine = engine;   
            DavClient = engine.DavClient;
            resourceLoader = ResourceLoader.GetForViewIndependentUse();

            WebDavHelper = new WebDavCheckoutHelper(this);
            SoapClient = CreateSoapSession();
        }

        private SoapSession CreateSoapSession()
        {
            //var proxy = new WebProxy
            //{
            //    Address = new Uri($"http://127.0.0.1:8888"),
            //    BypassProxyOnLocal = false,
            //};
            HttpClientHandler handler = new HttpClientHandler()
            {
                AllowAutoRedirect = false,

                //Proxy = proxy, // Fiddler proxy
            };
            SoapSession soapClient = new SoapSession(handler, Engine.RemoteStorageRootPath);
            soapClient.CookieContainer.Add(Engine.Cookies);
            soapClient.Client.Timeout = TimeSpan.FromMinutes(10);

            soapClient.SoapEventMessage += SoapClientEvent_Message;
            return soapClient;
        }
        private void SoapClientEvent_Message(SoapMessageEventArgs e)
        {
            Engine.Logger.LogDebug(e.Message);
        }

        /// <summary>
        /// Add cookies to underlying soap client.
        /// </summary>
        /// <param name="cookies">Cookies.</param>
        public void AddCookies(CookieCollection cookies)
        {
            SoapClient.CookieContainer.Add(cookies);
        }

        /// <summary>
        /// Try to checkout local file associated with remote path.
        /// </summary>
        /// <param name="userFileSystemPath">Local file path.</param>
        /// <param name="remoteStoragePath">File remote path.</param>
        /// <param name="lockInfo">Lock token for given file.</param>
        /// <returns></returns>
        public async Task TryCheckOutAsync(string userFileSystemPath, string remoteStoragePath, LockInfo lockInfo)
        {
            if (IsShowUi(userFileSystemPath))
            {
                await ShowCheckOutDialogAsync(userFileSystemPath, remoteStoragePath, lockInfo);
            }
            else
            {
                await CheckOutAsync(remoteStoragePath, lockInfo.LockToken.LockToken);
            }
        }

        /// <summary>
        /// Show checkout dialog if checkout supported and file not checked out.
        /// </summary>
        /// <param name="userFileSystemPath">Local file path.</param>
        /// <param name="remoteStoragePath">Remote file url.</param>
        /// <param name="lockInfo">Lock token info.</param>
        /// <returns></returns>
        private async Task ShowCheckOutDialogAsync(string userFileSystemPath, string remoteStoragePath, LockInfo lockInfo)
        {
            if (isCheckoutSupported == null) await IsCheckedOutSupportedAsync(remoteStoragePath);
            if (isCheckoutSupported == false) return;

            if (await IsFileCheckedOut(remoteStoragePath))
            {
                Engine.Logger.LogMessage($"Already checkouted", userFileSystemPath, default, default);
            }
            else
            {
                _ = ServiceProvider.DispatcherQueue.TryEnqueue(() => new Checkout(this, userFileSystemPath, remoteStoragePath, lockInfo.LockToken.LockToken).Show());
            }
        }

        /// <summary>
        /// Do checkout for given file and lock token.
        /// </summary>
        /// <param name="remoteStoragePath">File to checkout.</param>
        /// <param name="lockToken">Lock token for given file.</param>
        /// <returns>True if checkout was successfull.</returns>
        public async Task<bool> CheckOutAsync(string remoteStoragePath, string lockToken)
        {
            bool isSuccess = false;
            if (isSpsServer)
                isSuccess = await SoapClient.CheckOutAsync(remoteStoragePath);
            else
                isSuccess = await WebDavHelper.CheckOutAsync(remoteStoragePath, lockToken);
            Engine.Logger.LogMessage($"CheckOut is success: {isSuccess}", remoteStoragePath, default, default);
            return isSuccess;
        }

        /// <summary>
        /// Try to checkin local file associated with remote path.
        /// </summary>
        /// <param name="userFileSystemPath">Local file path.</param>
        /// <param name="remoteStoragePath">File remote path.</param>
        /// <param name="lockInfo">Lock token for given file.</param>
        /// <returns></returns>
        public async Task TryCheckInAsync(string userFileSystemPath, string remoteStoragePath, ServerLockInfo lockInfo)
        {
            if (IsShowUi(userFileSystemPath))
            {
                await ShowCheckInDialogAsync(userFileSystemPath, remoteStoragePath, lockInfo);
            }
            else
            {
                await CheckInAsync(remoteStoragePath, "no ui", lockInfo);
            }
        }

        /// <summary>
        /// Show checkin dialog if checkout supported and file was checked out.
        /// </summary>
        /// <param name="userFileSystemPath">Local file path.</param>
        /// <param name="remoteStoragePath">Remote file url.</param>
        /// <param name="lockInfo">Lock token info.</param>
        /// <returns></returns>
        private async Task ShowCheckInDialogAsync(string userFileSystemPath, string remoteStoragePath, ServerLockInfo lockInfo)
        {
            if (isCheckoutSupported == false) return;

            if (!await IsFileCheckedOut(remoteStoragePath)) return;

            _ = ServiceProvider.DispatcherQueue.TryEnqueue(() => {
                new Alert(resourceLoader.GetString("Alert_Question/Text"), resourceLoader.GetString("Yes"),
                        resourceLoader.GetString("No"), () =>
                        {
                            new Checkin(this, userFileSystemPath, remoteStoragePath, lockInfo).Show();
                        }, () => { }).Show();
            });
        }

        /// <summary>
        /// Do checkin for given file and lock token.
        /// </summary>
        /// <param name="remoteStoragePath">File to checkin.</param>
        /// <param name="comment">Comment for a new version.</param>
        /// <param name="lockInfo">Lock token for given file.</param>
        /// <returns>True if checkout was successfull.</returns>
        public async Task<bool> CheckInAsync(string remoteStoragePath, string comment, ServerLockInfo lockInfo)
        {
            bool isSuccess = false;
            if (isSpsServer)
                isSuccess = await SoapClient.CheckInAsync(remoteStoragePath, comment);
            else
                isSuccess = await WebDavHelper.CheckInAsync(remoteStoragePath, comment, lockInfo);
            Engine.Logger.LogMessage($"CheckIn is success: {isSuccess}", remoteStoragePath, default, default);
            return isSuccess;
        }

        /// <summary>
        /// Check if file was checked out.
        /// </summary>
        /// <param name="remoteStoragePath">File to check.</param>
        /// <returns>True if file was checked out.</returns>
        private async Task<bool> IsFileCheckedOut(string remoteStoragePath)
        {
            if (isSpsServer)
            {
                (bool isCheckedOut, bool isCheckedOutByMe, string checkedByUserDisplayName) = await SoapClient.GetItemInfoAsync(remoteStoragePath);
                return isCheckedOut;
            }
            else
            {
                IWebDavResponse<IHierarchyItem> item = await DavClient.GetItemAsync(remoteStoragePath, null, null);
                IFile file = (item.WebDavResponse as IFile);
                return file.CheckedOut;
            }
        }

        /// <summary>
        /// Check if checked out is supported for file.
        /// </summary>
        /// <param name="remoteStoragePath">File to check.</param>
        /// <returns></returns>
        private async Task IsCheckedOutSupportedAsync(string remoteStoragePath)
        {
            if (remoteStoragePath.Contains("sharepoint.com", StringComparison.InvariantCultureIgnoreCase))
            {
                isCheckoutSupported = true;
                isSpsServer = true;
            }
            else
            {
                IWebDavResponse<IHierarchyItem> item = await DavClient.GetItemAsync(remoteStoragePath, null, null);
                IFile file = (item.WebDavResponse as IFile);
                IWebDavResponse<OptionsInfo> features = await file.SupportedFeaturesAsync();
                isCheckoutSupported = (features.WebDavResponse.Features & (Features.CheckoutInPlace)) != 0;
                isSpsServer = false;
            }
            Engine.Logger.LogMessage($"CheckOut supported: {isCheckoutSupported}, SPS: {isSpsServer}", remoteStoragePath, default, default);
        }

        /// <summary>
        /// Check if show UI dialogs for confirming checkout/checkin.
        /// </summary>
        /// <param name="userFileSystemPath">File to check.</param>
        /// <returns>True if dialogs should be shown.</returns>
        private bool IsShowUi(string userFileSystemPath)
        {
            string extension = Path.GetExtension(userFileSystemPath).ToLower();
            return !ExtensionsNoUI.Contains(extension);
        }
    }

    /// <summary>
    /// Incapsulate webdav commands.
    /// </summary>
    class WebDavCheckoutHelper
    {
        /// <summary>
        /// Checkout manager.
        /// </summary>
        private readonly CheckoutManager Manager;
        /// <summary>
        /// Engine.
        /// </summary>
        private readonly VirtualEngine Engine;
        /// <summary>
        /// WEbDav client to perform webdav commands.
        /// </summary>
        private readonly WebDavSession DavClient;

        public WebDavCheckoutHelper(CheckoutManager manager) { 
            Manager = manager;
            Engine = manager.Engine;
            DavClient = manager.DavClient;
        }

        /// <summary>
        /// Do checkout for given file and lock token.
        /// </summary>
        /// <param name="remoteStoragePath">File to checkout.</param>
        /// <param name="lockToken">Lock token for given file.</param>
        /// <returns>True if checkout was successful.</returns>
        public async Task<bool> CheckOutAsync(string remoteStoragePath, string lockToken)
        {
            if (Manager.isCheckoutSupported == true)
            {
                try
                {
                    IWebDavResponse<IHierarchyItem> item = await DavClient.GetItemAsync(remoteStoragePath, null, null);
                    IFile file = (item.WebDavResponse as IFile);
                    IResponse responce = await file.CheckOutAsync(lockToken);
                    Engine.Logger.LogDebug($"CheckOut status: {responce.Status}", file.Href.ToString(), default, default);
                    return responce.Status == HttpStatus.OK;
                }
                catch (Exception ex)
                {
                    Engine.Logger.LogError("CheckOutAsync", ex: ex);
                }
            }
            return false;
        }

        /// <summary>
        /// Do checkin for given file and lock token.
        /// </summary>
        /// <param name="remoteStoragePath">File to checkin.</param>
        /// <param name="comment">Comment to current version.</param>
        /// <param name="lockInfo">Lock token info.</param>
        /// <returns>True if checkin was successful.</returns>
        public async Task<bool> CheckInAsync(string remoteStoragePath, string comment, ServerLockInfo lockInfo)
        {
            if (Manager.isCheckoutSupported == true)
                try
                {
                    IWebDavResponse<IHierarchyItem> item = await DavClient.GetItemAsync(remoteStoragePath, null, null);
                    IFile file = (item.WebDavResponse as IFile);
                    IVersion version = (await file.CheckInAsync(lockInfo.LockToken)).WebDavResponse;
                    IResponse responce = await version.SetCommentAndAuthorAsync(comment, lockInfo.Owner);
                    return true;
                }
                catch (Exception e)
                {
                    Engine.Logger.LogError("CheckIn finish with error", ex: e);
                }
            return false;
        }

        /// <summary>
        /// Check if file has checked out already.
        /// </summary>
        /// <param name="remoteStoragePath">File to check.</param>
        /// <returns>True if file is checked out.</returns>
        public async Task<bool> IsCheckedOut(string remoteStoragePath)
        {
            IWebDavResponse<IHierarchyItem> item = await DavClient.GetItemAsync(remoteStoragePath, null, null);
            IFile file = (item.WebDavResponse as IFile);
            return file.CheckedOut;
        }
    }
}
