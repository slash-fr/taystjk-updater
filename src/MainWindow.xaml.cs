using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using TaystJkUpdater.GitHub;
using TaystJkUpdater.Logging;
using TaystJkUpdater.ProgramData;
using TaystJkUpdater.Updater;

namespace TaystJkUpdater
{
    public partial class MainWindow : Window
    {
        private Logger logger;
        private UpdateLock updateLock;
        private TaystJkUpdaterLogic taystJkUpdater;
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

                try
                {
                    // Try to put the window in the same spot as the initial splash screen
                    DpiScale dpiScale = VisualTreeHelper.GetDpi(this);
                    this.Left = (SystemParameters.PrimaryScreenWidth / dpiScale.DpiScaleX - this.Width) / 2;
                    this.Top = (SystemParameters.PrimaryScreenHeight / dpiScale.DpiScaleY - this.Height) / 2;

                    // Close the initial splash screen
                    ((App)App.Current).SplashScreen.Close(TimeSpan.FromSeconds(0.5));

                    await PerformUpdate(logger);
                }
                catch (TaskCanceledException exception)
                {
                    // Typically happens when the user clicks the "Skip" button
                    logger.Debug("Cancelled " + exception.Task);
                }
                catch (ProgramDataNotReadableException exception)
                {
                    logger.Error(exception.Message, exception.InnerException);
                    DisplayError("Could not read from the ProgramData folder!");
                }
                catch (ProgramDataNotWritableException exception)
                {
                    logger.Error(exception.Message, exception.InnerException);
                    DisplayError("Could not write to the ProgramData folder!");
                }
                catch (Exception exception)
                {
                    if (!(exception is UnloggableErrorException))
                    {
                        const string message = "An unexpected error occurred!";
                        logger.Error(message, exception);
                        DisplayError(message);
                    }
                }
                finally
                {
                    updateLock?.Dispose();
                }
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
        }

        private async Task PerformUpdate(Logger logger)
        {
            GithubApiClient githubApiClient = new GithubApiClient();
            PersistentState persistentState = new PersistentState();

            taystJkUpdater = new TaystJkUpdaterLogic(logger);

            if (!await Task.Run(taystJkUpdater.InitializeUpdater))
            {
                DisplayError("TaystJK executable was not found!", true);
                return;
            }

            await AcquireLock(logger);

            await Task.Run(persistentState.Load);

            StatusLabel.Content = "Checking for TaystJK updates...";
            SkipButton.Visibility = Visibility.Visible;

            UpdateCheckResult updateCheckResult = await Task.Run(
                () => taystJkUpdater.CheckForUpdates(githubApiClient, cancellationTokenSource.Token, persistentState)
            );

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

            bool downloadedSuccessfully = await Task.Run(
                () => taystJkUpdater.DownloadLatestRelease(cancellationTokenSource.Token, progress)
            );

            if (!downloadedSuccessfully)
            {
                DisplayError("Failed to download the new TaystJK version!");
                return;
            }

            if (cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            StatusLabel.Content = "Unpacking TaystJK update...";
            if (!await Task.Run(taystJkUpdater.UnpackLatestRelease))
            {
                DisplayError("Failed to unpack the new TaystJK version!");
                return;
            }

            if (cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            StatusLabel.Content = "Backing up current TaystJK files...";
            bool backedUpSuccessfully = await Task.Run(
                () => taystJkUpdater.BackupCurrentFiles(cancellationTokenSource.Token)
            );
            if (!backedUpSuccessfully)
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
            if (!await taystJkUpdater.CopyNewFiles(persistentState))
            {
                DisplayError("Failed to install the new TaystJK version!");
                return;
            }

            await LaunchTaystJkWithSuccessMessage("Update complete. Launching...");
        }

        /// <summary>
        /// Waits until other instances of the updater finish their job.
        /// </summary>
        /// <exception cref="ProgramDataNotWritableException"></exception>
        private async Task AcquireLock(Logger logger)
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

            ButtonRow.Visibility = Visibility.Hidden;
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
            updateLock?.Dispose(); // Release the lock now (no need to wait until the updater closes)

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
            logger.Debug("The \"Open log file\" button was clicked.");
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
            logger.Debug("The \"Exit\" button was clicked.");
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
