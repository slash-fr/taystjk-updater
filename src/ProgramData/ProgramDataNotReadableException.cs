using System;

namespace TaystJkUpdater.ProgramData
{
    internal class ProgramDataNotReadableException : Exception
    {
        public ProgramDataNotReadableException(Exception innerException)
            : base("Could not read from %ProgramData%.", innerException)
        {
        }
    }
}
