using ITHit.FileSystem.Samples.Common;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace WebDAVDrive.UI
{
    public class RegistryManager
    {
        /// <summary>
        /// Reads url from registry, if it will be null shows ConnectWindow.
        /// </summary>
        /// <param name="settings"></param>
        /// <returns>WebDAV server url.</returns>
        public static string GetURL(Settings settings) 
        {
            //Try to load URL from Windows Registry
            string webDAVServerUrl = GetURLFromRegistry(settings);

            //Try to get URL from URL form
            if (string.IsNullOrEmpty(webDAVServerUrl))
            {
                webDAVServerUrl = GetURLFromUser(settings);
            }

            if (string.IsNullOrEmpty(webDAVServerUrl))
            {
                throw new ArgumentNullException("Settings.WebDAVServerUrl");
            }
            return webDAVServerUrl;
        }

        /// <summary>
        /// Returns WebDAV server URL from user dialog and stores this URL to windows registry (HKCU\SOFTWARE\AppID\WeDAVServerUrl).
        /// </summary>
        /// <returns>WebDAV server URL.</returns>
        private static string GetURLFromUser(Settings settings)
        {
            string url = null;
            bool dialogResult = false;
            // Show URL dialog
            WebDAVDrive.UI.ConnectForm connectForm = null;
            Thread thread = new Thread(() =>
            {
                connectForm = new WebDAVDrive.UI.ConnectForm();
                ((WebDAVDrive.UI.ViewModels.ConnectViewModel)connectForm.DataContext).WindowTitle = settings.ProductName;
                connectForm.ShowDialog();

                url = ((WebDAVDrive.UI.ViewModels.ConnectViewModel)connectForm.DataContext).Url;
                dialogResult = (bool)connectForm.DialogResult;
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (dialogResult)
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey($@"SOFTWARE\{settings.AppID}"))
                {
                    key.SetValue("WeDAVServerUrl", url);
                }
            }
            return (dialogResult) ? url : null;
        }

        /// <summary>
        /// Read URL from Windows Registry (HCKU\SOFTWARE\AppID\WeDAVServerUrl).
        /// </summary>
        /// <param name="settings"></param>
        /// <returns>WebDAV server URL.</returns>
        private static string GetURLFromRegistry(Settings settings)
        {
            string url = null;
            RegistryKey key = Registry.CurrentUser.OpenSubKey($@"SOFTWARE\{settings.AppID}");
            if (key != null && key.GetValue("WeDAVServerUrl") != null)
            {
                url = key.GetValue("WeDAVServerUrl").ToString();
            }
            return url;
        }
    }
}
