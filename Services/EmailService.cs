using System.Net;
using System.Net.Mail;

namespace growy_server.Services
{
    public class EmailService(IConfiguration configuration) : IEmailService
    {
        public async Task SendTagRequestAsync(string symbol, string tagType, string reason, string requesterEmail)
        {
            var smtp = configuration.GetSection("Smtp");
            var host = smtp["Host"]!;
            var port = int.Parse(smtp["Port"]!);
            var username = smtp["Username"]!;
            var password = smtp["Password"]!;
            var from = smtp["From"]!;
            var adminEmail = smtp["AdminEmail"]!;

            var subject = $"[Growy] Tag request: {symbol} as {tagType}";
            var body = $"""
                A user has requested to tag a symbol.

                Symbol:     {symbol}
                Tag type:   {tagType}
                Requester:  {requesterEmail}

                Reason:
                {reason}
                """;

            using var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(username, password),
                EnableSsl = true,
            };

            var message = new MailMessage(from, adminEmail, subject, body);
            await client.SendMailAsync(message);
        }
    }
}
