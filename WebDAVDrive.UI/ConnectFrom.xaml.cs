using System.Windows;
using System.Windows.Media;
using WebDAVDrive.UI.ViewModels;

namespace WebDAVDrive.UI
{   
    /// <summary>
    /// Interaction logic for ChallengeLogin.xaml
    /// </summary>
    public partial class ConnectForm : Window
    {
        public ConnectForm()
        {
            InitializeComponent();

            this.DataContext = new ConnectViewModel();

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

}
