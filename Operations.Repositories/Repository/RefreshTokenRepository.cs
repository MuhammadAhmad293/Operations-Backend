using Microsoft.EntityFrameworkCore;
using Operations.DataModel.Entities;
using Operations.IRepositories.IRepository;
using Operations.Repositories.Base;
using Operations.Repositories.Context;

namespace Operations.Repositories.Repository
{
    internal class RefreshTokenRepository : BaseRepository<RefreshToken>, IRefreshTokenRepository
    {
        public RefreshTokenRepository(Lazy<AppDbContext> appDbContext) : base(appDbContext) { }

        // Intentionally no active/expiry filter — reuse detection needs to see already-revoked rows too.
        public async Task<RefreshToken> GetByTokenHashAsync(string tokenHash)
            => await AppDbContext.Value.Set<RefreshToken>()
                .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

        public async Task<List<RefreshToken>> GetActiveByUserIdAsync(int userId)
            => await AppDbContext.Value.Set<RefreshToken>()
                .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > DateTime.UtcNow)
                .ToListAsync();

        public async Task<List<RefreshToken>> GetActiveByFamilyIdAsync(Guid familyId)
            => await AppDbContext.Value.Set<RefreshToken>()
                .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
                .ToListAsync();

        public async Task<List<RefreshToken>> GetOlderThanForCleanupAsync(DateTime cutoffDate)
            => await AppDbContext.Value.Set<RefreshToken>()
                .Where(t => t.CreationTime < cutoffDate && (t.RevokedAt != null || t.ExpiresAt <= DateTime.UtcNow))
                .ToListAsync();
    }
}
