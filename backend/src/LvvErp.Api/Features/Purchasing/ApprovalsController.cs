using LvvErp.Application.Features.Purchasing.Approvals;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LvvErp.Api.Features.Purchasing;

/// <summary>Any signed-in user; the lists are empty for users who approve nothing.</summary>
[ApiController]
[Route("api/purchasing/approvals")]
[Authorize]
public sealed class ApprovalsController(IApprovalInboxService inbox) : ControllerBase
{
    [HttpGet]
    public Task<ApprovalInboxDto> Get(CancellationToken ct) => inbox.GetAsync(ct);
}
