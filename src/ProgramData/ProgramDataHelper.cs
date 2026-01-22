using System;
using System.IO;
using TaystJkUpdater.Logging;
using TaystJkUpdater.Updater;

namespace TaystJkUpdater.ProgramData
{
    internal static class ProgramDataHelper
    {
        /// <summary>
        /// Deletes ALL downloads
        /// </summary>
        /// <exception cref="ProgramDataNotWritableException"></exception>
        public static void PurgeAllDownloads()
        {
            string pathToDownloadDirectory = AppEnvironment.PathToUpdaterProgramData
                + Path.DirectorySeparatorChar + "Downloads";

            if (!Directory.Exists(pathToDownloadDirectory))
            {
                return;
            }

            try
            {
                // Nuke it from orbit. It's the only way to be sure.
                Directory.Delete(pathToDownloadDirectory, recursive: true);
            }
            catch (Exception exception)
            {
                if (exception is IOException          || exception is UnauthorizedAccessException ||
                    exception is PathTooLongException || exception is DirectoryNotFoundException  )
                {
                    throw new ProgramDataNotWritableException(exception);
                }

                // Unexpected exception, just rethrow it:
                throw exception;
            }
        }

        /// <summary>
        /// Deletes backups older than 30 days
        /// </summary>
        /// <param name="logger">Logger dependency</param>
        public static void PurgeOldBackups(Logger logger)
        {
            DirectoryInfo backupDirectory = new DirectoryInfo(
                AppEnvironment.PathToUpdaterProgramData + Path.DirectorySeparatorChar + "Backups"
            );

            if (!backupDirectory.Exists)
            {
                return;
            }

            foreach (DirectoryInfo subdirectory in backupDirectory.EnumerateDirectories())
            {
                if (subdirectory.CreationTime < DateTime.Now.Subtract(TimeSpan.FromDays(30)))
                {
                    // Protip: If you need to test this on short notice,
                    //         open up a PowerShell window and experiment:
                    //
                    // (Get-Item 2026-03-14_13-37-42).CreationTime = "2026-02-12T13:37:42"

                    logger.Debug("Deleting " + '"' + subdirectory.FullName + '"');

                    try
                    {
                        subdirectory.Delete(recursive: true);
                    }
                    catch (Exception exception)
                    {
                        if (exception is IOException          || exception is UnauthorizedAccessException ||
                            exception is PathTooLongException || exception is DirectoryNotFoundException  )
                        {
                            logger.Warning("Could not delete " + '"' + subdirectory.FullName + '"' + ".");
                            logger.Debug(exception.ToString());
                            continue; // Try to delete the next directory
                        }

                        // Unexpected exception, just rethrow it:
                        throw exception;
                    }
                }
            }
        }

        /// <summary>
        /// Creates a unique subdirectory in ProgramData, to store a new download.
        /// </summary>
        /// <returns>
        ///     The path to the created subdirectory
        ///     (e.g. "C:\ProgramData\TaystJK Updater\Downloads\2026-03-14_13-37-42")
        /// </returns>
        /// <exception cref="ProgramDataNotWritableException"></exception>
        public static string CreateNewDownloadSubdirectory()
        {
            return CreateTimestampedSubdirectory("Downloads");
        }

        /// <summary>
        /// Creates a unique subdirectory in ProgramData, to store a new backup.
        /// </summary>
        /// <returns>
        ///     The path to the created subdirectory
        ///     (e.g. "C:\ProgramData\TaystJK Updater\Backups\2026-03-14_13-37-42")
        /// </returns>
        /// <exception cref="ProgramDataNotWritableException"></exception>
        public static string CreateNewBackupSubdirectory()
        {
            return CreateTimestampedSubdirectory("Backups");
        }

        /// <summary>
        /// Creates a timestamped subdirectory in ProgramData, inside the specified intermediate directory.
        /// </summary>
        /// <param name="intermediateDirectoryName">e.g. "Downloads" or "Backups"</param>
        /// <returns>
        ///     The path to the created subdirectory
        ///     (e.g. "C:\ProgramData\TaystJK Updater\Downloads\2026-03-14_13-37-42")
        /// </returns>
        /// <exception cref="ProgramDataNotWritableException"></exception>
        private static string CreateTimestampedSubdirectory(string intermediateDirectoryName)
        {
            string pathToNewSubdirectory = AppEnvironment.PathToUpdaterProgramData
                + Path.DirectorySeparatorChar + intermediateDirectoryName
                + Path.DirectorySeparatorChar + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

            try
            {
                Directory.CreateDirectory(pathToNewSubdirectory);
            }
            catch (Exception exception)
            {
                if (exception is IOException           || exception is UnauthorizedAccessException ||
                    exception is PathTooLongException  || exception is DirectoryNotFoundException  )
                {
                    throw new ProgramDataNotWritableException(exception);
                }

                // Unexpected exception, just rethrow it:
                throw exception;
            }

            return pathToNewSubdirectory;
        }
    }
}
