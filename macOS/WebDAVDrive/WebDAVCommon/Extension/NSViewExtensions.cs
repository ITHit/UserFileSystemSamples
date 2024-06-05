using System;
namespace WebDAVCommon.Extension
{
    public static class NSViewExtensions
    {
        public static TView SetBackgroundColor<TView>(this TView view, NSColor color)
            where TView : NSView
        {
            return view.SetBackgroundColor(color.CGColor);
        }

        public static TView SetBackgroundColor<TView>(this TView view, CGColor color)
            where TView : NSView
        {
            view.EnsureWantsLayer();
            if (view.Layer != null)
            {
                view.Layer.BackgroundColor = color;
            }

            return view;
        }

        public static TView EnsureWantsLayer<TView>(this TView view)
            where TView : NSView
        {
            view.WantsLayer = true;
            return view;
        }

        public static NSButton SetColoredTitle(
            this NSButton button,
            string text,
            NSColor color)
        {
            button.Title = text;

            button.AttributedTitle = text.CreateAttributedString(color);

            return button;
        }
    }
}

