
using Back_End.DTOs;
using Back_End.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Security.Claims;

namespace Back_End.Controllers
{
    [ApiController]
    [Route("api/profile")]
    [Authorize]
    public class ProfileController : ControllerBase
    {
        private readonly IMongoCollection<User> _users;
        private readonly IMongoCollection<CharacterSelection> _selections;
        private readonly IMongoCollection<Character> _characters;

        public ProfileController(IMongoDatabase database)
        {
            _users = database.GetCollection<User>("Users");

            _selections =
                database.GetCollection<CharacterSelection>("CharacterSelections");

            _characters =
                database.GetCollection<Character>("Characters");
        }

        // GET /api/profile
        [HttpGet]

        [HttpGet]
        public async Task<IActionResult> GetProfile()
        {
            string? userId = User.FindFirstValue("sub");

            if (string.IsNullOrWhiteSpace(userId) ||
                !ObjectId.TryParse(userId, out _))
            {
                return Unauthorized(new
                {
                    message = "Token không có Player ID hợp lệ."
                });
            }

            var user = await _users
                .Find(u => u.Id == userId)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                return NotFound(new
                {
                    message = "Không tìm thấy hồ sơ người chơi."
                });
            }

            // Tìm nhân vật mà người chơi đã chọn.
            var selection = await _selections
                .Find(s => s.UserId == userId)
                .FirstOrDefaultAsync();

            object? characterInfo = null;

            if (selection != null)
            {
                // Tìm thông tin nhân vật trong collection Characters.
                var character = await _characters
                    .Find(c => c.Id == selection.CharacterId)
                    .FirstOrDefaultAsync();

                if (character != null)
                {
                    characterInfo = new
                    {
                        id = character.Id,
                        name = character.Name,
                        avatarKey = character.AvatarKey,
                        description = character.Description
                    };
                }
            }

            return Ok(new
            {
                id = user.Id,
                email = user.Email,
                displayName = user.DisplayName,
                createdAt = user.CreatedAt,
                updatedAt = user.UpdatedAt,

                // Thêm thông tin nhân vật vào hồ sơ.
                character = characterInfo
            });
        }


        // PATCH /api/profile
        [HttpPatch]
        public async Task<IActionResult> UpdateProfile(
            [FromBody] UpdateProfileRequest request)
        {
            string? userId = User.FindFirstValue("sub");

            if (string.IsNullOrWhiteSpace(userId) ||
                !ObjectId.TryParse(userId, out _))
            {
                return Unauthorized(new
                {
                    message = "Token không có Player ID hợp lệ."
                });
            }

            string displayName = request.DisplayName.Trim();

            if (displayName.Any(char.IsControl))
            {
                return BadRequest(new
                {
                    message = "Tên người chơi chứa ký tự không hợp lệ."
                });
            }

            var filter = Builders<User>.Filter
                .Eq(u => u.Id, userId);

            var update = Builders<User>.Update
                .Set(u => u.DisplayName, displayName)
                .Set(u => u.UpdatedAt, DateTime.UtcNow);

            var updatedUser = await _users.FindOneAndUpdateAsync(
                filter,
                update,
                new FindOneAndUpdateOptions<User>
                {
                    ReturnDocument = ReturnDocument.After
                });

            if (updatedUser == null)
            {
                return NotFound(new
                {
                    message = "Không tìm thấy hồ sơ người chơi."
                });
            }

            return Ok(new
            {
                message = "Cập nhật hồ sơ thành công.",
                profile = new
                {
                    id = updatedUser.Id,
                    email = updatedUser.Email,
                    displayName = updatedUser.DisplayName,
                    createdAt = updatedUser.CreatedAt,
                    updatedAt = updatedUser.UpdatedAt
                }
            });
        }
    }
}
