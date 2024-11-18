using Microsoft.Maui.LifecycleEvents;
using Microsoft.Windows.AppLifecycle;

namespace WebDAVDrive.Platforms.Windows.Extensions;

delegate void OnAppInstanceActivated(object? sender, AppActivationArguments e);

static class WindowsLifecycleExtensions
{
    public static IWindowsLifecycleBuilder OnAppInstanceActivated(this IWindowsLifecycleBuilder builder, OnAppInstanceActivated handler)
    {
        builder.AddEvent(nameof(OnAppInstanceActivated), handler);
        return builder;
    }

    public static void OnAppInstanceActivated(this ILifecycleEventService lifecycle, object? sender, AppActivationArguments e)
    {
        lifecycle.InvokeEvents<OnAppInstanceActivated>(nameof(OnAppInstanceActivated), del => del(sender, e));
    }
}
