using Omni.Domain.Entities;

namespace Omni.Application.Interfaces;

public interface ICallRepository
{
    Task<Call?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Call>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Call>> GetByCustomerIdAsync(Guid customerId, Guid organizationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Call>> GetByConversationIdAsync(Guid conversationId, Guid organizationId, CancellationToken cancellationToken);

    /// <summary>Platform-wide lookup (not scoped to an organization) — used to identify which organization a webhook belongs to from the provider's own call id, matching IChannelAccountRepository.FindByExternalAccountIdAsync's role for message webhooks.</summary>
    Task<Call?> FindByProviderCallIdAsync(string provider, string providerCallId, CancellationToken cancellationToken);

    Task<int> CountActiveCallsForAgentAsync(Guid agentId, Guid organizationId, CancellationToken cancellationToken);
    Task<DateTimeOffset?> GetLastAssignedAtForAgentAsync(Guid agentId, Guid organizationId, CancellationToken cancellationToken);

    Task<Guid> CreateAsync(Call call, CancellationToken cancellationToken);
    Task UpdateAsync(Call call, CancellationToken cancellationToken);
}
