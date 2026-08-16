using Meezan.Services.Localization;

namespace Meezan.Tests.TestSupport
{
    // The real LocalizationService resolves its JSON file relative to
    // Assembly.GetEntryAssembly().Location — the test host's location under `dotnet test`, not
    // this project's output — so it can't be used as-is from a test. These tests care about
    // service *behavior* (fee cascade, balance math, etc.), not translated text, so each
    // property just echoes its own name — a fine, cheap standin for assertions or exception
    // messages that don't otherwise inspect the string.
    public class FakeLocalizationService : ILocalizationService
    {
        public string GeneralError => "GeneralError";
        public string GeneralSuccess => "GeneralSuccess";
        public string InvalidRequest => "InvalidRequest";
        public string NoDataFound => "NoDataFound";
        public string EmailAlreadyExists => "EmailAlreadyExists";
        public string UserNameAlreadyExists => "UserNameAlreadyExists";
        public string UserNameOrEmailAlreadyExists => "UserNameOrEmailAlreadyExists";
        public string InvalidCredentials => "InvalidCredentials";
        public string InvalidCurrentPassword => "InvalidCurrentPassword";
        public string PasswordResetSent => "PasswordResetSent";
        public string InvalidResetToken => "InvalidResetToken";
        public string PasswordResetSuccess => "PasswordResetSuccess";
        public string RegistrationSuccess => "RegistrationSuccess";
        public string PasswordMismatch => "PasswordMismatch";
        public string PasswordPolicyViolation => "PasswordPolicyViolation";
        public string InvalidRefreshToken => "InvalidRefreshToken";
        public string RefreshTokenConflict => "RefreshTokenConflict";
        public string SessionExpired => "SessionExpired";
        public string LogoutSuccess => "LogoutSuccess";
        public string LogoutAllSuccess => "LogoutAllSuccess";
        public string MaxActiveDevicesReached => "MaxActiveDevicesReached";
        public string AccountAlreadyExists => "AccountAlreadyExists";
        public string AccountNotFound => "AccountNotFound";
        public string InvalidBaseCurrency => "InvalidBaseCurrency";
        public string AccountCreated => "AccountCreated";
        public string WalletNotFound => "WalletNotFound";
        public string WalletCurrencyLocked => "WalletCurrencyLocked";
        public string WalletBalanceNotZero => "WalletBalanceNotZero";
        public string WalletArchived => "WalletArchived";
        public string CategoryNotFound => "CategoryNotFound";
        public string SubcategoryParentMustBeTopLevel => "SubcategoryParentMustBeTopLevel";
        public string CategoryDeleted => "CategoryDeleted";
        public string ProtectedCategoryCannotBeModified => "ProtectedCategoryCannotBeModified";
        public string WalletsMustDiffer => "WalletsMustDiffer";
        public string TransactionSaved => "TransactionSaved";
        public string TransactionNotFound => "TransactionNotFound";
        public string CategoryKindMismatch => "CategoryKindMismatch";
        public string AttachmentNotFound => "AttachmentNotFound";
        public string InvalidAttachmentType => "InvalidAttachmentType";
        public string AttachmentTooLarge => "AttachmentTooLarge";
        public string ZakatPaymentDeletionWarning => "ZakatPaymentDeletionWarning";
        public string RatesUnavailable => "RatesUnavailable";
        public string StatisticsExcludedWalletsNote => "StatisticsExcludedWalletsNote";
        public string NisabNotReached => "NisabNotReached";
        public string ZakatCycleNotDue => "ZakatCycleNotDue";
        public string ZakatPaid => "ZakatPaid";
        public string ExternalPaymentRecorded => "ExternalPaymentRecorded";
        public string ZakatReminderToast => "ZakatReminderToast";
    }
}
