using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Google.Apis.Auth;
using growy_server.Data;
using growy_server.Models;
using growy_server.Models.DB;
using Microsoft.IdentityModel.Tokens;

namespace growy_server.Services
{
    public class UserService(IServiceScopeFactory scopeFactory, IConfiguration configuration) : IUserService
    {
        public async Task<(UserResult user, string token)> GoogleLoginAsync(string idToken)
        {
            var clientId = configuration["Google:ClientId"]
                ?? throw new InvalidOperationException("Google ClientId not configured");

            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { clientId }
            });

            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<GrowyDbContext>();

            var user = db.Users.FirstOrDefault(u => u.Email == payload.Email);
            if (user == null)
            {
                user = new UserEntity
                {
                    Email = payload.Email,
                    Name = payload.Name,
                    Picture = payload.Picture,
                    Role = "default",
                    CreatedAt = DateTime.UtcNow
                };
                db.Users.Add(user);
                await db.SaveChangesAsync();
            }

            var userResult = new UserResult
            {
                Id = user.Id,
                Email = user.Email,
                Name = user.Name,
                Picture = user.Picture,
                Role = user.Role
            };

            var jwt = BuildJwt(userResult);
            return (userResult, jwt);
        }

        private string BuildJwt(UserResult user)
        {
            var secret = configuration["Jwt:Secret"]
                ?? throw new InvalidOperationException("JWT Secret not configured");
            var issuer = configuration["Jwt:Issuer"] ?? "growy_server";
            var audience = configuration["Jwt:Audience"] ?? "growy_board";
            var expirationHours = int.Parse(configuration["Jwt:ExpirationHours"] ?? "8");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim("name", user.Name),
                new Claim("role", user.Role)
            };

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(expirationHours),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
