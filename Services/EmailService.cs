using System.Net;
using System.Net.Mail;

namespace growy_server.Services
{
    public class EmailService(IConfiguration configuration) : IEmailService
    {
        public async Task SendTagRequestAsync(string symbol, string tagType, string reason, string requesterEmail, CancellationToken cancellationToken = default)
        {
            var smtp = configuration.GetSection("Smtp");
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

            using var client = CreateClient(smtp);
            var message = new MailMessage(from, adminEmail, subject, body);
            await client.SendMailAsync(message, cancellationToken);
        }

        public async Task SendContactMessageAsync(string senderEmail, string title, string message, CancellationToken cancellationToken = default)
        {
            var smtp = configuration.GetSection("Smtp");
            var from = smtp["From"]!;
            // Contact messages go to a dedicated inbox; fall back to AdminEmail if unset.
            var recipient = smtp["ContactEmail"] ?? smtp["AdminEmail"]!;

            var subject = $"[Growy] Message from {senderEmail}: {title}";
            var body = $"""
                A user has sent a message.

                From:     {senderEmail}
                Title:    {title}

                Message:
                {message}
                """;

            using var client = CreateClient(smtp);
            var mail = new MailMessage(from, recipient, subject, body);
            // Replies go straight back to the user who wrote in.
            mail.ReplyToList.Add(new MailAddress(senderEmail));
            await client.SendMailAsync(mail, cancellationToken);
        }

        private static SmtpClient CreateClient(IConfiguration smtp)
        {
            var host = smtp["Host"]!;
            var port = int.Parse(smtp["Port"]!);
            var username = smtp["Username"]!;
            // Gmail app passwords are shown with spaces for readability; copy-paste can also
            // introduce non-breaking spaces (U+00A0). Strip all whitespace so the credential
            // matches what the SMTP server expects.
            var password = new string(smtp["Password"]!.Where(c => !char.IsWhiteSpace(c)).ToArray());

            return new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(username, password),
                EnableSsl = true,
            };
        }
    }
}
