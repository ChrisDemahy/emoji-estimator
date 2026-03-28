using System.Text.Json;

namespace EmojiEstimator.Web.Services;

internal static class RepositoryScanResultSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static string Serialize(RepositoryScanResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return JsonSerializer.Serialize(result, SerializerOptions);
    }

    public static RepositoryScanResult Deserialize(string resultJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resultJson);

        return JsonSerializer.Deserialize<RepositoryScanResult>(resultJson, SerializerOptions)
            ?? throw new InvalidOperationException("The stored repository scan could not be deserialized.");
    }
}
