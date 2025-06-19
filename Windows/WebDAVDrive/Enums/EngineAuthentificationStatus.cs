namespace WebDAVDrive.Enums
{
    /// <summary>
    /// Authentification status of engine.
    /// </summary>
    public enum EngineAuthentificationStatus
    {
        /// <summary>
        /// Engine does not require authentification.
        /// </summary>
        Anonymous,

        /// <summary>
        /// Engine is logged in.
        /// </summary>
        LoggedIn,

        /// <summary>
        /// Engine is logged out.
        /// </summary>
        LoggedOut
    }
}
