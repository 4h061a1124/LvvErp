using System.Threading.RateLimiting;
using LvvErp.Application.Common.Interfaces;
using LvvErp.Application.Common.Security;
using LvvErp.Infrastructure.Features.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LvvErp.Api.Infrastructure;

public static class AuthenticationSetup
{
    public const string LoginRateLimitPolicy = "login";

    public static IServiceCollection AddApiAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(jwt.SigningKeyBytes),
                    NameClaimType = AppClaimTypes.UserName,
                    RoleClaimType = AppClaimTypes.Role,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
                options.Events = new JwtBearerEvents { OnTokenValidated = ValidateSecurityStampAsync };
            });

        services.AddAuthorization(options =>
        {
            // Every endpoint requires a signed-in user unless marked [AllowAnonymous].
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();

            foreach (var permission in Permissions.All)
                options.AddPolicy(permission, policy => policy.RequireRole(RolePermissions.RolesWith(permission)));
        });

        var loginPermitLimit = configuration.GetValue("RateLimiting:LoginPermitPerMinute", 10);
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(LoginRateLimitPolicy, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = loginPermitLimit, Window = TimeSpan.FromMinutes(1) }));
        });

        return services;
    }

    /// <summary>Rejects tokens of deactivated users or issued before a password/role change.</summary>
    private static async Task ValidateSecurityStampAsync(TokenValidatedContext context)
    {
        var principal = context.Principal;
        if (!Guid.TryParse(principal?.FindFirst(AppClaimTypes.UserId)?.Value, out var userId)
            || !Guid.TryParse(principal?.FindFirst(AppClaimTypes.SecurityStamp)?.Value, out var stamp))
        {
            context.Fail("Invalid token claims.");
            return;
        }

        var db = context.HttpContext.RequestServices.GetRequiredService<IAppDbContext>();
        var valid = await db.Users.AsNoTracking()
            .AnyAsync(u => u.Id == userId && u.IsActive && u.SecurityStamp == stamp, context.HttpContext.RequestAborted);

        if (!valid)
            context.Fail("Token is no longer valid.");
    }
}
