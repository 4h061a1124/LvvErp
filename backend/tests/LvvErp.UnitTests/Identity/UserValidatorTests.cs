using LvvErp.Application.Features.Identity;
using LvvErp.Domain.Features.Identity;

namespace LvvErp.UnitTests.Identity;

public sealed class UserValidatorTests
{
    private readonly CreateUserRequestValidator _validator = new();

    private static CreateUserRequest Valid() =>
        new("nguyen.van.a", "Nguyễn Văn A", "a@lvv.vn", "Passw0rd!", [Roles.Employee]);

    [Fact]
    public void Valid_request_passes() => Assert.True(_validator.Validate(Valid()).IsValid);

    [Theory]
    [InlineData("ab")]
    [InlineData("has space")]
    [InlineData("tên_có_dấu")]
    public void Rejects_bad_usernames(string userName) =>
        Assert.False(_validator.Validate(Valid() with { UserName = userName }).IsValid);

    [Theory]
    [InlineData("short1")]
    [InlineData("onlyletters")]
    [InlineData("12345678")]
    public void Rejects_weak_passwords(string password) =>
        Assert.False(_validator.Validate(Valid() with { Password = password }).IsValid);

    [Fact]
    public void Requires_at_least_one_known_role()
    {
        Assert.False(_validator.Validate(Valid() with { Roles = [] }).IsValid);
        Assert.False(_validator.Validate(Valid() with { Roles = ["Root"] }).IsValid);
    }

    [Fact]
    public void Email_is_optional_but_must_be_valid_when_given()
    {
        Assert.True(_validator.Validate(Valid() with { Email = null }).IsValid);
        Assert.False(_validator.Validate(Valid() with { Email = "not-an-email" }).IsValid);
    }

    [Fact]
    public void Update_validator_ignores_null_password_but_checks_given_one()
    {
        var validator = new UpdateUserRequestValidator();
        var request = new UpdateUserRequest("Name", null, [Roles.HR], true, null);

        Assert.True(validator.Validate(request).IsValid);
        Assert.False(validator.Validate(request with { NewPassword = "weak" }).IsValid);
    }
}
