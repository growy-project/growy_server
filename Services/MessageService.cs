using growy_server.Data;
using growy_server.Models.DB;

namespace growy_server.Services
{
    public class MessageService(GrowyDbContext db, IEmailService emailService) : IMessageService
    {
        public async Task SendAsync(int userId, string title, string message, CancellationToken cancellationToken = default)
        {
            var user = await db.Users.FindAsync([userId], cancellationToken)
                ?? throw new UserNotFoundException(userId);

            // Persist the message first so the record survives even if SMTP delivery fails.
            db.EmailMessages.Add(new EmailMessageEntity
            {
                UserId = user.Id,
                SenderEmail = user.Email,
                Title = title,
                Message = message,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync(cancellationToken);

            await emailService.SendContactMessageAsync(user.Email, title, message, cancellationToken);
        }
    }
}
