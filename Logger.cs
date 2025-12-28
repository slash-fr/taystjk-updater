using System;
using System.IO;

namespace Updater
{
    internal class Logger
    {
        public string LogFilePath { get; }

        private readonly string logFolderPath;

        public Logger()
        {
            logFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
                + Path.DirectorySeparatorChar + "TaystJK Updater";
            LogFilePath = logFolderPath + Path.DirectorySeparatorChar + "Updater.log";
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

        private void WriteLogMessage(string level, string message)
        {
            if (!Directory.Exists(logFolderPath))
            {
                Directory.CreateDirectory(logFolderPath);
            }

            File.AppendAllText(
                LogFilePath,
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " - " + level + " - " + message+ "\n"
            );
        }
    }
}
