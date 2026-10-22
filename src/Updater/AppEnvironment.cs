using System;
using System.IO;

namespace TaystJkUpdater.Updater
{
    internal static class AppEnvironment
    {
        /// <summary>
        /// Path to the TaystJK-updater-specific "program data" folder. Not guaranteed to exist.
        /// </summary>
        public static string PathToUpdaterProgramData { get; }

        /// <summary>
        /// Path to the TaystJK-updater-specific "local app data" folder. Not guaranteed to exist.
        /// </summary>
        public static string PathToUpdaterLocalAppData { get; }

        public const string LatestUpdaterReleaseApiUrl
            = "https://api.github.com/repos/slash-fr/taystjk-updater/releases/latest";

        public const string LatestTaystJkReleaseApiUrl
            = "https://api.github.com/repos/taysta/TaystJK/releases/tags/latest-build";

        static AppEnvironment()
        {
            // "%ProgramData%\TaystJK Updater"
            PathToUpdaterProgramData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)
                + Path.DirectorySeparatorChar + "TaystJK Updater";

            // "%LocalAppData%\TaystJK Updater"
            PathToUpdaterLocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
                + Path.DirectorySeparatorChar + "TaystJK Updater";
        }
    }
}
