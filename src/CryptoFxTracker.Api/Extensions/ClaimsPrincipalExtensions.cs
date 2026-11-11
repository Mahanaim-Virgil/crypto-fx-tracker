using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace CryptoFxTracker.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static long GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return long.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("JWT kullanıcı kimliği bulunamadı.");
    }
}
