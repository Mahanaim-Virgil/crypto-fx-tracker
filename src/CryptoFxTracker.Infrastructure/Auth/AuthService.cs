using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CryptoFxTracker.Application.Abstractions;
using CryptoFxTracker.Application.Models;
using CryptoFxTracker.Domain.Entities;
using CryptoFxTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CryptoFxTracker.Infrastructure.Auth;

public sealed class AuthService(
    CryptoFxTrackerDbContext dbContext,
    IPasswordHasher<User> passwordHasher,
    IOptions<JwtOptions> jwtOptions) : IAuthService
{
    public async Task<AuthResponse?> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);
        ValidateCredentials(email, request.Password);

        if (await dbContext.Users.AnyAsync(user => user.Email == email, cancellationToken))
        {
            return null;
        }

        var user = new User
        {
            Email = email,
            PasswordHash = string.Empty,
            CreatedAtUtc = DateTime.UtcNow
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);
        return CreateToken(user);
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);
        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.Email == email, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        return result == PasswordVerificationResult.Failed ? null : CreateToken(user);
    }

    private AuthResponse CreateToken(User user)
    {
        var options = jwtOptions.Value;
        var expiresAt = DateTime.UtcNow.AddMinutes(options.ExpirationMinutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email)
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(options.Issuer, options.Audience, claims, expires: expiresAt, signingCredentials: credentials);
        return new AuthResponse(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static void ValidateCredentials(string email, string password)
    {
        if (!email.Contains('@') || password.Length < 8)
        {
            throw new ArgumentException("Geçerli bir e-posta ve en az 8 karakterli parola gereklidir.");
        }
    }
}
