namespace growy_server.Services
{
    public interface IEmailService
    {
        Task SendTagRequestAsync(string symbol, string tagType, string reason, string requesterEmail, CancellationToken cancellationToken = default);
    }
}
