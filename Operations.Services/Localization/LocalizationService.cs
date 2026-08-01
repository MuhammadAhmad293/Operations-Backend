namespace Operations.Services.Localization
{
    public class LocalizationService : LocalizationFileReader.LocalizationFileReader, ILocalizationService
    {
        public LocalizationService() : base("localizationFile") { }

        public string GeneralError => GetKeyValue("GeneralError", "altValue");
        public string GeneralSuccess => GetKeyValue("GeneralSuccess", "altValue");
        public string InvalidRequest => GetKeyValue("InvalidRequest", "altValue");
        public string NoDataFound => GetKeyValue("NoDataFound", "altValue");
        public string EmailAlreadyExists => GetKeyValue("EmailAlreadyExists", "altValue");
        public string UserNameAlreadyExists => GetKeyValue("UserNameAlreadyExists", "altValue");
        public string UserNameOrEmailAlreadyExists => GetKeyValue("UserNameOrEmailAlreadyExists", "altValue");
        public string InvalidCredentials => GetKeyValue("InvalidCredentials", "altValue");
        public string InvalidCurrentPassword => GetKeyValue("InvalidCurrentPassword", "altValue");
        public string PasswordResetSent => GetKeyValue("PasswordResetSent", "altValue");
        public string InvalidResetToken => GetKeyValue("InvalidResetToken", "altValue");
        public string PasswordResetSuccess => GetKeyValue("PasswordResetSuccess", "altValue");
        public string RegistrationSuccess => GetKeyValue("RegistrationSuccess", "altValue");
        public string PasswordMismatch => GetKeyValue("PasswordMismatch", "altValue");
        public string PasswordPolicyViolation => GetKeyValue("PasswordPolicyViolation", "altValue");
        public string InvalidRefreshToken => GetKeyValue("InvalidRefreshToken", "altValue");
        public string RefreshTokenConflict => GetKeyValue("RefreshTokenConflict", "altValue");
        public string SessionExpired => GetKeyValue("SessionExpired", "altValue");
        public string LogoutSuccess => GetKeyValue("LogoutSuccess", "altValue");
        public string LogoutAllSuccess => GetKeyValue("LogoutAllSuccess", "altValue");
        public string MaxActiveDevicesReached => GetKeyValue("MaxActiveDevicesReached", "altValue");

    }
}
