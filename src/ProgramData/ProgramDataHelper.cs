using System;
using System.IO;
using System.Security;
using TaystJkUpdater.Updater;

namespace TaystJkUpdater.ProgramData
{
    internal static class ProgramDataHelper
    {
        /// <summary>
        /// Creates a unique subdirectory in ProgramData, to store a new download.
        /// </summary>
        /// <returns>The path to the created subdirectory</returns>
        /// <exception cref="ProgramDataNotWritableException"></exception>
        public static string CreateNewDownloadSubdirectory()
        {
            string pathToNewDownloadSubDirectory = AppEnvironment.PathToUpdaterProgramData
                + Path.DirectorySeparatorChar + "Downloads"
                + Path.DirectorySeparatorChar + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

            try
            {
                Directory.CreateDirectory(pathToNewDownloadSubDirectory);
            }
            catch (UnauthorizedAccessException exception)
            {
                // File/folder not writable
                throw new ProgramDataNotWritableException(exception);
            }
            catch (SecurityException exception)
            {
                // File/folder not writable
                throw new ProgramDataNotWritableException(exception);
            }

            return pathToNewDownloadSubDirectory;
        }

        // TODO: Purge old download subdirs?
    }
}
