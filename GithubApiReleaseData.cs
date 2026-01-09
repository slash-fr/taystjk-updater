using System;

namespace Updater
{
    internal class GithubApiReleaseData
    {
        public DateTime CreatedAt { get; }
        public string TargetCommitish { get; }

        public GithubApiReleaseData(DateTime createdAt, string targetCommitish)
        {
            this.CreatedAt = createdAt;
            this.TargetCommitish = targetCommitish;
        }
    }
}
