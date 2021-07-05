using System;

namespace Argus
{
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
