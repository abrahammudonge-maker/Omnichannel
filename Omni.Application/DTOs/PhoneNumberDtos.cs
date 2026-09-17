namespace Omni.Application.DTOs;

public sealed record CreatePhoneNumberRequest(string Number, string Provider, string? ProviderNumberId, string DisplayName, string? Country, string Status = "Active");
public sealed record UpdatePhoneNumberRequest(string Number, string Provider, string? ProviderNumberId, string DisplayName, string? Country, string Status);
