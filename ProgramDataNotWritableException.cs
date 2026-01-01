using System;

namespace Updater
{
    internal class ProgramDataNotWritableException : Exception
    {
        public ProgramDataNotWritableException(Exception innerException)
            : base("Could not write to %ProgramData%.", innerException)
        {
        }
    }
}
