using Omni.Application.Interfaces;
using Omni.Infrastructure.Providers;

namespace Omni.Tests;

public sealed class FakeVoiceProviderTests
{
    [Fact]
    public async Task ValidateWebhookAsync_AcceptsCorrectlySignedPayload()
    {
        var provider = new FakeVoiceProvider();
        var body = """{"providerCallId":"call-1","eventType":"status"}""";
        var context = new WebhookValidationContext(
            body,
            "https://example.test/api/webhooks/voice/fake",
            new Dictionary<string, string> { [FakeVoiceProvider.SignatureHeaderName] = FakeVoiceProvider.Sign(body) });

        var isValid = await provider.ValidateWebhookAsync(context, CancellationToken.None);

        Assert.True(isValid);
    }

    [Fact]
    public async Task ValidateWebhookAsync_RejectsMissingSignature()
    {
        var provider = new FakeVoiceProvider();
        var context = new WebhookValidationContext("{}", "https://example.test", new Dictionary<string, string>());

        var isValid = await provider.ValidateWebhookAsync(context, CancellationToken.None);

        Assert.False(isValid);
    }

    [Fact]
    public async Task ValidateWebhookAsync_RejectsTamperedPayload()
    {
        var provider = new FakeVoiceProvider();
        var originalBody = """{"providerCallId":"call-1","eventType":"status"}""";
        var signatureForOriginal = FakeVoiceProvider.Sign(originalBody);
        var tamperedBody = """{"providerCallId":"call-1","eventType":"hangup"}""";

        var context = new WebhookValidationContext(
            tamperedBody,
            "https://example.test",
            new Dictionary<string, string> { [FakeVoiceProvider.SignatureHeaderName] = signatureForOriginal });

        var isValid = await provider.ValidateWebhookAsync(context, CancellationToken.None);

        Assert.False(isValid);
    }

    [Fact]
    public async Task ProcessWebhookAsync_ParsesKnownFields()
    {
        var provider = new FakeVoiceProvider();
        var payload = """
        {
            "providerCallId": "call-123",
            "eventType": "status",
            "status": "Answered",
            "durationSeconds": 42,
            "fromNumber": "+15550001111",
            "toNumber": "+15550002222"
        }
        """;

        var result = await provider.ProcessWebhookAsync(payload, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("call-123", result!.ProviderCallId);
        Assert.Equal("Answered", result.Status);
        Assert.Equal(42, result.DurationSeconds);
        Assert.Equal("+15550001111", result.FromNumber);
    }

    [Fact]
    public async Task ProcessWebhookAsync_ReturnsNull_ForMalformedPayload()
    {
        var provider = new FakeVoiceProvider();

        var result = await provider.ProcessWebhookAsync("""{"unrelated":"field"}""", CancellationToken.None);

        Assert.Null(result);
    }
}
