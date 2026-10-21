using System.Net;
using System.Net.Http.Json;
using LvvErp.Application.Common.Security;
using LvvErp.Application.Features.Identity;
using LvvErp.Domain.Features.Identity;
using LvvErp.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace LvvErp.IntegrationTests.Identity;

public sealed class AuthTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Seeded_admin_can_log_in_and_gets_all_permissions()
    {
        var login = await LoginAsync(ApiFactory.AdminUserName, ApiFactory.AdminPassword);

        Assert.False(string.IsNullOrWhiteSpace(login.AccessToken));
        Assert.True(login.ExpiresAt > DateTimeOffset.UtcNow);
        Assert.Equal([Roles.Admin], login.User.Roles);
        Assert.Equal(Permissions.All.Order(), login.User.Permissions);
    }

    [Fact]
    public async Task Username_is_case_insensitive()
    {
        var login = await LoginAsync("  ADMIN ", ApiFactory.AdminPassword);
        Assert.Equal(ApiFactory.AdminUserName, login.User.UserName);
    }

    [Theory]
    [InlineData("admin", "wrong-password")]
    [InlineData("nobody", "Admin@12345")]
    public async Task Invalid_credentials_return_401_with_problem_details(string userName, string password)
    {
        var response = await AnonymousClient().PostAsJsonAsync("/api/auth/login", new LoginRequest(userName, password));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Invalid username or password.", problem!.Detail);
    }

    [Fact]
    public async Task Empty_login_request_returns_400_with_field_errors()
    {
        var response = await AnonymousClient().PostAsJsonAsync("/api/auth/login", new LoginRequest("", ""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains("UserName", problem!.Errors.Keys);
        Assert.Contains("Password", problem.Errors.Keys);
    }

    [Fact]
    public async Task Me_requires_a_token()
    {
        var response = await AnonymousClient().GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_returns_current_user_with_permissions_for_role()
    {
        var (user, password) = await CreateUserAsync(Roles.Employee);
        var client = await ClientForAsync(user.UserName, password);

        var me = await client.GetFromJsonAsync<CurrentUserDto>("/api/auth/me");

        Assert.Equal(user.Id, me!.Id);
        Assert.Equal([Permissions.EmployeesView, Permissions.InventoryView], me.Permissions);
    }

    [Fact]
    public async Task Tampered_token_is_rejected()
    {
        var login = await LoginAsync(ApiFactory.AdminUserName, ApiFactory.AdminPassword);
        var tampered = login.AccessToken[..^4] + (login.AccessToken.EndsWith("AAAA") ? "BBBB" : "AAAA");

        var response = await ClientWithToken(tampered).GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Changing_password_invalidates_old_password_and_old_tokens()
    {
        var (user, password) = await CreateUserAsync(Roles.Employee);
        var oldClient = await ClientForAsync(user.UserName, password);

        var change = await oldClient.PostAsJsonAsync("/api/auth/change-password",
            new ChangePasswordRequest(password, "NewPassw0rd!"));
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await oldClient.GetAsync("/api/auth/me")).StatusCode);

        var oldLogin = await AnonymousClient().PostAsJsonAsync("/api/auth/login", new LoginRequest(user.UserName, password));
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

        var newLogin = await LoginAsync(user.UserName, "NewPassw0rd!");
        Assert.Equal(user.Id, newLogin.User.Id);
    }

    [Fact]
    public async Task Change_password_with_wrong_current_password_returns_400()
    {
        var (user, password) = await CreateUserAsync(Roles.Employee);
        var client = await ClientForAsync(user.UserName, password);

        var response = await client.PostAsJsonAsync("/api/auth/change-password",
            new ChangePasswordRequest("not-my-password1", "NewPassw0rd!"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Health_endpoint_is_anonymous()
    {
        var response = await AnonymousClient().GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
