using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LvvErp.Application.Common.Models;
using LvvErp.Application.Features.Employees;
using LvvErp.Application.Features.Identity;
using LvvErp.Domain.Features.Employees;

namespace LvvErp.IntegrationTests.Infrastructure;

[Collection(ApiCollection.Name)]
public abstract class ApiTestBase(ApiFactory factory)
{
    /// <summary>Matches the API's JSON settings (camelCase, enums as strings).</summary>
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    protected ApiFactory Factory { get; } = factory;

    protected HttpClient AnonymousClient() => Factory.CreateClient();

    protected async Task<HttpClient> ClientForAsync(string userName, string password)
    {
        var token = (await LoginAsync(userName, password)).AccessToken;
        return ClientWithToken(token);
    }

    protected Task<HttpClient> AdminClientAsync() => ClientForAsync(ApiFactory.AdminUserName, ApiFactory.AdminPassword);

    /// <summary>Signs in as a freshly created user with the given role.</summary>
    protected async Task<HttpClient> ClientWithRoleAsync(string role)
    {
        var (user, password) = await CreateUserAsync(role);
        return await ClientForAsync(user.UserName, password);
    }

    protected HttpClient ClientWithToken(string token)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    protected async Task<LoginResponse> LoginAsync(string userName, string password)
    {
        var response = await AnonymousClient().PostAsJsonAsync("/api/auth/login", new LoginRequest(userName, password));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
    }

    /// <summary>Creates a user through the API as admin. Returns the created user and its password.</summary>
    protected async Task<(UserDto User, string Password)> CreateUserAsync(params string[] roles)
    {
        const string password = "Passw0rd!";
        var admin = await AdminClientAsync();
        var request = new CreateUserRequest(UniqueName("user"), "Test User", null, password, roles);
        var response = await admin.PostAsJsonAsync("/api/users", request);
        response.EnsureSuccessStatusCode();
        return ((await response.Content.ReadFromJsonAsync<UserDto>())!, password);
    }

    protected static string UniqueName(string prefix) => $"{prefix}_{Guid.NewGuid():N}"[..20];

    /// <summary>Random upper-case employee code, e.g. "T3F9A1C2B".</summary>
    protected static string UniqueCode() => "T" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    protected static async Task<LookupDto> LookupAsync(HttpClient client, string resource, string code) =>
        (await client.GetFromJsonAsync<List<LookupDto>>($"/api/{resource}", Json))!.Single(l => l.Code == code);

    protected static async Task<SaveEmployeeRequest> NewEmployeeRequestAsync(
        HttpClient client, string? code = null, string fullName = "Nguyễn Văn Test",
        string department = "SX", string position = "CN", EmployeeStatus status = EmployeeStatus.Active,
        string? email = null) =>
        new(code ?? UniqueCode(), fullName,
            (await LookupAsync(client, "departments", department)).Id,
            (await LookupAsync(client, "positions", position)).Id,
            email, null, status);

    protected static async Task<EmployeeDto> CreateEmployeeAsync(HttpClient client, SaveEmployeeRequest request)
    {
        var response = await client.PostAsJsonAsync("/api/employees", request, Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EmployeeDto>(Json))!;
    }
}
