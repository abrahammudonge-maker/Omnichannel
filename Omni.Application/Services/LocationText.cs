using System.Globalization;

namespace Omni.Application.Services;

public static class LocationText
{
    public static string Format(double latitude, double longitude, string? name, string? address)
    {
        var label = string.Join(" - ", new[] { name, address }.Where(s => !string.IsNullOrWhiteSpace(s)));
        var lat = latitude.ToString(CultureInfo.InvariantCulture);
        var lng = longitude.ToString(CultureInfo.InvariantCulture);
        return $"Location: {(label.Length > 0 ? label + " " : string.Empty)}(https://maps.google.com/?q={lat},{lng})";
    }
}
