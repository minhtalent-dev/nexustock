namespace Nexustock.Modules.Identity.Entities;

public class RolePermission
{
    public Guid RoleId { get; set; }
    public Guid PermissionId { get; set; }
    public Guid TenantId { get; set; }

    // Navigation
    public ApplicationRole Role { get; set; } = null!;
    public Permission Permission { get; set; } = null!;
}
