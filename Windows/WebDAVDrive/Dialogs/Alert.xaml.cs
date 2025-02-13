using Microsoft.UI.Xaml;
using System;

using WebDAVDrive.Extensions;

namespace WebDAVDrive.Dialogs
{
    /// <summary>
    /// An empty dialog that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class Alert : DialogWindow
    {
        Action actionYes;
        Action actionNo;

        public Alert(string text, string yes, string no, Action actionYes, Action actionNo)
        {
            this.actionYes = actionYes;
            this.actionNo = actionNo;
            InitializeComponent();
            this.Resize(800, 400);

            // Make <line_break> works if text set from code.
            text = text.Replace("&#x0a;", "\n");
            lblAlertText.Text = text;
            if (yes == null)
            {
                btnYes.Visibility = Visibility.Collapsed;
            }
            else
            {
                btnYes.Content = yes;
            }
            if (no != null)
            {
                btnNo.Content = no;
            }

            // Center the window.
            SetDefaultPosition();
        }

        private void OnYesClicked(object sender, RoutedEventArgs e)
        {
            Close();
            actionYes.Invoke();
        }

        private void OnNoClicked(object sender, RoutedEventArgs e)
        {
            Close();
            actionNo.Invoke();
        }
    }
}
