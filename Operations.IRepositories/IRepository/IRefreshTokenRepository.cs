using Operations.DataModel.Entities;

namespace Operations.IRepositories.IRepository
{
    public interface IRefreshTokenRepository : IBaseRepository<RefreshToken>
    {
        Task<RefreshToken> GetByTokenHashAsync(string tokenHash);
        Task<List<RefreshToken>> GetActiveByUserIdAsync(int userId);
        Task<List<RefreshToken>> GetActiveByFamilyIdAsync(Guid familyId);
        Task<List<RefreshToken>> GetOlderThanForCleanupAsync(DateTime cutoffDate);
    }
}
