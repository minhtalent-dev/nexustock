using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Nexustock.Modules.Identity.Contexts;
using Nexustock.Modules.Identity.Entities;

namespace Nexustock.Modules.Identity.Services;

public interface IAuthService
{
    Task<(bool Succeeded, string? Token, string? RefreshToken, string[] Errors)> LoginAsync(string email, string password);
    Task<(bool Succeeded, string? Token, string? RefreshToken, string[] Errors)> RefreshTokenAsync(string refreshToken);
    Task<bool> LogoutAsync(string refreshToken);
    Task<(bool Succeeded, string[] Errors)> RegisterAsync(string email, string password, string fullName, Guid tenantId);
}

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IdentityDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IdentityDbContext dbContext,
        IConfiguration configuration,
        IHttpContextAccessor httpContextAccessor)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _dbContext = dbContext;
        _configuration = configuration;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<(bool Succeeded, string? Token, string? RefreshToken, string[] Errors)> LoginAsync(string email, string password)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null || !user.IsActive)
            return (false, null, null, new[] { "Invalid credentials or account disabled" });

        var result = await _signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        if (result.IsLockedOut)
            return (false, null, null, new[] { "Account locked. Try again later" });
        if (!result.Succeeded)
            return (false, null, null, new[] { "Invalid credentials" });

        var token = GenerateJwtToken(user);
        var refreshToken = await GenerateAndStoreRefreshTokenAsync(user);

        return (true, token, refreshToken, Array.Empty<string>());
    }

    public async Task<(bool Succeeded, string? Token, string? RefreshToken, string[] Errors)> RefreshTokenAsync(string refreshToken)
    {
        var storedToken = await _dbContext.UserRefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken);

        if (storedToken == null || !storedToken.IsActive)
            return (false, null, null, new[] { "Invalid or expired refresh token" });

        // Revoke current & rotate
        storedToken.IsRevoked = true;
        storedToken.RevokedAt = DateTime.UtcNow;

        var user = await _userManager.FindByIdAsync(storedToken.UserId.ToString());
        if (user == null || !user.IsActive)
            return (false, null, null, new[] { "User not found or disabled" });

        var newToken = GenerateJwtToken(user);
        var newRefreshToken = await GenerateAndStoreRefreshTokenAsync(user);

        storedToken.ReplacedByToken = newRefreshToken;
        await _dbContext.SaveChangesAsync();

        return (true, newToken, newRefreshToken, Array.Empty<string>());
    }

    public async Task<bool> LogoutAsync(string refreshToken)
    {
        var storedToken = await _dbContext.UserRefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken);

        if (storedToken == null)
            return false;

        storedToken.IsRevoked = true;
        storedToken.RevokedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        return true;
    }

    public async Task<(bool Succeeded, string[] Errors)> RegisterAsync(string email, string password, string fullName, Guid tenantId)
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = fullName,
            TenantId = tenantId,
            IsActive = true,
        };

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
            return (false, result.Errors.Select(e => e.Description).ToArray());

        return (true, Array.Empty<string>());
    }

    private string GenerateJwtToken(ApplicationUser user)
    {
        var secretKey = _configuration["JWT_SECRET_KEY"]
            ?? throw new InvalidOperationException("JWT_SECRET_KEY is not configured");

        var issuer = _configuration["JWT_ISSUER"] ?? "Nexustock";
        var audience = _configuration["JWT_AUDIENCE"] ?? "Nexustock";

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email ?? ""),
            new(ClaimTypes.Name, user.FullName),
            new("tenantId", user.TenantId.ToString()),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<string> GenerateAndStoreRefreshTokenAsync(ApplicationUser user)
    {
        var refreshToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray().Concat(Guid.NewGuid().ToByteArray()).ToArray());

        var tokenEntity = new UserRefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            TenantId = user.TenantId,
        };

        _dbContext.UserRefreshTokens.Add(tokenEntity);
        await _dbContext.SaveChangesAsync();

        return refreshToken;
    }
}
