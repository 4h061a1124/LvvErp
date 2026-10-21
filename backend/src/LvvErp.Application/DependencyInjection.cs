using FluentValidation;
using LvvErp.Application.Common.Files;
using LvvErp.Application.Common.Time;
using LvvErp.Application.Features.Employees;
using LvvErp.Application.Features.Employees.Import;
using LvvErp.Application.Features.Identity;
using LvvErp.Application.Features.Inventory.Catalog;
using LvvErp.Application.Features.Inventory.Documents;
using LvvErp.Application.Features.Inventory.Import;
using LvvErp.Application.Features.Inventory.Reports;
using Microsoft.Extensions.DependencyInjection;

namespace LvvErp.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        // Identity
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();

        // Employees
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<ILookupService, LookupService>();
        services.AddScoped<IEmployeeImportService, EmployeeImportService>();

        // Inventory
        services.AddScoped(typeof(ICatalogService<>), typeof(CatalogService<>));
        services.AddScoped<IWarehouseService, WarehouseService>();
        services.AddScoped<IMaterialService, MaterialService>();
        services.AddScoped<IStockDocumentService, StockDocumentService>();
        services.AddScoped<IStockQueryService, StockQueryService>();
        services.AddScoped<IStockReportService, StockReportService>();
        services.AddScoped<IMaterialImportService, MaterialImportService>();
        services.AddScoped<IOpeningBalanceImportService, OpeningBalanceImportService>();

        // Shared
        services.AddScoped<PhotoUploader>();
        services.AddSingleton<BusinessClock>();

        return services;
    }
}
