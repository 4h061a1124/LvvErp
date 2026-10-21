using LvvErp.Api.Infrastructure;
using LvvErp.Application.Features.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LvvErp.Api.Features.Identity;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthenticationSetup.LoginRateLimitPolicy)]
    public Task<LoginResponse> Login(LoginRequest request, CancellationToken ct) =>
        authService.LoginAsync(request, ct);

    [HttpGet("me")]
    public Task<CurrentUserDto> Me(CancellationToken ct) =>
        authService.GetCurrentUserAsync(ct);

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        await authService.ChangePasswordAsync(request, ct);
        return NoContent();
    }
}
