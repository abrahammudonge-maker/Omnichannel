namespace Omni.Application.Interfaces;

public sealed record MetaTemplateInfo(string Name, string Language, string Category, string Status, string BodyText, string ComponentsJson);

/// <summary>Reads the message template catalog for a WhatsApp Business Account. Templates live at the WABA level, not per phone number.</summary>
public interface IMetaTemplateService
{
    Task<IReadOnlyList<MetaTemplateInfo>> FetchTemplatesAsync(string wabaId, string accessToken, CancellationToken cancellationToken);

    /// <summary>
    /// Submits a new template to Meta for review. <paramref name="bodyText"/>'s {{1}}, {{2}}, ... variables
    /// each get an auto-generated example value, since Meta requires an example per variable to accept the submission.
    /// Returns the raw "components" array Meta stored, for local caching.
    /// Ignored when <paramref name="category"/> is "AUTHENTICATION" — use <see cref="CreateAuthenticationTemplateAsync"/> instead.
    /// </summary>
    Task<string> CreateTemplateAsync(
        string wabaId,
        string accessToken,
        string name,
        string language,
        string category,
        string? headerText,
        string bodyText,
        string? footerText,
        IReadOnlyList<string>? quickReplyButtons,
        CancellationToken cancellationToken);

    /// <summary>
    /// Submits an AUTHENTICATION-category (OTP) template. Meta writes the body/footer wording itself —
    /// there's no free-text body here, just the flags that control which sentences Meta appends — and
    /// requires a single OTP button (COPY_CODE, the option that works without any mobile app integration;
    /// Meta rejects creating an AUTHENTICATION template without exactly one such button).
    /// </summary>
    Task<string> CreateAuthenticationTemplateAsync(
        string wabaId,
        string accessToken,
        string name,
        string language,
        bool addSecurityRecommendation,
        int? codeExpirationMinutes,
        CancellationToken cancellationToken);
}
