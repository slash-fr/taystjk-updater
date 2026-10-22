using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TaystJkUpdater.Extensions;
using TaystJkUpdater.GitHub;
using TaystJkUpdater.Logging;
using TaystJkUpdater.ProgramData;

namespace TaystJkUpdater.Updater
{
    /// <summary>
    /// TaystJK updater logic (does not handle the UI)
    /// </summary>
    internal class TaystJkUpdaterLogic
    {
        private readonly HashSet<string> _allowedTaystJkFilenames = new HashSet<string>()
        {
            "taystjk.x86_64.exe",
            "taystjk.x86.exe",
            "taystjk.arm64.exe", // Just in case...
        };

        private readonly Dictionary<string, string> _taystJkArchitectureByFilename = new Dictionary<string, string>()
        {
            { "taystjk.x86_64.exe", "x86_64" },
            { "taystjk.x86.exe",    "x86"    },
            { "taystjk.arm64.exe",  "arm64"  },
        };

        private readonly Logger _logger;
        private string _commandLineArgsAsString = "";
        private string _pathToTaystJkExe = "";
        private string _taystJkArchitecture = "";
        private DateTime? _latestReleaseCreatedAt = null;
        private string _downloadFilename = "";
        private string _downloadUrl = "";
        private string _pathToDownloadedFile = "";
        private string _pathToExtractedArchive = "";

