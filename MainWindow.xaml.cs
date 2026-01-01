using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;

namespace Updater
{
    public partial class MainWindow : Window
    {
        private Logger logger;
        private UpdateLock updateLock;
        private TaystJkUpdater taystJkUpdater;
        private readonly CancellationTokenSource cancellationTokenSource;

        public MainWindow()
        {
            InitializeComponent();
            cancellationTokenSource = new CancellationTokenSource();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                logger = new Logger();
                await PerformUpdate(logger);
                updateLock?.Dispose();
            }
            catch (UnloggableErrorException exception)
            {
                MessageBox.Show(
                    "An error occurred:\n"
                    + "\n"
                    + exception.Message + "\n"
                    + "\n"
                    + "------------------------------------------\n"
                    + "\n"
                    + "Additionally: Could not write the error above to the log file!\n"
                    + "\n"
                    + exception.InnerException.GetType().Name + ":\n"
                    + exception.InnerException.Message,
                    "Error!",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                Close();
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    exception.Message,
                    exception.GetType().ToString(), // Caption
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                Close();
            }
        }

        private async Task PerformUpdate(Logger logger)
        {
            taystJkUpdater = new TaystJkUpdater(logger);

            if (!taystJkUpdater.InitializeUpdater())
            {
                DisplayError("TaystJK executable was not found!", true);
                return;
            }

            try
            {
                int totalLockDelayInSeconds = 0;
                updateLock = UpdateLock.Acquire();
                while (updateLock is null)
                {
                    if (totalLockDelayInSeconds == 0)
                    {
                        StatusLabel.Content = "Waiting for an update to finish...";
                        logger.Warning("Could not acquire the update lock. Waiting until the lock is released.");
                    }
                    else if (totalLockDelayInSeconds == 5)
                    {
                        // After 5 seconds, show the "Exit" button (+ "Open log file" button), but keep trying
                        LaunchButton.Visibility = Visibility.Collapsed;
                        ButtonRow.Visibility = Visibility.Visible;
                        ((Storyboard)ButtonRow.Resources["AnimateIn"]).Begin(this);
                    }

                    await Task.Delay(1000);
                    totalLockDelayInSeconds++;
                    updateLock = UpdateLock.Acquire();
                }
            }
            catch (ProgramDataNotWritableException exception)
            {
                logger.Error(exception.Message, exception.InnerException);
                DisplayError("Could not write to the ProgramData folder!");
                return;
            }

            ButtonRow.Visibility = Visibility.Hidden;

            StatusLabel.Content = "Checking for TaystJK updates...";
            SkipButton.Visibility = Visibility.Visible;

            UpdateCheckResult updateCheckResult = await Task.Run(taystJkUpdater.CheckForUpdates);

            if (cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            if (updateCheckResult == UpdateCheckResult.Error)
            {
                DisplayError("Failed to check for TaystJK updates!");
                return;
            }

            if (updateCheckResult == UpdateCheckResult.AlreadyUpToDate)
            {
                SkipButton.Visibility = Visibility.Hidden;
                await LaunchTaystJkWithSuccessMessage("Already up-to-date. Launching...");
                return;
            }

            if (updateCheckResult == UpdateCheckResult.AlreadyCheckedRecently)
            {
                SkipButton.Visibility = Visibility.Hidden;
                await LaunchTaystJkWithSuccessMessage("Already checked in the past hour. Launching...");
                return;
            }

            // NeedsUpdate
            StatusLabel.Content = "Downloading TaystJK update...";
            Progress<int> progress = new Progress<int>();
            progress.ProgressChanged += (progressSender, progressPercentage) => {
                StatusLabel.Content = "Downloading TaystJK update... (" + progressPercentage + "%)";
            };
            if (!await taystJkUpdater.DownloadLatestRelease(cancellationTokenSource.Token, progress))
            {
                DisplayError("Failed to download the new TaystJK version!");
                return;
            }

            if (cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            StatusLabel.Content = "Unpacking TaystJK update...";
            if (!await taystJkUpdater.UnpackLatestRelease())
            {
                DisplayError("Failed to unpack the new TaystJK version!");
                return;
            }

            if (cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            StatusLabel.Content = "Backing up current TaystJK files...";
            if (!await taystJkUpdater.BackupCurrentFiles())
            {
                DisplayError("Failed to backup the current TaystJK version!");
                return;
            }

            if (cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            StatusLabel.Content = "Applying TaystJK update...";
            SkipButton.Visibility = Visibility.Hidden; // Skipping would leave a partially updated version...
            if (!await taystJkUpdater.CopyNewFiles())
            {
                DisplayError("Failed to install the new TaystJK version!");
                return;
            }

            await LaunchTaystJkWithSuccessMessage("Update complete. Launching...");
        }

        /// <summary>
        /// Display the specified error message, along with appropriate buttons
        /// (Open logs, Exit, Launch anyway, ...)
        /// </summary>
        /// <param name="errorMessage">The error message to display in the UI</param>
        /// <param name="preventsGameLaunch">If true, the "Launch anyway" button won't be shown</param>
        private async void DisplayError(string errorMessage, bool preventsGameLaunch = false)
        {
            if (cancellationTokenSource.IsCancellationRequested)
            {
                // Don't display the error if the user has clicked the "Skip" button (the updater will close soon)
                return;
            }

            updateLock?.Dispose(); // Release the lock now (no need to wait until the updater closes)

            StatusLabel.Content = errorMessage;
            SkipButton.Visibility = Visibility.Hidden;
            LaunchButton.Visibility = preventsGameLaunch ? Visibility.Collapsed : Visibility.Visible;
            ButtonRow.Visibility = Visibility.Visible;
            ((Storyboard)ButtonRow.Resources["AnimateIn"]).Begin(this);
            await UpdatingIcon.MorphIntoCircle();
            UpdatingIcon.Visibility = Visibility.Hidden;
            ErrorIcon.Visibility = Visibility.Visible;
            await ErrorIcon.AnimateIn();
        }

        /// <summary>
        /// Displays the specified message, displays the "success" icon, launches TaystJK, and closes the updater.
        /// </summary>
        /// <param name="successMessage">Status message to display</param>
        private async Task LaunchTaystJkWithSuccessMessage(string successMessage)
        {
            StatusLabel.Content = successMessage;

            await UpdatingIcon.MorphIntoCircle();
            SuccessIcon.Visibility = Visibility.Visible;
            UpdatingIcon.Visibility = Visibility.Hidden;

            _ = Task.Run(taystJkUpdater.LaunchTaystJK);
            Topmost = true;

            await SuccessIcon.AnimateIn();
            await Task.Delay(2000);

            Close();
        }

        private async void LogsButton_Click(object sender, RoutedEventArgs e)
        {
            LogsButton.IsEnabled = false;

            using (Process textEditorProcess = new Process())
            {
                textEditorProcess.StartInfo = new ProcessStartInfo(logger.LogFilePath)
                {
                    UseShellExecute = true
                };
                textEditorProcess.Start();
            }

            await Task.Delay(2000);
            LogsButton.IsEnabled = true;
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
            await Task.Run(taystJkUpdater.LaunchTaystJK);
            await ErrorIcon.AnimateOut();
            Topmost = true;
            await Task.Delay(1000);
            Close();
        }

        private async void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            logger.Info("Skipping...");
            SkipButton.IsEnabled = false;
            cancellationTokenSource.Cancel();
            await LaunchTaystJkWithSuccessMessage("Skipped. Launching...");
        }
    }
}
