using System;
using System.Collections.Generic;

namespace TaystJkUpdater.GitHub
{
    internal class ReleaseInfo
    {
        public DateTime CreatedAt { get; }
        public string TargetCommitish { get; }
        public IDictionary<string, string> DownloadUrlByAssetName { get; }

        public ReleaseInfo(
            DateTime createdAt,
            string targetCommitish,
            IDictionary<string, string> downloadUrlByAssetName)
        {
            CreatedAt = createdAt;
            TargetCommitish = targetCommitish;
            DownloadUrlByAssetName = downloadUrlByAssetName;
        }
    }
}
