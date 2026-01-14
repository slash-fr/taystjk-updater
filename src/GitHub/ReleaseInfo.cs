using System;

namespace TaystJkUpdater.GitHub
{
    internal class ReleaseInfo
    {
        public DateTime CreatedAt { get; }
        public string TargetCommitish { get; }

        public ReleaseInfo(DateTime createdAt, string targetCommitish)
        {
            this.CreatedAt = createdAt;
            this.TargetCommitish = targetCommitish;
        }
    }
}
