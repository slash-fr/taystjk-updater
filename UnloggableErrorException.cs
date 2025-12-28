using System;

namespace Updater
{
    internal class UnloggableErrorException : Exception
    {
        public UnloggableErrorException(string message, Exception innerException) : base(message, innerException) { }
    }
}
