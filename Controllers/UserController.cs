using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using growy_server.Models;
using growy_server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace growy_server.Controllers
{
    [ApiController]
    [Route("my-list")]
    [Authorize]
    public class UserController(IWatchlistService watchlistService, IStatisticsJobService jobService) : ControllerBase
    {
        [HttpPost("symbol")]
        public async Task<IActionResult> AddSymbol([FromBody] AddWatchlistSymbolRequest request, CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            try
            {
                await watchlistService.AddAsync(userId, request.Symbol, request.Exchange, cancellationToken);
                return StatusCode(StatusCodes.Status201Created);
            }
            catch (WatchlistLimitReachedException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (WatchlistDuplicateException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        [HttpDelete("symbol")]
        public async Task<IActionResult> RemoveSymbol([FromQuery] string symbol, [FromQuery] string exchange, CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            if (string.IsNullOrWhiteSpace(symbol) || string.IsNullOrWhiteSpace(exchange))
                return BadRequest(new { message = "symbol and exchange are required" });

            var removed = await watchlistService.RemoveAsync(userId, symbol, exchange, cancellationToken);
            return removed ? NoContent() : NotFound();
        }

        [HttpGet]
        public IActionResult Get([FromQuery] long startUnixDate, [FromQuery] long endUnixDate)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            var jobId = jobService.StartWatchlistJob(new StartWatchlistJobParameters
            {
                UserId = userId,
                StartUnixDate = startUnixDate,
                EndUnixDate = endUnixDate
            });
            return Ok(new { JobId = jobId });
        }

        private bool TryGetUserId(out int userId)
        {
            var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(sub, out userId);
        }
    }
}
