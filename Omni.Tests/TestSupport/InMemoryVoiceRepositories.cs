using Omni.Application.Interfaces;
using Omni.Domain.Entities;

namespace Omni.Tests.TestSupport;

// Lightweight in-memory fakes for the repository interfaces VoiceService depends on. This project has
// no mocking library installed, and a real fake (backed by a List<T>, enforcing the same org-scoping
// every real repository enforces) exercises actual multi-tenant behavior instead of just recording
// "was this method called" — a better fit for testing tenant isolation and state transitions.

public sealed class InMemoryCallRepository : ICallRepository
{
    public readonly List<Call> Calls = new();

    public Task<Call?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult(Calls.FirstOrDefault(c => c.Id == id && c.OrganizationId == organizationId));

    public Task<IReadOnlyList<Call>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Call>>(Calls.Where(c => c.OrganizationId == organizationId).ToList());

    public Task<IReadOnlyList<Call>> GetByCustomerIdAsync(Guid customerId, Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Call>>(Calls.Where(c => c.CustomerId == customerId && c.OrganizationId == organizationId).ToList());

    public Task<IReadOnlyList<Call>> GetByConversationIdAsync(Guid conversationId, Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Call>>(Calls.Where(c => c.ConversationId == conversationId && c.OrganizationId == organizationId).ToList());

    public Task<Call?> FindByProviderCallIdAsync(string provider, string providerCallId, CancellationToken cancellationToken) =>
        Task.FromResult(Calls.FirstOrDefault(c => c.Provider == provider && c.ProviderCallId == providerCallId));

    public Task<int> CountActiveCallsForAgentAsync(Guid agentId, Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult(Calls.Count(c => c.AgentId == agentId && c.OrganizationId == organizationId &&
            Omni.Domain.Constants.CallStatus.Active.Contains(c.Status)));

    public Task<DateTimeOffset?> GetLastAssignedAtForAgentAsync(Guid agentId, Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult(Calls.Where(c => c.AgentId == agentId && c.OrganizationId == organizationId)
            .Select(c => (DateTimeOffset?)c.CreatedAt).OrderByDescending(d => d).FirstOrDefault());

    public Task<Guid> CreateAsync(Call call, CancellationToken cancellationToken)
    {
        Calls.Add(call);
        return Task.FromResult(call.Id);
    }

    public Task UpdateAsync(Call call, CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class InMemoryPhoneNumberRepository : IPhoneNumberRepository
{
    public readonly List<PhoneNumber> Numbers = new();

    public Task<PhoneNumber?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult(Numbers.FirstOrDefault(n => n.Id == id && n.OrganizationId == organizationId));

    public Task<IReadOnlyList<PhoneNumber>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PhoneNumber>>(Numbers.Where(n => n.OrganizationId == organizationId).ToList());

    public Task<PhoneNumber?> FindByNumberAsync(string number, CancellationToken cancellationToken) =>
        Task.FromResult(Numbers.FirstOrDefault(n => n.Number == number));

    public Task<Guid> CreateAsync(PhoneNumber phoneNumber, CancellationToken cancellationToken)
    {
        Numbers.Add(phoneNumber);
        return Task.FromResult(phoneNumber.Id);
    }

    public Task UpdateAsync(PhoneNumber phoneNumber, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task DeleteAsync(Guid id, Guid organizationId, CancellationToken cancellationToken)
    {
        Numbers.RemoveAll(n => n.Id == id && n.OrganizationId == organizationId);
        return Task.CompletedTask;
    }
}

public sealed class InMemoryCustomerRepository : ICustomerRepository
{
    public readonly List<Customer> Customers = new();

    public Task<Customer?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult(Customers.FirstOrDefault(c => c.Id == id && c.OrganizationId == organizationId));

    public Task<IReadOnlyList<Customer>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Customer>>(Customers.Where(c => c.OrganizationId == organizationId).ToList());

    public Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Customers.FirstOrDefault(c => c.Id == id));

    public Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Customer>>(Customers.ToList());

    public Task<Guid> CreateAsync(Customer customer, CancellationToken cancellationToken)
    {
        Customers.Add(customer);
        return Task.FromResult(customer.Id);
    }

    public Task UpdateAsync(Customer customer, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task DeleteAsync(Guid id, Guid organizationId, CancellationToken cancellationToken)
    {
        Customers.RemoveAll(c => c.Id == id && c.OrganizationId == organizationId);
        return Task.CompletedTask;
    }
}

public sealed class InMemoryConversationRepository : IConversationRepository
{
    public readonly List<Conversation> Conversations = new();

    public Task<Conversation?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult(Conversations.FirstOrDefault(c => c.Id == id && c.OrganizationId == organizationId));

    public Task<IReadOnlyList<Conversation>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Conversation>>(Conversations.Where(c => c.OrganizationId == organizationId).ToList());

    public Task<Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Conversations.FirstOrDefault(c => c.Id == id));

    public Task<IReadOnlyList<Conversation>> GetAllAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Conversation>>(Conversations.ToList());

    public Task<Guid> CreateAsync(Conversation conversation, CancellationToken cancellationToken)
    {
        Conversations.Add(conversation);
        return Task.FromResult(conversation.Id);
    }

    public Task UpdateAsync(Conversation conversation, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task DeleteAsync(Guid id, Guid organizationId, CancellationToken cancellationToken)
    {
        Conversations.RemoveAll(c => c.Id == id && c.OrganizationId == organizationId);
        return Task.CompletedTask;
    }
}

public sealed class InMemoryCallQueueRepository : ICallQueueRepository
{
    public readonly List<CallQueue> Queues = new();

    public Task<CallQueue?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult(Queues.FirstOrDefault(q => q.Id == id && q.OrganizationId == organizationId));

    public Task<IReadOnlyList<CallQueue>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CallQueue>>(Queues.Where(q => q.OrganizationId == organizationId).ToList());

    public Task<Guid> CreateAsync(CallQueue queue, CancellationToken cancellationToken)
    {
        Queues.Add(queue);
        return Task.FromResult(queue.Id);
    }

    public Task UpdateAsync(CallQueue queue, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task DeleteAsync(Guid id, Guid organizationId, CancellationToken cancellationToken)
    {
        Queues.RemoveAll(q => q.Id == id && q.OrganizationId == organizationId);
        return Task.CompletedTask;
    }
}

public sealed class InMemoryCallQueueMemberRepository : ICallQueueMemberRepository
{
    public readonly List<CallQueueMember> Members = new();

    public Task<IReadOnlyList<CallQueueMember>> GetByQueueIdAsync(Guid queueId, Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CallQueueMember>>(Members.Where(m => m.QueueId == queueId && m.OrganizationId == organizationId).ToList());

    public Task<Guid> CreateAsync(CallQueueMember member, CancellationToken cancellationToken)
    {
        Members.Add(member);
        return Task.FromResult(member.Id);
    }

    public Task DeleteAsync(Guid id, Guid organizationId, CancellationToken cancellationToken)
    {
        Members.RemoveAll(m => m.Id == id && m.OrganizationId == organizationId);
        return Task.CompletedTask;
    }
}

public sealed class InMemoryCallEventRepository : ICallEventRepository
{
    public readonly List<CallEvent> Events = new();

    public Task<IReadOnlyList<CallEvent>> GetByCallIdAsync(Guid callId, Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CallEvent>>(Events.Where(e => e.CallId == callId && e.OrganizationId == organizationId).ToList());

    public Task<Guid> CreateAsync(CallEvent callEvent, CancellationToken cancellationToken)
    {
        Events.Add(callEvent);
        return Task.FromResult(callEvent.Id);
    }
}

public sealed class NoOpVoiceNotifier : IVoiceNotifier
{
    public readonly List<(Guid OrganizationId, string EventName, object Payload)> Notifications = new();

    public Task NotifyAsync(Guid organizationId, string eventName, object payload, CancellationToken cancellationToken)
    {
        Notifications.Add((organizationId, eventName, payload));
        return Task.CompletedTask;
    }
}
