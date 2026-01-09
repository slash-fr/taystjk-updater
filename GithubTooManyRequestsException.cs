using System;

namespace Updater
{
    internal class GithubTooManyRequestsException : Exception
    {
        public GithubTooManyRequestsException(Uri requestUri)
            : base("Got \"429 Too Many Requests\" from: " + requestUri)
        {
        }
    }
}
