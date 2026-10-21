namespace LvvErp.Application.Common.Interfaces;

public interface IPasswordHasher
{
    string Hash(string password);
    PasswordVerification Verify(string hashedPassword, string password);
}

public enum PasswordVerification
{
    Failed,
    Success,
    SuccessRehashNeeded,
}
