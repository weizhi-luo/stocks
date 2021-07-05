using System;

namespace Argus
{
    public class DataScrapeFailException : Exception
    {
        public DataScrapeFailException() { }

        public DataScrapeFailException(string message) : base(message) { }

        public DataScrapeFailException(string message, Exception innerException) : base(message, innerException) { }
    }
}