        public TaystJkUpdaterLogic(Logger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Parses command-line arguments and locates TaystJK executable
        /// </summary>
        /// <returns>true on success, false if TaystJK exe was not found</returns>
        public bool InitializeUpdater()
        {
            return ParseCommandLineArgs() && FindTaystJkExe() && DetectTaystJkArchitecture();
        }

        /// <summary>
        /// Checks whether an update is available for TaystJK
        /// </summary>
        /// <param name="httpClient">HTTP client to use for the API request</param>
        /// <param name="persistentState">Updater state (for reading and writing)</param>
        public async Task<UpdateCheckResult> CheckForUpdates(
            HttpClient httpClient,
            CancellationToken cancellationToken,
            PersistentState persistentState)
        {
            if (String.IsNullOrEmpty(_pathToTaystJkExe))
            {
                throw new InvalidOperationException(
                    "CheckForUpdates() was called too soon (missing: _pathToTaystJkExe)."
                );
            }

            if (String.IsNullOrEmpty(_taystJkArchitecture))
            {
                throw new InvalidOperationException(
                    "CheckForUpdates() was called too soon (missing: _taystJkArchitecture)."
                );
            }

            _logger.Debug("Checking for TaystJK updates...");

            // Don't check more than once per hour
            if (persistentState.LastCheckedByPath.TryGetValue(_pathToTaystJkExe, out DateTime lastChecked)
                && DateTime.Now < lastChecked.AddHours(1))
            {
                _logger.Info("Already checked for TaystJK updates less than an hour ago.");
                return UpdateCheckResult.AlreadyCheckedRecently;
            }

            if (!(persistentState.GithubThrottledUntil is null)
                && persistentState.GithubThrottledUntil >= DateTime.Now)
            {
                _logger.Warning("GitHub API is throttled due to a recent 429 error.");
                return UpdateCheckResult.AlreadyCheckedRecently;
            }

            ReleaseInfo latestRelease;
            Uri requestUri = new Uri(AppEnvironment.LatestTaystJkReleaseApiUrl);
            _logger.Debug("GitHub API request: " + requestUri);

            try
            {
                latestRelease = await GithubApiHelper.GetReleaseInfo(httpClient, requestUri, cancellationToken);
                _latestReleaseCreatedAt = latestRelease.CreatedAt;
            }
            catch (TaskCanceledException exception)
            {
                _logger.Warning("A GitHub API request was cancelled: " + requestUri);
                _logger.Debug(exception.ToString());
                return UpdateCheckResult.Error;
            }
            catch (TooManyRequestsException exception)
            {
                _logger.Error(exception.Message);

                persistentState.LastCheckedByPath[_pathToTaystJkExe] = DateTime.Now;
                persistentState.GithubThrottledUntil = DateTime.Now.AddHours(1);
                persistentState.Save();

                return UpdateCheckResult.Error;
            }
            catch (GithubApiException exception)
            {
                if (exception.InnerException != null)
                {
                    _logger.Error(exception.Message, exception.InnerException);
                }
                else
                {
                    _logger.Error(exception.Message);
                }

                // Don't update persistentState.LastCheckedByPath here,
                // because we want to try again as soon as possible (i.e. even less than an hour later)

                return UpdateCheckResult.Error;
            }

            // Try to use the commit hash
            if (GitHelper.IsValidCommitHash(latestRelease.TargetCommitish))
            {
                string currentCommitHash = DetectCurrentTaystJkCommitHash();
                if (!String.IsNullOrEmpty(currentCommitHash))
                {
                    if (GitHelper.IsSameCommit(currentCommitHash, latestRelease.TargetCommitish))
                    {
                        _logger.Info("TaystJK is already up to date, according to the commit hash.");
                        _logger.Debug("Current commit: " + currentCommitHash);
                        _logger.Debug("Latest release: " + latestRelease.TargetCommitish);

                        // Already up-to-date - Don't check during the next hour
                        persistentState.LastCheckedByPath[_pathToTaystJkExe] = DateTime.Now;
                        persistentState.Save();

                        return UpdateCheckResult.AlreadyUpToDate;
                    }

                    _logger.Info("New TaystJK version found, based on the commit hash.");
                    _logger.Info("Current commit: " + currentCommitHash);
                    _logger.Info("Latest release: " + latestRelease.TargetCommitish);

                    if (!ProcessReleaseAssets(latestRelease))
                    {
                        return UpdateCheckResult.Error;
                    }

                    // Don't update persistentState.LastCheckedByPath yet,
                    // because the update might fail before being fully installed,
                    // in which case, we want to try again as soon as possible (i.e. even less than an hour later)

                    return UpdateCheckResult.NeedsUpdate;
                }
            }

            // Otherwise, use the date
            DateTime localCreationTime = new FileInfo(_pathToTaystJkExe).CreationTime;

            if (localCreationTime < latestRelease.CreatedAt)
            {
                _logger.Info("New TaystJK version found, based on the file timestamps.");
                _logger.Info("Local exe file created at: " + localCreationTime.ToString("yyyy-MM-dd HH:mm:ss"));
                _logger.Info("Latest release created at: " + latestRelease.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));

                if (!ProcessReleaseAssets(latestRelease))
                {
                    return UpdateCheckResult.Error;
                }

                // Don't update persistentState.LastCheckedByPath yet,
                // because the update might fail before being fully installed,
                // in which case, we want to try again as soon as possible (i.e. even less than an hour later)

                return UpdateCheckResult.NeedsUpdate;
            }

            // Already up to date - Don't check during the next hour
            persistentState.LastCheckedByPath[_pathToTaystJkExe] = DateTime.Now;
            persistentState.Save();

            _logger.Info("TaystJK is already up to date, according to the file timestamps.");
            _logger.Debug("Local exe file created at: " + localCreationTime.ToString("yyyy-MM-dd HH:mm:ss"));
            _logger.Debug("Latest release created at: " + latestRelease.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));

            return UpdateCheckResult.AlreadyUpToDate;
        }

