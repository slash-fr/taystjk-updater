using System;

namespace TaystJkUpdater.GitHub
{
    internal class TooManyRequestsException : Exception
    {
        public TooManyRequestsException(Uri requestUri)
            : base("Got \"429 Too Many Requests\" from: " + requestUri)
        {
        }
    }
}
