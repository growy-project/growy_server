using Microsoft.AspNetCore.Mvc;
using growy_server.Models;
using growy_server.Services;

namespace growy_server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AuthController(IUserService userService) : ControllerBase
    {
        [HttpPost("google-login")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var (user, token) = await userService.GoogleLoginAsync(request.IdToken, cancellationToken);
                return Ok(new { user, token });
            }
            catch (Exception ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }
    }
}
