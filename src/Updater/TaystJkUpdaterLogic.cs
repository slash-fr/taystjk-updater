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
        private readonly HashSet<string> allowedTaystJkFilenames = new HashSet<string>()
        {
            "taystjk.x86_64.exe",
            "taystjk.x86.exe",
            "taystjk.arm64.exe", // Just in case...
        };

        private readonly Dictionary<string, string> taystJkArchitectureByFilename = new Dictionary<string, string>()
        {
            { "taystjk.x86_64.exe", "x86_64" },
            { "taystjk.x86.exe",    "x86"    },
            { "taystjk.arm64.exe",  "arm64"  },
        };

        private readonly Logger logger;
        private string commandLineArgsAsString = "";
        private string pathToTaystJkExe = "";
        private string taystJkArchitecture = "";
        private DateTime? latestReleaseCreatedAt = null;
        private string pathToDownloadedFile = "";
        private string pathToExtractedArchive = "";

        public TaystJkUpdaterLogic(Logger logger)
        {
            this.logger = logger;
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
            if (String.IsNullOrEmpty(pathToTaystJkExe))
            {
                throw new InvalidOperationException(
                    "CheckForUpdates() was called too soon (missing: pathToTaystJkExe)."
                );
            }

            logger.Debug("Checking for TaystJK updates...");

            // Don't check more than once per hour
            if (persistentState.LastCheckedByPath.TryGetValue(pathToTaystJkExe, out DateTime lastChecked)
                && DateTime.Now < lastChecked.AddHours(1))
            {
                logger.Info("Already checked less than an hour ago.");
                return UpdateCheckResult.AlreadyCheckedRecently;
            }

            if (!(persistentState.GithubThrottledUntil is null)
                && persistentState.GithubThrottledUntil >= DateTime.Now)
            {
                logger.Warning("GitHub API is throttled due to a recent 429 error.");
                return UpdateCheckResult.AlreadyCheckedRecently;
            }

            ReleaseInfo latestRelease;
            Uri requestUri = new Uri("http://localhost/latest.php"); // TODO
            //Uri requestUri = new Uri("https://api.github.com/repos/taysta/TaystJK/releases/tags/latest");
            logger.Debug("GitHub API request: " + requestUri);

            try
            {
                latestRelease = await GithubApiHelper.GetReleaseInfo(httpClient, requestUri, cancellationToken);
                latestReleaseCreatedAt = latestRelease.CreatedAt;
            }
            catch (TooManyRequestsException exception)
            {
                logger.Error(exception.Message);

                persistentState.LastCheckedByPath[pathToTaystJkExe] = DateTime.Now;
                persistentState.GithubThrottledUntil = DateTime.Now.AddHours(1);
                persistentState.Save();

                return UpdateCheckResult.Error;
            }
            catch (GithubApiException exception)
            {
                if (exception.InnerException != null)
                {
                    logger.Error(exception.Message, exception.InnerException);
                }
                else
                {
                    logger.Error(exception.Message);
                }

                // Don't update persistentState.LastCheckedByPath here,
                // because we want to try again as soon as possible (i.e. even less than an hour later)

                return UpdateCheckResult.Error;
            }

            // Try to use the commit hash
            Match match = Regex.Match(latestRelease.TargetCommitish, "^[0-9A-Fa-f]{4,40}$");
            if (match.Success)
            {
                string currentCommitHash = DetectCurrentTaystJkCommitHash();
                if (!String.IsNullOrEmpty(currentCommitHash))
                {
                    if (match.Value.ToLower().StartsWith(currentCommitHash.ToLower()))
                    {
                        logger.Info("TaystJK is already up to date, according to the commit hash.");
                        logger.Debug("Current commit: " + currentCommitHash);
                        logger.Debug("Latest release: " + match.Value);

                        // Already up-to-date - Don't check during the next hour
                        persistentState.LastCheckedByPath[pathToTaystJkExe] = DateTime.Now;
                        persistentState.Save();

                        return UpdateCheckResult.AlreadyUpToDate;
                    }

                    logger.Info("New TaystJK version found, based on the commit hash.");
                    logger.Info("Current commit: " + currentCommitHash);
                    logger.Info("Latest release: " + match.Value);

                    // Don't update persistentState.LastCheckedByPath yet,
                    // because the update might fail before being fully installed,
                    // in which case, we want to try again as soon as possible (i.e. even less than an hour later)

                    return UpdateCheckResult.NeedsUpdate;
                }
            }

            // Otherwise, use the date
            DateTime localCreationTime = new FileInfo(pathToTaystJkExe).CreationTime;

            if (localCreationTime < latestRelease.CreatedAt)
            {
                logger.Info("New TaystJK version found, based on the file timestamps.");
                logger.Info("Local exe file created at: " + localCreationTime.ToString("yyyy-MM-dd HH:mm:ss"));
                logger.Info("Latest release created at: " + latestRelease.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));

                // Don't update persistentState.LastCheckedByPath yet,
                // because the update might fail before being fully installed,
                // in which case, we want to try again as soon as possible (i.e. even less than an hour later)

                return UpdateCheckResult.NeedsUpdate;
            }

            // Already up to date - Don't check during the next hour
            persistentState.LastCheckedByPath[pathToTaystJkExe] = DateTime.Now;
            persistentState.Save();

            logger.Info("TaystJK is already up to date, according to the file timestamps.");
            logger.Debug("Local exe file created at: " + localCreationTime.ToString("yyyy-MM-dd HH:mm:ss"));
            logger.Debug("Latest release created at: " + latestRelease.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));

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
            if (String.IsNullOrEmpty(taystJkArchitecture))
            {
                throw new InvalidOperationException(
                    "DownloadLatestRelease() was called too soon (missing: taystJkArchitecture)."
                );
            }

            try
            {
                ProgramDataHelper.PurgeAllDownloads();
            }
            catch (ProgramDataNotWritableException exception)
            {
                logger.Warning("Could not purge old downloads.");
                logger.Debug(exception.ToString());
            }

            FileStream destinationFilestream = null;

            try
            {
                string pathToDownloadDirectory = ProgramDataHelper.CreateNewDownloadSubdirectory();
                string downloadFilename = "TaystJK-windows-" + taystJkArchitecture + ".zip";
                pathToDownloadedFile = pathToDownloadDirectory + Path.DirectorySeparatorChar + downloadFilename;

                string downloadUri = "http://localhost/" + downloadFilename;
                //string downloadUri = "https://github.com/taysta/TaystJK/releases/download/latest/" + downloadFilename;

                logger.Debug("Downloading: " + downloadUri);

                destinationFilestream = new FileStream(
                    pathToDownloadedFile,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None
                );

                await httpClient.DownloadAsync(downloadUri, destinationFilestream, progress, cancellationToken);
            }
            catch (Exception exception)
            {
                if (exception is HttpRequestException  || exception is ProgramDataNotWritableException ||
                    exception is FileNotFoundException || exception is DirectoryNotFoundException      ||
                    exception is SecurityException     || exception is UnauthorizedAccessException     ||
                    exception is PathTooLongException  || exception is IOException                     )
                {
                    logger.Error("An error occurred while downloading the latest TaystJK release.", exception);
                    return false;
                }

                // Unexpected exception, just rethrow it:
                throw exception;
            }
            finally
            {
                destinationFilestream?.Dispose();
            }

            logger.Info(
                "Successfully downloaded the latest TaystJK release to: " + '"' + pathToDownloadedFile + '"' + "."
            );

            return true;
        }

        /// <summary>
        /// Unzips the new version
        /// </summary>
        /// <returns>true if the new version was unpacked successfully, false otherwise</returns>
        public bool UnpackLatestRelease()
        {
            if (String.IsNullOrEmpty(pathToDownloadedFile))
            {
                throw new InvalidOperationException(
                    "UnpackLatestRelease() was called too soon (missing: pathToDownloadedFile)."
                );
            }

            logger.Debug("Unpacking: " + '"' + pathToDownloadedFile + '"');

            pathToExtractedArchive = Path.GetDirectoryName(pathToDownloadedFile) 
                + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(pathToDownloadedFile);

            try
            {
                ZipFile.ExtractToDirectory(pathToDownloadedFile, pathToExtractedArchive);
            }
            catch (Exception exception)
            {
                if (exception is FileNotFoundException || exception is DirectoryNotFoundException  ||
                    exception is SecurityException     || exception is UnauthorizedAccessException ||
                    exception is PathTooLongException  || exception is IOException                 )
                {
                    logger.Error("An error occurred while unpacking the latest TaystJK release.", exception);
                    return false;
                }

                // Unexpected exception, just rethrow it:
                throw exception;
            }

            logger.Info(
                "Successfully unpacked the new TaystJK release to: " + '"' + pathToExtractedArchive + '"' + "."
            );

            return true;
        }

        /// <summary>
        /// Backup files that would get overwritten by the update
        /// </summary>
        /// <returns>true if the files were backed up successfully, false otherwise</returns>
        public bool BackupCurrentFiles(CancellationToken cancellationToken)
        {
            if (String.IsNullOrEmpty(pathToTaystJkExe))
            {
                throw new InvalidOperationException(
                    "BackupCurrentFiles() was called too soon (missing: pathToTaystJkExe)."
                );
            }

            if (String.IsNullOrEmpty(pathToExtractedArchive))
            {
                throw new InvalidOperationException(
                    "BackupCurrentFiles() was called too soon (missing: pathToExtractedArchive)."
                );
            }

            try
            {
                ProgramDataHelper.PurgeOldBackups(logger);
            }
            catch (ProgramDataNotWritableException exception)
            {
                logger.Warning("Could not purge old backups.");
                logger.Debug(exception.ToString());
            }

            string pathToBackupDirectory = "";

            try
            {
                pathToBackupDirectory = ProgramDataHelper.CreateNewBackupSubdirectory();
                logger.Debug("Backing up current TaystJK files to: " + '"' + pathToBackupDirectory + '"');

                DirectoryInfo sourceDirectory = new DirectoryInfo(pathToExtractedArchive);
                DirectoryInfo destinationDirectory = new DirectoryInfo(Path.GetDirectoryName(pathToTaystJkExe));
                DirectoryInfo backupDirectory = new DirectoryInfo(pathToBackupDirectory);

                BackupExistingFilesRecursively(
                    sourceDirectory,
                    destinationDirectory,
                    backupDirectory,
                    cancellationToken
                );
            }
            catch (Exception exception)
            {
                if (exception is IOException           || exception is ProgramDataNotWritableException ||
                    exception is FileNotFoundException || exception is DirectoryNotFoundException      ||
                    exception is SecurityException     || exception is UnauthorizedAccessException     ||
                    exception is PathTooLongException  )
                {
                    logger.Error("An error occurred while backing up current TaystJK files.", exception);
                    return false;
                }

                // Unexpected exception, just rethrow it:
                throw exception;
            }

            logger.Info("Successfully backed up current TaystJK files to: " + '"' + pathToBackupDirectory + '"' + ".");
            return true;
        }

        /// <summary>
        /// Copy files from the new version to the game's folder
        /// </summary>
        /// <returns>true if the files were copied successfully, false otherwise</returns>
        public bool CopyNewFiles(PersistentState persistentState)
        {
            if (String.IsNullOrEmpty(pathToTaystJkExe))
            {
                throw new InvalidOperationException(
                    "CopyNewFiles() was called too soon (missing: pathToTaystJkExe)."
                );
            }

            if (String.IsNullOrEmpty(pathToExtractedArchive))
            {
                throw new InvalidOperationException(
                    "CopyNewFiles() was called too soon (missing: pathToExtractedArchive)."
                );
            }

            if (latestReleaseCreatedAt is null)
            {
                throw new InvalidOperationException(
                    "CopyNewFiles() was called too soon (missing: latestReleaseCreatedAt)."
                );
            }

            logger.Debug("Copying files from the new TaystJK version...");

            string pathToTaystJkDirectory = Path.GetDirectoryName(pathToTaystJkExe);
            
            try
            {
                string pathToNewTaystJkExe = pathToExtractedArchive
                    + Path.DirectorySeparatorChar + Path.GetFileName(pathToTaystJkExe);

                if (!File.Exists(pathToNewTaystJkExe))
                {
                    logger.Error(
                        "Could not find the new executable file. "
                        + "Was expecting: " + '"' + pathToNewTaystJkExe + '"'
                    );
                    return false;
                }

                DirectoryInfo sourceDirectory = new DirectoryInfo(pathToExtractedArchive);
                DirectoryInfo destinationDirectory = new DirectoryInfo(pathToTaystJkDirectory);

                // Copy all files EXCEPT the main exe:
                CopyNewFilesRecursively(sourceDirectory, destinationDirectory);

                // Copy the main exe AFTER the others:
                logger.Debug("Copying:  " + Path.GetFileName(pathToNewTaystJkExe));
                File.Copy(pathToNewTaystJkExe, pathToTaystJkExe, overwrite: true);
                FixFileTimestamps(pathToTaystJkExe);
                // Because the main exe is used to determine what version is currently installed.
            }
            catch (Exception exception)
            {
                if (exception is IOException           || exception is PathTooLongException            ||
                    exception is FileNotFoundException || exception is DirectoryNotFoundException      ||
                    exception is SecurityException     || exception is UnauthorizedAccessException     )
                {
                    logger.Error("An error occurred while copying files from the new TaystJK release.", exception);
                    return false;
                }

                // Unexpected exception, just rethrow it:
                throw exception;
            }

            logger.Info("Applied TaystJK update successfully.");
            DetectCurrentTaystJkCommitHash();

            persistentState.LastCheckedByPath[pathToTaystJkExe] = DateTime.Now;
            persistentState.Save();

            try
            {
                ProgramDataHelper.PurgeAllDownloads();
            }
            catch (ProgramDataNotWritableException exception)
            {
                logger.Warning("Could not purge downloads.");
                logger.Debug(exception.ToString());
            }

            return true;
        }

        public void LaunchTaystJK()
        {
            logger.Debug("Launching TaystJK...");
            using (Process.Start(pathToTaystJkExe, commandLineArgsAsString)) { }
        }
        
        ////////////////////////////////////////////////////////////////////////
        // Private methods
        // ↓

        /// <summary>
        /// Parses the command line arguments, initializes commandLineArgsAsString,
        /// and pathToTaystJkExe if present in the arguments.
        /// </summary>
        /// <returns>true if the parsing succeeds, false otherwise</returns>
        private bool ParseCommandLineArgs()
        {
            List<string> commandLineArgs = Environment.GetCommandLineArgs().ToList();
            if (commandLineArgs.Count < 2)
            {
                return true;
            }

            // Ignore the current executable file name (e.g. "Updater.exe")
            commandLineArgs.RemoveAt(0);

            // Detect whether the first argument points to the TaystJK executable
            string pathCandidate = (string)commandLineArgs[0].Clone();
            if (!String.IsNullOrEmpty(pathCandidate) && pathCandidate.IndexOfAny(Path.GetInvalidPathChars()) < 0)
            {
                if (!pathCandidate.EndsWith(".exe"))
                {
                    pathCandidate = String.Concat(pathCandidate, ".exe");
                }

                if (allowedTaystJkFilenames.Contains(Path.GetFileName(pathCandidate).ToLower()))
                {
                    pathToTaystJkExe = pathCandidate;
                    commandLineArgs.RemoveAt(0);
                }
            }

            commandLineArgsAsString = String.Join(" ", commandLineArgs);
            // TODO: escape arguments?

            return true;
        }

        /// <summary>
        /// Find TaystJK's executable, and initialize pathToTaystJkExe
        /// </summary>
        /// <returns>true if the executable was found, false otherwise</returns>
        private bool FindTaystJkExe()
        {
            // Validate the "path to TaystJK" argument (if any)
            if (!String.IsNullOrEmpty(pathToTaystJkExe))
            {
                pathToTaystJkExe = Path.GetFullPath(pathToTaystJkExe);
                if (File.Exists(pathToTaystJkExe))
                {
                    logger.Info("Found the specified TaystJK executable: " + '"' + pathToTaystJkExe + '"');
                    return true;
                }

                logger.Error("The specified TaystJK executable does not exist (" + pathToTaystJkExe + ").");

                return false;
            }

            // Otherwise, look for the default executable names, in the current folder
            foreach (string filename in allowedTaystJkFilenames)
            {
                string fullPath = Path.GetFullPath(filename);
                if (File.Exists(fullPath))
                {
                    pathToTaystJkExe = fullPath;
                    logger.Info("Found TaystJK executable in the current folder: " + '"' + pathToTaystJkExe + '"');
                    return true;
                }
            }

            logger.Error(
                "TaystJK executable was not found in the current folder (" + Directory.GetCurrentDirectory() + ")."
            );

            return false;
        }

        /// <summary>
        /// Detects the CPU architecture from the name of the TaystJK executable,
        /// and initializes the "taystJkArchitecture" class attribute accordingly.
        /// </summary>
        /// <returns>True on success, false on failure.</returns>
        private bool DetectTaystJkArchitecture()
        {
            string basename = Path.GetFileName(pathToTaystJkExe);
            if (!taystJkArchitectureByFilename.ContainsKey(basename))
            {
                logger.Error("Could not determine the architecture!");
                return false;
            }

            taystJkArchitecture = taystJkArchitectureByFilename[basename];
            logger.Debug("Detected architecture: " + taystJkArchitecture);

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
                long taystJkExeSize = new FileInfo(pathToTaystJkExe).Length;
                if (taystJkExeSize > 10e6) // Totally arbitrary limit
                {
                    // Don't load a file larger than 10MB into memory
                    logger.Warning(
                        '"' + pathToTaystJkExe + '"' + " was unexpectedly large (" + taystJkExeSize + " bytes) "
                        + "-> Will not load it to determine its version."
                    );
                    return null;
                }

                string taystJkExeContent = File.ReadAllText(pathToTaystJkExe, System.Text.Encoding.ASCII); // Ouch
                if (taystJkExeContent.Length < 20)
                {
                    logger.Warning(
                        "Got unexpectedly small content from " + '"' + pathToTaystJkExe + '"'
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
                        logger.Warning("Could not detect the commit hash from " + '"' + pathToTaystJkExe + '"' + ".");
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
                        logger.Info(
                            "Detected commit hash " + '"' + commitHash + '"'
                        );
                        return commitHash;
                    }

                    index += prefix.Length;
                }
                while (true); // Yes.
            }
            catch (Exception exception)
            {
                logger.Error(
                    "An error occurred when trying to detect the version from " + '"' + pathToTaystJkExe + '"' + ".",
                    exception
                );
                // Other than that, fail silently. (This whole method is a dirty hack, anyway...)
                return null;
            }
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
                if (relativePathToSubdir.StartsWith(pathToExtractedArchive + Path.DirectorySeparatorChar))
                {
                    relativePathToSubdir = relativePathToSubdir.Substring(pathToExtractedArchive.Length + 1);
                }

                DirectoryInfo destinationSubdir = new DirectoryInfo(
                    destinationDirectory.FullName + Path.DirectorySeparatorChar + sourceSubdir.Name
                );

                if (!destinationSubdir.Exists)
                {
                    logger.Debug("Folder does not exist yet:  " + relativePathToSubdir);
                    continue;
                }

                logger.Debug("Backing up existing folder: " + relativePathToSubdir);

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
                if (relativePathToFile.StartsWith(pathToExtractedArchive + Path.DirectorySeparatorChar))
                {
                    relativePathToFile = relativePathToFile.Substring(pathToExtractedArchive.Length + 1);
                }

                FileInfo destinationFile = new FileInfo(
                    destinationDirectory.FullName + Path.DirectorySeparatorChar + sourceFile.Name
                );

                if (!destinationFile.Exists)
                {
                    logger.Debug("File does not exist yet:    " + relativePathToFile);
                    continue;
                }

                logger.Debug("Backing up existing file:   " + relativePathToFile);

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
                if (relativePathToSubdir.StartsWith(pathToExtractedArchive + Path.DirectorySeparatorChar))
                {
                    relativePathToSubdir = relativePathToSubdir.Substring(pathToExtractedArchive.Length + 1);
                }

                DirectoryInfo destinationSubdir = new DirectoryInfo(
                    destinationDirectory.FullName + Path.DirectorySeparatorChar + sourceSubdir.Name
                );

                if (!destinationSubdir.Exists)
                {
                    logger.Debug("Creating: " + relativePathToSubdir);
                    destinationSubdir.Create();
                }
                else
                {
                    logger.Debug("Entering: " + relativePathToSubdir);
                }

                CopyNewFilesRecursively(sourceSubdir, destinationSubdir);
            }

            foreach (FileInfo sourceFile in sourceDirectory.EnumerateFiles())
            {
                string pathToDestinationFile = destinationDirectory.FullName
                    + Path.DirectorySeparatorChar + sourceFile.Name;

                string relativePathToFile = sourceFile.FullName;
                if (relativePathToFile.StartsWith(pathToExtractedArchive + Path.DirectorySeparatorChar))
                {
                    relativePathToFile = relativePathToFile.Substring(pathToExtractedArchive.Length + 1);
                }

                if (pathToDestinationFile == pathToTaystJkExe)
                {
                    // Update the main exe AFTER all of the other files, as it is used to detect the current version.
                    logger.Debug("Skipping: " + relativePathToFile + " (will handle it last)");
                    continue;
                }

                logger.Debug("Copying:  " + relativePathToFile);

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
                File.SetCreationTime(pathToFile, (DateTime)latestReleaseCreatedAt);
                File.SetLastWriteTime(pathToFile, (DateTime)latestReleaseCreatedAt);
            }
            catch (Exception exception)
            {
                if (exception is FileNotFoundException || exception is UnauthorizedAccessException ||
                    exception is PathTooLongException  || exception is IOException                 )
                {
                    logger.Warning("Could not fix the file timestamp.");
                    logger.Debug(exception.ToString());
                }

                // Unexpected exception, just rethrow it:
                throw exception;
            }
        }
    }
}
