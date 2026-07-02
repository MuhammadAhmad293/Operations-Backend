using Common.Validator;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace Common.Notification.Mail
{
    public class MailSender : IMailSender
    {
        private ILogger Logger { get; }
        private IValidatorHelper ValidatorHelper { get; }

        public MailSender(ILogger<MailSender> logger, IValidatorHelper validatorHelper)
        {
            Logger = logger;
            ValidatorHelper = validatorHelper;
        }

        public async Task<bool> SendMail(MailDto mailDto, MailSettingDto settingDto, CancellationToken cancellationToken = default)
        {
            bool result = default;
            Logger.LogInformation($"Start handel mail with ID:{mailDto.Id}.", "SendMail");
            if (ValidateMailData(mailDto))
            {
                List<string> mailTo = await GetValidMailRecipient(mailDto.MailTo, mailDto.Id);
                if (mailTo?.Count > default(int))
                {
                    List<string> mailCc = await GetValidMailRecipient(mailDto.MailCc, mailDto.Id);
                    List<string> mailBcc = await GetValidMailRecipient(mailDto.MailBcc, mailDto.Id);

                    #region Create and Send mail
                    MailAddress mailAddress = new(settingDto.EmailAddress);
                    MailMessage mailMessage = new()
                    {
                        Subject = mailDto.Subject,
                        From = mailAddress,
                        Body = mailDto.Body,
                        Sender = mailAddress,
                        BodyEncoding = Encoding.UTF8,
                        IsBodyHtml = mailDto.IsBodyHtml
                    };
                    mailTo.ForEach(rec => mailMessage.To.Add(rec));
                    if (mailCc?.Count > default(int))
                        mailCc.ForEach(cc => mailMessage.CC.Add(cc));
                    if (mailBcc?.Count > default(int))
                        mailBcc.ForEach(bcc => mailMessage.Bcc.Add(bcc));
                    if (mailDto.Attachment?.Count > default(int))
                        mailDto.Attachment.ForEach(att => mailMessage.Attachments.Add(new Attachment(att)));

                    SmtpClient mailClient = new(settingDto.SmtpServer)
                    {
                        Port = settingDto.EmailSmtpPort,
                        EnableSsl = true,                        
                        DeliveryMethod = SmtpDeliveryMethod.Network,
                        UseDefaultCredentials = false
                    };
                    mailClient.Credentials = new NetworkCredential(settingDto.Username, settingDto.Password);
                    try
                    {
                        Logger.LogInformation($"Start send mail with ID:{mailDto.Id} with mail client.", "SendMail");
                        await mailClient.SendMailAsync(mailMessage, cancellationToken);
                        result = true;
                    }
                    catch (Exception exception)
                    {
                        Logger.LogInformation(exception, "SendMail");
                    }
                    #endregion
                }
                else
                    Logger.LogError($"Failed to send mail with ID:{mailDto.Id} due to invalid mail Recipient", "SendMail");
            }
            else
                Logger.LogError($"Failed to send mail with ID:{mailDto.Id} due to invalid mail data", "SendMail");
            if (result)
                Logger.LogInformation($"Mail with ID:{mailDto.Id} sent successfully");

            return result;
        }

        private bool ValidateMailData(MailDto mailDto)
        {
            bool result = true;
            if (string.IsNullOrWhiteSpace(mailDto.Subject))
            {
                Logger.LogError($"Invalid mail subject for mail with ID:{mailDto.Id}.", "Send Mail");
                return !result;
            }
            if (string.IsNullOrWhiteSpace(mailDto.Body))
            {
                Logger.LogError($"Invalid mail Body for mail with ID:{mailDto.Id}.", "Send Mail");
                return !result;
            }
            if (mailDto.MailTo is null || mailDto.MailTo?.Count < default(int))
            {
                Logger.LogError($"Empty mail Recipient for mail with ID:{mailDto.Id}.", "SendMail");
                return !result;
            }

            return result;
        }
        private async Task<List<string>> GetValidMailRecipient(List<string> mailRecipientList, int mailId)
        {
            List<string> recList = new();
            if (mailRecipientList?.Count > default(int))
            {
                Dictionary<string, bool> mailValidatorResult = ValidatorHelper.ValidateEmail(mailRecipientList);
                if (mailValidatorResult.ContainsValue(false))
                {
                    Logger.LogError($"Invalid mail Recipient: {string.Join(",", mailValidatorResult.Where(a => a.Value == false).Select(a => a.Key))} for mail with ID:{mailId}.", "SendMail");
                }
                recList = mailValidatorResult.Where(a => a.Value == true).Select(a => a.Key).ToList();
            }
            return recList;
        }
    }
}
