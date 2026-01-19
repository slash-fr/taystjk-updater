using System.Windows;

namespace TaystJkUpdater
{
    public partial class App : Application
    {
        public SplashScreen SplashScreen { get; }

        App()
        {
            // Unfortunately, the splash screen does not account for DPI scaling (unlike the main window).
            // We could use different images for different DPI scaling settings, but...
            // ...there is an unlimited amount of possible scaling values (users can set custom values).
            SplashScreen = new SplashScreen("/assets/background-image.jpg");
            SplashScreen.Show(autoClose: false, topMost: false);
        }
    }
}
