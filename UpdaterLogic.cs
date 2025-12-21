using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

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

        /// <exception cref="FileNotFoundException">If the TaystJK executable was not found</exception>
        public UpdaterLogic()
        {
            ParseCommandLineArgs();
            FindTaystJkExe();
        }

        public void LaunchTaystJK()
        {
            Process.Start(pathToTaystJkExe, commandLineArgsAsString);
        }

        private void ParseCommandLineArgs()
        {
            List<string> commandLineArgs = Environment.GetCommandLineArgs().ToList();
            if (commandLineArgs.Count < 2)
            {
                return;
            }

            // Ignore the current executable file name (e.g. "Updater.exe")
            commandLineArgs.RemoveAt(0);

            // Detect whether the first argument points to the TaystJK executable
            string pathCandidate = (string)commandLineArgs[0].Clone();
            if (!string.IsNullOrEmpty(pathCandidate) && pathCandidate.IndexOfAny(Path.GetInvalidPathChars()) < 0)
            {
                if (!pathCandidate.EndsWith(".exe"))
                {
                    pathCandidate = string.Concat(pathCandidate, ".exe");
                }

                if (allowedTaystJkFilenames.Contains(Path.GetFileName(pathCandidate).ToLower()))
                {
                    pathToTaystJkExe = pathCandidate;
                    commandLineArgs.RemoveAt(0);
                }
            }

            commandLineArgsAsString = String.Join(" ", commandLineArgs);
            // TODO: escape arguments?
        }

        /// <summary>
        /// Find TaystJK's executable, and initialize pathToTaystJkExe
        /// </summary>
        /// <exception cref="FileNotFoundException">If the executable was not found</exception>
        private void FindTaystJkExe()
        {
            // Validate the "path to TaystJK" argument (if any)
            if (!string.IsNullOrEmpty(pathToTaystJkExe))
            {
                if (File.Exists(pathToTaystJkExe))
                {
                    return;
                }

                throw new FileNotFoundException("The specified TaystJK executable was not found.");
            }

            // Otherwise, look for the default executable names, in the current folder
            foreach (string filename in allowedTaystJkFilenames)
            {
                if (File.Exists(filename))
                {
                    pathToTaystJkExe = filename;
                    return;
                }
            }

            throw new FileNotFoundException("TaystJK executable not found.");
        }
    }
}
