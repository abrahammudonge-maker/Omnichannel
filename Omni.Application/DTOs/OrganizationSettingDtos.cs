namespace Omni.Application.DTOs;

public sealed record CreateOrganizationSettingRequest(string SettingName, string SettingValue);
public sealed record OrganizationSettingDto(Guid Id, Guid OrganizationId, string SettingName, string SettingValue);
public sealed record UpdateOrganizationSettingRequest(string SettingName, string SettingValue);
