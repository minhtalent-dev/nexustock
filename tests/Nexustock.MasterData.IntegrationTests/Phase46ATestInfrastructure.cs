using System;
using System.IO;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexustock.Modules.Identity.Services;
using Nexustock.Modules.Files.Services;
using Nexustock.Modules.Files.Providers;
using Nexustock.Modules.Files.Entities;

namespace Nexustock.MasterData.IntegrationTests;

public static class TestAuthConstants
{
    public const string Scheme = "TestAuth";
    public const string UserId = "00000000-0000-0000-0000-000000000099";
    public const string TenantId = "00000000-0000-0000-0000-000000000001";
}

public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    [Obsolete]
    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock)
        : base(options, logger, encoder, clock)
    {
    }

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim("sub", TestAuthConstants.UserId),
            new Claim("tenantId", TestAuthConstants.TenantId),
            new Claim(ClaimTypes.Name, "Test User"),
            new Claim(ClaimTypes.Email, "testuser@example.com")
        };

        var identity = new ClaimsIdentity(claims, TestAuthConstants.Scheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, TestAuthConstants.Scheme);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}



public sealed class FakeUserPermissionService : IUserPermissionService
{
    public HashSet<string> AllowedPermissions { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Task<bool> HasPermissionAsync(Guid userId, string permission)
    {
        return Task.FromResult(AllowedPermissions.Contains(permission));
    }
}

public sealed class FakeSecretProtector : Nexustock.Modules.Files.Services.ISecretProtector
{
    public string Protect(string plaintext) => $"protected_{plaintext}";
    public string Unprotect(string protectedPayload)
    {
        if (protectedPayload.StartsWith("protected_"))
            return protectedPayload.Substring("protected_".Length);
        return protectedPayload;
    }
}

public static class TestStorageFailureControl
{
    public static bool ShouldFailRead { get; set; }
}

public sealed class ThrowingReadObjectStorageProvider : IObjectStorageProvider
{
    private readonly IObjectStorageProvider _inner;

    public ThrowingReadObjectStorageProvider(IObjectStorageProvider inner)
    {
        _inner = inner;
    }

    public string ProviderId => _inner.ProviderId;

    public Task PutAsync(string key, Stream content, string contentType, System.Threading.CancellationToken ct)
        => _inner.PutAsync(key, content, contentType, ct);

    public Task DeleteAsync(string key, System.Threading.CancellationToken ct)
        => _inner.DeleteAsync(key, ct);

    public Task<bool> ExistsAsync(string key, System.Threading.CancellationToken ct)
        => _inner.ExistsAsync(key, ct);

    public Task<Stream> OpenReadAsync(string key, System.Threading.CancellationToken ct)
    {
        if (TestStorageFailureControl.ShouldFailRead)
        {
            throw new Exception("Simulated storage read failure");
        }
        return _inner.OpenReadAsync(key, ct);
    }

    public string BuildPublicUrl(string key, string? publicBaseUrl)
        => _inner.BuildPublicUrl(key, publicBaseUrl);
}

public sealed class TestObjectStorageResolver : IObjectStorageResolver
{
    private readonly IObjectStorageResolver _inner;

    public TestObjectStorageResolver(IObjectStorageResolver inner)
    {
        _inner = inner;
    }

    public IObjectStorageProvider Resolve(FileStorageSettings settings, string? providerOverride = null, string? configJsonPlain = null)
    {
        var provider = _inner.Resolve(settings, providerOverride, configJsonPlain);
        return new ThrowingReadObjectStorageProvider(provider);
    }

    public IObjectStorageProvider ResolveByProviderId(string providerId, FileStorageSettings settings, string? configJsonPlain = null)
    {
        var provider = _inner.ResolveByProviderId(providerId, settings, configJsonPlain);
        return new ThrowingReadObjectStorageProvider(provider);
    }
}
