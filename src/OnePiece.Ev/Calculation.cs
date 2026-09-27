using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OnePiece.Ev;

public static class Calculation
{
    public static readonly string[] Categories = ["TR", "AA", "SP", "Manga / Other Chase", "Foils", "Base", "Leaders", "DON!!", "Other"];
    public static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    public static string ModelHash(SetProfile profile) => Hash(JsonSerializer.Serialize(profile, JsonFiles.Options));

    public static void Validate(SetProfile profile)
    {
        void Require(bool ok, string message) { if (!ok) throw new InvalidDataException($"{profile.Code}: {message}"); }
        Require(!string.IsNullOrWhiteSpace(profile.Code) && !string.IsNullOrWhiteSpace(profile.Version), "code/version required");
        Require(profile.PacksPerBox > 0 && profile.CardsPerPack > 0 && profile.BoxesPerCase > 0 && profile.BonusCardsPerBox >= 0, "invalid physical counts");
        Require(profile.GroupId > 0 && profile.BoosterBoxProductId > 0, "source identities required");
        Require(profile.ReleaseDate != default, "release date required");
        Require(profile.Sources.Length > 0 && profile.Assumptions.Length > 0 && !string.IsNullOrWhiteSpace(profile.Confidence), "sources, assumptions and confidence required");
        Require(profile.Pools.Length > 0 && profile.Variants.Length > 0, "empty profile");
        Require(profile.Pools.Select(p => p.Id).Distinct().Count() == profile.Pools.Length, "duplicate pools");
        Require(profile.Variants.Select(v => v.Key).Distinct().Count() == profile.Variants.Length, "duplicate card/finish keys");
        Require(profile.Pools.All(p => p.ExpectedCopiesPerBox >= 0), "negative pool count");
        var total = profile.Pools.Sum(p => p.ExpectedCopiesPerBox);
        Require(Math.Abs(total - (profile.PacksPerBox * profile.CardsPerPack + profile.BonusCardsPerBox)) < 0.00000001m, $"expected cards {total} do not reconcile to physical contents");
        foreach (var pool in profile.Pools)
            Require(profile.Variants.Any(v => v.Pool == pool.Id), $"empty pool '{pool.Id}'");
        foreach (var variant in profile.Variants)
        {
            Require(variant.Weight > 0 && variant.GroupId > 0 && variant.ProductId > 0, "invalid variant weight/identity");
            Require(variant.SubTypeName is "Normal" or "Foil", $"unreviewed finish '{variant.SubTypeName}'");
            Require(Categories.Contains(variant.Category), $"unknown category '{variant.Category}'");
            Require(profile.Pools.Any(p => p.Id == variant.Pool), $"unknown pool '{variant.Pool}'");
            Require(!profile.Exclusions.Any(e => e.ProductId == variant.ProductId && (e.SubTypeName is null || e.SubTypeName == variant.SubTypeName)), $"included and excluded: {variant.Key}");
        }
    }

