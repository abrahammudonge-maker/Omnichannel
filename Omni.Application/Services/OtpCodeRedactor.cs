using Omni.Domain.Entities;

namespace Omni.Application.Services;

public static class OtpCodeRedactor
{
    public const string ExpiredBody = "Verification code expired.";

    /// <summary>
    /// OTP messages keep the real code so an agent can give it to a customer while it's still valid.
    /// Once the code expires, the stored body is hidden on read.
    /// </summary>
    public static void HideExpiredCodes(IReadOnlyList<Message> messages, IReadOnlyList<OtpMessage> otpRows, DateTimeOffset now)
    {
        var expiredMessageIds = otpRows
            .Where(o => o.MessageId is not null && o.ExpiresAt <= now)
            .Select(o => o.MessageId!.Value)
            .ToHashSet();

        foreach (var message in messages.Where(m => expiredMessageIds.Contains(m.Id)))
        {
            message.Body = ExpiredBody;
        }
    }
}
