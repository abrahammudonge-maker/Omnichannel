using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;

namespace Omni.Infrastructure.Providers;

public sealed class WhatsAppTemplateSyncService : IWhatsAppTemplateSyncService
{
    private static readonly TimeSpan MinOnDemandInterval = TimeSpan.FromMinutes(1);
    private static readonly ConcurrentDictionary<Guid, DateTimeOffset> LastSyncByAccount = new();

    private readonly IMetaTemplateService _metaTemplateService;
    private readonly IMessageTemplateRepository _messageTemplateRepository;
    private readonly ILogger<WhatsAppTemplateSyncService> _logger;

    public WhatsAppTemplateSyncService(
        IMetaTemplateService metaTemplateService,
        IMessageTemplateRepository messageTemplateRepository,
        ILogger<WhatsAppTemplateSyncService> logger)
    {
        _metaTemplateService = metaTemplateService;
        _messageTemplateRepository = messageTemplateRepository;
        _logger = logger;
    }

    public bool CanSync(ChannelAccount account) =>
        account.ChannelType == "WhatsApp" && !string.IsNullOrWhiteSpace(account.ExternalWabaId) && !string.IsNullOrWhiteSpace(account.AccessToken);

    public async Task<int> SyncAsync(ChannelAccount account, CancellationToken cancellationToken)
    {
        LastSyncByAccount[account.Id] = DateTimeOffset.UtcNow;
        var templates = await _metaTemplateService.FetchTemplatesAsync(account.ExternalWabaId!, account.AccessToken!, cancellationToken);
        foreach (var template in templates)
        {
            await _messageTemplateRepository.UpsertAsync(new MessageTemplate
            {
                OrganizationId = account.OrganizationId,
                ChannelAccountId = account.Id,
                Name = template.Name,
                Language = template.Language,
                Category = template.Category,
                Status = template.Status,
                BodyText = template.BodyText,
                ComponentsJson = template.ComponentsJson
            }, cancellationToken);
        }
        return templates.Count;
    }

    public async Task<bool> TrySyncIfStaleAsync(ChannelAccount account, CancellationToken cancellationToken)
    {
        if (!CanSync(account)
            || (LastSyncByAccount.TryGetValue(account.Id, out var last) && DateTimeOffset.UtcNow - last < MinOnDemandInterval))
        {
            return false;
        }

        try
        {
            await SyncAsync(account, cancellationToken);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "On-demand template sync failed for channel account {ChannelAccountId}.", account.Id);
            return false;
        }
    }
}
