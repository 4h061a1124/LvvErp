using LvvErp.Application.Common.Models;
using LvvErp.Application.Common.Security;
using LvvErp.Application.Features.Employees;
using LvvErp.Application.Features.Employees.Import;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LvvErp.Api.Features.Employees;

[ApiController]
[Route("api/employees")]
[Authorize(Policy = Permissions.EmployeesView)]
public sealed class EmployeesController(IEmployeeService employees, IEmployeeImportService import) : ControllerBase
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [HttpGet]
    public Task<PagedResult<EmployeeDto>> List([FromQuery] EmployeeListQuery query, CancellationToken ct) =>
        employees.ListAsync(query, ct);

    [HttpGet("{id:guid}")]
    public Task<EmployeeDto> Get(Guid id, CancellationToken ct) => employees.GetAsync(id, ct);

    [HttpPost]
    [Authorize(Policy = Permissions.EmployeesManage)]
    public async Task<ActionResult<EmployeeDto>> Create(SaveEmployeeRequest request, CancellationToken ct)
    {
        var employee = await employees.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = employee.Id }, employee);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.EmployeesManage)]
    public Task<EmployeeDto> Update(Guid id, SaveEmployeeRequest request, CancellationToken ct) =>
        employees.UpdateAsync(id, request, ct);

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.EmployeesManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await employees.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/photo")]
    [Authorize(Policy = Permissions.EmployeesManage)]
    [RequestSizeLimit(EmployeeRules.MaxPhotoBytes + 64 * 1024)]
    public async Task<EmployeeDto> UploadPhoto(Guid id, IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return await employees.SetPhotoAsync(id, stream, ct);
    }

    [HttpDelete("{id:guid}/photo")]
    [Authorize(Policy = Permissions.EmployeesManage)]
    public async Task<IActionResult> DeletePhoto(Guid id, CancellationToken ct)
    {
        await employees.RemovePhotoAsync(id, ct);
        return NoContent();
    }

    [HttpGet("import/template")]
    [Authorize(Policy = Permissions.EmployeesManage)]
    public async Task<IActionResult> DownloadImportTemplate(CancellationToken ct) =>
        File(await import.CreateTemplateAsync(ct), XlsxContentType, "mau-import-nhan-vien.xlsx");

    /// <summary>Always 200 for a readable file; check <c>succeeded</c> and <c>errors</c> in the body.</summary>
    [HttpPost("import")]
    [Authorize(Policy = Permissions.EmployeesManage)]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<EmployeeImportResult> Import(IFormFile file, [FromQuery] bool updateExisting, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return await import.ImportAsync(stream, updateExisting, ct);
    }
}
