using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Argus.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GrpcServiceProcedureStatusController : Controller
    {
        private readonly GrpcServiceProcedureStatusQueue _serviceProcedureStatusQueue;
        private readonly StringEnumConverter _stringEnumConverter;

        public GrpcServiceProcedureStatusController(GrpcServiceProcedureStatusQueue serviceProcedureStatusQueue)
        {
            _serviceProcedureStatusQueue = serviceProcedureStatusQueue;
            _stringEnumConverter = new StringEnumConverter();
        }

        /// <summary>
        /// Get latest statuses for executing service procedures
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetLatest")]
        public ActionResult GetLatestServiceProcedureStatuses()
        {
            return Ok(JsonConvert.SerializeObject(_serviceProcedureStatusQueue.GetLatestServiceProcedureStatuses(), _stringEnumConverter));
        }

        /// <summary>
        /// Get latest success for executing service procedures
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetLatest/Success")]
        public ActionResult GetLatestServiceProcedureStatusesSuccess()
        {
            return Ok(JsonConvert.SerializeObject(_serviceProcedureStatusQueue.GetLatestServiceProcedureStatusesSuccess(), _stringEnumConverter));
        }

        /// <summary>
        /// Get latest errors for executing service procedures
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetLatest/Error")]
        public ActionResult GetLatestServiceProcedureStatusesError()
        {
            return Ok(JsonConvert.SerializeObject(_serviceProcedureStatusQueue.GetLatestServiceProcedureStatusesError(), _stringEnumConverter));
        }

        /// <summary>
        /// Get latest warnings for executing service procedures
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetLatest/Warning")]
        public ActionResult GetLatestServiceProcedureStatusesWarning()
        {
            return Ok(JsonConvert.SerializeObject(_serviceProcedureStatusQueue.GetLatestServiceProcedureStatusesWarning(), _stringEnumConverter));
        }
    }
}
