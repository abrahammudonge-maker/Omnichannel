using System.Text.RegularExpressions;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Omni.Application.Interfaces;
using Omni.Domain.Entities;
using Omni.Domain.Enums;

namespace Omni.Infrastructure.Providers;

public sealed class EmailInboxPollingService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private static readonly Regex[] QuoteMarkers =
    {
        new(@"^On .+ wrote:\s*$", RegexOptions.IgnoreCase),
        new(@"^-{2,}\s*Original Message\s*-{2,}$", RegexOptions.IgnoreCase),
        new(@"^From:\s.+$", RegexOptions.IgnoreCase)
    };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailInboxPollingService> _logger;

    public EmailInboxPollingService(IServiceScopeFactory scopeFactory, ILogger<EmailInboxPollingService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollAllMailboxesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Email inbox polling cycle failed.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // shutting down
            }
        }
    }

    private async Task PollAllMailboxesAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var organizationRepository = scope.ServiceProvider.GetRequiredService<IOrganizationRepository>();
        var channelAccountRepository = scope.ServiceProvider.GetRequiredService<IChannelAccountRepository>();

        var organizations = await organizationRepository.GetAllAsync(cancellationToken);

        foreach (var organization in organizations)
        {
            var channelAccounts = await channelAccountRepository.GetAllAsync(organization.Id, cancellationToken);
            var emailAccounts = channelAccounts.Where(a =>
                a.ChannelType == "Email" &&
                a.Status == "Active" &&
                !string.IsNullOrWhiteSpace(a.ExternalAccountId) &&
                !string.IsNullOrWhiteSpace(a.AccessToken));

            foreach (var account in emailAccounts)
            {
                try
                {
                    await PollMailboxAsync(organization, account, scope.ServiceProvider, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to poll mailbox {Mailbox} for organization {OrganizationId}", account.ExternalAccountId, organization.Id);
                }
            }
        }
    }

    private async Task PollMailboxAsync(Organization organization, ChannelAccount account, IServiceProvider services, CancellationToken cancellationToken)
    {
        var preset = EmailProviderPresets.Resolve(account.ExternalAccountId!);
        var imapHost = account.ImapHost ?? preset?.ImapHost;
        var imapPort = account.ImapPort ?? preset?.ImapPort;
        if (string.IsNullOrWhiteSpace(imapHost) || imapPort is null)
        {
            _logger.LogWarning("No IMAP host/port configured or recognized for {Mailbox} — set it under Settings → Channels.", account.ExternalAccountId);
            return;
        }

        using var client = new ImapClient();
        await client.ConnectAsync(imapHost, imapPort.Value, SecureSocketOptions.Auto, cancellationToken);
        await client.AuthenticateAsync(account.ExternalAccountId!, account.AccessToken!, cancellationToken);

        var inbox = client.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadWrite, cancellationToken);

        var unseenUids = await inbox.SearchAsync(SearchQuery.NotSeen, cancellationToken);
        if (unseenUids.Count > 0)
        {
            _logger.LogInformation("Found {Count} new email(s) for {Mailbox}", unseenUids.Count, account.ExternalAccountId);
        }

        var customerRepository = services.GetRequiredService<ICustomerRepository>();
        var conversationRepository = services.GetRequiredService<IConversationRepository>();
        var messageRepository = services.GetRequiredService<IMessageRepository>();
        var notificationRepository = services.GetRequiredService<INotificationRepository>();
        var userRepository = services.GetRequiredService<IUserRepository>();

        foreach (var uid in unseenUids)
        {
            var mimeMessage = await inbox.GetMessageAsync(uid, cancellationToken);
            var sender = mimeMessage.From.Mailboxes.FirstOrDefault();
            if (sender is null)
            {
                await inbox.AddFlagsAsync(uid, MessageFlags.Seen, true, cancellationToken);
                continue;
            }

            var customer = await FindOrCreateCustomerAsync(customerRepository, organization.Id, sender.Address, sender.Name, cancellationToken);
            var conversation = await FindOrCreateConversationAsync(conversationRepository, organization.Id, customer.Id, cancellationToken);

            var rawBody = mimeMessage.TextBody ?? StripHtml(mimeMessage.HtmlBody) ?? "(no content)";
            var body = StripQuotedReply(rawBody);

            await messageRepository.CreateAsync(new Message
            {
                OrganizationId = organization.Id,
                ConversationId = conversation.Id,
                Direction = "Inbound",
                MessageType = "Text",
                Body = body,
                Status = "Received",
                SentAt = DateTimeOffset.UtcNow
            }, cancellationToken);

            await NotifyAsync(notificationRepository, userRepository, organization.Id, conversation, customer, body, cancellationToken);

            await inbox.AddFlagsAsync(uid, MessageFlags.Seen, true, cancellationToken);
        }

        await client.DisconnectAsync(true, cancellationToken);
    }

    private static async Task NotifyAsync(
        INotificationRepository notificationRepository,
        IUserRepository userRepository,
        Guid organizationId,
        Conversation conversation,
        Customer customer,
        string body,
        CancellationToken cancellationToken)
    {
        var title = $"New email from {customer.FullName}";
        var preview = body.Length > 140 ? body[..140] + "…" : body;

        var recipientIds = new List<Guid>();
        if (conversation.AssignedUserId is Guid assignedUserId)
        {
            recipientIds.Add(assignedUserId);
        }
        else
        {
            var users = await userRepository.GetAllAsync(organizationId, cancellationToken);
            recipientIds.AddRange(users.Where(u => u.IsActive).Select(u => u.Id));
        }

        foreach (var userId in recipientIds)
        {
            await notificationRepository.CreateAsync(new Notification
            {
                OrganizationId = organizationId,
                UserId = userId,
                Title = title,
                Message = preview,
                IsRead = false
            }, cancellationToken);
        }
    }

    private static async Task<Customer> FindOrCreateCustomerAsync(ICustomerRepository customerRepository, Guid organizationId, string email, string? displayName, CancellationToken cancellationToken)
    {
        var customers = await customerRepository.GetAllAsync(organizationId, cancellationToken);
        var existing = customers.FirstOrDefault(c => string.Equals(c.Email, email, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return existing;
        }

        var customer = new Customer
        {
            OrganizationId = organizationId,
            FullName = string.IsNullOrWhiteSpace(displayName) ? email : displayName,
            Email = email,
            Phone = string.Empty
        };
        await customerRepository.CreateAsync(customer, cancellationToken);
        return customer;
    }

    private static async Task<Conversation> FindOrCreateConversationAsync(IConversationRepository conversationRepository, Guid organizationId, Guid customerId, CancellationToken cancellationToken)
    {
        var conversations = await conversationRepository.GetAllAsync(organizationId, cancellationToken);
        var open = conversations.FirstOrDefault(c =>
            c.CustomerId == customerId &&
            c.Channel == Channel.Email &&
            c.Status is not ("Closed" or "Resolved"));
        if (open is not null)
        {
            return open;
        }

        var conversation = new Conversation
        {
            OrganizationId = organizationId,
            CustomerId = customerId,
            Channel = Channel.Email,
            Status = "Open"
        };
        await conversationRepository.CreateAsync(conversation, cancellationToken);
        return conversation;
    }

    private static string? StripHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        return Regex.Replace(html, "<.*?>", string.Empty).Trim();
    }

    private static string StripQuotedReply(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var cutoffIndex = lines.Length;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimStart();
            if (line.StartsWith('>') || QuoteMarkers.Any(pattern => pattern.IsMatch(line.Trim())))
            {
                cutoffIndex = i;
                break;
            }
        }

        var result = string.Join("\n", lines.Take(cutoffIndex)).Trim();
        return string.IsNullOrWhiteSpace(result) ? text.Trim() : result;
    }
}
