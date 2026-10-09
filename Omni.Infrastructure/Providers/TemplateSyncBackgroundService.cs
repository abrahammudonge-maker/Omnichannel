using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Omni.Application.Interfaces;

namespace Omni.Infrastructure.Providers;

/// <summary>
/// Keeps every WhatsApp number's template catalog current, so templates Meta approves (or pauses, or
/// rejects) show up for agents and API callers without an admin pressing Sync.
/// </summary>
public sealed class TemplateSyncBackgroundService : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan SyncInterval = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TemplateSyncBackgroundService> _logger;

    public TemplateSyncBackgroundService(IServiceScopeFactory scopeFactory, ILogger<TemplateSyncBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await SyncAllAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Template sync cycle failed.");
                }

                await Task.Delay(SyncInterval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
    }

    private async Task SyncAllAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var organizations = await scope.ServiceProvider.GetRequiredService<IOrganizationRepository>().GetAllAsync(cancellationToken);
        var channelAccountRepository = scope.ServiceProvider.GetRequiredService<IChannelAccountRepository>();
        var syncService = scope.ServiceProvider.GetRequiredService<IWhatsAppTemplateSyncService>();

        foreach (var organization in organizations)
        {
            var accounts = await channelAccountRepository.GetAllAsync(organization.Id, cancellationToken);
            foreach (var account in accounts.Where(a => a.Status == "Active" && syncService.CanSync(a)))
            {
                try
                {
                    await syncService.SyncAsync(account, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Template sync failed for channel account {ChannelAccountId}.", account.Id);
                }
            }
        }
    }
}
