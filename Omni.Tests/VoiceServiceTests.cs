using Microsoft.Extensions.Logging.Abstractions;
using Omni.Application.Interfaces;
using Omni.Application.Services;
using Omni.Domain.Constants;
using Omni.Domain.Entities;
using Omni.Domain.Enums;
using Omni.Infrastructure.Providers;
using Omni.Tests.TestSupport;

namespace Omni.Tests;

public sealed class VoiceServiceTests
{
    private sealed record Harness(
        VoiceService Service,
        InMemoryCallRepository Calls,
        InMemoryCallEventRepository Events,
        InMemoryPhoneNumberRepository PhoneNumbers,
        InMemoryCustomerRepository Customers,
        InMemoryConversationRepository Conversations,
        InMemoryCallQueueRepository Queues,
        InMemoryCallQueueMemberRepository QueueMembers,
        NoOpVoiceNotifier Notifier);

    private static Harness CreateHarness()
    {
        var calls = new InMemoryCallRepository();
        var events = new InMemoryCallEventRepository();
        var phoneNumbers = new InMemoryPhoneNumberRepository();
        var customers = new InMemoryCustomerRepository();
        var conversations = new InMemoryConversationRepository();
        var queues = new InMemoryCallQueueRepository();
        var queueMembers = new InMemoryCallQueueMemberRepository();
        var notifier = new NoOpVoiceNotifier();
        var providerFactory = new VoiceProviderFactory(new IVoiceProvider[] { new FakeVoiceProvider() });

        var service = new VoiceService(
            calls, events, phoneNumbers, customers, conversations, queues, queueMembers,
            providerFactory, notifier, NullLogger<VoiceService>.Instance);

        return new Harness(service, calls, events, phoneNumbers, customers, conversations, queues, queueMembers, notifier);
    }

    private static (Guid OrganizationId, PhoneNumber Number, Customer Customer) SeedOrgWithPhoneAndCustomer(Harness h, string customerPhone = "+15550001111")
    {
        var organizationId = Guid.NewGuid();
        var number = new PhoneNumber { OrganizationId = organizationId, Number = "+15559990000", Provider = VoiceProviderName.Fake, DisplayName = "Support", Status = "Active" };
        var customer = new Customer { OrganizationId = organizationId, FullName = "Jane Doe", Phone = customerPhone, Email = "" };
        h.PhoneNumbers.Numbers.Add(number);
        h.Customers.Customers.Add(customer);
        return (organizationId, number, customer);
    }

    [Fact]
    public async Task InitiateCallAsync_CreatesCall_AndStoresProviderCallId_OnSuccess()
    {
        var h = CreateHarness();
        var (orgId, number, customer) = SeedOrgWithPhoneAndCustomer(h);
        var agentId = Guid.NewGuid();

        var result = await h.Service.InitiateCallAsync(orgId, agentId, customer.Id, number.Id, null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Call);
        Assert.Equal(CallDirection.Outbound, result.Call!.Direction);
        Assert.False(string.IsNullOrEmpty(result.Call.ProviderCallId));
        Assert.Equal(customer.Phone, result.Call.ToNumber);
        Assert.Single(h.Calls.Calls);
    }

    [Fact]
    public async Task InitiateCallAsync_ReturnsNotFound_ForPhoneNumberBelongingToAnotherOrganization()
    {
        var h = CreateHarness();
        var (orgId, number, customer) = SeedOrgWithPhoneAndCustomer(h);
        var attackerOrgId = Guid.NewGuid();

        var result = await h.Service.InitiateCallAsync(attackerOrgId, Guid.NewGuid(), customer.Id, number.Id, null, CancellationToken.None);

        Assert.True(result.NotFound);
        Assert.False(result.Success);
        Assert.Empty(h.Calls.Calls);
    }

