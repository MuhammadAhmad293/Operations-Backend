namespace Meezan.Services.CategoryDefaults
{
    // Shared between AccountService's DefaultCategoryTemplate (new accounts, created up front)
    // and WalletService's find-or-lazily-create path (accounts that predate this feature) — one
    // canonical name pair for both creation paths, so they can never drift apart.
    internal static class BalanceAdjustmentCategoryName
    {
        public const string En = "Balance Adjustment";
        public const string Ar = "تسوية الرصيد";
    }
}
