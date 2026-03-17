using Microsoft.AspNetCore.Mvc;
using Synerixis.Application.DTOs;
using Synerixis.Application.Interfaces;
using System.Threading.Tasks;

namespace Synerixis.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MarketingController : ControllerBase
    {
        private readonly IMarketingCopyService _marketingCopyService;

        public MarketingController(IMarketingCopyService marketingCopyService)
        {
            _marketingCopyService = marketingCopyService;
        }

        /// <summary>
        /// Generates marketing copy for a product.
        /// </summary>
        /// <param name="request">The request containing product details.</param>
        /// <returns>The generated marketing copy.</returns>
        [HttpPost("generate-copy")]
        public async Task<IActionResult> GenerateMarketingCopy([FromBody] GenerateCopyDto request)
        {
            if (request == null)
            {
                return BadRequest("Request body cannot be null.");
            }

            try
            {
                var result = await _marketingCopyService.GenerateCopyAsync(request);
                return Ok(result);
            }
            catch (System.Exception ex)
            {
                // In a real app, log this exception
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }
    }
}
