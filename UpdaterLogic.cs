using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Updater
{
    internal class UpdaterLogic
    {
        private readonly HashSet<string> allowedTaystJkFilenames = new HashSet<string>()
        {
            "taystjk.x86_64.exe",
            "taystjk.x86.exe",
        };

        private string commandLineArgsAsString = "";
        private string pathToTaystJkExe = "";

        public UpdaterLogic()
        {
        }

        public bool InitializeUpdater()
        {
            return ParseCommandLineArgs() && FindTaystJkExe();
        }

        /// <summary>
        /// Check whether an update is available for TaystJK
        /// </summary>
        public async Task<UpdateCheckResult> CheckTaystJkUpdate()
        {
            if (String.IsNullOrEmpty(pathToTaystJkExe))
            {
                throw new InvalidOperationException("CheckTaystJkUpdate() MUST be called AFTER InitializeUpdater()");
            }

            // TODO: actually check for updates
            await Task.Delay(1000);

            switch (3)
            {
                case 0:
                    // TODO: log the error
                    return UpdateCheckResult.Error;
                case 1:
                    return UpdateCheckResult.AlreadyUpToDate;
                case 2:
                    return UpdateCheckResult.AlreadyCheckedRecently;
                default:
                    return UpdateCheckResult.NeedsUpdate;
            }
        }

        /// <summary>
        /// Download the lastest version of TaystJK
        /// </summary>
        /// <param name="progress">Progress report, from 0 to 100 (percent)</param>
        /// <returns>true if the update was downloaded successfully, false otherwise</returns>
        public async Task<bool> DownloadTaystJkUpdate(CancellationToken cancellationToken, IProgress<int> progress)
        {
            // TODO: actually attempt to download the update
            await Task.Delay(100);

            switch (1)
            {
                case 0:
                    // TODO: log the error
                    return false;
                default:
                    for (int i = 0; i < 100; i++)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            return false;
                        }
                        progress?.Report(i);
                        await Task.Delay(50);
                    }
                    return true;
            }
        }

        /// <summary>
        /// Unzip the new version
        /// </summary>
        /// <returns>true if the new version was unpacked successfully, false otherwise</returns>
        public async Task<bool> UnpackTaystJkUpdate()
        {
            // TODO: actually attempt to unzip the new version
            await Task.Delay(1000);

            switch (1)
            {
                case 0:
                    // TODO: log the error
                    return false;
                default:
                    return true;
            }
        }

        /// <summary>
        /// Backup files that would get overwritten by the update
        /// </summary>
        /// <returns>true if the files were backed up successfully, false otherwise</returns>
        public async Task<bool> BackupTaystJkFiles()
        {
            // TODO: actually attempt to backup files that would get overwritten by the update
            await Task.Delay(1000);

            switch (1)
            {
                case 0:
                    // TODO: log the error
                    return false;
                default:
                    return true;
            }
        }

        /// <summary>
        /// Copy files from the new version to the game's folder
        /// </summary>
        /// <returns>true if the files were copied successfully, false otherwise</returns>
        public async Task<bool> CopyTaystJkFiles()
        {
            // TODO: actually attempt to copy files from the new version
            await Task.Delay(1000);

            switch (1)
            {
                case 0:
                    // TODO: log the error
                    return false;
                default:
                    return true;
            }
        }

        public void LaunchTaystJK()
        {
            Process.Start(pathToTaystJkExe, commandLineArgsAsString);
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
                if (File.Exists(pathToTaystJkExe))
                {
                    return true;
                }

                // TODO: log the error
                // "The specified TaystJK executable was not found."
                return false;
            }

            // Otherwise, look for the default executable names, in the current folder
            foreach (string filename in allowedTaystJkFilenames)
            {
                if (File.Exists(filename))
                {
                    pathToTaystJkExe = filename;
                    return true;
                }
            }

            // TODO: log the error
            // "TaystJK executable not found."
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
                if (new FileInfo(pathToTaystJkExe).Length > 10e7)
                {
                    // Don't load a file larger than 10MB into memory
                    // TODO: Add a log message
                    return null;
                }

                string taystJkExeContent = File.ReadAllText(pathToTaystJkExe, System.Text.Encoding.ASCII); // Ouch
                if (taystJkExeContent.Length < 20)
                {
                    // TODO: Add a log message
                    return null;
                }

                string prefix = "TaystJK: latest-";

                int index = 0;
                do
                {
                    index = taystJkExeContent.IndexOf(prefix, index);
                    if (index == -1)
                    {
                        // TODO: Add a log message?
                        return null;
                    }

                    string hashCandidate = taystJkExeContent.Substring(
                        index + prefix.Length,
                        Math.Min(41, taystJkExeContent.Length - index - prefix.Length)
                    );
                    Match match = Regex.Match(hashCandidate, @"^([0-9A-Fa-f]{4,40})[ \0\n]");
                    if (match.Success)
                    {
                        return match.Captures[0].Value;
                    }

                    index += prefix.Length;
                }
                while (true); // Yes.
            }
            catch (Exception e)
            {
                // TODO: log the error
                // Other than that, fail silently. (This whole method is a dirty hack, anyway...)
                return null;
            }
        }
    }
}
