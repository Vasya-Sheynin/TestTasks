//  TASK: formulate a detailed prompt for LLM to generate markdown documentation for this service, 
//      for example use markdown documentation from the example below.

using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using FirebaseAdmin.Messaging;
using Microsoft.Extensions.Logging;
using Task2.Models;
using Task2.Settings;
using Twilio;
using Twilio.Rest.Api.V2010.Account;

namespace Task2.Services
{
    public interface INotificationService
    {
        Task<NotificationResult> SendEmailAsync(string to, string subject, string body);
        Task<NotificationResult> SendSmsAsync(string phoneNumber, string message);
        Task<NotificationResult> SendPushAsync(string deviceToken, string title, string body);
    }

    public class NotificationService : INotificationService
    {
        private readonly ILogger<NotificationService> _logger;
        private readonly SmtpSettings _smtp;
        private readonly TwilioSettings _twilio;
        private readonly FirebaseMessaging _firebase;

        public NotificationService(
            ILogger<NotificationService> logger,
            SmtpSettings smtp,
            TwilioSettings twilio,
            FirebaseMessaging firebase)
        {
            _logger   = logger;
            _smtp     = smtp;
            _twilio   = twilio;
            _firebase = firebase;

            TwilioClient.Init(_twilio.AccountSid, _twilio.AuthToken);
        }

        public async Task<NotificationResult> SendEmailAsync(string to, string subject, string body)
        {
            try
            {
                using var client = new SmtpClient(_smtp.Host, _smtp.Port)
                {
                    Credentials = new NetworkCredential(_smtp.Username, _smtp.Password),
                    EnableSsl  = true
                };

                var msg = new MailMessage(_smtp.From, to, subject, body);
                await client.SendMailAsync(msg);

                _logger.LogInformation("Email sent to {To}", to);
                return NotificationResult.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {To}", to);
                return NotificationResult.Failed("EmailError", ex.Message);
            }
        }

        public async Task<NotificationResult> SendSmsAsync(string phoneNumber, string message)
        {
            try
            {
                var msg = await MessageResource.CreateAsync(
                    body:    message,
                    from:    new Twilio.Types.PhoneNumber(_twilio.FromNumber),
                    to:      new Twilio.Types.PhoneNumber(phoneNumber)
                );

                _logger.LogInformation("SMS sent SID={Sid}", msg.Sid);
                return NotificationResult.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send SMS to {Phone}", phoneNumber);
                return NotificationResult.Failed("SmsError", ex.Message);
            }
        }

        public async Task<NotificationResult> SendPushAsync(string deviceToken, string title, string body)
        {
            try
            {
                var message = new Message()
                {
                    Token = deviceToken,
                    Notification = new Notification
                    {
                        Title = title,
                        Body  = body
                    }
                };

                var response = await _firebase.SendAsync(message);
                _logger.LogInformation("Push sent: {Response}", response);
                return NotificationResult.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send push to {Token}", deviceToken);
                return NotificationResult.Failed("PushError", ex.Message);
            }
        }
    }
}
