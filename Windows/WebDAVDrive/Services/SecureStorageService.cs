using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Security.Credentials;

namespace WebDAVDrive.Services
{
    /// <summary>
    /// Represents basic authentication credentials.
    /// </summary>
    public class BasicAuthCredentials
    {
        /// <summary>
        /// Gets or sets the username.
        /// </summary>
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the password.
        /// </summary>
        public string Password { get; set; } = string.Empty;
    }

    /// <summary>
    /// Service for securely storing sensitive data using the Windows PasswordVault.
    /// </summary>
    public class SecureStorageService
    {
        private const string VaultResourceName = "WebDAVSampleSecureStorage";

        /// <summary>
        /// Saves a sensitive value securely using the PasswordVault.
        /// </summary>
        /// <typeparam name="T">The type of the sensitive value.</typeparam>
        /// <param name="key">The key to associate with the sensitive value.</param>
        /// <param name="value">The sensitive value to store.</param>
        public void SaveSensitiveData<T>(string key, T value)
        {
            string json = JsonSerializer.Serialize(value);
            PasswordVault vault = new PasswordVault();
            RemoveSensitiveData(key);  // Remove existing value if present
            vault.Add(new PasswordCredential(VaultResourceName, key, json));
        }

        /// <summary>
        /// Retrieves a sensitive value securely from the PasswordVault.
        /// </summary>
        /// <typeparam name="T">The type of the sensitive value.</typeparam>
        /// <param name="key">The key associated with the sensitive value.</param>
        /// <returns>The sensitive value or null if not found.</returns>
        public T? RetrieveSensitiveData<T>(string key)
        {
            try
            {
                PasswordVault vault = new PasswordVault();
                PasswordCredential credential = vault.Retrieve(VaultResourceName, key);
                credential.RetrievePassword();
                return JsonSerializer.Deserialize<T>(credential.Password);
            }
            catch (Exception)
            {
                // Handle exceptions or log errors, e.g., when data is not found
                return default;
            }
        }

        /// <summary>
        /// Attempts to retrieve a sensitive value securely from the PasswordVault.
        /// </summary>
        /// <typeparam name="T">The type of the sensitive value.</typeparam>
        /// <param name="key">The key associated with the sensitive value.</param>
        /// <param name="value">When this method returns, contains the sensitive value if found; otherwise, the default value for the type of the value parameter.</param>
        /// <returns>true if the sensitive value was found; otherwise, false.</returns>
        public bool TryGetSensitiveData<T>(string key, out T value)
        {
            try
            {
                PasswordVault vault = new PasswordVault();
                PasswordCredential credential = vault.Retrieve(VaultResourceName, key);
                credential.RetrievePassword();
                value = JsonSerializer.Deserialize<T>(credential.Password)!;
                return true;
            }
            catch (Exception)
            {
                value = default(T)!;
                return false;
            }
        }

        /// <summary>
        /// Removes a sensitive value from the PasswordVault.
        /// </summary>
        /// <param name="key">The key associated with the sensitive value to remove.</param>
        public void RemoveSensitiveData(string key)
        {
            try
            {
                PasswordVault vault = new PasswordVault();
                PasswordCredential credential = vault.Retrieve(VaultResourceName, key);
                vault.Remove(credential);
            }
            catch (Exception)
            {
                // No action needed if the data doesn't exist
            }
        }
    }
}
