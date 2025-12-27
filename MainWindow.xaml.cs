using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;

namespace Updater
{
    public partial class MainWindow : Window
    {
        private UpdaterLogic updater;
        private CancellationTokenSource cancellationTokenSource;

        public MainWindow()
        {
            InitializeComponent();
            cancellationTokenSource = new CancellationTokenSource();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            updater = new UpdaterLogic();

            if (!updater.InitializeUpdater())
            {
                DisplayError("TaystJK executable was not found!", true);
                return;
            }

            StatusLabel.Content = "Checking for TaystJK updates...";
            SkipButton.Visibility = Visibility.Visible;

            UpdateCheckResult updateCheckResult = await updater.CheckTaystJkUpdate();

            if (cancellationTokenSource.IsCancellationRequested) { return; }

            if (updateCheckResult == UpdateCheckResult.Error)
            {
                DisplayError("Failed to check for TaystJK updates!");
                return;
            }

            if (updateCheckResult == UpdateCheckResult.AlreadyUpToDate)
            {
                StatusLabel.Content = "Already up-to-date. Launching...";
                SkipButton.Visibility = Visibility.Hidden;
                await Task.Run(() => updater.LaunchTaystJK());
                await UpdatingIcon.MorphIntoCircle();
                Close();
                return;
            }

            if (updateCheckResult == UpdateCheckResult.AlreadyCheckedRecently)
            {
                StatusLabel.Content = "Already checked in the past hour. Launching...";
                SkipButton.Visibility = Visibility.Hidden;
                await Task.Run(() => updater.LaunchTaystJK());
                await UpdatingIcon.MorphIntoCircle();
                Close();
                return;
            }

            // NeedsUpdate
            StatusLabel.Content = "Downloading TaystJK update...";
            Progress<int> progress = new Progress<int>();
            progress.ProgressChanged += (progressSender, progressPercentage) => {
                StatusLabel.Content = "Downloading TaystJK update... (" + progressPercentage + "%)";
            };
            if (!await updater.DownloadTaystJkUpdate(cancellationTokenSource.Token, progress))
            {
                if (cancellationTokenSource.IsCancellationRequested) { return; }
                DisplayError("Failed to download the new TaystJK version!");
                return;
            }

            if (cancellationTokenSource.IsCancellationRequested) { return; }

            StatusLabel.Content = "Unpacking TaystJK update...";
            if (!await updater.UnpackTaystJkUpdate())
            {
                if (cancellationTokenSource.IsCancellationRequested) { return; }
                DisplayError("Failed to unpack the new TaystJK version!");
                return;
            }

            if (cancellationTokenSource.IsCancellationRequested) { return; }

            StatusLabel.Content = "Backing up current TaystJK files...";
            if (!await updater.BackupTaystJkFiles())
            {
                if (cancellationTokenSource.IsCancellationRequested) { return; }
                DisplayError("Failed to backup the current TaystJK version!");
                return;
            }

            if (cancellationTokenSource.IsCancellationRequested) { return; }

            StatusLabel.Content = "Applying TaystJK update...";
            SkipButton.Visibility = Visibility.Hidden; // Skipping would leave a partially updated version...
            if (!await updater.CopyTaystJkFiles())
            {
                DisplayError("Failed to install the new TaystJK version!");
                return;
            }

            StatusLabel.Content = "Update complete. Launching...";
            await UpdatingIcon.MorphIntoCircle();
            // TODO: success icon
            await Task.Run(() => updater.LaunchTaystJK());
            Close();
        }

        /// <summary>
        /// Display the specified error message, along with appropriate buttons
        /// (Open logs, Exit, Launch anyway, ...)
        /// </summary>
        /// <param name="errorMessage">The error message to display in the UI</param>
        /// <param name="preventsGameLaunch">If true, the "Launch anyway" button won't be shown</param>
        private async void DisplayError(string errorMessage, bool preventsGameLaunch = false)
        {
            StatusLabel.Content = errorMessage;
            SkipButton.Visibility = Visibility.Hidden;
            if (preventsGameLaunch)
            {
                LaunchButton.Visibility = Visibility.Collapsed;
            }
            ButtonRow.Visibility = Visibility.Visible;
            ((Storyboard)ButtonRow.Resources["AnimateIn"]).Begin(this);
            await UpdatingIcon.MorphIntoCircle();
            UpdatingIcon.Visibility = Visibility.Hidden;
            ErrorIcon.Visibility = Visibility.Visible;
            await ErrorIcon.AnimateIn();
        }

        private void LogsButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            LogsButton.IsEnabled = false;
            ExitButton.IsEnabled = false;
            LaunchButton.IsEnabled = false;
            StatusLabel.Content = "Launching...";
            await Task.Run(() => updater.LaunchTaystJK());
            await ErrorIcon.AnimateOut();
            Close();
        }

        private async void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            SkipButton.IsEnabled = false;
            StatusLabel.Content = "Skipped. Launching...";
            cancellationTokenSource.Cancel();
            await Task.Run(() => updater.LaunchTaystJK());
            await UpdatingIcon.MorphIntoCircle();
            Close();
        }
    }
}
