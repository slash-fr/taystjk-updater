using System;

namespace TaystJkUpdater.ProgramData
{
    internal class ProgramDataNotWritableException : Exception
    {
        public ProgramDataNotWritableException(Exception innerException)
            : base("Could not write to %ProgramData%.", innerException)
        {
        }
    }
}
