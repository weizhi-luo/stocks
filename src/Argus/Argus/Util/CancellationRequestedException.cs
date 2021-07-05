using System;

namespace Argus
{
    public class CancellationRequestedException : Exception
    {
        public CancellationRequestedException() { }

        public CancellationRequestedException(string message) : base(message) { }

        public CancellationRequestedException(string message, Exception innerException) : base(message, innerException) { } 
    }
}
