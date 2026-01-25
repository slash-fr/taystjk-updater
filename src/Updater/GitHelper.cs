using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace TaystJkUpdater.Updater
{
    internal static class GitHelper
    {
        public static bool IsValidCommitHash(string commitHash)
        {
            return Regex.Match(commitHash, "^[0-9A-Fa-f]{4,40}$").Success;
        }

        public static bool IsSameCommit(string commitHash1, string commitHash2)
        {
            if (commitHash1.Length < commitHash2.Length)
            {
                return commitHash2.StartsWith(commitHash1, StringComparison.InvariantCultureIgnoreCase);
            }

            return commitHash1.StartsWith(commitHash2, StringComparison.InvariantCultureIgnoreCase);
        }

        public static string GetGitCommitHashFromAssembly()
        {
            // cf. https://stackoverflow.com/a/15145121
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("TaystJkUpdater.GitVersion"))
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd().Trim();
                }
            }
        }
    }
}
