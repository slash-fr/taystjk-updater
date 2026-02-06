using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using TaystJkUpdater.Extensions;
using TaystJkUpdater.GitHub;
using TaystJkUpdater.Logging;
using TaystJkUpdater.ProgramData;

namespace TaystJkUpdater.Updater
{
    /// <summary>
    /// Class responsible for updating the updater.
    /// (Yo dawg, I heard you like updates...)
    /// </summary>
    internal class UpdaterUpdater
    {
        private const string UpdaterExeFilename = "TaystJK_Updater.exe";
        private readonly Logger _logger;
        private readonly string _pathToUpdaterExe;
        private readonly string _pathToOldUpdaterExe;
        private string _downloadUrl;
        private string _pathToDownloadedFile;

        public UpdaterUpdater(Logger logger)
        {
            _logger = logger;
            _pathToUpdaterExe = Assembly.GetExecutingAssembly().Location;
            _pathToOldUpdaterExe = _pathToUpdaterExe + ".old";

            try
            {
                if (File.Exists(_pathToOldUpdaterExe))
                {
                    File.Delete(_pathToOldUpdaterExe);
                }
            }
            catch (Exception exception)
            {
                logger.Warning(
                    "An error occurred while trying to remove an old version of the updater.\n"
                    + exception.ToString()
                );
            }
        }

        /// <summary>
        /// Checks whether an update is available for the updater.
        /// </summary>
        /// <param name="httpClient">HTTP client to use for the API request</param>
        /// <param name="persistentState">Updater state (for reading and writing)</param>
        public async Task<bool> CanUpdate(
            HttpClient httpClient,
            PersistentState persistentState,
            CancellationToken cancellationToken)
        {
            _logger.Debug("Checking for updater updates...");

            string updaterCommitHash = GitHelper.GetGitCommitHashFromAssembly();

            if (String.IsNullOrEmpty(updaterCommitHash))
            {
                _logger.Warning($"The updater's commit hash is missing.");
                return false;
            }

            if (!GitHelper.IsValidCommitHash(updaterCommitHash))
            {
                _logger.Warning($"The updater's commit hash is invalid. Got: \"{updaterCommitHash}\".");
                return false;
            }

            _logger.Debug("Current updater commit hash: " + '"' + updaterCommitHash + '"' + ".");
            
            if (String.IsNullOrEmpty(_pathToUpdaterExe))
            {
                _logger.Warning("Could not determine the path to the updater.");
                return false;
            }

            if (_pathToUpdaterExe.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                _logger.Warning("The detected path to the updater is invalid: " + '"' + _pathToUpdaterExe + '"' + ".");
                return false;
            }

            _logger.Info("Detected path to the updater: " + '"' + _pathToUpdaterExe + '"' + ".");

            // Don't check more than once per hour
            if (persistentState.LastCheckedByPath.TryGetValue(_pathToUpdaterExe, out DateTime lastChecked)
                && DateTime.Now < lastChecked.AddHours(1))
            {
                _logger.Info("Already checked for updater updates less than an hour ago.");
                return false;
            }

            if (!(persistentState.GithubThrottledUntil is null)
                && persistentState.GithubThrottledUntil >= DateTime.Now)
            {
                _logger.Warning("GitHub API is throttled due to a recent 429 error.");
                return false;
            }

            Uri requestUri = new Uri(AppEnvironment.LatestUpdaterReleaseApiUrl);
            _logger.Debug("GitHub API request: " + requestUri);

            ReleaseInfo latestRelease;

            try
            {
                latestRelease = await GithubApiHelper.GetReleaseInfo(httpClient, requestUri, cancellationToken);
            }
            catch (TaskCanceledException exception)
            {
                _logger.Warning("A GitHub API request was cancelled: " + requestUri);
                _logger.Debug(exception.ToString());
                return false;
            }
            catch (TooManyRequestsException exception)
            {
                _logger.Warning(exception.Message);

                persistentState.LastCheckedByPath[_pathToUpdaterExe] = DateTime.Now;
                persistentState.GithubThrottledUntil = DateTime.Now.AddHours(1);
                persistentState.Save();

                return false;
            }
            catch (GithubApiException exception)
            {
                _logger.Warning(exception.Message);

                if (exception.InnerException != null)
                {
                    _logger.Debug(exception.InnerException.ToString());
                }

                // Don't update persistentState.LastCheckedByPath here,
                // because we want to try again as soon as possible (i.e. even less than an hour later)

                return false;
            }

            if (!GitHelper.IsValidCommitHash(latestRelease.TargetCommitish))
            {
                _logger.Warning(
                    "The commit hash from the updater's latest release is invalid. "
                    + "Got: " + '"' + latestRelease.TargetCommitish + '"' + "."
                );
                return false;
            }


            if (GitHelper.IsSameCommit(updaterCommitHash, latestRelease.TargetCommitish))
            {
                _logger.Info("The updater is up to date (commit: " + updaterCommitHash + ").");

                // Don't check in the next hour:
                persistentState.LastCheckedByPath[_pathToUpdaterExe] = DateTime.Now;
                persistentState.Save();

                return false;
            }

            if (!latestRelease.DownloadUrlByAssetName.TryGetValue(UpdaterExeFilename, out _downloadUrl))
            {
                _logger.Warning(
                    "The updater's latest release does not seem to have a "
                    + '"' + UpdaterExeFilename + '"' + " asset."
                );

                _logger.Debug("Assets:");
                foreach (var asset in latestRelease.DownloadUrlByAssetName)
                {
                    _logger.Debug(asset.Key + " => " + asset.Value);
                }

                return false;
            }

            _logger.Info("The updater can be updated.");
            _logger.Info("Current commit: " + updaterCommitHash);
            _logger.Info("Latest release: " + latestRelease.TargetCommitish);

            return true;
        }

