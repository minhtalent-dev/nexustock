using Microsoft.AspNetCore.Mvc;
using Nexustock.Modules.Identity.Services;

namespace Nexustock.Modules.Identity.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>Login with email and password</summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var (succeeded, token, refreshToken, errors) = await _authService.LoginAsync(request.Email, request.Password);

        if (!succeeded)
            return Unauthorized(new { message = errors.FirstOrDefault() });

        return Ok(new
        {
            token,
            refreshToken,
            tokenType = "Bearer"
        });
    }

    /// <summary>Refresh access token</summary>
    [HttpPost("refresh")]
    [HttpPost("refresh-token")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
    {
        var (succeeded, token, refreshToken, errors) = await _authService.RefreshTokenAsync(request.RefreshToken);

        if (!succeeded)
            return Unauthorized(new { message = errors.FirstOrDefault() });

        return Ok(new
        {
            token,
            refreshToken,
            tokenType = "Bearer"
        });
    }

    /// <summary>Logout (revoke refresh token)</summary>
    [HttpPost("logout")]
    [HttpPost("revoke-token")]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request)
    {
        await _authService.LogoutAsync(request.RefreshToken);
        return Ok(new { message = "Logged out successfully" });
    }

    /// <summary>Register a new user</summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var (succeeded, errors) = await _authService.RegisterAsync(
            request.Email, request.Password, request.FullName, request.TenantId);

        if (!succeeded)
            return BadRequest(new { errors });

        return Ok(new { message = "Registration successful" });
    }
}

public record LoginRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);
public record RegisterRequest(string Email, string Password, string FullName, Guid TenantId);
