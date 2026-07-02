namespace Operations.Services.Localization
{
    public interface ILocalizationService
    {
        string GeneralError { get; }
        string GeneralSuccess { get; }
        string InvalidRequest { get; }
        string NoDataFound { get; }
        string EmailAlreadyExists { get; }
        string UserNameAlreadyExists { get; }
        string UserNameOrEmailAlreadyExists { get; }
        string InvalidCredentials { get; }
        string InvalidCurrentPassword { get; }
        string PasswordResetSent { get; }
        string InvalidResetToken { get; }
        string PasswordResetSuccess { get; }
        string RegistrationSuccess { get; }
        string PasswordMismatch { get; }
        string PasswordPolicyViolation { get; }

    }
}
