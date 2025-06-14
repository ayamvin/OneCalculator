namespace OneCalculator
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            MainPage = new AppShell();

            // Load saved theme preference
            var savedTheme = Preferences.Get("SelectedTheme", "System Default");
            var theme = savedTheme switch
            {
                "Light" => AppTheme.Light,
                "Dark" => AppTheme.Dark,
                _ => AppTheme.Unspecified
            };

            if (Application.Current != null)
            {
                Application.Current.UserAppTheme = theme;
            }
        }
    }
}
