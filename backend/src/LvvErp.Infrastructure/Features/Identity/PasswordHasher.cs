using LvvErp.Application.Common.Interfaces;
using LvvErp.Domain.Features.Identity;
using Microsoft.AspNetCore.Identity;

namespace LvvErp.Infrastructure.Features.Identity;

/// <summary>Uses ASP.NET Core Identity's PBKDF2 hasher without pulling in the full Identity stack.</summary>
public sealed class PasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<User> _inner = new();

    public string Hash(string password) => _inner.HashPassword(null!, password);

    public PasswordVerification Verify(string hashedPassword, string password) =>
        _inner.VerifyHashedPassword(null!, hashedPassword, password) switch
        {
            PasswordVerificationResult.Success => PasswordVerification.Success,
            PasswordVerificationResult.SuccessRehashNeeded => PasswordVerification.SuccessRehashNeeded,
            _ => PasswordVerification.Failed,
        };
}
