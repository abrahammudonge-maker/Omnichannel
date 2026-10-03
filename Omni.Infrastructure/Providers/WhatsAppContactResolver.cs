using Omni.Application.Interfaces;
using Omni.Domain.Entities;
using Omni.Domain.Enums;
using Omni.Domain.ValueObjects;

namespace Omni.Infrastructure.Providers;

public sealed class WhatsAppContactResolver : IWhatsAppContactResolver
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IConversationRepository _conversationRepository;

    public WhatsAppContactResolver(ICustomerRepository customerRepository, IConversationRepository conversationRepository)
    {
        _customerRepository = customerRepository;
        _conversationRepository = conversationRepository;
    }

    public async Task<Customer> FindOrCreateCustomerAsync(Guid organizationId, string whatsAppNumber, string? displayName, CancellationToken cancellationToken)
    {
        var customers = await _customerRepository.GetAllAsync(organizationId, cancellationToken);
        var existing = customers.FirstOrDefault(c => WhatsAppNumber.AreEqual(c.WhatsAppNumber, whatsAppNumber));
        if (existing is not null)
        {
            return existing;
        }

        var normalizedNumber = WhatsAppNumber.Normalize(whatsAppNumber)!;
        var customer = new Customer
        {
            OrganizationId = organizationId,
            FullName = string.IsNullOrWhiteSpace(displayName) ? normalizedNumber : displayName,
            Phone = string.Empty,
            Email = string.Empty,
            WhatsAppNumber = normalizedNumber
        };
        await _customerRepository.CreateAsync(customer, cancellationToken);
        return customer;
    }

    public async Task<Conversation> FindOrCreateConversationAsync(Guid organizationId, Guid customerId, Guid channelAccountId, CancellationToken cancellationToken)
    {
        var conversations = await _conversationRepository.GetAllAsync(organizationId, cancellationToken);
        // Must use the same rule as MetaWebhookController.FindOrCreateConversationAsync (open conversations only),
        // otherwise a template sent into a closed conversation gets its reply filed under a brand-new one.
        var existing = conversations.FirstOrDefault(c =>
            c.CustomerId == customerId &&
            c.ChannelAccountId == channelAccountId &&
            c.Channel == Channel.WhatsApp &&
            c.Status is not ("Closed" or "Resolved"));
        if (existing is not null)
        {
            return existing;
        }

        var conversation = new Conversation
        {
            OrganizationId = organizationId,
            CustomerId = customerId,
            ChannelAccountId = channelAccountId,
            Channel = Channel.WhatsApp,
            Status = "Open"
        };
        await _conversationRepository.CreateAsync(conversation, cancellationToken);
        return conversation;
    }
}
