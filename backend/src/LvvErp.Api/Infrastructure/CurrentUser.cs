using System.Security.Claims;
using LvvErp.Application.Common.Interfaces;
using LvvErp.Application.Common.Security;

namespace LvvErp.Api.Infrastructure;

public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public Guid? UserId =>
        Guid.TryParse(Principal?.FindFirstValue(AppClaimTypes.UserId), out var id) ? id : null;

    public string? UserName => Principal?.Identity?.IsAuthenticated == true ? Principal.Identity.Name : null;
}
