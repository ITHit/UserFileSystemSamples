using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using WebDAVDrive.LoginWPF.ViewModels;
using System.Security;
using System.Runtime.InteropServices;

namespace WebDAVDrive.LoginWPF
{   
    /// <summary>
    /// Interaction logic for ChallengeLogin.xaml
    /// </summary>
    public partial class ChallengeLogin : Window
    {
        public ChallengeLogin()
        {
            InitializeComponent();

            this.DataContext = new ChallengeLoginViewModel();

            //Setting backgorund color from windows settings
            Brush brush = SystemParameters.WindowGlassBrush.Clone();
            Color backColor = ((SolidColorBrush)brush).Color;

            //Calculating text color from backgorund
            //Using of luma, for more see https://en.wikipedia.org/wiki/Luma_%28video%29
            var l = 0.2126 * backColor.ScR + 0.7152 * backColor.ScG + 0.0722 * backColor.ScB;
            Brush textColor = l < 0.5 ? Brushes.White : Brushes.Black;

            //set form background and foreground color
            this.Resources["FormBackground"] = brush;
            this.Resources["FormForeground"] = textColor;
        }

        /// <summary>
        /// This method updates Password in ViewModel (we cannot bind text directly from PasswordBox as with TexBox in security reasons)
        /// </summary>
        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (this.DataContext != null)
            { ((dynamic)this.DataContext).Password = ((PasswordBox)sender).SecurePassword; }
        }

        private void Button_Ok_Click(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this).DialogResult = true;
            Close();
        }

        private void Button_Cancel_Click(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this).DialogResult = false;
            Close();
        }

        private void OnCloseButtonClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    /// <summary>
    /// Class PasswordBoxMonitor used to monitor state of PasswordBox and if length is 0 placeholder will be shown.
    /// WPF does not contain defalut mechanism for placehloders(watermarks) like in WinForms, so we need this solution
    /// </summary>
    public class PasswordBoxMonitor : DependencyObject
    {
        public static bool GetIsMonitoring(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsMonitoringProperty);
        }

        public static void SetIsMonitoring(DependencyObject obj, bool value)
        {
            obj.SetValue(IsMonitoringProperty, value);
        }

        public static readonly DependencyProperty IsMonitoringProperty = DependencyProperty.RegisterAttached("IsMonitoring", typeof(bool), typeof(PasswordBoxMonitor), new UIPropertyMetadata(false, OnIsMonitoringChanged));

        public static int GetPasswordLength(DependencyObject obj)
        {
            return (int)obj.GetValue(PasswordLengthProperty);
        }

        public static void SetPasswordLength(DependencyObject obj, int value)
        {
            obj.SetValue(PasswordLengthProperty, value);
        }

        public static readonly DependencyProperty PasswordLengthProperty = DependencyProperty.RegisterAttached("PasswordLength", typeof(int), typeof(PasswordBoxMonitor), new UIPropertyMetadata(0));

        private static void OnIsMonitoringChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var pb = d as PasswordBox;
            if (pb == null)
            {
                return;
            }
            if ((bool)e.NewValue)
            {
                pb.PasswordChanged += PasswordChanged;
            }
            else
            {
                pb.PasswordChanged -= PasswordChanged;
            }
        }

        static void PasswordChanged(object sender, RoutedEventArgs e)
        {
            var pb = sender as PasswordBox;
            if (pb == null)
            {
                return;
            }
            SetPasswordLength(pb, pb.Password.Length);
        }
    }
}
