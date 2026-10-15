using Back_End.DTOs;
using Back_End.Models.Back_End.Models;
using Microsoft.AspNetCore.Identity;
using MongoDB.Driver;

namespace Back_End.Services
{
    public class AuthService
    {
        private readonly IMongoCollection<User> users;
        private readonly PasswordHasher<User> passwordHasher;

        public AuthService(IMongoDatabase database)
        {
            users = database.GetCollection<User>("Users");
            passwordHasher = new PasswordHasher<User>();
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
    }
}
