using System;
using System.IO;
using System.Security;

namespace Updater
{
    /// <summary>
    /// File-based lock preventing other instances of the updater from modifying the same files at the same time.
    /// </summary>
    internal class UpdateLock : IDisposable
    {
        private readonly FileStream lockFileStream;
        private bool disposed = false;

        /// <summary>
        /// Creates a lock file, unless already locked by another process.
        /// </summary>
        /// <returns>An UpdateLock instance, or null if already locked by another process.</returns>
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
        private UpdateLock()
        {
            string pathToLockFolder = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)
                + Path.DirectorySeparatorChar + "TaystJK Updater";
            
            try
            {
                if (!Directory.Exists(pathToLockFolder))
                {
                    Directory.CreateDirectory(pathToLockFolder);
                }

                string pathToLockFile = pathToLockFolder + Path.DirectorySeparatorChar + "update.lock";

                lockFileStream = new FileStream(
                    pathToLockFile,
                    FileMode.OpenOrCreate, // CreateNew would fail if the file already existed
                    FileAccess.Write,
                    FileShare.None,
                    4096,
                    FileOptions.DeleteOnClose
                );
                File.SetAttributes(pathToLockFile, FileAttributes.Hidden);
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
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                lockFileStream.Dispose();
            }

            disposed = true;
        }

        ~UpdateLock()
        {
            Dispose(false);
        }
    }
}
