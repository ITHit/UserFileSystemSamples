using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;

namespace WebDAVDrive.Controls
{
    /// <summary>
    /// Custom control presenting button with cursor Hand on hovering. This behavior is unavailable from usual XAML or code-behind.
    /// </summary>
    public class CursorButton : Button
    {
        public CursorButton()
        {
            ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
        }
    }
}
