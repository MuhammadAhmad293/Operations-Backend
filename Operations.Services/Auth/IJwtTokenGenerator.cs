using Operations.DataModel.Entities;

namespace Operations.Services.Auth
{
    public interface IJwtTokenGenerator
    {
        string GenerateToken(User user);
    }
}
