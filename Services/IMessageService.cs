namespace growy_server.Services
{
    public interface IMessageService
    {
        Task SendAsync(int userId, string title, string message, CancellationToken cancellationToken = default);
    }

    public class UserNotFoundException : Exception
    {
        public UserNotFoundException(int userId)
            : base($"User {userId} was not found") { }
    }
}
