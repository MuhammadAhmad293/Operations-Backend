using Microsoft.EntityFrameworkCore;
using Meezan.DataModel.Entities;
using Meezan.IRepositories.IRepository;
using Meezan.Repositories.Base;
using Meezan.Repositories.Context;

namespace Meezan.Repositories.Repository
{
    internal class PasswordResetTokenRepository : BaseRepository<PasswordResetToken>, IPasswordResetTokenRepository
    {
        public PasswordResetTokenRepository(Lazy<AppDbContext> appDbContext) : base(appDbContext) { }

        public async Task<PasswordResetToken> GetByTokenHashAsync(string tokenHash)
            => await AppDbContext.Value.Set<PasswordResetToken>()
                .FirstOrDefaultAsync(t => t.TokenHash == tokenHash && !t.IsUsed && t.ExpiresAt > DateTime.UtcNow);

        public async Task<List<PasswordResetToken>> GetActiveByUserIdAsync(int userId)
            => await AppDbContext.Value.Set<PasswordResetToken>()
                .Where(t => t.UserId == userId && !t.IsUsed && t.ExpiresAt > DateTime.UtcNow)
                .ToListAsync();
    }
}
