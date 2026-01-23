using System;
using System.IO;
using System.Security;
using TaystJkUpdater.Updater;

namespace TaystJkUpdater.ProgramData
{
    /// <summary>
    /// File-based lock preventing other instances of the updater from modifying the same files at the same time.
    /// </summary>
    internal class UpdateLock : IDisposable
    {
        private readonly FileStream _lockFileStream;
        private bool _disposed = false;

        /// <summary>
        /// Creates a lock file, unless already locked by another process.
        /// </summary>
        /// <returns>An UpdateLock instance, or null if already locked by another process.</returns>
        /// <exception cref="ProgramDataNotWritableException"></exception>
        public static UpdateLock Acquire()
        {
            try
            {
                return new UpdateLock();
            }
            catch (IOException)
            {
                // IOException => The lock file is (probably) used by another process
                return null;
            }
        }

        /// <summary>
        /// Call Acquire() instead
        /// </summary>
        /// <exception cref="ProgramDataNotWritableException"></exception>
        private UpdateLock()
        {
            string pathToLockFolder = AppEnvironment.PathToUpdaterProgramData;
            
            try
            {
                if (!Directory.Exists(pathToLockFolder))
                {
                    Directory.CreateDirectory(pathToLockFolder);
                }

                string pathToLockFile = pathToLockFolder + Path.DirectorySeparatorChar + "update.lock";

                _lockFileStream = new FileStream(
                    pathToLockFile,
                    FileMode.OpenOrCreate, // CreateNew would fail if the file already existed
                    FileAccess.Write,
                    FileShare.None,
                    4096,
                    FileOptions.DeleteOnClose
                );
                File.SetAttributes(pathToLockFile, FileAttributes.Hidden);
            }
            catch (Exception exception)
            {
                if (exception is SecurityException || exception is UnauthorizedAccessException ||
                    exception is IOException       )
                {
                    // File/folder not writable
                    throw new ProgramDataNotWritableException(exception);
                }

                // Unexpected exception, just rethrow it:
                throw exception;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                _lockFileStream.Dispose();
            }

            _disposed = true;
        }

        ~UpdateLock()
        {
            Dispose(false);
        }
    }
}