    public static SetReport Evaluate(SetProfile profile, CatalogSnapshot catalog)
    {
        Validate(profile);
        var issues = new List<string>();
        var groups = catalog.Data.ToDictionary(g => g.GroupId);
        var products = groups.ToDictionary(g => g.Key, g => g.Value.Products.ToDictionary(p => p.ProductId));
        var prices = groups.ToDictionary(g => g.Key, g => g.Value.Prices.ToDictionary(p => p.Key));
        if (!products.TryGetValue(profile.GroupId, out var setProducts) || !setProducts.ContainsKey(profile.BoosterBoxProductId))
            issues.Add("Configured booster-box listing is missing.");

        // Explicit allowlists prevent new chase treatments or external bonus products being priced at ordinary rates.
        if (setProducts is not null)
        {
            foreach (var product in setProducts.Values.Where(p => p.IsCard))
            {
                if (!profile.Variants.Any(v => v.ProductId == product.ProductId) && !profile.Exclusions.Any(e => e.ProductId == product.ProductId && e.SubTypeName is null))
                    issues.Add($"Unmapped card listing {product.ProductId}; profile review needed.");
            }
            foreach (var price in groups[profile.GroupId].Prices.Where(p => setProducts.TryGetValue(p.ProductId, out var product) && product.IsCard))
                if (!profile.Variants.Any(v => v.Key == price.Key) && !profile.Exclusions.Any(e => e.ProductId == price.ProductId && (e.SubTypeName is null || e.SubTypeName == price.SubTypeName)))
                    issues.Add($"Unmapped finish {price.Key}; profile review needed.");
        }
        var poolRates = profile.Pools.ToDictionary(p => p.Id, p => p.ExpectedCopiesPerBox);
        var weightTotals = profile.Variants.GroupBy(v => v.Pool).ToDictionary(g => g.Key, g => g.Sum(v => v.Weight));
        var cards = profile.Variants.Select(v =>
        {
            decimal? price = null;
            var priceSource = "Missing";
            if (!products.TryGetValue(v.GroupId, out var sourceProducts) || !sourceProducts.TryGetValue(v.ProductId, out var product))
                issues.Add($"Missing product {v.ProductId}.");
            else if (v.Number is not null && product.Field("Number") != v.Number)
                issues.Add($"Card number changed for {v.ProductId}; profile review needed.");
            else if (prices.TryGetValue(v.GroupId, out var sourcePrices) && sourcePrices.TryGetValue(v.Key, out var current))
            {
                if (current.MarketPrice >= 0) { price = current.MarketPrice; priceSource = "Market"; }
                else if (current.MidPrice >= 0) { price = current.MidPrice; priceSource = "Mid"; }
            }
            return new CardValue(v.Key, v.Name, v.Category, v.Pool, poolRates[v.Pool] * v.Weight / weightTotals[v.Pool], price) { PriceSource = priceSource };
        }).ToArray();
        decimal? boxMarketPrice = null;
        if (setProducts?.ContainsKey(profile.BoosterBoxProductId) == true
            && prices.TryGetValue(profile.GroupId, out var boxPrices)
            && boxPrices.TryGetValue($"{profile.BoosterBoxProductId}:Normal", out var boxPrice)
            && boxPrice.MarketPrice >= 0)
            boxMarketPrice = boxPrice.MarketPrice;
        return new SetReport(profile.Code, profile.Name, ModelHash(profile), profile.Confidence, cards, issues.Distinct().ToArray())
        {
            BoxMarketPrice = boxMarketPrice
        };
    }

    public static bool Compatible(SetReport current, SetReport? previous) => previous is not null && current.Complete && previous.Complete
        && current.ModelHash == previous.ModelHash
        && current.Cards.Select(c => c.Key).Order().SequenceEqual(previous.Cards.Select(c => c.Key).Order());

    public static CardValue[] SourceChanges(SetReport current, SetReport? previous)
    {
        if (!Compatible(current, previous)) return [];
        var old = previous!.Cards.ToDictionary(c => c.Key);
        return current.Cards.Where(c => c.PriceSource != old[c.Key].PriceSource).ToArray();
    }

    public static bool Comparable(SetReport current, SetReport? previous) => Compatible(current, previous) && SourceChanges(current, previous).Length == 0;

    public static Mover[] Movers(SetReport current, SetReport? previous, MoverSettings settings)
    {
        if (!Compatible(current, previous)) return [];
        var old = previous!.Cards.ToDictionary(c => c.Key);
        return current.Cards.Where(c => c.Price is not null && old[c.Key].Price > 0 && c.PriceSource == old[c.Key].PriceSource)
            .Select(c => new Mover(c, c.Price!.Value - old[c.Key].Price!.Value,
                100 * (c.Price.Value - old[c.Key].Price!.Value) / old[c.Key].Price!.Value,
                c.ExpectedCopies * (c.Price.Value - old[c.Key].Price!.Value)))
            .Where(m => Math.Abs(m.PriceChange) > 0 && Math.Abs(m.PercentChange) >= settings.MinPriceChangePercent && Math.Abs(m.BoxImpact) >= settings.MinBoxImpact)
            .OrderByDescending(m => Math.Abs(m.BoxImpact)).ThenBy(m => m.Card.Key, StringComparer.Ordinal)
            .Take(settings.MaximumCards).ToArray();
    }
}
