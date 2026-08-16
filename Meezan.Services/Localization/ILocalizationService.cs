namespace Meezan.Services.Localization
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
        string InvalidRefreshToken { get; }
        string RefreshTokenConflict { get; }
        string SessionExpired { get; }
        string LogoutSuccess { get; }
        string LogoutAllSuccess { get; }
        string MaxActiveDevicesReached { get; }
        string AccountAlreadyExists { get; }
        string AccountNotFound { get; }
        string InvalidBaseCurrency { get; }
        string AccountCreated { get; }
        string WalletNotFound { get; }
        string WalletCurrencyLocked { get; }
        string WalletBalanceNotZero { get; }
        string WalletArchived { get; }
        string CategoryNotFound { get; }
        string SubcategoryParentMustBeTopLevel { get; }
        string CategoryDeleted { get; }
        string ProtectedCategoryCannotBeModified { get; }
        string WalletsMustDiffer { get; }
        string TransactionSaved { get; }
        string TransactionNotFound { get; }
        string CategoryKindMismatch { get; }
        string AttachmentNotFound { get; }
        string InvalidAttachmentType { get; }
        string AttachmentTooLarge { get; }
        string ZakatPaymentDeletionWarning { get; }
        string RatesUnavailable { get; }
        string StatisticsExcludedWalletsNote { get; }
        string NisabNotReached { get; }
        string ZakatCycleNotDue { get; }
        string ZakatPaid { get; }
        string ExternalPaymentRecorded { get; }
        string ZakatReminderToast { get; }

    }
}
