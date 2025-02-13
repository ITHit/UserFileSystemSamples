using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using System;

namespace WebDAVDrive
{
    /// <summary>
    /// Provides a service provider for the application.
    /// </summary>
    public static class ServiceProvider
    {
        /// <summary>
        /// Gets or sets the service provider.
        /// </summary>
        public static IServiceProvider Services { get; set; }

        /// <summary>
        /// Gets or sets the dispatcher queue.
        /// </summary>
        public static DispatcherQueue DispatcherQueue { get; set; }

        /// <summary>
        /// Gets a service of the specified type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T GetService<T>() => Services.GetService<T>()!;

        /// <summary>
        /// Indicates whether current application theme is dark.
        /// </summary>
        public static bool IsDarkTheme { get; set; }

        /// <summary>
        /// Windows Scale factor at the moment of application launch (e.g. 100, 125).
        /// </summary>
        public static int Scale { get; set; }
    }
}
