using Omni.Application.Interfaces;
using Omni.Domain.Constants;
using Omni.Infrastructure.Providers;

namespace Omni.Tests;

public sealed class VoiceProviderFactoryTests
{
    [Fact]
    public void Resolve_ReturnsMatchingProvider_ByName()
    {
        var fake = new FakeVoiceProvider();
        var twilio = new TwilioVoiceProvider();
        var factory = new VoiceProviderFactory(new IVoiceProvider[] { fake, twilio });

        var resolved = factory.Resolve(VoiceProviderName.Fake);

        Assert.Same(fake, resolved);
    }

    [Fact]
    public void Resolve_IsCaseInsensitive()
    {
        var fake = new FakeVoiceProvider();
        var factory = new VoiceProviderFactory(new IVoiceProvider[] { fake });

        var resolved = factory.Resolve("fake");

        Assert.Same(fake, resolved);
    }

    [Fact]
    public void Resolve_ThrowsForUnregisteredProvider()
    {
        var factory = new VoiceProviderFactory(new IVoiceProvider[] { new FakeVoiceProvider() });

        Assert.Throws<InvalidOperationException>(() => factory.Resolve(VoiceProviderName.Vonage));
    }
}
