namespace Meezan.Services.Setting
{
    public class RefreshTokenSettings
    {
        public int RefreshTokenExpiryDays { get; set; } = 30;
        public int AbsoluteSessionLifetimeDays { get; set; } = 90;
        public int MaxActiveDevices { get; set; } = 5;
        public string MaxActiveDevicesStrategy { get; set; } = "RevokeOldest";
        public int RetentionDays { get; set; } = 90;
        public CookiePolicySettings Cookie { get; set; } = new();
    }

    public class CookiePolicySettings
    {
        // Must match the actual route casing ("/api/Auth", from AuthController) — cookie Path
        // matching is case-sensitive in real browsers, unlike ASP.NET Core's own route matching.
        public string Path { get; set; } = "/api/Auth";
        public string Domain { get; set; }
        public string SameSite { get; set; } = "Lax";
        public bool Secure { get; set; } = true;
    }
}
