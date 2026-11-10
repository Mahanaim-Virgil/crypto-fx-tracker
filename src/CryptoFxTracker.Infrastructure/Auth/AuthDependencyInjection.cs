using CryptoFxTracker.Application.Abstractions;
using CryptoFxTracker.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CryptoFxTracker.Infrastructure.Auth;

public static class AuthDependencyInjection
{
    public static IServiceCollection AddAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Secret) && options.Secret.Length >= 32,
                "Jwt:Secret en az 32 karakter olmalıdır.")
            .ValidateOnStart();

        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<IAuthService, AuthService>();
        return services;
    }
}
