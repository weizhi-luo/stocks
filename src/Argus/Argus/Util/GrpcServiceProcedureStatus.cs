using System;

namespace Argus
{
    public class GrpcServiceProcedureStatus
    {
        public GrpcServiceProcedure ServiceProcedure { get; set; }
        public Status Status { get; set; }
        public string Detail { get; set; }
        public DateTime UtcTimestamp { get; set; }
    }

    public enum Status
    {
        Success,
        Warning,
        Error,
        Information
    }
}
