using Meezan.DataModel.Entities;

namespace Meezan.Services.Auth
{
    public interface IJwtTokenGenerator
    {
        string GenerateToken(User user);
    }
}
