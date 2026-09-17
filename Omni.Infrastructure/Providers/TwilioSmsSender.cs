using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Omni.Application.Interfaces;

namespace Omni.Infrastructure.Providers;

public sealed class TwilioSmsSender : ISmsSender
{
    private readonly IHttpClientFactory _httpClientFactory;

    public TwilioSmsSender(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<SmsSendResult> SendAsync(string accountSid, string authToken, string fromNumber, string toNumber, string body, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("Twilio");
        var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.twilio.com/2010-04-01/Accounts/{accountSid}/Messages.json")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["From"] = fromNumber,
                ["To"] = toNumber,
                ["Body"] = body
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{accountSid}:{authToken}")));

        var response = await client.SendAsync(request, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Twilio send failed ({(int)response.StatusCode}): {json}");
        }

        using var document = JsonDocument.Parse(json);
        var sid = document.RootElement.GetProperty("sid").GetString()!;
        return new SmsSendResult(sid);
    }
}
