using System;
namespace WebDAVCommon.Extension
{
    public static class StringExtensions
    {
        public static NSMutableAttributedString CreateAttributedString(
            this string text,
            NSColor color)
        {
            var attributedString = new NSMutableAttributedString(text);
            var range = new NSRange(0, text.Length);
            attributedString.AddAttribute(NSStringAttributeKey.ForegroundColor, color, range);
            return attributedString;
        }
    }
}

