using System;
using System.Collections.Generic;
using Foundation;
using ObjCRuntime;
using UIKit;

namespace WebDAViOSApp
{
    [Register("SceneDelegate")]
    [Preserve(AllMembers = true)]
    public class SceneDelegate : UIResponder, IUISceneDelegate, IUIWindowSceneDelegate
    {
        [Export("window")]
        public UIWindow? Window { get; set; }

        [Export("scene:willConnectToSession:options:")]
        public void WillConnect(UIScene scene, UISceneSession session, UISceneConnectionOptions connectionOptions)
        {
            if (scene is not UIWindowScene windowScene)
            {
                return;
            }

            Window = new UIWindow(windowScene);

            if (UIApplication.SharedApplication.Delegate is AppDelegate appDelegate)
            {
                Window.RootViewController = new UINavigationController(new DriveListViewController(appDelegate, appDelegate.ServerUrls));
            }
            else
            {
                Window.RootViewController = new UIViewController();
            }

            Window.MakeKeyAndVisible();
        }

        [Export("sceneDidBecomeActive:")]
        public void DidBecomeActive(UIScene scene)
        {
            (UIApplication.SharedApplication.Delegate as AppDelegate)?.OnAppActive();
        }

        [Export("sceneDidEnterBackground:")]
        public void DidEnterBackground(UIScene scene)
        {
            (UIApplication.SharedApplication.Delegate as AppDelegate)?.OnAppBackground(UIApplication.SharedApplication);
        }
    }
}
