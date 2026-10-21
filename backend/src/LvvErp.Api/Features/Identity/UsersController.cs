using LvvErp.Application.Common.Security;
using LvvErp.Application.Features.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LvvErp.Api.Features.Identity;

[ApiController]
[Route("api/users")]
[Authorize(Policy = Permissions.UsersManage)]
public sealed class UsersController(IUserService userService) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<UserDto>> List(CancellationToken ct) => userService.ListAsync(ct);

    [HttpGet("{id:guid}")]
    public Task<UserDto> Get(Guid id, CancellationToken ct) => userService.GetAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create(CreateUserRequest request, CancellationToken ct)
    {
        var user = await userService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = user.Id }, user);
    }

    [HttpPut("{id:guid}")]
    public Task<UserDto> Update(Guid id, UpdateUserRequest request, CancellationToken ct) =>
        userService.UpdateAsync(id, request, ct);
}
