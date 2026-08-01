using Operations.DataModel.Base;
using Operations.DataModel.Enums;

namespace Operations.DataModel.Entities
{
    public class RefreshToken : BaseEntity
    {
        public int Id { get; set; }
        public string TokenHash { get; set; }
        public int UserId { get; set; }
        public Guid FamilyId { get; set; }
        public DateTime FamilyCreatedAt { get; set; }
        public string? DeviceId { get; set; }
        public string? DeviceName { get; set; }
        public string? Platform { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime? LastUsedAt { get; set; }
        public DateTime? RevokedAt { get; set; }
        public RefreshTokenRevocationReason? RevokedReason { get; set; }
        public string? CreatedByIp { get; set; }
        public string? LastUsedIp { get; set; }
        public byte[] RowVersion { get; set; }
        public User User { get; set; }
    }
}
