namespace WebDAVDrive.Services
{
    public class UserSettings
    {
        public double AutomaticLockTimeout { get; set; }
        public double ManualLockTimeout { get; set; }
        public bool SetLockReadOnly { get; set; }
        public bool AutoLock { get; set; }
    }

    public class UserSettingsService
    {
        //Save basic settings for engine - to secure storage and to engine
        public void SaveSettings(VirtualEngine engine, UserSettings model)
        {
            SecureStorageService secureStorage = ServiceProvider.GetService<SecureStorageService>();
            secureStorage.SaveSensitiveData(engine.RemoteStorageRootPath + "UserSettings", model);            

            //save to engine
            engine.AutoLockTimeoutMs = model.AutomaticLockTimeout;
            engine.ManualLockTimeoutMs = model.ManualLockTimeout;
            engine.AutoLock = model.AutoLock;
            engine.SetLockReadOnly = model.SetLockReadOnly;
        }

        //Get basic settings for engine from secure storage
        public UserSettings? GetSettings(string engineRemotePath)
        {
            UserSettings settingsData;
            SecureStorageService secureStorage = ServiceProvider.GetService<SecureStorageService>();
            return secureStorage.TryGetSensitiveData(engineRemotePath + "UserSettings", out settingsData) ? settingsData : null;
        }
    }
}
