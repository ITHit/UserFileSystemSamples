using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using log4net;

using ITHit.FileSystem.Windows;
using ITHit.FileSystem.Windows.Package;


namespace ITHit.FileSystem.Samples.Common.Windows
{
    /// <summary>
    /// Provides sparse package registration functionality.
    /// </summary>
    /// <remarks>
    /// As soon as sparse package requires a valid cerificate, this class also registers 
    /// a development certificate if the application runs in the debug mode.
    /// </remarks>
    public class SparsePackageRegistrar : Registrar
    {
        private static Version minimalSupportedVersion = new Version(10, 0, 19043);

        /// <summary>
        /// Creates instance of this class.
        /// </summary>
        /// <param name="syncRootId">ID of the sync root.</param>
        /// <param name="userFileSystemRootPath">A root folder of your user file system. Your file system tree will be located under this folder.</param>
        /// <param name="log">log4net logger.</param>
        /// <param name="shellExtensionHandlers">
        /// List of shell extension handlers. Use it only for applications without application or package identity.
        /// For applications with identity this list is ignored.
        /// </param>
        public SparsePackageRegistrar(
            ILog log,
            IEnumerable<(string Name, Guid Guid, bool AlwaysRegister)> shellExtensionHandlers = null)
            : base(log, shellExtensionHandlers)
        {

        }

        /// <summary>
        /// Registers sparse package if needed. In development mode also registers development certificate.
        /// </summary>
        /// <returns>
        /// True if app has identity and the app can start execution.
        /// False if the app needs to restart after sparse package registrtatio or if installation failed.
        /// </returns>
        public async Task<bool> RegisterSparsePackageAsync()
        {
            // This check is in case this method is called form the packaged app
            // or from installed sparse package.
            if (PackageRegistrar.IsRunningWithIdentity())
            {
                return true; // App has identity, ready to run.
            }

            if (!PackageRegistrar.SparsePackageRegistered())
            {
                EnsureOSVersionIsSupported();
#if DEBUG
                /// Registering sparse package requires a valid certificate.
                /// In the development mode we use the below call to install the development certificate.
                if (!EnsureDevelopmentCertificateInstalled(Log))
                {
                    return false;
                }
#endif
                // In the case of a regular installer (msi) call this method during installation.
                // This method call should be omitted for packaged application.
                Log.Info("\n\nRegistering sparse package...");
                await PackageRegistrar.RegisterSparsePackageAsync();
                Log.Info("\nSparse package successfully registered. Restart the application.\n\n");

                return false;
            }

            return true;
        }

        /// <summary>
        /// Unregisters sparse package. In development mode also unregisters development certificate.
        /// </summary>
        public async Task UnregisterSparsePackageAsync()
        {
#if DEBUG
            // Uninstall developer certificate.
            EnsureDevelopmentCertificateUninstalled(Log);

            // Uninstall conflicting packages if any
            await EnsureConflictingPackagesUninstalled();
#endif
            // Unregister sparse package.
            Log.Info("\n\nUnregistering sparse package...");
            await PackageRegistrar.UnregisterSparsePackageAsync();
            Log.Info("\nSparse package unregistered sucessfully.");
        }

        /// <inheritdocs/>
        public override async Task<bool> UnregisterAllSyncRootsAsync(string providerId, bool fullUnregistration = true)
        {
            bool res = await base.UnregisterAllSyncRootsAsync(providerId, fullUnregistration);

            if (fullUnregistration)
            {
                await UnregisterSparsePackageAsync();
            }

            return res;
        }

#if DEBUG
        /// <summary>
        /// Installs a development certificate.
        /// </summary>
        /// <remarks>
        /// In a real-world application your application will be signed with a trusted
        /// certificate and you do not need to install it.
        /// Development certificate installation is needed for sparse package only,
        /// should be omitted for packaged application.
        /// </remarks>
        /// <returns>True if the the certificate is installed, false - if the installation failed.</returns>
        public static bool EnsureDevelopmentCertificateInstalled(ILog log)
        {
            string sparsePackagePath = PackageRegistrar.GetSparsePackagePath();
            CertificateRegistrar certificateRegistrar = new CertificateRegistrar(sparsePackagePath);
            if (!certificateRegistrar.IsCertificateInstalled())
            {
                log.Info("\n\nInstalling developer certificate...");
                if (certificateRegistrar.TryInstallCertificate(true, out int errorCode))
                {
                    log.Info("\nDeveloper certificate successfully installed.");
                }
                else
                {
                    log.Error($"\nFailed to install the developer certificate. Error code: {errorCode}");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Uninstalls a development certificate.
        /// </summary>
        /// <returns>True if the the certificate is uninstalled, false - if the uninstallation failed.</returns>
        public static bool EnsureDevelopmentCertificateUninstalled(ILog log)
        {
            string sparsePackagePath = PackageRegistrar.GetSparsePackagePath();
            CertificateRegistrar certRegistrar = new CertificateRegistrar(sparsePackagePath);
            if (certRegistrar.IsCertificateInstalled())
            {
                log.Info("\n\nUninstalling developer certificate...");
                if (certRegistrar.TryUninstallCertificate(true, out int errorCode))
                {
                    log.Info("\nDeveloper certificate successfully uninstalled.");
                }
                else
                {
                    log.Error($"\nFailed to uninstall the developer certificate. Error code: {errorCode}");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Uninstalls packages that registered classes when sparse package contains them to prevent conflicts.
        /// </summary>
        /// <returns></returns>
        private async Task EnsureConflictingPackagesUninstalled()
        {
            if (PackageRegistrar.IsRunningWithSparsePackageIdentity() && PackageRegistrar.ConflictingPackagesRegistered())
            {
                Log.Info("\nUninstalling conflicting packages...");
                await PackageRegistrar.UnregisterConflictingPackages();
            }
        }
#endif

        private void EnsureOSVersionIsSupported()
        {
            if (minimalSupportedVersion.CompareTo(Environment.OSVersion.Version) == 1)
            {
                throw new NotSupportedException($"Minimal Windows version with sparse package support is: {minimalSupportedVersion}. Please, update your Windows version.");
            }
        }
    }
}
