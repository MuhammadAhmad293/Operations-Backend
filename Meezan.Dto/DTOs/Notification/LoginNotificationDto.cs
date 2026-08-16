namespace Meezan.Dto.DTOs.Notification
{
    // API #28: pending login toasts. Currently only the BR-16 Zakat reminder exists; Type lets
    // the frontend distinguish future notification kinds without a breaking response shape change.
    public class LoginNotificationDto
    {
        public string Type { get; set; }
        public string Message { get; set; }
    }
}
