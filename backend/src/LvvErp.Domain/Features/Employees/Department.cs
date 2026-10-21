using LvvErp.Domain.Common;

namespace LvvErp.Domain.Features.Employees;

/// <summary>Bộ phận / phòng ban.</summary>
public class Department : AuditableEntity
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>Chức danh.</summary>
public class Position : AuditableEntity
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public int SortOrder { get; set; }
}
