using System.Net;
using System.Net.Http.Json;
using LvvErp.Application.Features.Identity;
using LvvErp.Domain.Features.Identity;
using LvvErp.IntegrationTests.Infrastructure;

namespace LvvErp.IntegrationTests.Identity;

public sealed class UsersTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Admin_can_create_user_who_can_then_log_in()
    {
        var (user, password) = await CreateUserAsync(Roles.HR);

        var login = await LoginAsync(user.UserName, password);

        Assert.Equal([Roles.HR], login.User.Roles);
        Assert.True(user.IsActive);
    }

    [Theory]
    [InlineData(Roles.HR)]
    [InlineData(Roles.Employee)]
    public async Task Non_admin_roles_get_403_on_user_management(string role)
    {
        var (user, password) = await CreateUserAsync(role);
        var client = await ClientForAsync(user.UserName, password);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/users")).StatusCode);
        var create = await client.PostAsJsonAsync("/api/users",
            new CreateUserRequest(UniqueName("x"), "X", null, "Passw0rd!", [Roles.Admin]));
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
    }

    [Fact]
    public async Task Duplicate_username_returns_409()
    {
        var (user, _) = await CreateUserAsync(Roles.Employee);
        var admin = await AdminClientAsync();

        var response = await admin.PostAsJsonAsync("/api/users",
            new CreateUserRequest(user.UserName.ToUpperInvariant(), "Dup", null, "Passw0rd!", [Roles.Employee]));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("ab", "Passw0rd!", Roles.Employee)]          // username too short
    [InlineData("valid_name", "short1", Roles.Employee)]      // password too short
    [InlineData("valid_name", "NoDigitsHere", Roles.Employee)] // password without digit
    [InlineData("valid_name", "Passw0rd!", "SuperUser")]      // unknown role
    public async Task Invalid_create_request_returns_400(string userName, string password, string role)
    {
        var admin = await AdminClientAsync();

        var response = await admin.PostAsJsonAsync("/api/users",
            new CreateUserRequest(userName, "Name", null, password, [role]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Deactivating_user_revokes_existing_tokens_and_blocks_login()
    {
        var (user, password) = await CreateUserAsync(Roles.Employee);
        var userClient = await ClientForAsync(user.UserName, password);
        var admin = await AdminClientAsync();

        var update = await admin.PutAsJsonAsync($"/api/users/{user.Id}",
            new UpdateUserRequest(user.FullName, null, user.Roles, IsActive: false, NewPassword: null));
        update.EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Unauthorized, (await userClient.GetAsync("/api/auth/me")).StatusCode);
        var login = await AnonymousClient().PostAsJsonAsync("/api/auth/login", new LoginRequest(user.UserName, password));
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Changing_roles_takes_effect_after_re_login()
    {
        var (user, password) = await CreateUserAsync(Roles.Employee);
        var admin = await AdminClientAsync();

        var update = await admin.PutAsJsonAsync($"/api/users/{user.Id}",
            new UpdateUserRequest(user.FullName, null, [Roles.Admin], IsActive: true, NewPassword: null));
        update.EnsureSuccessStatusCode();

        var promoted = await ClientForAsync(user.UserName, password);
        Assert.Equal(HttpStatusCode.OK, (await promoted.GetAsync("/api/users")).StatusCode);
    }

    [Fact]
    public async Task Admin_cannot_deactivate_self_or_drop_own_admin_role()
    {
        var (adminUser, password) = await CreateUserAsync(Roles.Admin);
        var client = await ClientForAsync(adminUser.UserName, password);

        var deactivate = await client.PutAsJsonAsync($"/api/users/{adminUser.Id}",
            new UpdateUserRequest(adminUser.FullName, null, [Roles.Admin], IsActive: false, NewPassword: null));
        Assert.Equal(HttpStatusCode.Conflict, deactivate.StatusCode);

        var demote = await client.PutAsJsonAsync($"/api/users/{adminUser.Id}",
            new UpdateUserRequest(adminUser.FullName, null, [Roles.HR], IsActive: true, NewPassword: null));
        Assert.Equal(HttpStatusCode.Conflict, demote.StatusCode);
    }

    [Fact]
    public async Task Unknown_user_returns_404()
    {
        var admin = await AdminClientAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/users/{Guid.NewGuid()}")).StatusCode);
    }
}
