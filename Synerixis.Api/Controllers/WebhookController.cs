using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Synerixis.Api.Controllers
{
    [ApiController]
    [Route("api/webhooks")]
    public class WebhookController : ControllerBase
    {
        private readonly ILogger<WebhookController> _logger;
        // We will inject a service to handle the message processing logic later.
        // private readonly IWebhookHandlerService _handlerService;

        public WebhookController(ILogger<WebhookController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Endpoint to receive messages from Taobao (Qianniu).
        /// </summary>
        [HttpPost("taobao")]
        public async Task<IActionResult> HandleTaobaoWebhook()
        {
            _logger.LogInformation("Received a webhook call for Taobao.");

            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            var body = await reader.ReadToEndAsync();

            _logger.LogInformation("Taobao Webhook Body: {Body}", body);

            // Here, we would add logic to:
            // 1. Verify the signature from Taobao to ensure it's a legitimate request.
            // 2. Parse the message body.
            // 3. Pass the message to a dedicated Application Service (e.g., IConversationService).
            // 4. The service then processes the message, involves the AI, and sends a reply.

            // For now, we just acknowledge receipt. Taobao requires a specific success response.
            // This might need to be adjusted based on their API documentation.
            return Ok(new { success = true });
        }

        /// <summary>
        /// Endpoint to receive messages from Douyin (Doudian).
        /// </summary>
        [HttpPost("douyin")]
        public async Task<IActionResult> HandleDouyinWebhook()
        {
            _logger.LogInformation("Received a webhook call for Douyin.");

            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            var body = await reader.ReadToEndAsync();

            _logger.LogInformation("Douyin Webhook Body: {Body}", body);

            // Similar logic to Taobao:
            // 1. Verify signature.
            // 2. Parse message.
            // 3. Pass to a service.
            
            // Douyin requires a specific JSON response for success validation.
            return Ok(new { code = 0, msg = "success" });
        }
    }
}
