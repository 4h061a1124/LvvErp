using LvvErp.Application.Common.Models;
using LvvErp.Application.Common.Security;
using LvvErp.Application.Features.Employees;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LvvErp.Api.Features.Employees;

[ApiController]
[Authorize(Policy = Permissions.EmployeesView)]
public sealed class LookupsController(ILookupService lookups) : ControllerBase
{
    [HttpGet("api/departments")]
    public Task<IReadOnlyList<LookupDto>> Departments(CancellationToken ct) => lookups.GetDepartmentsAsync(ct);

    [HttpGet("api/positions")]
    public Task<IReadOnlyList<LookupDto>> Positions(CancellationToken ct) => lookups.GetPositionsAsync(ct);
}
