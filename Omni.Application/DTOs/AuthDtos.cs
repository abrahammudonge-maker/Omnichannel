namespace Omni.Application.DTOs;

public sealed record LoginRequest(string Email, string Password);
public sealed record RegisterRequest(string OrganizationName, string AdminFirstName, string AdminLastName, string Email, string Password, string Phone, string Country);
public sealed record LoginResponse(string AccessToken, string RefreshToken, Guid UserId, Guid OrganizationId, string Role);
public sealed record RefreshTokenRequest(string RefreshToken);
public sealed record CreateOrganizationRequest(string Name, string Email, string Phone, string Country);
public sealed record CreateUserRequest(Guid OrganizationId, string FirstName, string LastName, string Email, string Password, string Role);
public sealed record CreateCustomerRequest(string FullName, string Phone, string Email, string? FacebookId, string? InstagramId, string? WhatsAppNumber);
public sealed record CreateConversationRequest(Guid CustomerId, string Channel, string Status, Guid? AssignedUserId);
public sealed record CreateMessageRequest(Guid ConversationId, string Direction, string MessageType, string Body, string? AttachmentUrl, string Status);
