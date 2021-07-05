using System;

namespace Argus
{
    public class DataProcessFailException : Exception
    {
        public DataProcessFailException() { }

        public DataProcessFailException(string message) : base(message) { }

        public DataProcessFailException(string message, Exception innerException) : base(message, innerException) { }
    }
}
