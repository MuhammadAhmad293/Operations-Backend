namespace Meezan.Services.Setting
{
    public class MailSettings
    {
        public string EmailAddress { get; set; }
        public string SmtpServer { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public int EmailSmtpPort { get; set; }
        public int SmtpTimeOut { get; set; }
        public string Subject { get; set; }
        public string CC { get; set; }
        public string Body { get; set; }
        public string From { get; set; }
    }
}
