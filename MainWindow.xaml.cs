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
                // TODO: log the error
                DisplayError("ERROR: TaystJK executable was not found!", true);
                return;
            }

            SkipButton.Visibility = Visibility.Visible;

            // TODO: check for a new version
        }

        private async void DisplayError(string errorMessage, bool preventsGameLaunch = false)
        {
            StatusLabel.Content = errorMessage;
            await UpdatingIcon.MorphIntoCircle();
            UpdatingIcon.Visibility = Visibility.Hidden;
            ErrorIcon.Visibility = Visibility.Visible;
            ErrorIcon.AnimateIn();
            OkButton.Visibility = Visibility.Visible;
            // TODO: "Launch anyway" button (preventsGameLaunch == false)
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            SkipButton.IsEnabled = false;
            StatusLabel.Content = "Skipped. Launching...";
            await Task.Run(() => updater.LaunchTaystJK());
            await UpdatingIcon.MorphIntoCircle();
            Close();
        }
    }
}
