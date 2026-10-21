using LvvErp.Domain.Features.Identity;

namespace LvvErp.Application.Common.Interfaces;

public interface IJwtTokenService
{
    AccessToken CreateAccessToken(User user);
}

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);