        public async Task<bool> DownloadLatestRelease(HttpClient httpClient, CancellationToken cancellationToken)
        {
            if (String.IsNullOrEmpty(_downloadUrl))
            {
                throw new InvalidOperationException(
                    "DownloadLatestRelease was called too soon (missing: _downloadUrl)."
                );
            }

            try
            {
                ProgramDataHelper.PurgeAllDownloads();
            }
            catch (ProgramDataNotWritableException exception)
            {
                _logger.Warning("Could not purge old downloads.");
                _logger.Debug(exception.ToString());
            }

            FileStream destinationFilestream = null;

            try
            {
                string pathToDownloadDirectory = ProgramDataHelper.CreateNewDownloadSubdirectory();
                _pathToDownloadedFile = pathToDownloadDirectory + Path.DirectorySeparatorChar + UpdaterExeFilename;

                _logger.Debug("Downloading: " + _downloadUrl);

                destinationFilestream = new FileStream(
                    _pathToDownloadedFile,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None
                );

                await httpClient.DownloadAsync(_downloadUrl, destinationFilestream, null, cancellationToken);
            }
            catch (TaskCanceledException exception)
            {
                _logger.Warning("A download was cancelled: " + _downloadUrl);
                _logger.Debug(exception.ToString());
                return false;
            }
            catch (Exception exception)
            {
                if (exception is HttpRequestException  || exception is ProgramDataNotWritableException ||
                    exception is FileNotFoundException || exception is DirectoryNotFoundException      ||
                    exception is SecurityException     || exception is UnauthorizedAccessException     ||
                    exception is PathTooLongException  || exception is IOException                     )
                {
                    _logger.Warning(
                        "An error occurred while downloading the latest updater release.\n"
                        + exception.ToString()
                    );
                    return false;
                }

                // Unexpected exception, just rethrow it:
                throw exception;
            }
            finally
            {
                destinationFilestream?.Dispose();
            }

            _logger.Info(
                "Successfully downloaded the latest updater release to: " + '"' + _pathToDownloadedFile + '"' + "."
            );

            return true;
        }

        public bool BackupCurrentUpdater()
        {
            string pathToBackupDirectory;

            try
            {
                pathToBackupDirectory = ProgramDataHelper.CreateNewBackupSubdirectory();

                _logger.Debug("Backing up the current updater to: " + '"' + pathToBackupDirectory + '"');

                // Note: We can't MOVE the current exe to the backup folder, because it may be on another drive.
                //
                // https://learn.microsoft.com/en-us/dotnet/api/system.io.file.move?view=netframework-4.8
                // > If you try to move a file across disk volumes and that file is in use,
                // > the file is copied to the destination, but it is not deleted from the source.
                //
                // So, let's just COPY it for now:
                File.Copy(
                    _pathToUpdaterExe,
                    pathToBackupDirectory + Path.DirectorySeparatorChar + UpdaterExeFilename
                );
            }
            catch (Exception exception)
            {
                if (exception is IOException           || exception is ProgramDataNotWritableException ||
                    exception is FileNotFoundException || exception is DirectoryNotFoundException      ||
                    exception is SecurityException     || exception is UnauthorizedAccessException     ||
                    exception is PathTooLongException  )
                {
                    _logger.Warning(
                        "An error occurred while backing up the current updater.\n"
                        + exception.ToString()
                    );
                    return false;
                }

                // Unexpected exception, just rethrow it:
                throw exception;
            }

            _logger.Info("Successfully backed up the current updater to: " + '"' + pathToBackupDirectory + '"' + ".");
            return true;
        }

        public bool InstallNewUpdater(PersistentState persistentState)
        {
            if (String.IsNullOrEmpty(_pathToDownloadedFile))
            {
                throw new InvalidOperationException(
                    "InstallNewUpdater() was called too soon (missing: _pathToDownloadedFile)."
                );
            }

            _logger.Debug("Installing the new updater...");

            try
            {
                File.Move(_pathToUpdaterExe, _pathToOldUpdaterExe);
                File.Copy(_pathToDownloadedFile, _pathToUpdaterExe);
            }
            catch (Exception exception)
            {
                if (exception is IOException           || exception is PathTooLongException        ||
                    exception is FileNotFoundException || exception is DirectoryNotFoundException  ||
                    exception is SecurityException     || exception is UnauthorizedAccessException )
                {
                    _logger.Error("An error occurred while installing the new updater.", exception);
                    return false;
                }

                // Unexpected exception, just rethrow it:
                throw exception;
            }

            _logger.Info("Installed the new updater version successfully.");

            persistentState.LastCheckedByPath[_pathToUpdaterExe] = DateTime.Now;
            persistentState.Save();

            try
            {
                ProgramDataHelper.PurgeAllDownloads();
            }
            catch (ProgramDataNotWritableException exception)
            {
                _logger.Warning("Could not purge downloads.");
                _logger.Debug(exception.ToString());
            }

            return true;
        }

        public void LaunchNewUpdater()
        {
            List<string> commandLineArgs = Environment.GetCommandLineArgs().ToList();

            if (commandLineArgs.Count < 2)
            {
                System.Diagnostics.Process.Start(_pathToUpdaterExe);
                return;
            }

            // Ignore the current executable file name (e.g. "Updater.exe")
            commandLineArgs.RemoveAt(0);

            string commandLineArgsAsString = '"' + String.Join('"' + " " + '"', commandLineArgs) + '"';

            System.Diagnostics.Process.Start(_pathToUpdaterExe, commandLineArgsAsString);
        }
    }
}
