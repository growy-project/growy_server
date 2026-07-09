using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using growy_server.Models;
using growy_server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace growy_server.Controllers
{
    [ApiController]
    [Route("messages")]
    [Authorize]
    public class MessageController(IMessageService messageService) : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> Send([FromBody] SendMessageRequest request, CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            try
            {
                await messageService.SendAsync(userId, request.Title, request.Message, cancellationToken);
                return NoContent();
            }
            catch (UserNotFoundException)
            {
                return Unauthorized();
            }
        }

        private bool TryGetUserId(out int userId)
        {
            var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(sub, out userId);
        }
    }
}