        /// <summary>
        /// Downloads the lastest version of TaystJK
        /// </summary>
        /// <param name="httpClient">HTTP client to use for the download request</param>
        /// <param name="progress">Progress report, from 0 to 100 (percent)</param>
        /// <returns>true if the update was downloaded successfully, false otherwise</returns>
        public async Task<bool> DownloadLatestRelease(
            HttpClient httpClient,
            CancellationToken cancellationToken,
            IProgress<int> progress)
        {
            if (String.IsNullOrEmpty(_downloadFilename))
            {
                throw new InvalidOperationException(
                    "DownloadLatestRelease() was called too soon (missing: _downloadFilename)."
                );
            }

            if (String.IsNullOrEmpty(_downloadUrl))
            {
                throw new InvalidOperationException(
                    "DownloadLatestRelease() was called too soon (missing: _downloadUrl)."
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
                _pathToDownloadedFile = pathToDownloadDirectory + Path.DirectorySeparatorChar + _downloadFilename;

                _logger.Debug("Downloading: " + _downloadUrl);

                destinationFilestream = new FileStream(
                    _pathToDownloadedFile,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None
                );

                await httpClient.DownloadAsync(_downloadUrl, destinationFilestream, progress, cancellationToken);
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
                    _logger.Error("An error occurred while downloading the latest TaystJK release.", exception);
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
                "Successfully downloaded the latest TaystJK release to: " + '"' + _pathToDownloadedFile + '"' + "."
            );

            return true;
        }

        /// <summary>
        /// Unzips the new version
        /// </summary>
        /// <returns>true if the new version was unpacked successfully, false otherwise</returns>
        public bool UnpackLatestRelease()
        {
            if (String.IsNullOrEmpty(_pathToDownloadedFile))
            {
                throw new InvalidOperationException(
                    "UnpackLatestRelease() was called too soon (missing: _pathToDownloadedFile)."
                );
            }

            _logger.Debug("Unpacking: " + '"' + _pathToDownloadedFile + '"');

            _pathToExtractedArchive = Path.GetDirectoryName(_pathToDownloadedFile) 
                + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(_pathToDownloadedFile);

            try
            {
                ZipFile.ExtractToDirectory(_pathToDownloadedFile, _pathToExtractedArchive);
            }
            catch (Exception exception)
            {
                if (exception is FileNotFoundException || exception is DirectoryNotFoundException  ||
                    exception is SecurityException     || exception is UnauthorizedAccessException ||
                    exception is PathTooLongException  || exception is IOException                 )
                {
                    _logger.Error("An error occurred while unpacking the latest TaystJK release.", exception);
                    return false;
                }

                // Unexpected exception, just rethrow it:
                throw exception;
            }

            _logger.Info(
                "Successfully unpacked the new TaystJK release to: " + '"' + _pathToExtractedArchive + '"' + "."
            );

            return true;
        }

        /// <summary>
        /// Backup files that would get overwritten by the update
        /// </summary>
        /// <returns>true if the files were backed up successfully, false otherwise</returns>
        public bool BackupCurrentFiles(CancellationToken cancellationToken)
        {
            if (String.IsNullOrEmpty(_pathToTaystJkExe))
            {
                throw new InvalidOperationException(
                    "BackupCurrentFiles() was called too soon (missing: _pathToTaystJkExe)."
                );
            }

            if (String.IsNullOrEmpty(_pathToExtractedArchive))
            {
                throw new InvalidOperationException(
                    "BackupCurrentFiles() was called too soon (missing: _pathToExtractedArchive)."
                );
            }

            try
            {
                ProgramDataHelper.PurgeOldBackups(_logger);
            }
            catch (ProgramDataNotWritableException exception)
            {
                _logger.Warning("Could not purge old backups.");
                _logger.Debug(exception.ToString());
            }

            string pathToBackupDirectory = "";

            try
            {
                pathToBackupDirectory = ProgramDataHelper.CreateNewBackupSubdirectory();
                _logger.Debug("Backing up current TaystJK files to: " + '"' + pathToBackupDirectory + '"');

                DirectoryInfo sourceDirectory = new DirectoryInfo(_pathToExtractedArchive);
                DirectoryInfo destinationDirectory = new DirectoryInfo(Path.GetDirectoryName(_pathToTaystJkExe));
                DirectoryInfo backupDirectory = new DirectoryInfo(pathToBackupDirectory);

                BackupExistingFilesRecursively(
                    sourceDirectory,
                    destinationDirectory,
                    backupDirectory,
                    cancellationToken
                );
            }
            catch (TaskCanceledException)
            {
                _logger.Debug("TaystJK backup was cancelled.");
                return false;
            }
            catch (Exception exception)
            {
                if (exception is IOException           || exception is ProgramDataNotWritableException ||
                    exception is FileNotFoundException || exception is DirectoryNotFoundException      ||
                    exception is SecurityException     || exception is UnauthorizedAccessException     ||
                    exception is PathTooLongException  )
                {
                    _logger.Error("An error occurred while backing up current TaystJK files.", exception);
                    return false;
                }

                // Unexpected exception, just rethrow it:
                throw exception;
            }

            _logger.Info("Successfully backed up current TaystJK files to: " + '"' + pathToBackupDirectory + '"' + ".");
            return true;
        }

        /// <summary>
        /// Copy files from the new version to the game's folder
        /// </summary>
        /// <returns>true if the files were copied successfully, false otherwise</returns>
        public bool CopyNewFiles(PersistentState persistentState)
        {
            if (String.IsNullOrEmpty(_pathToTaystJkExe))
            {
                throw new InvalidOperationException(
                    "CopyNewFiles() was called too soon (missing: _pathToTaystJkExe)."
                );
            }

            if (String.IsNullOrEmpty(_pathToExtractedArchive))
            {
                throw new InvalidOperationException(
                    "CopyNewFiles() was called too soon (missing: _pathToExtractedArchive)."
                );
            }

            if (_latestReleaseCreatedAt is null)
            {
                throw new InvalidOperationException(
                    "CopyNewFiles() was called too soon (missing: _latestReleaseCreatedAt)."
                );
            }

            _logger.Debug("Copying files from the new TaystJK version...");

            string pathToTaystJkDirectory = Path.GetDirectoryName(_pathToTaystJkExe);
            
            try
            {
                string pathToNewTaystJkExe = _pathToExtractedArchive
                    + Path.DirectorySeparatorChar + Path.GetFileName(_pathToTaystJkExe);

                if (!File.Exists(pathToNewTaystJkExe))
                {
                    _logger.Error(
                        "Could not find the new executable file. "
                        + "Was expecting: " + '"' + pathToNewTaystJkExe + '"'
                    );
                    return false;
                }

                DirectoryInfo sourceDirectory = new DirectoryInfo(_pathToExtractedArchive);
                DirectoryInfo destinationDirectory = new DirectoryInfo(pathToTaystJkDirectory);

                // Copy all files EXCEPT the main exe:
                CopyNewFilesRecursively(sourceDirectory, destinationDirectory);

                // Copy the main exe AFTER the others:
                _logger.Debug("Copying:  " + Path.GetFileName(pathToNewTaystJkExe));
                File.Copy(pathToNewTaystJkExe, _pathToTaystJkExe, overwrite: true);
                FixFileTimestamps(_pathToTaystJkExe);
                // Because the main exe is used to determine what version is currently installed.
            }
            catch (Exception exception)
            {
                if (exception is IOException           || exception is PathTooLongException            ||
                    exception is FileNotFoundException || exception is DirectoryNotFoundException      ||
                    exception is SecurityException     || exception is UnauthorizedAccessException     )
                {
                    _logger.Error("An error occurred while copying files from the new TaystJK release.", exception);
                    return false;
                }

                // Unexpected exception, just rethrow it:
                throw exception;
            }

            _logger.Info("Applied TaystJK update successfully.");
            DetectCurrentTaystJkCommitHash();

            persistentState.LastCheckedByPath[_pathToTaystJkExe] = DateTime.Now;
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

        public void LaunchTaystJK()
        {
            _logger.Debug("Launching TaystJK...");
            using (Process.Start(_pathToTaystJkExe, _commandLineArgsAsString)) { }
        }
        
        ////////////////////////////////////////////////////////////////////////
        // Private methods
        // ↓

        /// <summary>
        /// Parses the command line arguments, initializes _commandLineArgsAsString,
        /// and _pathToTaystJkExe if present in the arguments.
        /// </summary>
        /// <returns>true if the parsing succeeds, false otherwise</returns>
        private bool ParseCommandLineArgs()
        {
            _logger.Debug("Command line: " + Environment.CommandLine);

            List<string> commandLineArgs = Environment.GetCommandLineArgs().ToList();
            if (commandLineArgs.Count < 2)
            {
                return true;
            }

            // Ignore the current executable file name (e.g. "TaystJK_Updater.exe")
            commandLineArgs.RemoveAt(0);

            // Detect whether the first argument points to the TaystJK executable
            string pathCandidate = (string)commandLineArgs[0].Clone();
            if (!String.IsNullOrEmpty(pathCandidate) && pathCandidate.IndexOfAny(Path.GetInvalidPathChars()) < 0)
            {
                if (!pathCandidate.EndsWith(".exe"))
                {
                    pathCandidate = String.Concat(pathCandidate, ".exe");
                }

                if (_allowedTaystJkFilenames.Contains(Path.GetFileName(pathCandidate).ToLower()))
                {
                    _pathToTaystJkExe = pathCandidate;
                    commandLineArgs.RemoveAt(0);
                }
            }

            _commandLineArgsAsString = '"' + String.Join('"' + " " + '"', commandLineArgs) + '"';

            return true;
        }

        /// <summary>
        /// Finds TaystJK's executable, and initializes _pathToTaystJkExe
        /// </summary>
        /// <returns>true if the executable was found, false otherwise</returns>
        private bool FindTaystJkExe()
        {
            // Validate the "path to TaystJK" argument (if any)
            if (!String.IsNullOrEmpty(_pathToTaystJkExe))
            {
                _pathToTaystJkExe = Path.GetFullPath(_pathToTaystJkExe);
                if (File.Exists(_pathToTaystJkExe))
                {
                    _logger.Info("Found the specified TaystJK executable: " + '"' + _pathToTaystJkExe + '"');
                    return true;
                }

                _logger.Error("The specified TaystJK executable does not exist (" + _pathToTaystJkExe + ").");

                return false;
            }

            // Otherwise, look for the default executable names, in the current folder
            foreach (string filename in _allowedTaystJkFilenames)
            {
                string fullPath = Path.GetFullPath(filename);
                if (File.Exists(fullPath))
                {
                    _pathToTaystJkExe = fullPath;
                    _logger.Info("Found TaystJK executable in the current folder: " + '"' + _pathToTaystJkExe + '"');
                    return true;
                }
            }

            _logger.Error(
                "TaystJK executable was not found in the current folder (" + Directory.GetCurrentDirectory() + ")."
            );

            return false;
        }

        /// <summary>
        /// Detects the CPU architecture from the name of the TaystJK executable,
        /// and initializes the "_taystJkArchitecture" class attribute accordingly.
        /// </summary>
        /// <returns>True on success, false on failure.</returns>
        private bool DetectTaystJkArchitecture()
        {
            string basename = Path.GetFileName(_pathToTaystJkExe);
            if (!_taystJkArchitectureByFilename.ContainsKey(basename))
            {
                _logger.Error("Could not determine the architecture!");
                return false;
            }

            _taystJkArchitecture = _taystJkArchitectureByFilename[basename];
            _logger.Debug("Detected architecture: " + _taystJkArchitecture);

            return true;
        }

        /// <summary>
        /// Try to find the current commit hash from the TaystJK executable (best-effort).
        /// </summary>
        /// <returns>The commit hash if found, null otherwise</returns>
        private string DetectCurrentTaystJkCommitHash()
        {
            // Ugliest method ever
            try
            {
                long taystJkExeSize = new FileInfo(_pathToTaystJkExe).Length;
                if (taystJkExeSize > 10_000_000) // Totally arbitrary limit
                {
                    // Don't load a file larger than 10MB into memory
                    _logger.Warning(
                        '"' + _pathToTaystJkExe + '"' + " was unexpectedly large (" + taystJkExeSize + " bytes) "
                        + "-> Will not load it to determine its version."
                    );
                    return null;
                }

                string taystJkExeContent = File.ReadAllText(_pathToTaystJkExe, System.Text.Encoding.ASCII); // Ouch
                if (taystJkExeContent.Length < 20)
                {
                    _logger.Warning(
                        "Got unexpectedly small content from " + '"' + _pathToTaystJkExe + '"'
                        + " (" + taystJkExeContent.Length + " bytes) "
                        + "-> Will not determine the version from its content."
                    );
                    return null;
                }

                string prefix = "TaystJK: latest-";

                int index = 0;
                do
                {
                    index = taystJkExeContent.IndexOf(prefix, index);
                    if (index == -1)
                    {
                        _logger.Warning("Could not detect the commit hash from " + '"' + _pathToTaystJkExe + '"' + ".");
                        return null;
                    }

                    string hashCandidate = taystJkExeContent.Substring(
                        index + prefix.Length,
                        Math.Min(41, taystJkExeContent.Length - index - prefix.Length)
                    );
                    Match match = Regex.Match(hashCandidate, @"^([0-9A-Fa-f]{4,40})[ \0\n]");
                    if (match.Success)
                    {
                        string commitHash = match.Groups[1].Value;
                        _logger.Info(
                            "Detected TaystJK commit hash: " + '"' + commitHash + '"'
                        );
                        return commitHash;
                    }

                    index += prefix.Length;
                }
                while (true); // Yes.
            }
            catch (Exception exception)
            {
                _logger.Error(
                    "An error occurred when trying to detect the version from " + '"' + _pathToTaystJkExe + '"' + ".",
                    exception
                );
                // Other than that, fail silently. (This whole method is a dirty hack, anyway...)
                return null;
            }
        }

        /// <summary>
        /// Checks the latest release assets, and initializes _downloadFilename and _downloadUrl.
        /// </summary>
        /// <returns>true on success, false on failure</returns>
        private bool ProcessReleaseAssets(ReleaseInfo latestRelease)
        {
            _downloadFilename = "TaystJK-windows-" + _taystJkArchitecture + ".zip";

            if (latestRelease.DownloadUrlByAssetName.TryGetValue(_downloadFilename, out _downloadUrl))
            {
                // The download URL is typically:
                // "https://github.com/taysta/TaystJK/releases/download/latest-build/TaystJK-windows-{arch}.zip"

                return true;
            }

            _logger.Error(
                "The latest TaystJK release does not seem to have a "
                + '"' + _downloadFilename + '"' + " asset."
            );

            _logger.Debug("Assets:");
            foreach (var asset in latestRelease.DownloadUrlByAssetName)
            {
                _logger.Debug(asset.Key + " => " + asset.Value);
            }

            return false;
        }

        /// <summary>
        /// Recursively backs up files from the destination directory to the backup directory,
        /// if they exist in the source directory.
        /// </summary>
        /// <param name="sourceDirectory">Directory with the new TaystJK files</param>
        /// <param name="destinationDirectory">Directory with the current TaystJK files</param>
        /// <param name="backupDirectory">Backup directory (e.g. in ProgramData)</param>
        /// <exception cref="OperationCanceledException"></exception>
        /// <exception cref="IOException"></exception>
        /// <exception cref="DirectoryNotFoundException"></exception>
        /// <exception cref="FileNotFoundException"></exception>
        /// <exception cref="SecurityException"></exception>
        /// <exception cref="UnauthorizedAccessException"></exception>
        /// <exception cref="PathTooLongException"></exception>
        private void BackupExistingFilesRecursively(
            DirectoryInfo sourceDirectory,
            DirectoryInfo destinationDirectory,
            DirectoryInfo backupDirectory,
            CancellationToken cancellationToken)
        {
            foreach (DirectoryInfo sourceSubdir in sourceDirectory.EnumerateDirectories())
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw new TaskCanceledException("Cancelled TaystJK backup");
                }

                string relativePathToSubdir = sourceSubdir.FullName;
                if (relativePathToSubdir.StartsWith(_pathToExtractedArchive + Path.DirectorySeparatorChar))
                {
                    relativePathToSubdir = relativePathToSubdir.Substring(_pathToExtractedArchive.Length + 1);
                }

                DirectoryInfo destinationSubdir = new DirectoryInfo(
                    destinationDirectory.FullName + Path.DirectorySeparatorChar + sourceSubdir.Name
                );

                if (!destinationSubdir.Exists)
                {
                    _logger.Debug("Folder does not exist yet:  " + relativePathToSubdir);
                    continue;
                }

                _logger.Debug("Backing up existing folder: " + relativePathToSubdir);

                DirectoryInfo backupSubdir = new DirectoryInfo(
                    backupDirectory.FullName + Path.DirectorySeparatorChar + sourceSubdir.Name
                );

                backupSubdir.Create();

                // Recursive backup:
                BackupExistingFilesRecursively(sourceSubdir, destinationSubdir, backupSubdir, cancellationToken);
            }

            foreach (FileInfo sourceFile in sourceDirectory.EnumerateFiles())
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw new TaskCanceledException("Cancelled TaystJK backup");
                }

