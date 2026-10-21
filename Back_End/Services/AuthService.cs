
using Back_End.DTOs;
using Back_End.Models;
using Microsoft.AspNetCore.Identity;
using MongoDB.Driver;

namespace Back_End.Services;

public class AuthService
{
    private readonly IMongoCollection<User> _users;
    private readonly IMongoCollection<RefreshToken> _refreshTokens;
    private readonly PasswordHasher<User> _passwordHasher = new();
    private readonly TokenService _tokenService;
    private readonly IConfiguration _configuration;
    private readonly IMongoCollection<RevokedAccessToken> _revokedAccessTokens;
    public AuthService(
        IMongoDatabase database,
        TokenService tokenService,
        IConfiguration configuration,
        IMongoCollection<RevokedAccessToken> revokedAccessTokens)
    {
        _users = database.GetCollection<User>("Users");
        _refreshTokens = database.GetCollection<RefreshToken>("refresh_tokens");
        _tokenService = tokenService;
        _configuration = configuration;
        _revokedAccessTokens = revokedAccessTokens;
    }

    public async Task<bool> RegisterAsync(RegisterRequest request)
    {
        string email = request.Email.Trim().ToLowerInvariant();

        var existingUser = await _users
            .Find(u => u.Email == email)
            .FirstOrDefaultAsync();

        if (existingUser != null)
            return false;

        var user = new User
        {
            Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString(),
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        user.PasswordHash = _passwordHasher.HashPassword(
            user, request.Password);

        try
        {
            await _users.InsertOneAsync(user);
            return true;
        }
        catch (MongoWriteException ex)
            when (ex.WriteError?.Category ==
                  ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    public async Task<User?> LoginAsync(LoginRequest request)
    {
        string email = request.Email.Trim().ToLowerInvariant();

        var user = await _users
            .Find(u => u.Email == email)
            .FirstOrDefaultAsync();

        if (user == null)
            return null;

        var result = _passwordHasher.VerifyHashedPassword(
            user, user.PasswordHash, request.Password);

        if (result == PasswordVerificationResult.Failed)
            return null;

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _passwordHasher.HashPassword(
                user, request.Password);

            await _users.ReplaceOneAsync(u => u.Id == user.Id, user);
        }

        return user;
    }

    public async Task<string> CreateRefreshTokenAsync(User user)
    {
        string refreshToken = _tokenService.GenerateRefreshToken();

        var tokenDocument = new RefreshToken
        {
            UserId = user.Id!,
            TokenHash = _tokenService.HashRefreshToken(refreshToken),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(
                _configuration.GetValue<int>("Jwt:RefreshTokenDays", 30)),
            RevokedAt = null
        };

        await _refreshTokens.InsertOneAsync(tokenDocument);
        return refreshToken;
    }

    public async Task<string?> RefreshAsync(string oldRefreshToken)
    {
        if (string.IsNullOrWhiteSpace(oldRefreshToken))
            return null;

        string tokenHash = _tokenService.HashRefreshToken(oldRefreshToken);
        DateTime now = DateTime.UtcNow;

        var session = await _refreshTokens.Find(t =>
            t.TokenHash == tokenHash &&
            t.RevokedAt == null &&
            t.ExpiresAt > now
        ).FirstOrDefaultAsync();

        if (session == null)
            return null;

        var user = await _users
            .Find(u => u.Id == session.UserId)
            .FirstOrDefaultAsync();

        if (user == null)
            return null;

        // Do not replace or extend the existing refresh token.
        return _tokenService.GenerateAccessToken(user);
    }

    public async Task<bool> LogoutAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return false;

        string tokenHash = _tokenService.HashRefreshToken(refreshToken);

        var filter = Builders<RefreshToken>.Filter.And(
            Builders<RefreshToken>.Filter.Eq(
                t => t.TokenHash, tokenHash),
            Builders<RefreshToken>.Filter.Eq(
                t => t.RevokedAt, null));

        var update = Builders<RefreshToken>.Update
            .Set(t => t.RevokedAt, DateTime.UtcNow);

        var result = await _refreshTokens.UpdateOneAsync(filter, update);

        return result.ModifiedCount == 1;
    }

    public async Task RevokeAccessTokenAsync(
        string jti,
        DateTime expiresAt)
    {
        var filter = Builders<RevokedAccessToken>.Filter
            .Eq(t => t.Jti, jti);

        var update = Builders<RevokedAccessToken>.Update
            .SetOnInsert(t => t.Jti, jti)
            .SetOnInsert(t => t.ExpiresAt, expiresAt);

        await _revokedAccessTokens.UpdateOneAsync(
            filter,
            update,
            new UpdateOptions { IsUpsert = true });
    }

}
