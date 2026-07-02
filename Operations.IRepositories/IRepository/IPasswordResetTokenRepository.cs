using Operations.DataModel.Entities;

namespace Operations.IRepositories.IRepository
{
    public interface IPasswordResetTokenRepository : IBaseRepository<PasswordResetToken>
    {
        Task<PasswordResetToken> GetByTokenHashAsync(string tokenHash);
        Task<List<PasswordResetToken>> GetActiveByUserIdAsync(int userId);
    }
}
