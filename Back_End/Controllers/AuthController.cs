using Back_End.DTOs;
using Back_End.Models;
using Back_End.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Back_End.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService authService;
        private readonly TokenService tokenService;

        public AuthController(AuthService authService, TokenService tokenService)
        {
            this.authService = authService;
            this.tokenService = tokenService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            var result = await authService.RegisterAsync(request);

            if (!result)
            {
                return Conflict(new
                {
                message = "Email đã tồn tại"
                });
            }   

            return StatusCode(201, new
            {
                message = "Đăng ký tài khoản thành công"
            });
        }


        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var user = await authService.LoginAsync(request);

            if (user == null)
            {
                return Unauthorized(new
                {
                    message = "Email hoặc mật khẩu không đúng"
                });
            }

            var accessToken = tokenService.GenerateAccessToken(user);

            var refreshToken = await authService.CreateRefreshTokenAsync(user);

            return Ok(new
            {
                message = "Đăng nhập thành công",
                accessToken = accessToken,
                refreshToken = refreshToken,
                tokenType = "Bearer",
                expiresIn = 3600
            });
        }


        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<IActionResult> Refresh(
            [FromBody] RefreshTokenRequest request)
        {
            var result = await authService.RefreshAsync(
                request.RefreshToken
            );

            if (result == null)
            {
                return Unauthorized(new
                {
                    message = "Refresh Token không hợp lệ hoặc đã hết hạn"
                });
            }

            return Ok(new
            {
                message = "Làm mới token thành công",
                accessToken = result.Value.AccessToken,
                refreshToken = result.Value.RefreshToken,
                tokenType = "Bearer",
                expiresIn = 3600
            });
        }

    }
}
