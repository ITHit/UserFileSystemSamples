namespace WebDAVDrive
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            MainPage = new AppShell();                  
        }

        protected override void OnStart()
        {
            base.OnStart();              
        }
    }
}
