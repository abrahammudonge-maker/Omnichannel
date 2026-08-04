namespace Omni.Domain.Entities;

public sealed class Organization
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
