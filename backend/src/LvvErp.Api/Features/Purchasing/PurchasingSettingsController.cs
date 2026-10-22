using LvvErp.Application.Common.Security;
using LvvErp.Application.Features.Purchasing.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LvvErp.Api.Features.Purchasing;

[ApiController]
[Route("api/purchasing/settings")]
[Authorize(Policy = Permissions.PurchasingView)]
public sealed class PurchasingSettingsController(IPurchasingSettingsService settings) : ControllerBase
{
    [HttpGet]
    public Task<PurchasingSettingsDto> Get(CancellationToken ct) => settings.GetAsync(ct);

    [HttpPut]
    [Authorize(Policy = Permissions.PurchasingManageSettings)]
    public Task<PurchasingSettingsDto> Update(SavePurchasingSettingsRequest request, CancellationToken ct) =>
        settings.UpdateAsync(request, ct);
}
