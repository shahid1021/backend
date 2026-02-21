using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace StudentAPI.Services
{
    public class EmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendOtpEmailAsync(string toEmail, string otpCode, string userName)
        {
            var emailSettings = _config.GetSection("EmailSettings");

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(
                emailSettings["SenderName"],
                emailSettings["SenderEmail"]
            ));
            message.To.Add(new MailboxAddress(userName, toEmail));
            message.Subject = "Password Reset OTP - Project Management";

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = $@"
                <div style='font-family: Arial, sans-serif; max-width: 500px; margin: 0 auto; padding: 20px;'>
                    <h2 style='color: #E7B914; text-align: center;'>Password Reset</h2>
                    <p>Hello <strong>{userName}</strong>,</p>
                    <p>You requested to reset your password. Use the OTP code below:</p>
                    <div style='background-color: #f4f4f4; padding: 20px; text-align: center; border-radius: 10px; margin: 20px 0;'>
                        <h1 style='color: #333; letter-spacing: 8px; margin: 0;'>{otpCode}</h1>
                    </div>
                    <p style='color: #888; font-size: 13px;'>This code expires in <strong>10 minutes</strong>.</p>
                    <p style='color: #888; font-size: 13px;'>If you didn't request this, please ignore this email.</p>
                    <hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;' />
                    <p style='color: #aaa; font-size: 11px; text-align: center;'>Project Management System</p>
                </div>"
            };

            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(
                emailSettings["SmtpServer"],
                int.Parse(emailSettings["SmtpPort"]!),
                SecureSocketOptions.StartTls
            );
            await client.AuthenticateAsync(
                emailSettings["SenderEmail"],
                emailSettings["SenderPassword"]
            );
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
    }
}
