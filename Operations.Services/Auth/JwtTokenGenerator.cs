using Microsoft.IdentityModel.Tokens;
using Operations.DataModel.Entities;
using Operations.Services.Setting;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Operations.Services.Auth
{
    public class JwtTokenGenerator : IJwtTokenGenerator
    {
        private JwtSettings JwtSettings { get; }

        public JwtTokenGenerator(JwtSettings jwtSettings)
        {
            JwtSettings = jwtSettings;
        }

        public string GenerateToken(User user)
        {
            List<Claim> claims = new()
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.UserName)
            };

            SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(JwtSettings.Secret));
            SigningCredentials credentials = new(key, SecurityAlgorithms.HmacSha256);

            JwtSecurityToken token = new(
                issuer: JwtSettings.Issuer,
                audience: JwtSettings.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(JwtSettings.ExpiryMinutes),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
