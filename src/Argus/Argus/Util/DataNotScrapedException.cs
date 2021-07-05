using System;

namespace Argus
{
    public class DataNotScrapedException : Exception
    {
        public DataNotScrapedException() { }

        public DataNotScrapedException(string message) : base(message) { }

        public DataNotScrapedException(string message, Exception innerException) : base(message, innerException) { }
    }
}
