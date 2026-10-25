
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
    [Route("api/character-selection")]
    [Authorize]
    public class CharacterSelectionController : ControllerBase
    {
        private readonly IMongoCollection<CharacterSelection> _selections;

        private static readonly HashSet<string> AllowedCharacters =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "char1",
                "char2",
                "char3",
                "char4"
            };

        public CharacterSelectionController(IMongoDatabase database)
        {
            _selections =
                database.GetCollection<CharacterSelection>(
                    "CharacterSelections");
        }

        // GET /api/character-selection
        [HttpGet]
        public async Task<IActionResult> GetSelection()
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

            var selection = await _selections
                .Find(s => s.UserId == userId)
                .FirstOrDefaultAsync();

            return Ok(new
            {
                userId,
                characterId = selection?.CharacterId,
                selectedAt = selection?.SelectedAt
            });
        }

        // PUT /api/character-selection
        [HttpPut]
        public async Task<IActionResult> SelectCharacter(
            [FromBody] SelectCharacterRequest request)
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

            string characterId = request.CharacterId?.Trim() ?? "";
            characterId = characterId.ToLowerInvariant();

            if (!AllowedCharacters.Contains(characterId))
            {
                return BadRequest(new
                {
                    message = "Nhân vật không hợp lệ.",
                    allowedCharacters = AllowedCharacters
                });
            }

            DateTime now = DateTime.UtcNow;

            var filter = Builders<CharacterSelection>.Filter
                .Eq(s => s.UserId, userId);

            var update = Builders<CharacterSelection>.Update
                .SetOnInsert(s => s.UserId, userId)
                .Set(s => s.CharacterId, characterId)
                .Set(s => s.SelectedAt, now);

            var options = new FindOneAndUpdateOptions<CharacterSelection>
            {
                IsUpsert = true,
                ReturnDocument = ReturnDocument.After
            };

            var selection = await _selections.FindOneAndUpdateAsync(
                filter,
                update,
                options);

            return Ok(new
            {
                message = "Chọn nhân vật thành công.",
                userId = selection.UserId,
                characterId = selection.CharacterId,
                selectedAt = selection.SelectedAt
            });
        }
    }
}
