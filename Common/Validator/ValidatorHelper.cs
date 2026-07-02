using Common.Notification.Mail;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace Common.Validator
{
    public class ValidatorHelper : IValidatorHelper
    {
        private ILogger Logger { get; }
        private MailSetting MailSetting { get; }

        public ValidatorHelper(ILogger<ValidatorHelper> logger, MailSetting mailSetting)
        {
            Logger = logger;
            MailSetting = mailSetting;
        }

        public Dictionary<string, bool> ValidateEmail(List<string> mailList)
        {
            Dictionary<string, bool> mailResult = new();
            if (mailList?.Count > default(int))
            {
                foreach (string mail in mailList)
                {
                    if (!mailResult.ContainsKey(mail))
                        mailResult.Add(mail, ValidateMailPattern(mail));
                }
            }
            return mailResult;
        }

        public bool ValidateMailPattern(string mail)
        {
            bool result = default;
            Match? match = Regex.Match(mail, MailSetting.MailValidationRegex, RegexOptions.IgnoreCase);
            if (match.Success)
                result = true;
            else
                Logger.LogError($"Invalid mail {mail}", "ValidateMail");
            return result;
        }

        public (bool IsValid, string ErrorMessage) ValidatePasswordPolicy(string password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < 8)
                return (false, "Password must be at least 8 characters long");
            if (!password.Any(char.IsUpper))
                return (false, "Password must contain at least one uppercase letter");
            if (!password.Any(char.IsLower))
                return (false, "Password must contain at least one lowercase letter");
            if (!password.Any(char.IsDigit))
                return (false, "Password must contain at least one digit");
            if (!password.Any(c => SpecialChars.Contains(c)))
                return (false, "Password must contain at least one special character");
            return (true, null);
        }

        private const string SpecialChars = "!@#$%^&*()_+-=[]{}|;':\",./<>?";
    }
}
