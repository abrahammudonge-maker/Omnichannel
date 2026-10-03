using Omni.Domain.Entities;

namespace Omni.Application.Interfaces;

public interface IWhatsAppContactResolver
{
    Task<Customer> FindOrCreateCustomerAsync(Guid organizationId, string whatsAppNumber, string? displayName, CancellationToken cancellationToken);
    Task<Conversation> FindOrCreateConversationAsync(Guid organizationId, Guid customerId, Guid channelAccountId, CancellationToken cancellationToken);
}
