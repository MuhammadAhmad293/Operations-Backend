namespace Meezan.IServices.IService
{
    public interface IZakatPotCalculator
    {
        // BR-13: sum of ALL non-excludeFromTotal wallets (cash + gold + silver combined),
        // expressed as pure-24K-gold-gram equivalent, valued at current market prices. Shared by
        // ZakatEngine (hawl state transitions) and ZakatService (GetStatus display) so pot math
        // can never drift between the two callers.
        Task<decimal> ComputePotGoldGramsAsync(int accountId, CancellationToken cancellationToken = default);
    }
}
