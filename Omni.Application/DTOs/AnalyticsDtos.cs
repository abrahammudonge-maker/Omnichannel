namespace Omni.Application.DTOs;

public sealed class MessageStatRow
{
    public DateTime Day { get; set; }
    public short Channel { get; set; }
    public string Direction { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Count { get; set; }
}

public sealed record MessagingSummaryView(
    int Days,
    int Inbound,
    int Outbound,
    int Delivered,
    int Failed,
    double DeliveryRate,
    IReadOnlyList<ChannelSummaryView> ByChannel,
    IReadOnlyList<DailySummaryView> Daily);

public sealed record ChannelSummaryView(string Channel, int Inbound, int Outbound, int Failed);

public sealed record DailySummaryView(DateOnly Date, int Inbound, int Outbound, int Failed);
