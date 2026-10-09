using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;

namespace Omni.Infrastructure.Providers;

/// <summary>Lets the API wake the dispatcher the moment it queues rows, instead of waiting for the next poll.</summary>
public sealed class TemplateSendSignal : ITemplateSendSignal
{
    private readonly SemaphoreSlim _signal = new(0, 1);

    public void Notify()
    {
        try
        {
            _signal.Release();
        }
        catch (SemaphoreFullException)
        {
            // already signalled
        }
    }

    public Task WaitAsync(TimeSpan timeout, CancellationToken cancellationToken) => _signal.WaitAsync(timeout, cancellationToken);
}

/// <summary>
/// Sends queued template_sends rows. Different WhatsApp numbers are sent in parallel (each number is
/// still paced by GraphApiMessageSender). Throttling, Meta outages and network errors are retried with
/// backoff; anything else fails the row with Meta's reason. Rows survive restarts because the queue is
/// the database.
/// </summary>
public sealed class TemplateSendDispatcher : BackgroundService
{
    private const int ClaimBatchSize = 50;
    private static readonly TimeSpan IdlePollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StuckAfter = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan[] RetryDelays =
    {
        TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(30)
    };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TemplateSendSignal _signal;
    private readonly ILogger<TemplateSendDispatcher> _logger;

    public TemplateSendDispatcher(IServiceScopeFactory scopeFactory, TemplateSendSignal signal, ILogger<TemplateSendDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _signal = signal;
        _logger = logger;
    }

    public static int MaxAttempts => RetryDelays.Length + 1;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var nextStuckCheck = DateTimeOffset.MinValue;
        while (!stoppingToken.IsCancellationRequested)
        {
            var claimedAny = false;
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<ITemplateSendRepository>();

                if (DateTimeOffset.UtcNow >= nextStuckCheck)
                {
                    var stuck = await repository.FailStuckAsync(DateTimeOffset.UtcNow - StuckAfter, stoppingToken);
                    if (stuck > 0) _logger.LogWarning("Marked {Count} interrupted template send(s) as failed.", stuck);
                    nextStuckCheck = DateTimeOffset.UtcNow.AddMinutes(1);
                }

                var claimed = await repository.ClaimDueAsync(ClaimBatchSize, stoppingToken);
                claimedAny = claimed.Count > 0;

                // In-flight sends get CancellationToken.None so a shutdown doesn't abandon a message Meta may already have.
                await Task.WhenAll(claimed.GroupBy(s => s.ChannelAccountId).Select(group => SendGroupAsync(group.ToList())));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Template send dispatch cycle failed.");
            }

            if (claimedAny)
            {
                continue; // more may be waiting
            }

            try
            {
                await _signal.WaitAsync(IdlePollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task SendGroupAsync(IReadOnlyList<TemplateSend> sends)
    {
        using var scope = _scopeFactory.CreateScope();
        var services = scope.ServiceProvider;
        var repository = services.GetRequiredService<ITemplateSendRepository>();
        var channelAccountRepository = services.GetRequiredService<IChannelAccountRepository>();
        var templateRepository = services.GetRequiredService<IMessageTemplateRepository>();
        var contactResolver = services.GetRequiredService<IWhatsAppContactResolver>();
        var templateMessageService = services.GetRequiredService<ITemplateMessageService>();

        var first = sends[0];
        var account = await channelAccountRepository.GetByIdAsync(first.ChannelAccountId, first.OrganizationId, CancellationToken.None);
        var templates = new Dictionary<Guid, MessageTemplate?>();

        foreach (var send in sends)
        {
            Guid? conversationId = send.ConversationId;
            TemplateSendResult? accepted = null;
            try
            {
                if (account is null || account.ChannelType != "WhatsApp" || account.Status != "Active"
                    || string.IsNullOrWhiteSpace(account.AccessToken) || string.IsNullOrWhiteSpace(account.ExternalAccountId))
                {
                    await repository.MarkFailedAsync(send.Id, "The WhatsApp number this message sends from is no longer connected.", null, null, CancellationToken.None);
                    continue;
                }

                if (!templates.TryGetValue(send.TemplateId, out var template))
                {
                    template = await templateRepository.GetByIdAsync(send.TemplateId, send.OrganizationId, CancellationToken.None);
                    templates[send.TemplateId] = template;
                }
                if (template is null || !string.Equals(template.Status, "APPROVED", StringComparison.OrdinalIgnoreCase))
                {
                    await repository.MarkFailedAsync(send.Id, $"The template is no longer approved (status: {template?.Status ?? "deleted"}).", null, null, CancellationToken.None);
                    continue;
                }

                var parameters = JsonSerializer.Deserialize<List<string>>(send.BodyParametersJson) ?? new List<string>();
                var customer = await contactResolver.FindOrCreateCustomerAsync(send.OrganizationId, send.PhoneNumber, send.CustomerName, CancellationToken.None);
                var conversation = await contactResolver.FindOrCreateConversationAsync(send.OrganizationId, customer.Id, account.Id, CancellationToken.None);
                conversationId = conversation.Id;

                var result = await templateMessageService.SendAsync(
                    send.OrganizationId, conversation.Id, account, template, send.PhoneNumber, parameters, CancellationToken.None, send.MessageId);

                if (result.Success)
                {
                    accepted = result;
                    await repository.MarkSentAsync(send.Id, conversation.Id, result.MessageId, result.ExternalMessageId, CancellationToken.None);
                }
                else if (result.Transient && send.Attempts < MaxAttempts)
                {
                    var retryAt = DateTimeOffset.UtcNow + RetryDelays[send.Attempts - 1];
                    await repository.RescheduleAsync(send.Id, retryAt, result.ErrorMessage ?? "Temporary failure.", conversation.Id, result.MessageId, CancellationToken.None);
                }
                else
                {
                    await repository.MarkFailedAsync(send.Id, result.ErrorMessage ?? "Send failed.", conversation.Id, result.MessageId, CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                // Our own failure (e.g. the database blipped): retry like a transient Meta error.
                _logger.LogError(ex, "Template send {TemplateSendId} failed unexpectedly.", send.Id);
                await TryRecordFailureAsync(repository, send, conversationId, accepted, ex.Message);
            }
        }
    }

    private async Task TryRecordFailureAsync(ITemplateSendRepository repository, TemplateSend send, Guid? conversationId, TemplateSendResult? accepted, string error)
    {
        try
        {
            if (accepted is not null && conversationId is not null)
            {
                // Meta already has the message; never queue it again, just try once more to record that.
                await repository.MarkSentAsync(send.Id, conversationId.Value, accepted.MessageId, accepted.ExternalMessageId, CancellationToken.None);
            }
            else if (send.Attempts < MaxAttempts)
            {
                await repository.RescheduleAsync(send.Id, DateTimeOffset.UtcNow + RetryDelays[send.Attempts - 1], error, conversationId, null, CancellationToken.None);
            }
            else
            {
                await repository.MarkFailedAsync(send.Id, error, conversationId, null, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            // Left in "sending"; the stuck-row sweep fails it after StuckAfter.
            _logger.LogError(ex, "Could not record the failure of template send {TemplateSendId}.", send.Id);
        }
    }
}
