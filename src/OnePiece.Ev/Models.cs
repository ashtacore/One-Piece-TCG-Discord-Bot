using System.Text.Json;
using System.Text.Json.Serialization;

namespace OnePiece.Ev;

public static class JsonFiles
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    public static readonly JsonSerializerOptions SourceOptions = new(JsonSerializerDefaults.Web);
    public static T Read<T>(string path) => JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options)
        ?? throw new InvalidDataException($"Empty JSON: {Path.GetFileName(path)}");
    public static void Write<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(value, Options));
        File.Move(temp, path, true);
    }
}

public sealed record AppSettings
{
    public string ProfilesPath { get; init; } = "config/pullrates.json";
    public string StateDirectory { get; init; } = "data";
    public string ReportDirectory { get; init; } = "out";
    public string[] EnabledSets { get; init; } = [];
    public SourceSettings Source { get; init; } = new();
    public MoverSettings Movers { get; init; } = new();
    public Destination[] Destinations { get; init; } = [];
    public bool PostIncompleteReports { get; init; } = true;
}

public sealed record SourceSettings
{
    public string BaseUrl { get; init; } = "https://tcgcsv.com/";
    public int CategoryId { get; init; } = 68;
    public int RequestDelayMilliseconds { get; init; } = 150;
    public int TimeoutSeconds { get; init; } = 45;
    public int RefreshHours { get; init; } = 24;
    public int MaxSourceAgeHours { get; init; } = 48;
}
public sealed record MoverSettings
{
    public decimal MinPriceChangePercent { get; init; } = 1m;
    public decimal MinBoxImpact { get; init; } = 0.05m;
    public int MaximumCards { get; init; } = 10;
}
public sealed record Destination
{
    public required string Id { get; init; }
    public bool Enabled { get; init; }
    public string? WebhookUrl { get; init; }
    public string? WebhookEnvironmentVariable { get; init; }
    public string[] Sets { get; init; } = [];
    public string? ThreadId { get; init; }
    public string ResolveUrl() => (!string.IsNullOrWhiteSpace(WebhookEnvironmentVariable)
        ? Environment.GetEnvironmentVariable(WebhookEnvironmentVariable) : WebhookUrl)
        ?? throw new InvalidDataException($"Webhook missing for destination '{Id}'.");
}

public sealed record ProfileFile
{
    public int SchemaVersion { get; init; } = 1;
    public required SetProfile[] Sets { get; init; }
}
public sealed record SetProfile
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required string Version { get; init; }
    public required string Confidence { get; init; }
    public bool Enabled { get; init; } = true;
    public int GroupId { get; init; }
    public int BoosterBoxProductId { get; init; }
    public DateOnly ReleaseDate { get; init; }
    public int PacksPerBox { get; init; }
    public int CardsPerPack { get; init; }
    public int BoxesPerCase { get; init; }
    public int BonusCardsPerBox { get; init; }
    public string[] Sources { get; init; } = [];
    public string[] Assumptions { get; init; } = [];
    public required Pool[] Pools { get; init; }
    public required Variant[] Variants { get; init; }
    public Exclusion[] Exclusions { get; init; } = [];
}
public sealed record Pool
{
    public required string Id { get; init; }
    public decimal ExpectedCopiesPerBox { get; init; }
    public string? Note { get; init; }
}
public sealed record Variant
{
    public int GroupId { get; init; }
    public int ProductId { get; init; }
    public required string SubTypeName { get; init; }
    public required string Name { get; init; }
    public string? Number { get; init; }
    public required string Category { get; init; }
    public required string Pool { get; init; }
    public decimal Weight { get; init; } = 1;
    public string? PricingNote { get; init; }
    [JsonIgnore] public string Key => $"{ProductId}:{SubTypeName}";
}
public sealed record Exclusion(int ProductId, string? SubTypeName, string Reason);

// Source DTOs intentionally allow additional provider fields.
public sealed record ApiResponse<T>(bool Success, string[] Errors, T[] Results);
public sealed record Group(int GroupId, string Name, string Abbreviation);
public sealed record ExtendedField(string Name, string Value);
public sealed record Product(int ProductId, int GroupId, string Name, ExtendedField[] ExtendedData)
{
    public string? Field(string name) => ExtendedData.FirstOrDefault(x => x.Name == name)?.Value;
    public bool IsCard => Field("Rarity") is not null || Field("Number") is not null;
}
public sealed record Price(int ProductId, string SubTypeName, decimal? MarketPrice)
{
    [JsonIgnore] public string Key => $"{ProductId}:{SubTypeName}";
}
public sealed record SourceGroup(int GroupId, Product[] Products, Price[] Prices);
public sealed record CatalogSnapshot(DateTimeOffset SourceUpdatedAt, DateTimeOffset RetrievedAt, Group[] Groups, SourceGroup[] Data);
public sealed record CacheIndex(string SnapshotFile, DateTimeOffset LastCheckedAt);
public sealed record CardValue(string Key, string Name, string Category, string Pool, decimal ExpectedCopies, decimal? Price)
{
    [JsonIgnore] public decimal? Ev => Price * ExpectedCopies;
}
public sealed record SetReport(string Code, string Name, string ModelHash, string Confidence, CardValue[] Cards, string[] Issues)
{
    public decimal? BoxMarketPrice { get; init; }
    [JsonIgnore] public bool Complete => Issues.Length == 0 && Cards.All(c => c.Price is not null);
    [JsonIgnore] public decimal KnownEv => Cards.Sum(c => c.Ev ?? 0);
    [JsonIgnore] public decimal KnownMaster => Cards.Sum(c => c.Price ?? 0);
}
public sealed record ReportSnapshot(DateTimeOffset SourceUpdatedAt, DateTimeOffset CreatedAt, SetReport[] Sets);
public sealed record Mover(CardValue Card, decimal PriceChange, decimal PercentChange, decimal BoxImpact);
public sealed record DeliveryPart(string Key, string Content, string Status, string? MessageId, DateTimeOffset UpdatedAt);
public sealed record DeliveryLedger(Dictionary<string, DeliveryPart> Parts);
