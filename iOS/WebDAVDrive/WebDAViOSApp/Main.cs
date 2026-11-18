using WebDAViOSApp;

// Keep the scene delegate class reachable so the AOT compiler does not strip it.
_ = typeof(SceneDelegate).FullName;

UIApplication.Main(args, null, typeof(AppDelegate));
