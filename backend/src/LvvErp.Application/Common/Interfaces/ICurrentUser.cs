namespace LvvErp.Application.Common.Interfaces;

/// <summary>The user making the current request; all members are null for anonymous calls.</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
    string? UserName { get; }
}
