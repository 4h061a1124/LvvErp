using FluentValidation;
using FluentValidation.Results;
using LvvErp.Application.Common.Interfaces;
using LvvErp.Application.Common.Text;
using LvvErp.Domain.Features.Purchasing;
using Microsoft.EntityFrameworkCore;

namespace LvvErp.Application.Features.Purchasing.Settings;

public sealed record PurchasingSettingsDto(
    decimal DirectorApprovalThreshold,
    Guid? PurchasingDepartmentId,
    string? PurchasingDepartmentName,
    string? CompanyName,
    string? CompanyAddress,
    string? CompanyTaxCode,
    string? CompanyPhone,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);

public sealed record SavePurchasingSettingsRequest(
    decimal DirectorApprovalThreshold,
    Guid? PurchasingDepartmentId,
    string? CompanyName = null,
    string? CompanyAddress = null,
    string? CompanyTaxCode = null,
    string? CompanyPhone = null);

public sealed class SavePurchasingSettingsRequestValidator : AbstractValidator<SavePurchasingSettingsRequest>
{
    public SavePurchasingSettingsRequestValidator()
    {
        RuleFor(x => x.DirectorApprovalThreshold)
            .GreaterThan(0)
            .LessThanOrEqualTo(999_999_999_999_999m)
            .Must(v => v == decimal.Truncate(v)).WithMessage("Use whole VND.");
        RuleFor(x => x.PurchasingDepartmentId).NotEmpty();
        RuleFor(x => x.CompanyName).MaximumLength(PurchasingSettings.CompanyNameMaxLength);
        RuleFor(x => x.CompanyAddress).MaximumLength(PurchasingSettings.CompanyAddressMaxLength);
        RuleFor(x => x.CompanyTaxCode!.Trim())
            .Matches(Supplier.TaxCodePattern()).WithMessage("Tax code must be 10 digits, or 10 digits-3 digits for a branch.")
            .When(x => !string.IsNullOrWhiteSpace(x.CompanyTaxCode))
            .OverridePropertyName(nameof(SavePurchasingSettingsRequest.CompanyTaxCode));
        RuleFor(x => x.CompanyPhone).MaximumLength(PurchasingSettings.CompanyPhoneMaxLength);
    }
}

public interface IPurchasingSettingsService
{
    Task<PurchasingSettingsDto> GetAsync(CancellationToken ct = default);
    Task<PurchasingSettingsDto> UpdateAsync(SavePurchasingSettingsRequest request, CancellationToken ct = default);
}

public sealed class PurchasingSettingsService(IAppDbContext db, IValidator<SavePurchasingSettingsRequest> validator)
    : IPurchasingSettingsService
{
    public async Task<PurchasingSettingsDto> GetAsync(CancellationToken ct = default) =>
        await db.PurchasingSettings.AsNoTracking()
            .Where(s => s.Id == PurchasingSettings.SingletonId)
            .Select(s => new PurchasingSettingsDto(
                s.DirectorApprovalThreshold, s.PurchasingDepartmentId,
                s.PurchasingDepartment != null ? s.PurchasingDepartment.Name : null,
                s.CompanyName, s.CompanyAddress, s.CompanyTaxCode, s.CompanyPhone, s.UpdatedAt, s.UpdatedBy))
            .SingleOrDefaultAsync(ct)
        ?? new PurchasingSettingsDto(PurchasingSettings.DefaultDirectorApprovalThreshold, null, null, null, null, null, null, null, null);

    public async Task<PurchasingSettingsDto> UpdateAsync(SavePurchasingSettingsRequest request, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        if (!await db.Departments.AnyAsync(d => d.Id == request.PurchasingDepartmentId, ct))
            throw new ValidationException([new ValidationFailure(nameof(request.PurchasingDepartmentId), "Department does not exist.")]);

        var settings = await db.PurchasingSettings.SingleOrDefaultAsync(s => s.Id == PurchasingSettings.SingletonId, ct);
        if (settings is null)
        {
            settings = new PurchasingSettings { Id = PurchasingSettings.SingletonId };
            db.PurchasingSettings.Add(settings);
        }
        settings.DirectorApprovalThreshold = request.DirectorApprovalThreshold;
        settings.PurchasingDepartmentId = request.PurchasingDepartmentId;
        settings.CompanyName = TextNormalizer.NullIfBlank(request.CompanyName);
        settings.CompanyAddress = TextNormalizer.NullIfBlank(request.CompanyAddress);
        settings.CompanyTaxCode = TextNormalizer.NullIfBlank(request.CompanyTaxCode);
        settings.CompanyPhone = TextNormalizer.NullIfBlank(request.CompanyPhone);
        await db.SaveChangesAsync(ct);
        return await GetAsync(ct);
    }
}
