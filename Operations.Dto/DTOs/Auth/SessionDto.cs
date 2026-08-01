namespace Operations.Dto.DTOs.Auth
{
    public class SessionDto
    {
        public int Id { get; set; }
        public string DeviceId { get; set; }
        public string DeviceName { get; set; }
        public string Platform { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastUsedAt { get; set; }
        public string LastUsedIp { get; set; }
    }
}
