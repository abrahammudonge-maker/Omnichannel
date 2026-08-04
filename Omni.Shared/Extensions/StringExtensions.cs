namespace Omni.Shared.Extensions;

public static class StringExtensions
{
    public static string ToSlug(this string value) =>
        string.Join("-", value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => x.ToLowerInvariant()));
}
