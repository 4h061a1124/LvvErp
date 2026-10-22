using FluentValidation;
using LvvErp.Application.Common.Files;
using LvvErp.Application.Common.Security;
using LvvErp.Application.Common.Time;
using LvvErp.Application.Features.Employees;
using LvvErp.Application.Features.Employees.Import;
using LvvErp.Application.Features.Identity;
using LvvErp.Application.Features.Inventory.Catalog;
using LvvErp.Application.Features.Inventory.Documents;
using LvvErp.Application.Features.Inventory.Import;
using LvvErp.Application.Features.Inventory.Reports;
using LvvErp.Application.Features.Purchasing.Approvals;
using LvvErp.Application.Features.Purchasing.Orders;
using LvvErp.Application.Features.Purchasing.Receiving;
using LvvErp.Application.Features.Purchasing.Reports;
using LvvErp.Application.Features.Purchasing.Requests;
using LvvErp.Application.Features.Purchasing.Settings;
using LvvErp.Application.Features.Purchasing.Suppliers;
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

        // Purchasing
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<ISupplierImportService, SupplierImportService>();
        services.AddScoped<IPurchasingSettingsService, PurchasingSettingsService>();
        services.AddScoped<IPurchaseRequestService, PurchaseRequestService>();
        services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
        services.AddScoped<IApprovalInboxService, ApprovalInboxService>();
        services.AddScoped<IPurchaseReceiving, PurchaseReceiving>();
        services.AddScoped<IPurchasingReportService, PurchasingReportService>();

        // Shared
        services.AddScoped<IUserScopeProvider, UserScopeProvider>();
        services.AddScoped<PhotoUploader>();
        services.AddSingleton<BusinessClock>();

        return services;
    }
}
