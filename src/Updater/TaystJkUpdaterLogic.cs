using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
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

        private readonly Logger logger;
        private string commandLineArgsAsString = "";
        private string pathToTaystJkExe = "";

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
            return ParseCommandLineArgs() && FindTaystJkExe();
        }

        /// <summary>
        /// Check whether an update is available for TaystJK
        /// </summary>
        /// <param name="githubApiClient">GitHub API Client (dependency to inject)</param>
        /// <param name="persistentState">Updater state (for reading and writing)</param>
        public async Task<UpdateCheckResult> CheckForUpdates(
            GithubApiClient githubApiClient,
            CancellationToken cancellationToken,
            PersistentState persistentState)
        {
            if (String.IsNullOrEmpty(pathToTaystJkExe))
            {
                throw new InvalidOperationException(
                    "CheckForUpdates() was called too soon (missing: pathToTaystJkExe)"
                );
            }

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

            try
            {
                latestRelease = await githubApiClient.GetLatestTaystJkReleaseInfo(cancellationToken);
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
                string currentCommitHash = GetCurrentTaystJkCommitHash();
                if (!String.IsNullOrEmpty(currentCommitHash))
                {
                    if (match.Value.ToLower().StartsWith(currentCommitHash.ToLower()))
                    {
                        logger.Info("TaystJK is already up to date, according to the commit hash.");
                        logger.Info("Current commit: " + currentCommitHash);
                        logger.Info("Latest release: " + match.Value);

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
            // TODO: Which one ? Compare LastWriteTime to published_at ? Or CreationTime to created_at ?
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
            logger.Info("Local exe file created at: " + localCreationTime.ToString("yyyy-MM-dd HH:mm:ss"));
            logger.Info("Latest release created at: " + latestRelease.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));

            return UpdateCheckResult.AlreadyUpToDate;
        }

        /// <summary>
        /// Download the lastest version of TaystJK
        /// </summary>
        /// <param name="progress">Progress report, from 0 to 100 (percent)</param>
        /// <returns>true if the update was downloaded successfully, false otherwise</returns>
        public async Task<bool> DownloadLatestRelease(CancellationToken cancellationToken, IProgress<int> progress)
        {
            // TODO: actually attempt to download the update
            await Task.Delay(100);

            switch (1)
            {
                case 0:
                    // TODO: Be more specific
                    logger.Error("An error occurred while downloading the latest TaystJK release.");
                    return false;
                default:
                    for (int i = 0; i < 100; i++)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            logger.Info("Cancelled TaystJK download at " + i + "%");
                            return false;
                        }
                        progress?.Report(i);
                        await Task.Delay(50);
                    }
                    logger.Info("Downloaded the latest TaystJK release successfully."); // TODO: Be more specific
                    return true;
            }
        }

        /// <summary>
        /// Unzip the new version
        /// </summary>
        /// <returns>true if the new version was unpacked successfully, false otherwise</returns>
        public async Task<bool> UnpackLatestRelease()
        {
            // TODO: actually attempt to unzip the new version
            await Task.Delay(1000);

            switch (1)
            {
                case 0:
                    logger.Error("An error occurred while unpacking the latest TaystJK release."); // TODO: Be more specific
                    return false;
                default:
                    logger.Info("Unpacked the new TaystJK release successfully."); // TODO: Be more specific
                    return true;
            }
        }

        /// <summary>
        /// Backup files that would get overwritten by the update
        /// </summary>
        /// <returns>true if the files were backed up successfully, false otherwise</returns>
        public async Task<bool> BackupCurrentFiles()
        {
            // TODO: actually attempt to backup files that would get overwritten by the update
            await Task.Delay(1000);

            switch (1)
            {
                case 0:
                    logger.Error("An error occurred while backing up current TaystJK files."); // TODO: Be more specific
                    return false;
                default:
                    logger.Info("Backed up current TaystJK files successfully."); // TODO: Be more specific
                    return true;
            }
        }

        /// <summary>
        /// Copy files from the new version to the game's folder
        /// </summary>
        /// <returns>true if the files were copied successfully, false otherwise</returns>
        public async Task<bool> CopyNewFiles(PersistentState persistentState)
        {
            // TODO: actually attempt to copy files from the new version
            await Task.Delay(1000);

            switch (1)
            {
                case 0:
                    // TODO: Be more specific
                    logger.Error("An error occurred while copying files from the new TaystJK release.");
                    return false;
                default:
                    logger.Info("Applied TaystJK update successfully."); // TODO: Be more specific

                    persistentState.LastCheckedByPath[pathToTaystJkExe] = DateTime.Now;
                    persistentState.Save();

                    return true;
            }
        }

        public void LaunchTaystJK()
        {
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
        /// Try to find the current commit hash from the TaystJK executable (best-effort).
        /// </summary>
        /// <returns>The commit hash if found, null otherwise</returns>
        private string GetCurrentTaystJkCommitHash()
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
                            + " in " + '"' + pathToTaystJkExe + '"'
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
    }
}
