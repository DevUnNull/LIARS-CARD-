using Back_End.DTOs;
using Back_End.Models;
using Back_End.Models.Back_End.Models;
using Microsoft.AspNetCore.Identity;
using MongoDB.Driver;

namespace Back_End.Services
{
    public class AuthService
    {
        private readonly IMongoCollection<User> users;
        private readonly PasswordHasher<User> passwordHasher;
        private readonly IMongoCollection<RefreshToken> refreshTokens;
        private readonly TokenService tokenService;

        public AuthService(IMongoDatabase database, TokenService tokenService)
        {
            users = database.GetCollection<User>("Users");
            refreshTokens = database.GetCollection<RefreshToken>("refresh_tokens");
            passwordHasher = new PasswordHasher<User>();
            this.tokenService = tokenService;
        }

        public async Task<bool> RegisterAsync(RegisterRequest request)
        {
            string email = request.Email.Trim().ToLowerInvariant();

            var existingUser = await users
                .Find(u => u.Email == email)
                .FirstOrDefaultAsync();

            if (existingUser != null)
            {
                return false;
            }

            var user = new User
            {
                Email = email,
                DisplayName = request.DisplayName.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            user.PasswordHash = passwordHasher.HashPassword(
                user,
                request.Password
            );

            await users.InsertOneAsync(user);

            return true;
        }

        public async Task<User?> LoginAsync(LoginRequest request)
        {
            string email = request.Email.Trim().ToLowerInvariant();

            var user = await users.Find(u => u.Email == email).FirstOrDefaultAsync();

            if (user == null)
            {
                return null;
            }

            var result = passwordHasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                request.Password
            );

            if (result == PasswordVerificationResult.Failed)
            {
                return null;
            }

            return user;
        }

        public async Task<string> CreateRefreshTokenAsync(User user)
        {
            string refreshToken = tokenService.GenerateRefreshToken();

            string tokenHash = tokenService.HashRefreshToken(refreshToken);

            var tokenDocument = new RefreshToken
            {
                UserId = user.Id!,
                TokenHash = tokenHash,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                RevokedAt = null
            };

            await refreshTokens.InsertOneAsync(tokenDocument);

            return refreshToken;
        }


        public async Task<(string AccessToken, string RefreshToken)?>
            RefreshAsync(string oldRefreshToken)
        {
            if (string.IsNullOrWhiteSpace(oldRefreshToken))
                return null;

            string tokenHash =
                tokenService.HashRefreshToken(oldRefreshToken);

            var now = DateTime.UtcNow;

            string newRefreshToken =
                tokenService.GenerateRefreshToken();

            string newTokenHash =
                tokenService.HashRefreshToken(newRefreshToken);

            var filter = Builders<RefreshToken>.Filter.And(
                Builders<RefreshToken>.Filter.Eq(
                    t => t.TokenHash, tokenHash),
                Builders<RefreshToken>.Filter.Eq(
                    t => t.RevokedAt, null),
                Builders<RefreshToken>.Filter.Gt(
                    t => t.ExpiresAt, now)
            );

            var update = Builders<RefreshToken>.Update
                .Set(t => t.RevokedAt, now);

            var oldToken = await refreshTokens
                .FindOneAndUpdateAsync(
                    filter,
                    update,
                    new FindOneAndUpdateOptions<RefreshToken>
                    {
                        ReturnDocument = ReturnDocument.Before
                    });

            if (oldToken == null)
                return null;

            var user = await users
                .Find(u => u.Id == oldToken.UserId)
                .FirstOrDefaultAsync();

            if (user == null)
                return null;

            var newTokenDocument = new RefreshToken
            {
                UserId = user.Id!,
                TokenHash = newTokenHash,
                CreatedAt = now,
                ExpiresAt = now.AddDays(7),
                RevokedAt = null
            };

            await refreshTokens.InsertOneAsync(newTokenDocument);

            string newAccessToken =
                tokenService.GenerateAccessToken(user);

            return (newAccessToken, newRefreshToken);
        }

    }
}
