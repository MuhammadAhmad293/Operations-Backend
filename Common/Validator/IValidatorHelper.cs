namespace Common.Validator
{
    public interface IValidatorHelper
    {
        Dictionary<string, bool> ValidateEmail(List<string> mailList);
        bool ValidateMailPattern(string mail);
        (bool IsValid, string ErrorMessage) ValidatePasswordPolicy(string password);
    }
}
