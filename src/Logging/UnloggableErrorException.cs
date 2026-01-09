using System;

namespace TaystJkUpdater.Logging
{
    internal class UnloggableErrorException : Exception
    {
        public UnloggableErrorException(string message, Exception innerException) : base(message, innerException) { }
    }
}
