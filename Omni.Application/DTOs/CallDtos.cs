namespace Omni.Application.DTOs;

public sealed record InitiateCallApiRequest(Guid CustomerId, Guid PhoneNumberId, Guid? ConversationId = null);
public sealed record TransferCallRequest(string Destination);
