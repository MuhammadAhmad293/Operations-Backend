namespace Meezan.Services.Localization
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
        public string AccountAlreadyExists => GetKeyValue("AccountAlreadyExists", "altValue");
        public string AccountNotFound => GetKeyValue("AccountNotFound", "altValue");
        public string InvalidBaseCurrency => GetKeyValue("InvalidBaseCurrency", "altValue");
        public string AccountCreated => GetKeyValue("AccountCreated", "altValue");
        public string WalletNotFound => GetKeyValue("WalletNotFound", "altValue");
        public string WalletCurrencyLocked => GetKeyValue("WalletCurrencyLocked", "altValue");
        public string WalletBalanceNotZero => GetKeyValue("WalletBalanceNotZero", "altValue");
        public string WalletArchived => GetKeyValue("WalletArchived", "altValue");
        public string CategoryNotFound => GetKeyValue("CategoryNotFound", "altValue");
        public string SubcategoryParentMustBeTopLevel => GetKeyValue("SubcategoryParentMustBeTopLevel", "altValue");
        public string CategoryDeleted => GetKeyValue("CategoryDeleted", "altValue");
        public string ProtectedCategoryCannotBeModified => GetKeyValue("ProtectedCategoryCannotBeModified", "altValue");
        public string WalletsMustDiffer => GetKeyValue("WalletsMustDiffer", "altValue");
        public string TransactionSaved => GetKeyValue("TransactionSaved", "altValue");
        public string TransactionNotFound => GetKeyValue("TransactionNotFound", "altValue");
        public string CategoryKindMismatch => GetKeyValue("CategoryKindMismatch", "altValue");
        public string AttachmentNotFound => GetKeyValue("AttachmentNotFound", "altValue");
        public string InvalidAttachmentType => GetKeyValue("InvalidAttachmentType", "altValue");
        public string AttachmentTooLarge => GetKeyValue("AttachmentTooLarge", "altValue");
        public string ZakatPaymentDeletionWarning => GetKeyValue("ZakatPaymentDeletionWarning", "altValue");
        public string RatesUnavailable => GetKeyValue("RatesUnavailable", "altValue");
        public string StatisticsExcludedWalletsNote => GetKeyValue("StatisticsExcludedWalletsNote", "altValue");
        public string NisabNotReached => GetKeyValue("NisabNotReached", "altValue");
        public string ZakatCycleNotDue => GetKeyValue("ZakatCycleNotDue", "altValue");
        public string ZakatPaid => GetKeyValue("ZakatPaid", "altValue");
        public string ExternalPaymentRecorded => GetKeyValue("ExternalPaymentRecorded", "altValue");
        public string ZakatReminderToast => GetKeyValue("ZakatReminderToast", "altValue");

    }
}
