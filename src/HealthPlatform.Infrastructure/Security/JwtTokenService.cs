using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using HealthPlatform.Application.Features.Auth;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace HealthPlatform.Infrastructure.Security;

internal sealed class JwtTokenService : IJwtTokenService
{
    private const int AccessTokenMinutes     = 30;
    private const int RefreshTokenDays       = 7;
    private const int RefreshTokenByteLength = 32; // 256-bit → 44-char Base64

    private readonly string        _secretKey;
    private readonly string        _issuer;
    private readonly string        _audience;
    private readonly ICacheService _cache;
    private readonly TimeSpan      _refreshTtl = TimeSpan.FromDays(RefreshTokenDays);

    public JwtTokenService(IConfiguration configuration, ICacheService cache)
    {
        var jwt    = configuration.GetSection("JwtSettings");
        _secretKey = jwt["SecretKey"]
            ?? throw new InvalidOperationException("JwtSettings:SecretKey is required.");
        _issuer    = jwt["Issuer"]   ?? "HealthPlatformApi";
        _audience  = jwt["Audience"] ?? "HealthPlatformClients";
        _cache     = cache;
    }

    // ── Token pair generation ─────────────────────────────────────────────────

    public TokenResult GenerateTokenPair(User user, Guid sessionId)
    {
        var accessToken  = BuildJwt(user, sessionId);
        var refreshToken = GenerateRefreshToken();
        return new TokenResult(accessToken, refreshToken, AccessTokenMinutes * 60, sessionId);
    }

    // ── Refresh token lifecycle ───────────────────────────────────────────────

    public Task StoreRefreshTokenAsync(
        Guid userId, string refreshToken, CancellationToken ct = default)
    {
        var key = RefreshKey(userId);
        return _cache.SetAsync(key, new RefreshEntry(refreshToken), _refreshTtl, ct);
    }

    public async Task<bool> ValidateAndConsumeRefreshTokenAsync(
        Guid userId, string refreshToken, CancellationToken ct = default)
    {
        var key   = RefreshKey(userId);
        var entry = await _cache.GetAsync<RefreshEntry>(key, ct);

        if (entry is null || entry.Token != refreshToken)
            return false;

        // Consume — delete before returning to prevent reuse.
        await _cache.DeleteAsync(key, ct);
        return true;
    }

    public Task RevokeRefreshTokenAsync(Guid userId, CancellationToken ct = default)
        => _cache.DeleteAsync(RefreshKey(userId), ct);

    // ── Private helpers ───────────────────────────────────────────────────────

    private string BuildJwt(User user, Guid sessionId)
    {
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var now   = DateTime.UtcNow;

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,   user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Role,               user.Role.ToString()),
            new Claim("sid",                         sessionId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer:             _issuer,
            audience:           _audience,
            claims:             claims,
            notBefore:          now,
            expires:            now.AddMinutes(AccessTokenMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(RefreshTokenByteLength);
        return Convert.ToBase64String(bytes);
    }

    private static string RefreshKey(Guid userId) => $"refresh:{userId}";

    // ── Private record for Redis serialisation ────────────────────────────────

    private sealed record RefreshEntry(string Token);
}
