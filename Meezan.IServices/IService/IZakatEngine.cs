namespace Meezan.IServices.IService
{
    public interface IZakatEngine
    {
        Task ReevaluateAsync(int accountId, CancellationToken cancellationToken = default);
        Task RecomputeCyclePaymentAsync(int zakatCycleId, CancellationToken cancellationToken = default);
    }
}
