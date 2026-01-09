using System;

namespace Updater
{
    internal class GithubApiException : Exception
    {
        public GithubApiException(Uri requestUri, string message)
            : base(GetMessage(requestUri, message))
        {
        }

        public GithubApiException(Uri requestUri, string message, Exception innerException)
            : base(GetMessage(requestUri, message), innerException)
        {
        }

        private static string GetMessage(Uri requestUri, string message)
        {
            return "GitHub API Error - " + message + " - " + requestUri;
        }
    }
}
