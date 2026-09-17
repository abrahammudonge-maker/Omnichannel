namespace Omni.Application.Interfaces;

public sealed record SmsSendResult(string ExternalMessageId);

public interface ISmsSender
{
    /// <param name="accountSid">Twilio Account SID.</param>
    /// <param name="authToken">Twilio Auth Token.</param>
    /// <param name="fromNumber">The connected number sending the message, in E.164 format.</param>
    /// <param name="toNumber">The recipient's number, in E.164 format.</param>
    Task<SmsSendResult> SendAsync(string accountSid, string authToken, string fromNumber, string toNumber, string body, CancellationToken cancellationToken);
}
