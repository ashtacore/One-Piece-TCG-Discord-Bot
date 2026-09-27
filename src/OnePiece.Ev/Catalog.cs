using System.Globalization;
using System.Text.Json;

namespace OnePiece.Ev;

public static class Catalog
{
    private static T[] Parse<T>(string json)
    {
        var response = JsonSerializer.Deserialize<ApiResponse<T>>(json, JsonFiles.SourceOptions);
        if (response is null || !response.Success || response.Results is null || response.Errors?.Length > 0)
            throw new InvalidDataException("Source returned an unsuccessful response.");
        return response.Results;
    }

    public static DateTimeOffset Timestamp(string value) => DateTimeOffset.Parse(value.Trim(), CultureInfo.InvariantCulture);

    public static CatalogSnapshot Import(string directory, int[] groups)
    {
        var stamp = Timestamp(File.ReadAllText(Path.Combine(directory, "last-updated.txt")));
        return new(stamp, DateTimeOffset.UtcNow,
            Parse<Group>(File.ReadAllText(Path.Combine(directory, "groups.json"))),
            groups.Select(id => new SourceGroup(id,
                Parse<Product>(File.ReadAllText(Path.Combine(directory, id.ToString(), "products.json"))),
                Parse<Price>(File.ReadAllText(Path.Combine(directory, id.ToString(), "prices.json"))))).ToArray()) { PricingSchemaVersion = 1 };
    }

    public static async Task<CatalogSnapshot> Load(AppSettings settings, int[] groups, bool offline)
    {
        var path = Path.Combine(settings.StateDirectory, "catalog.json");
        var existing = File.Exists(path) ? JsonFiles.Read<CatalogSnapshot>(path) : null;
        bool Covers() => existing is not null && existing.PricingSchemaVersion == 1 && groups.All(id => existing.Data.Any(g => g.GroupId == id));
        if (offline)
            return Covers() ? existing! : throw new InvalidDataException("No compatible complete local catalog exists. Run --dry-run online to refresh pricing fields.");
        if (Covers() && DateTimeOffset.UtcNow - existing!.RetrievedAt < TimeSpan.FromHours(settings.Source.RefreshHours))
            return existing;
        using var http = new HttpClient { BaseAddress = new Uri(settings.Source.BaseUrl), Timeout = TimeSpan.FromSeconds(settings.Source.TimeoutSeconds) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("OnePieceEv/0.1");
        async Task<string> Get(string endpoint)
        {
            for (int attempt = 0; ; attempt++)
            {
                await Task.Delay(settings.Source.RequestDelayMilliseconds);
                using var response = await http.GetAsync(endpoint);
                if (((int)response.StatusCode == 429 || (int)response.StatusCode >= 500) && attempt < 3)
                {
                    var delay = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(2 << attempt);
                    if (delay > TimeSpan.FromSeconds(60)) throw new InvalidDataException("Source retry delay exceeds one minute; try later.");
                    await Task.Delay(delay);
                    continue;
                }
                if (!response.IsSuccessStatusCode) throw new InvalidDataException($"Source request failed: HTTP {(int)response.StatusCode}.");
                return await response.Content.ReadAsStringAsync();
            }
        }
        var stamp = Timestamp(await Get("last-updated.txt"));
        if (Covers() && existing!.SourceUpdatedAt == stamp)
        {
            existing = existing with { RetrievedAt = DateTimeOffset.UtcNow };
            JsonFiles.Write(path, existing);
            return existing;
        }
        var prefix = $"tcgplayer/{settings.Source.CategoryId}/";
        var sourceGroups = Parse<Group>(await Get(prefix + "groups"));
        var data = new List<SourceGroup>();
        foreach (var id in groups)
            data.Add(new(id, Parse<Product>(await Get(prefix + id + "/products")), Parse<Price>(await Get(prefix + id + "/prices"))));
        if (Timestamp(await Get("last-updated.txt")) != stamp)
            throw new InvalidDataException("Provider updated during download; no snapshot saved. Run again later.");
        var snapshot = new CatalogSnapshot(stamp, DateTimeOffset.UtcNow, sourceGroups, data.ToArray()) { PricingSchemaVersion = 1 };
        JsonFiles.Write(path, snapshot);
        return snapshot;
    }
}
