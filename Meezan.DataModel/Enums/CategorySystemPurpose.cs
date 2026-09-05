namespace Meezan.DataModel.Enums
{
    // Discriminates *which* system-owned purpose a protected Category serves — null for
    // ordinary user-created categories. Introduced because IsProtected alone stopped being a
    // unique key once a second protected purpose (BalanceAdjustment) existed alongside Zakat.
    public enum CategorySystemPurpose
    {
        Zakat = 0,
        BalanceAdjustment = 1,
    }
}
