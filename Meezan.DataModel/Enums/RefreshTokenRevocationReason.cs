namespace Meezan.DataModel.Enums
{
    public enum RefreshTokenRevocationReason
    {
        Logout = 0,
        LogoutAll = 1,
        RefreshRotation = 2,
        ReuseDetected = 3,
        PasswordChanged = 4,
        PasswordReset = 5,
        AbsoluteLifetimeExceeded = 6,
        DeviceLimitReached = 7,
        AdminRevoked = 8,
    }
}