                string relativePathToFile = sourceFile.FullName;
                if (relativePathToFile.StartsWith(_pathToExtractedArchive + Path.DirectorySeparatorChar))
                {
                    relativePathToFile = relativePathToFile.Substring(_pathToExtractedArchive.Length + 1);
                }

                FileInfo destinationFile = new FileInfo(
                    destinationDirectory.FullName + Path.DirectorySeparatorChar + sourceFile.Name
                );

                if (!destinationFile.Exists)
                {
                    _logger.Debug("File does not exist yet:    " + relativePathToFile);
                    continue;
                }

                _logger.Debug("Backing up existing file:   " + relativePathToFile);

                File.Copy(
                    destinationFile.FullName,
                    backupDirectory.FullName + Path.DirectorySeparatorChar + sourceFile.Name
                );
            }
        }

        /// <summary>
        /// Copies files recursively - EXCEPT THE MAIN EXE - and overwrites if necessary.
        /// </summary>
        /// <param name="sourceDirectory">Directory with the new TaystJK files</param>
        /// <param name="destinationDirectory">Directory with the old TaystJK files</param>
        /// <exception cref="IOException"></exception>
        /// <exception cref="DirectoryNotFoundException"></exception>
        /// <exception cref="FileNotFoundException"></exception>
        /// <exception cref="SecurityException"></exception>
        /// <exception cref="UnauthorizedAccessException"></exception>
        /// <exception cref="PathTooLongException"></exception>
        private void CopyNewFilesRecursively(
            DirectoryInfo sourceDirectory,
            DirectoryInfo destinationDirectory)
        {
            foreach (DirectoryInfo sourceSubdir in sourceDirectory.EnumerateDirectories())
            {
                string relativePathToSubdir = sourceSubdir.FullName;
                if (relativePathToSubdir.StartsWith(_pathToExtractedArchive + Path.DirectorySeparatorChar))
                {
                    relativePathToSubdir = relativePathToSubdir.Substring(_pathToExtractedArchive.Length + 1);
                }

                DirectoryInfo destinationSubdir = new DirectoryInfo(
                    destinationDirectory.FullName + Path.DirectorySeparatorChar + sourceSubdir.Name
                );

                if (!destinationSubdir.Exists)
                {
                    _logger.Debug("Creating: " + relativePathToSubdir);
                    destinationSubdir.Create();
                }
                else
                {
                    _logger.Debug("Entering: " + relativePathToSubdir);
                }

                CopyNewFilesRecursively(sourceSubdir, destinationSubdir);
            }

            foreach (FileInfo sourceFile in sourceDirectory.EnumerateFiles())
            {
                string pathToDestinationFile = destinationDirectory.FullName
                    + Path.DirectorySeparatorChar + sourceFile.Name;

                string relativePathToFile = sourceFile.FullName;
                if (relativePathToFile.StartsWith(_pathToExtractedArchive + Path.DirectorySeparatorChar))
                {
                    relativePathToFile = relativePathToFile.Substring(_pathToExtractedArchive.Length + 1);
                }

                if (pathToDestinationFile == _pathToTaystJkExe)
                {
                    // Update the main exe AFTER all of the other files, as it is used to detect the current version.
                    _logger.Debug("Skipping: " + relativePathToFile + " (will handle it last)");
                    continue;
                }

                _logger.Debug("Copying:  " + relativePathToFile);

                File.Copy(sourceFile.FullName, pathToDestinationFile, overwrite: true);

                FixFileTimestamps(pathToDestinationFile);
            }
        }

        /// <summary>
        /// Set the file timestamps to the creation date/time of the latest release.
        /// </summary>
        private void FixFileTimestamps(string pathToFile)
        {
            try
            {
                File.SetCreationTime(pathToFile, (DateTime)_latestReleaseCreatedAt);
                File.SetLastWriteTime(pathToFile, (DateTime)_latestReleaseCreatedAt);
            }
            catch (Exception exception)
            {
                if (exception is FileNotFoundException || exception is UnauthorizedAccessException ||
                    exception is PathTooLongException  || exception is IOException                 )
                {
                    _logger.Warning("Could not fix the file timestamp.");
                    _logger.Debug(exception.ToString());
                }

                // Unexpected exception, just rethrow it:
                throw exception;
            }
        }
    }
}
