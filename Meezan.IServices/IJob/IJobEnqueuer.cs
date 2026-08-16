namespace Meezan.IServices.IJob
{
    // Anti-corruption adapter (mirrors IRateProvider's role for Frankfurter/GoldApi): lets a
    // Meezan.Services class trigger a background job without depending on Hangfire directly —
    // that dependency stays confined to the Meezan host project, where IJobService is actually
    // wired up to Hangfire (Program.cs). BR-19: adding a wallet in a currency with no recent
    // rate data must schedule a sync, never call the rate provider inline on the request.
    public interface IJobEnqueuer
    {
        void EnqueueRateSync();
    }
}
