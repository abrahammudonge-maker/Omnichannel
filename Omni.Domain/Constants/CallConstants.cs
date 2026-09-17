namespace Omni.Domain.Constants;

/// <summary>
/// Call fields are plain strings (matching Message.Status, Conversation.Status elsewhere in this
/// codebase) rather than enums, since they largely mirror whatever vocabulary the connected voice
/// provider reports — a new provider can introduce a status this list doesn't anticipate without
/// requiring a schema change. These constants exist only to avoid magic strings in our own code.
/// </summary>
public static class CallDirection
{
    public const string Inbound = "Inbound";
    public const string Outbound = "Outbound";
}

public static class CallStatus
{
    public const string Ringing = "Ringing";
    public const string Answered = "Answered";
    public const string OnHold = "OnHold";
    public const string Transferred = "Transferred";
    public const string Completed = "Completed";
    public const string Missed = "Missed";
    public const string Rejected = "Rejected";
    public const string Failed = "Failed";

    public static readonly IReadOnlySet<string> Active = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Ringing, Answered, OnHold, Transferred
    };

    public static readonly IReadOnlySet<string> Terminal = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Completed, Missed, Rejected, Failed
    };
}

public static class RecordingStatus
{
    public const string NotRecorded = "NotRecorded";
    public const string Processing = "Processing";
    public const string Available = "Available";
    public const string Failed = "Failed";
}

public static class CallQueueStrategy
{
    public const string RoundRobin = "RoundRobin";
    public const string LeastBusy = "LeastBusy";
    public const string LongestIdle = "LongestIdle";
    public const string Manual = "Manual";
}

public static class VoiceProviderName
{
    public const string Fake = "Fake";
    public const string Twilio = "Twilio";
    public const string Vonage = "Vonage";
    public const string AfricasTalking = "AfricasTalking";
    public const string Sip = "Sip";
    public const string Custom = "Custom";
}
