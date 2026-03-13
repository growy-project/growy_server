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
        public async Task<IActionResult> RequestTag([FromBody] TagRequestModel request)
        {
            try
            {
                await emailService.SendTagRequestAsync(request.Symbol, request.TagType, request.Reason, request.RequesterEmail);
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        [HttpPut("{symbol}/top-growth")]
        public async Task<IActionResult> SetTopGrowth(string symbol, [FromQuery] bool value)
        {
            try
            {
                await symbolService.SetSymbolAsTopGrowth(symbol, value);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
        }

        [HttpPut("{symbol}/toxic")]
        public async Task<IActionResult> SetToxic(string symbol, [FromQuery] bool value)
        {
            try
            {
                await symbolService.SetSymbolAsToxic(symbol, value);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
        }
    }
}
