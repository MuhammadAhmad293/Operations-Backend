using Common.Dto;
using Common.Notification.Mail;
using Meezan.DataModel.Entities;
using Meezan.Services.Setting;

namespace Meezan.Services.Email
{
    public class SmtpEmailSender : ISmtpEmailSender
    {
        private readonly IMailSender _mailSender;
        private readonly MailSettings _mailSettings;

        public SmtpEmailSender(IMailSender mailSender, MailSettings mailSettings)
        {
            _mailSender = mailSender;
            _mailSettings = mailSettings;
        }

        public async Task SendAsync(Mail mail, CancellationToken cancellationToken = default)
        {
            MailDto mailDto = new()
            {
                Id = mail.MailId,
                MailTo = new List<string> { mail.To },
                Subject = mail.Subject,
                Body = mail.Body,
                IsBodyHtml = false,
            };

            MailSettingDto settingDto = new()
            {
                EmailAddress = _mailSettings.EmailAddress,
                Username = _mailSettings.Username,
                Password = _mailSettings.Password,
                SmtpServer = _mailSettings.SmtpServer,
                EmailSmtpPort = _mailSettings.EmailSmtpPort,
                SmtpTimeOut = _mailSettings.SmtpTimeOut,
            };

            bool sent = await _mailSender.SendMail(mailDto, settingDto, cancellationToken);
            if (!sent)
                throw new InvalidOperationException($"SMTP sender returned false for MailId={mail.MailId}");
        }
    }
}