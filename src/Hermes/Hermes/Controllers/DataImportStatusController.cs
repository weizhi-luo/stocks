using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Hermes.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DataImportStatusController : ControllerBase
    {
        private readonly DataImportStatusQueue _dataImportStatusQueue;
        private readonly StringEnumConverter _stringEnumConverter;

        public DataImportStatusController(DataImportStatusQueue dataImportStatusQueue)
        {
            _dataImportStatusQueue = dataImportStatusQueue;
            _stringEnumConverter = new StringEnumConverter();
        }

        /// <summary>
        /// Get latest statuses for importing data
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetLatest")]
        public ActionResult GetLatestDataImportStatuses()
        {
            return Ok(JsonConvert.SerializeObject(_dataImportStatusQueue.GetLatestDataImportStatuses(), _stringEnumConverter));
        }

        /// <summary>
        /// Get latest success for importing data
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetLatest/Success")]
        public ActionResult GetLatestDataImportStatusesSuccess()
        {
            return Ok(JsonConvert.SerializeObject(_dataImportStatusQueue.GetLatestDataImportStatusesSuccess(), _stringEnumConverter));
        }

        /// <summary>
        /// Get latest errors for importing data
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetLatest/Error")]
        public ActionResult GetLatestDataImportStatusesError()
        {
            return Ok(JsonConvert.SerializeObject(_dataImportStatusQueue.GetLatestDataImportStatusesError(), _stringEnumConverter));
        }

        /// <summary>
        /// Get latest warnings for importing data
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetLatest/Warning")]
        public ActionResult GetLatestDataImportStatusesWarning()
        {
            return Ok(JsonConvert.SerializeObject(_dataImportStatusQueue.GetLatestDataImportStatusesWarning(), _stringEnumConverter));
        }
    }
}
