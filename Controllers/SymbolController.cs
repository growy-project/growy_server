using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using growy_server.Models;
using growy_server.Services;

namespace growy_server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class SymbolController(ISymbolService symbolService, IEmailService emailService) : ControllerBase
    {
        [HttpPost("request-tag")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> RequestTag([FromBody] TagRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                await emailService.SendTagRequestAsync(request.Symbol, request.TagType, request.Reason, request.RequesterEmail, cancellationToken);
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        [HttpPut("{symbol}/top-growth")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> SetTopGrowth(string symbol, [FromQuery] bool value, CancellationToken cancellationToken)
        {
            try
            {
                await symbolService.SetSymbolAsTopGrowth(symbol, value, cancellationToken);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
        }

        [HttpPut("{symbol}/toxic")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> SetToxic(string symbol, [FromQuery] bool value, CancellationToken cancellationToken)
        {
            try
            {
                await symbolService.SetSymbolAsToxic(symbol, value, cancellationToken);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
        }

        [HttpGet("date-range")]
        public async Task<IActionResult> GetDateRange([FromQuery] string exchange, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(exchange))
                return BadRequest(new { Message = "Exchange parameter is required." });

            try
            {
                var result = await symbolService.GetDateRange(exchange, cancellationToken);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = ex.Message });
            }
        }
    }
}
