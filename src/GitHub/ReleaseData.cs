using System;

namespace TaystJkUpdater.GitHub
{
    internal class ReleaseData
    {
        public DateTime CreatedAt { get; }
        public string TargetCommitish { get; }

        public ReleaseData(DateTime createdAt, string targetCommitish)
        {
            this.CreatedAt = createdAt;
            this.TargetCommitish = targetCommitish;
        }
    }
}
