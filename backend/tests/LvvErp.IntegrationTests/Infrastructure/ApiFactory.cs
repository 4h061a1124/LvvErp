using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace LvvErp.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the real API against a throwaway PostgreSQL container (requires Docker).
/// One container is shared by all tests in the <see cref="ApiCollection"/>; tests create
/// their own uniquely named data so they don't depend on each other.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminUserName = "admin";
    public const string AdminPassword = "Admin@12345";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public string StorageRoot { get; } = Path.Combine(Path.GetTempPath(), "lvverp-tests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", _postgres.GetConnectionString());
        builder.UseSetting("Database:InitializeOnStartup", "true");
        builder.UseSetting("Jwt:SigningKey", "integration-tests-signing-key-0123456789abcdef");
        builder.UseSetting("Seed:AdminUserName", AdminUserName);
        builder.UseSetting("Seed:AdminPassword", AdminPassword);
        builder.UseSetting("RateLimiting:LoginPermitPerMinute", "1000");
        builder.UseSetting("Storage:LocalRootPath", StorageRoot);
        builder.UseSetting("Serilog:MinimumLevel:Default", "Warning");
    }

    public Task InitializeAsync() => _postgres.StartAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await DisposeAsync();
        if (Directory.Exists(StorageRoot)) Directory.Delete(StorageRoot, recursive: true);
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
