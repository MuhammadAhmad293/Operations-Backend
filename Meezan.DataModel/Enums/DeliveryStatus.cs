namespace Meezan.DataModel.Enums
{
    public enum DeliveryStatus
    {
        Pending = 0,
        Queued = 1,
        Processing = 2,
        Retrying = 3,
        DeadLetter = 4,
    }
}