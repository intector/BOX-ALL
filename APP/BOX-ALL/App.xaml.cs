using BOX_ALL.Views;

namespace BOX_ALL
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            // Force dark theme
            Application.Current.UserAppTheme = AppTheme.Dark;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            // Start with the animated splash screen
            return new Window(new AnimatedSplashPage());
        }
    }
}
