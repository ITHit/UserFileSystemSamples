using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using Windows.Security.Credentials;

namespace WebDAVDrive
{
    /// <summary>
    /// Saves and reads login and password to/from Windows Credentials Manger.
    /// </summary>
    public class CredentialManager
    {

        /// <summary>
        /// Saves login and password to the Windows Credentials Manger.
        /// </summary>
        /// <param name="resource">Resource name under which credentials will be saved.</param>
        /// <param name="login">User name to be saved.</param>
        /// <param name="password">Password to be saved.</param>
        public static void SaveCredentials(string resource, string login, string password)
        {
            PasswordVault vault = new PasswordVault();

            //retrive string password form SecureString (password can not be retrived directly from Security string)
            //string password = new System.Net.NetworkCredential(string.Empty, securePassword).Password;

            PasswordCredential credential = new PasswordCredential()
            {
                UserName = login,
                Password = password,
                Resource = resource
            };

            // Save credentials in vault.
            vault.Add(credential);
        }

        /// <summary>
        /// Gets credentials from the Windows Credentials Manger.
        /// </summary>
        /// <param name="resource">Resource name from which ro retrieve the credentials.</param>
        /// <param name="log">Logger.</param>
        /// <returns>Credentials for current application.</returns>
        public static PasswordCredential GetCredentials(string resource, ILog log)
        {
            PasswordCredential credential = null;

            PasswordVault vault = new PasswordVault();

            try
            {
                var credentialList = vault.FindAllByResource(resource);
                if (credentialList.Count > 0)
                {
                    if (credentialList.Count == 1)
                    {
                        credential = credentialList[0];
                    }
                    else
                    {
                        // When there are multiple usernames, return null to show login form, but this code can be relaced
                        // by code, which will show form only with login (password already saved in Windows Crededntial Manager)
                        return null;
                    }
                }
            }
            catch (Exception e)
            {
                log.Error($"Users credentials not found\n{e.Message}");
            }

            return credential;
        }
    }
}
