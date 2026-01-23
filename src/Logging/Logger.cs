using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using TaystJkUpdater.Updater;

namespace TaystJkUpdater.Logging
{
    internal class Logger
    {
        public string LogFilePath { get; }

        private readonly string _logFolderPath;
        private readonly int _processId;

        public Logger()
        {
            // Log to %LOCALAPPDATA% to protect user privacy,
            // i.e. avoid making all TaystJK launch times available to all users (on a multi-user system).
            _logFolderPath = AppEnvironment.PathToUpdaterLocalAppData;
            LogFilePath = _logFolderPath + Path.DirectorySeparatorChar
                + DateTime.Now.ToString("yyyy-MM") + "-updater-log.txt";
            using (Process currentProcess = Process.GetCurrentProcess())
            {
                _processId = currentProcess.Id;
            }
        }

        public void Debug(string message)
        {
#if DEBUG
            try
            {
                WriteLogMessage("DEBUG", message);
            }
            catch (Exception)
            {
                // Don't crash the app over a logging error...
            }
#endif
        }

        public void Info(string message)
        {
            try
            {
                WriteLogMessage("INFO", message);
            }
            catch (Exception)
            {
                // Don't crash the app over a logging error...
            }
        }

        public void Warning(string message)
        {
            try
            {
                WriteLogMessage("WARNING", message);
            }
            catch (Exception)
            {
                // Don't crash the app over a logging error (unless absolutely necessary)
            }
        }

        public void Error(string message)
        {
            try
            {
                WriteLogMessage("ERROR", message);
            }
            catch (Exception exception)
            {
                // This time, fail loudly (or the error will vanish into the void)
                throw new UnloggableErrorException(message, exception);
            }
        }

        public void Error(string message, Exception exception)
        {
            Error(message + "\n" + exception.ToString());
        }

        private void WriteLogMessage(string level, string message)
        {
            // Global Mutex (cf. https://stackoverflow.com/a/229567)
            string mutexName = "Global\\TaystJK_Updater_WriteLogMessage";

            using (Mutex mutex = new Mutex(false, mutexName))
            {
                bool hasHandle = false;
                try
                {
                    try
                    {
                        hasHandle = mutex.WaitOne();
                    }
                    catch (AbandonedMutexException)
                    {
                        // Abandoned by another process
                        hasHandle = true;
                    }

                    ////////////////////////////////////////////////////////////
                    // Enough boilerplate already, let's log the damn message

                    if (!Directory.Exists(_logFolderPath))
                    {
                        Directory.CreateDirectory(_logFolderPath);
                    }

                    string dateTimeString = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                    File.AppendAllText(
                        LogFilePath,
                        dateTimeString + " - [" + _processId + "] - " + level + " - " + message + "\n"
                    );

                    // End of the logging code
                    ////////////////////////////////////////////////////////////
                }
                finally
                {
                    if (hasHandle)
                    {
                        mutex.ReleaseMutex();
                    }
                }
            }
        }
    }
}
