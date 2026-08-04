namespace Omni.Domain.Entities;

public sealed class OrganizationSetting
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public string SettingName { get; set; } = string.Empty;
    public string SettingValue { get; set; } = string.Empty;
}
