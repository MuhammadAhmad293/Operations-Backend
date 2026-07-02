using Operations.DataModel.Base;

namespace Operations.DataModel.Entities
{
    public class PasswordResetToken : BaseEntity
    {
        public int Id { get; set; }
        public string TokenHash { get; set; }
        public int UserId { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsUsed { get; set; }
        public User User { get; set; }
    }
}
