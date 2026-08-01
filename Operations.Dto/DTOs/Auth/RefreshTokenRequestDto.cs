namespace Operations.Dto.DTOs.Auth
{
    public class RefreshTokenRequestDto
    {
        public string RefreshToken { get; set; }
        public string? DeviceId { get; set; }
        public string? DeviceName { get; set; }
        public string? Platform { get; set; }
    }
}
