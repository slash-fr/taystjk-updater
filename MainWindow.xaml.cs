using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace Updater
{
    public partial class MainWindow : Window
    {
        private UpdaterLogic updater;

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await Task.Run(() => {
                    updater = new UpdaterLogic();
                });
            }
            catch (FileNotFoundException)
            {
                StatusLabel.Content = "ERROR: TaystJK executable was not found!";
                UpdatingIcon.Visibility = Visibility.Hidden;
                ErrorIcon.Visibility = Visibility.Visible;
                OkButton.Visibility = Visibility.Visible;
                return;
            }

            SkipButton.Visibility = Visibility.Visible;

            // TODO: check for a new version
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            SkipButton.IsEnabled = false;
            StatusLabel.Content = "Skipped. Launching...";
            // TODO: Stop the "updating" icon animation

            // TODO: Cancel any ongoing tasks

            await Task.Run(() => updater.LaunchTaystJK());

            // Close the "splash screen" (updater) 1 second later
            await Task.Delay(1000);
            Close();
        }
    }
}
