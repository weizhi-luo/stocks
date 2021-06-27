using Microsoft.AspNetCore.Mvc;

namespace Argus.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DataPublishController : ControllerBase
    {
        private readonly DataPublishQueue _dataPublishQueue;

        public DataPublishController(DataPublishQueue dataPublishQueue)
        {
            _dataPublishQueue = dataPublishQueue;
        }
        
        /// <summary>
        /// Get latest errors when publishing data
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetLatestErrors")]
        public ActionResult GetLatestDataPublishErrors()
        {
            return Ok(_dataPublishQueue.GetLatestDataPublishErrors());
        }
    }
}
