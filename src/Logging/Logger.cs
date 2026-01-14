using System;
using System.Diagnostics;
using System.IO;
using TaystJkUpdater.Updater;

namespace TaystJkUpdater.Logging
{
    internal class Logger
    {
        public string LogFilePath { get; }

        private readonly string logFolderPath;
        private readonly int processId;

        public Logger()
        {
            // Log to %LOCALAPPDATA% to protect user privacy,
            // i.e. avoid making all TaystJK launch times available to all users (on a multi-user system).
            logFolderPath = AppEnvironment.PathToUpdaterLocalAppData;
            LogFilePath = logFolderPath + Path.DirectorySeparatorChar + "Updater.log";
            using (Process currentProcess = Process.GetCurrentProcess())
            {
                processId = currentProcess.Id;
            }
        }

        // TODO: add a DEBUG level?

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
            if (!Directory.Exists(logFolderPath))
            {
                Directory.CreateDirectory(logFolderPath);
            }

            string dateTimeString = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            File.AppendAllText(
                LogFilePath,
                dateTimeString + " - [" + processId + "] - " +  level + " - " + message + "\n"
            );
        }
    }
}
