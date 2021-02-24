using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using VirtualFileSystem;

namespace WebDAVDrive
{
    class CredentialManager
    {

        /// <summary>
        /// Save credential from login form to Windows Credentials Manger
        /// </summary>
        /// <param name="resource">Name of credential</param>
        /// <param name="login">User login</param>
        /// <param name="securePassword">User password</param>
        public static void SaveCredentials(string resource,string login, SecureString securePassword)
        {
            Windows.Security.Credentials.PasswordVault vault = new Windows.Security.Credentials.PasswordVault();

            //retrive string password form SecureString (password can not be retrived directly from Security string)
            string password = new System.Net.NetworkCredential(string.Empty, securePassword).Password;

            Windows.Security.Credentials.PasswordCredential credential = new Windows.Security.Credentials.PasswordCredential()
            {
                UserName = login,
                Password = password,
                Resource = resource
            };

            //save credential in Credential Manager
            vault.Add(credential);
        }

        /// <summary>
        /// Get credentials from windows credentials manger
        /// </summary>
        /// <param name="resource">Name of credential</param>
        /// <param name="log">Name of credential</param>
        /// <returns>Credentials for current application</returns>
        public static Windows.Security.Credentials.PasswordCredential GetCredentials(string resource, ILog log)
        {
            Windows.Security.Credentials.PasswordCredential credential = null;

            var vault = new Windows.Security.Credentials.PasswordVault();

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
