namespace Common.Notification.Mail
{
    public interface IMailSender
    {
        Task<bool> SendMail(MailDto mailDto, MailSettingDto settingDto, CancellationToken cancellationToken = default);
    }
}
