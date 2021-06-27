using Newtonsoft.Json;
using RabbitMQ.Client;
using System;

namespace Hermes
{
    public class DataToImport
    {
        [JsonProperty("ServiceProcedure")]
        public GrpcServiceProcedure DataScrapeServiceProcedure { get; set; }
        public string Data { get; set; }
    }

    public class DataImportConfiguration
    {
        public GrpcServiceProcedure DataScrapeServiceProcedure { get; set; }
        public string DataImportStoredProcedure { get; set; }
        public string DataImportStoredProcedureParameterName { get; set; }
    }

    public class DataImportStatus
    {
        public GrpcServiceProcedure DataScrapeServiceProcedure { get; set; }
        public Status Status { get; set; }
        public string Detail { get; set; }
        public DateTime UtcTimestamp { get; set; }
    }

    public class UnprocessableMessage
    {
        public string ConsumerTag { get; set; }
        public ulong DeliveryTag { get; set; }
        public bool Redelivered { get; set; }
        public string Exchange { get; set; }
        public string RoutingKey { get; set; }
        public IBasicProperties BasicProperties { get; set; }
        public string Detail { get; set; }
        public DateTime UtcTimestamp { get; set; }
    }

    public enum Status
    {
        Success,
        Warning,
        Error
    }

    public class GrpcServiceProcedure
    {
        public string Service { get; set; }
        public string Procedure { get; set; }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;

            if (ReferenceEquals(this, obj))
                return true;

            if (GetType() != obj.GetType())
                return false;

            var converted = obj as GrpcServiceProcedure;

            return string.Equals(Service, converted.Service, StringComparison.OrdinalIgnoreCase) && 
                string.Equals(Procedure, converted.Procedure, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                const int HashingBase = (int)2166136261;
                const int HashingMultiplier = 16777619;

                var hash = HashingBase;
                hash = (hash * HashingMultiplier) ^ (!ReferenceEquals(null, Service) ? Service.GetHashCode() : 0);
                hash = (hash * HashingMultiplier) ^ (!ReferenceEquals(null, Procedure) ? Procedure.GetHashCode() : 0);

                return hash;
            }
        }
    }
}