    [Fact]
    public async Task InitiateCallAsync_Fails_WhenCustomerHasNoPhoneNumber()
    {
        var h = CreateHarness();
        var (orgId, number, _) = SeedOrgWithPhoneAndCustomer(h);
        var customerWithoutPhone = new Customer { OrganizationId = orgId, FullName = "No Phone", Phone = "", Email = "" };
        h.Customers.Customers.Add(customerWithoutPhone);

        var result = await h.Service.InitiateCallAsync(orgId, Guid.NewGuid(), customerWithoutPhone.Id, number.Id, null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(result.NotFound);
    }

    [Fact]
    public async Task HangupAsync_SetsCompletedStatusAndComputesDuration()
    {
        var h = CreateHarness();
        var (orgId, number, customer) = SeedOrgWithPhoneAndCustomer(h);
        var initiated = await h.Service.InitiateCallAsync(orgId, Guid.NewGuid(), customer.Id, number.Id, null, CancellationToken.None);
        var call = initiated.Call!;
        call.AnsweredAt = DateTimeOffset.UtcNow.AddSeconds(-30);

        var result = await h.Service.HangupAsync(orgId, call.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(CallStatus.Completed, result.Call!.Status);
        Assert.NotNull(result.Call.EndedAt);
        Assert.True(result.Call.DurationSeconds >= 29);
    }

    [Fact]
    public async Task HangupAsync_ReturnsNotFound_ForCallInAnotherOrganization()
    {
        var h = CreateHarness();
        var (orgId, number, customer) = SeedOrgWithPhoneAndCustomer(h);
        var initiated = await h.Service.InitiateCallAsync(orgId, Guid.NewGuid(), customer.Id, number.Id, null, CancellationToken.None);

        var result = await h.Service.HangupAsync(Guid.NewGuid(), initiated.Call!.Id, CancellationToken.None);

        Assert.True(result.NotFound);
    }

    [Fact]
    public async Task HoldAsync_ThenResumeAsync_TransitionsStatusBothWays()
    {
        var h = CreateHarness();
        var (orgId, number, customer) = SeedOrgWithPhoneAndCustomer(h);
        var initiated = await h.Service.InitiateCallAsync(orgId, Guid.NewGuid(), customer.Id, number.Id, null, CancellationToken.None);
        var callId = initiated.Call!.Id;

        var held = await h.Service.HoldAsync(orgId, callId, CancellationToken.None);
        Assert.Equal(CallStatus.OnHold, held.Call!.Status);

        var resumed = await h.Service.ResumeAsync(orgId, callId, CancellationToken.None);
        Assert.Equal(CallStatus.Answered, resumed.Call!.Status);
    }

    [Fact]
    public async Task SelectAgentForQueueAsync_Manual_NeverAutoAssigns()
    {
        var h = CreateHarness();
        var orgId = Guid.NewGuid();
        var queue = new CallQueue { OrganizationId = orgId, Name = "Manual queue", Strategy = CallQueueStrategy.Manual, IsActive = true };
        h.Queues.Queues.Add(queue);
        h.QueueMembers.Members.Add(new CallQueueMember { OrganizationId = orgId, QueueId = queue.Id, UserId = Guid.NewGuid(), IsActive = true });

        var selected = await h.Service.SelectAgentForQueueAsync(queue.Id, orgId, CancellationToken.None);

        Assert.Null(selected);
    }

    [Fact]
    public async Task SelectAgentForQueueAsync_LeastBusy_PicksAgentWithFewestActiveCalls()
    {
        var h = CreateHarness();
        var (orgId, number, customer) = SeedOrgWithPhoneAndCustomer(h);
        var busyAgent = Guid.NewGuid();
        var idleAgent = Guid.NewGuid();

        var queue = new CallQueue { OrganizationId = orgId, Name = "Support", Strategy = CallQueueStrategy.LeastBusy, IsActive = true };
        h.Queues.Queues.Add(queue);
        h.QueueMembers.Members.Add(new CallQueueMember { OrganizationId = orgId, QueueId = queue.Id, UserId = busyAgent, IsActive = true });
        h.QueueMembers.Members.Add(new CallQueueMember { OrganizationId = orgId, QueueId = queue.Id, UserId = idleAgent, IsActive = true });

        // Give the busy agent two currently-active calls.
        h.Calls.Calls.Add(new Call { OrganizationId = orgId, CustomerId = customer.Id, AgentId = busyAgent, Provider = VoiceProviderName.Fake, Direction = CallDirection.Outbound, FromNumber = number.Number, ToNumber = customer.Phone, Status = CallStatus.Answered });
        h.Calls.Calls.Add(new Call { OrganizationId = orgId, CustomerId = customer.Id, AgentId = busyAgent, Provider = VoiceProviderName.Fake, Direction = CallDirection.Outbound, FromNumber = number.Number, ToNumber = customer.Phone, Status = CallStatus.Ringing });

        var selected = await h.Service.SelectAgentForQueueAsync(queue.Id, orgId, CancellationToken.None);

        Assert.Equal(idleAgent, selected);
    }

    [Fact]
    public async Task SelectAgentForQueueAsync_RoundRobin_PicksLeastRecentlyAssignedAgent()
    {
        var h = CreateHarness();
        var (orgId, number, customer) = SeedOrgWithPhoneAndCustomer(h);
        var recentlyAssignedAgent = Guid.NewGuid();
        var neverAssignedAgent = Guid.NewGuid();

        var queue = new CallQueue { OrganizationId = orgId, Name = "Sales", Strategy = CallQueueStrategy.RoundRobin, IsActive = true };
        h.Queues.Queues.Add(queue);
        h.QueueMembers.Members.Add(new CallQueueMember { OrganizationId = orgId, QueueId = queue.Id, UserId = recentlyAssignedAgent, IsActive = true });
        h.QueueMembers.Members.Add(new CallQueueMember { OrganizationId = orgId, QueueId = queue.Id, UserId = neverAssignedAgent, IsActive = true });

        h.Calls.Calls.Add(new Call { OrganizationId = orgId, CustomerId = customer.Id, AgentId = recentlyAssignedAgent, Provider = VoiceProviderName.Fake, Direction = CallDirection.Outbound, FromNumber = number.Number, ToNumber = customer.Phone, Status = CallStatus.Completed });

        var selected = await h.Service.SelectAgentForQueueAsync(queue.Id, orgId, CancellationToken.None);

        Assert.Equal(neverAssignedAgent, selected);
    }

    [Fact]
    public async Task ProcessWebhookAsync_CreatesNewInboundCall_WhenNoExistingCallMatchesProviderCallId()
    {
        var h = CreateHarness();
        var orgId = Guid.NewGuid();
        var number = new PhoneNumber { OrganizationId = orgId, Number = "+15559990000", Provider = VoiceProviderName.Fake, DisplayName = "Support", Status = "Active" };
        h.PhoneNumbers.Numbers.Add(number);

        var webhookEvent = new VoiceWebhookEvent(
            ProviderCallId: "inbound-call-1",
            EventType: "status",
            Status: CallStatus.Ringing,
            Timestamp: DateTimeOffset.UtcNow,
            RecordingUrl: null,
            RecordingStatus: null,
            DurationSeconds: null,
            FromNumber: "+15551234567",
            ToNumber: number.Number,
            Direction: CallDirection.Inbound);

        await h.Service.ProcessWebhookAsync(VoiceProviderName.Fake, webhookEvent, "{}", CancellationToken.None);

        var created = Assert.Single(h.Calls.Calls);
        Assert.Equal(orgId, created.OrganizationId);
        Assert.Equal(CallDirection.Inbound, created.Direction);
        Assert.Equal("+15551234567", created.FromNumber);
        Assert.Single(h.Customers.Customers);
        Assert.Single(h.Conversations.Conversations);
        Assert.Single(h.Events.Events);
    }

    [Fact]
    public async Task ProcessWebhookAsync_UpdatesExistingCall_WhenProviderCallIdMatches()
    {
        var h = CreateHarness();
        var (orgId, number, customer) = SeedOrgWithPhoneAndCustomer(h);
        var initiated = await h.Service.InitiateCallAsync(orgId, Guid.NewGuid(), customer.Id, number.Id, null, CancellationToken.None);
        var providerCallId = initiated.Call!.ProviderCallId!;

        var webhookEvent = new VoiceWebhookEvent(providerCallId, "status", CallStatus.Answered, DateTimeOffset.UtcNow, null, null, null);
        await h.Service.ProcessWebhookAsync(VoiceProviderName.Fake, webhookEvent, "{}", CancellationToken.None);

        var updated = await h.Calls.GetByIdAsync(initiated.Call.Id, orgId, CancellationToken.None);
        Assert.Equal(CallStatus.Answered, updated!.Status);
        Assert.NotNull(updated.AnsweredAt);
        Assert.Single(h.Calls.Calls); // no duplicate call created
    }

    [Fact]
    public async Task ProcessWebhookAsync_DoesNothing_WhenToNumberMatchesNoPhoneNumber()
    {
        var h = CreateHarness();

        var webhookEvent = new VoiceWebhookEvent("unknown-call", "status", CallStatus.Ringing, DateTimeOffset.UtcNow, null, null, null, "+15550000000", "+15559999999", CallDirection.Inbound);
        await h.Service.ProcessWebhookAsync(VoiceProviderName.Fake, webhookEvent, "{}", CancellationToken.None);

        Assert.Empty(h.Calls.Calls);
    }
}
