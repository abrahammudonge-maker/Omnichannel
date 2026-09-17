using Omni.Application.Interfaces;

namespace Omni.Infrastructure.Providers;

/// <summary>
/// Resolves the right IVoiceProvider by name (e.g. a PhoneNumber's or Call's own Provider field) —
/// the one place in the application that knows which concrete providers exist. Adding a new provider
/// (Vonage, Africa's Talking, SIP, a custom one) means registering it in DI and nowhere else; nothing
/// in VoiceService or the controllers needs to change.
/// </summary>
public sealed class VoiceProviderFactory : IVoiceProviderFactory
{
    private readonly Dictionary<string, IVoiceProvider> _providersByName;

    public VoiceProviderFactory(IEnumerable<IVoiceProvider> providers)
    {
        _providersByName = providers.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
    }

    public IVoiceProvider Resolve(string providerName)
    {
        if (_providersByName.TryGetValue(providerName, out var provider))
        {
            return provider;
        }

        throw new InvalidOperationException($"No voice provider named '{providerName}' is registered. Registered providers: {string.Join(", ", _providersByName.Keys)}.");
    }
}
