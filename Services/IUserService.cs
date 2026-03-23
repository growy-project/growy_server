using growy_server.Models;

namespace growy_server.Services
{
    public interface IUserService
    {
        Task<(UserResult user, string token)> GoogleLoginAsync(string idToken, CancellationToken cancellationToken = default);
    }
}
