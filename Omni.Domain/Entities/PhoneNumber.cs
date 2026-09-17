namespace Omni.Domain.Entities;

public sealed class PhoneNumber
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public string Number { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string? ProviderNumberId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Country { get; set; }
    public string Status { get; set; } = "Active";
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}
