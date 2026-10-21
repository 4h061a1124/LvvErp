using FluentValidation;
using LvvErp.Application.Features.Employees;
using LvvErp.Application.Features.Employees.Import;
using LvvErp.Application.Features.Identity;
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

        return services;
    }
}
