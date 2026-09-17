namespace Omni.Application.DTOs;

public sealed record UpdateOrganizationRequest(string Name, string Email, string Phone, string Country, string Status);

public sealed record AdminCreateCustomerRequest(Guid OrganizationId, string FullName, string Phone, string Email, string? FacebookId, string? InstagramId, string? WhatsAppNumber);
public sealed record AdminUpdateCustomerRequest(string FullName, string Phone, string Email, string? FacebookId, string? InstagramId, string? WhatsAppNumber);
public sealed record AdminCustomerView(Guid Id, Guid OrganizationId, string OrganizationName, string FullName, string Phone, string Email, string? FacebookId, string? InstagramId, string? WhatsAppNumber, DateTimeOffset CreatedAt);

public sealed record AdminCreateConversationRequest(Guid OrganizationId, Guid CustomerId, string Channel, string Status, Guid? AssignedUserId);
public sealed record AdminUpdateConversationRequest(string Status, Guid? AssignedUserId);
public sealed record AdminConversationView(Guid Id, Guid OrganizationId, string OrganizationName, Guid CustomerId, string CustomerName, string Channel, string Status, Guid? AssignedUserId, string? AssignedUserName, DateTimeOffset CreatedAt);

public sealed record AdminCreateDepartmentRequest(Guid OrganizationId, string Name, string? Description, bool IsActive = true);
public sealed record AdminUpdateDepartmentRequest(string Name, string? Description, bool IsActive = true);
public sealed record AdminDepartmentView(Guid Id, Guid OrganizationId, string OrganizationName, string Name, string? Description, bool IsActive, DateTimeOffset CreatedAt);

public sealed record AdminCreateTeamRequest(Guid OrganizationId, Guid DepartmentId, string Name, string? Description, Guid? LeaderId, bool IsActive = true);
public sealed record AdminUpdateTeamRequest(Guid DepartmentId, string Name, string? Description, Guid? LeaderId, bool IsActive = true);
public sealed record AdminTeamView(Guid Id, Guid OrganizationId, string OrganizationName, Guid DepartmentId, string DepartmentName, string Name, string? Description, Guid? LeaderId, string? LeaderName, bool IsActive, DateTimeOffset CreatedAt);

public sealed record AdminCreateOrganizationSettingRequest(Guid OrganizationId, string SettingName, string SettingValue);
public sealed record AdminUpdateOrganizationSettingRequest(string SettingName, string SettingValue);
public sealed record AdminOrganizationSettingView(Guid Id, Guid OrganizationId, string OrganizationName, string SettingName, string SettingValue);

/// <summary>Channel account without secrets (AccessToken/RefreshToken/WebhookSecret) — safe for any agent to read, not just org admins.</summary>
public sealed record ChannelAccountView(Guid Id, Guid OrganizationId, string ChannelType, string DisplayName, string? ExternalAccountId, string? ExternalWabaId, string? SmtpHost, int? SmtpPort, string? ImapHost, int? ImapPort, string Status, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

/// <summary>User without PasswordHash — safe for any agent to read (e.g. to populate an assignee picker), not just org admins.</summary>
public sealed record TeamMemberView(Guid Id, Guid OrganizationId, string FirstName, string LastName, string Email, string Role, bool IsActive, DateTimeOffset CreatedAt);
