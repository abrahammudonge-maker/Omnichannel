using Omni.Domain.Enums;

namespace Omni.Domain.Entities;

public sealed class User
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public Role Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
