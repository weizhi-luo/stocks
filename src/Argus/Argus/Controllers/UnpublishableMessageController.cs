﻿using Microsoft.AspNetCore.Mvc;

namespace Argus.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UnpublishableMessageController : ControllerBase
    {
        private readonly UnpublishableMessageQueue _unpublishableMessageQueue;

        public UnpublishableMessageController(UnpublishableMessageQueue unpublishableMessageQueue)
        {
            _unpublishableMessageQueue = unpublishableMessageQueue;
        }

        /// <summary>
        /// Get latest unpublishable messages
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetLatestUnpublisable")]
        public ActionResult GetLatestUnpublisableMessages()
        {
            return Ok(_unpublishableMessageQueue.GetLatestUnpublishableMessages());
        }

        /// <summary>
        /// Delete an unpublishable message by key
        /// </summary>
        /// <param name="key">The key value is generated by SHA256 hashing based on the values of message exchange|message replycode|message replytext|message routingkey</param>
        /// <returns></returns>
        [HttpDelete("Delete/{key}")]
        public ActionResult DeleteLatestUnpublisableMessage(string key)
        {
            if (_unpublishableMessageQueue.DeleteLatestUnpublishableMessage(key))
            {
                return Ok();
            }
            else
            {
                return NotFound();
            }
        }
    }
}
