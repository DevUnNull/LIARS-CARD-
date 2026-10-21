
using System.Security.Claims;
using Back_End.DTOs;
using Back_End.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Back_End.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly TokenService _tokenService;
    private readonly IConfiguration _configuration;

    public AuthController(
        AuthService authService,
        TokenService tokenService,
        IConfiguration configuration)
    {
        _authService = authService;
        _tokenService = tokenService;
        _configuration = configuration;
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        bool created = await _authService.RegisterAsync(request);

        if (!created)
            return Conflict(new { message = "Email đã tồn tại" });

        return StatusCode(201, new
        {
            message = "Đăng ký tài khoản thành công"
        });
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await _authService.LoginAsync(request);

        if (user == null)
            return Unauthorized(new
            {
                message = "Email hoặc mật khẩu không đúng"
            });

        string accessToken = _tokenService.GenerateAccessToken(user);
        string refreshToken =
            await _authService.CreateRefreshTokenAsync(user);

        int accessMinutes =
            _configuration.GetValue<int>("Jwt:ExpiryMinutes", 30);

        int refreshDays =
            _configuration.GetValue<int>("Jwt:RefreshTokenDays", 30);

        return Ok(new
        {
            message = "Đăng nhập thành công",
            user = new
            {
                id = user.Id,
                email = user.Email,
                displayName = user.DisplayName
            },
            accessToken,
            refreshToken,
            tokenType = "Bearer",
            expiresIn = accessMinutes * 60,
            refreshTokenExpiresIn = refreshDays * 24 * 60 * 60
        });
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshTokenRequest request)
    {
        string? accessToken =
            await _authService.RefreshAsync(request.RefreshToken);

        if (accessToken == null)
            return Unauthorized(new
            {
                message = "Refresh Token không hợp lệ, đã hết hạn hoặc bị thu hồi"
            });

        int accessMinutes =
            _configuration.GetValue<int>("Jwt:ExpiryMinutes", 30);

        return Ok(new
        {
            message = "Làm mới Access Token thành công",
            accessToken,
            tokenType = "Bearer",
            expiresIn = accessMinutes * 60
        });
    }


    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshTokenRequest request)
    {
        var jti = User.FindFirst("jti")?.Value;

        if (string.IsNullOrWhiteSpace(jti))
        {
            return Unauthorized(new
            {
                message = "Access Token không hợp lệ"
            });
        }

        // Thu hồi Refresh Token của session.
        await _authService.LogoutAsync(request.RefreshToken);

        // Đánh dấu Access Token hiện tại đã bị thu hồi.
        int accessMinutes =
            _configuration.GetValue<int>("Jwt:ExpiryMinutes", 30);

        await _authService.RevokeAccessTokenAsync(
            jti,
            DateTime.UtcNow.AddMinutes(accessMinutes));

        return Ok(new
        {
            message = "Đăng xuất thành công"
        });
    }


    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        return Ok(new
        {
            id = User.FindFirstValue("sub"),
            email = User.FindFirstValue("email"),
            displayName = User.FindFirstValue("displayName")
        });
    }
}
