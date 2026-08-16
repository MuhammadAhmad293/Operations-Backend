namespace Meezan.Services.Setting
{
    public class RateIntegrationSettings
    {
        public FrankfurterSettings Frankfurter { get; set; } = new();
        public GoldApiSettings GoldApi { get; set; } = new();
        public RedisSettings Redis { get; set; } = new();
        public ResilienceSettings Resilience { get; set; } = new();
        public string CronExpression { get; set; } = "0 2 * * *"; // daily at 02:00 — BR-19: daily is sufficient
    }

    public class FrankfurterSettings
    {
        public string BaseUrl { get; set; } = "https://api.frankfurter.dev/v2";
    }

    public class GoldApiSettings
    {
        public string BaseUrl { get; set; } = "https://api.gold-api.com";
    }

    public class RedisSettings
    {
        public string ConnectionString { get; set; } = "localhost:6379";
    }
}
