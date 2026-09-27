using System.Globalization;
using System.Text;

namespace OnePiece.Ev;

public static class AssumptionDocument
{
    public static string Warning(SetProfile profile)
    {
        var weak = new List<string>();
        if (profile.Pools.Any(p => p.Id == "tr")) weak.Add("Treasure Rare frequency");
        if (profile.Pools.Any(p => p.Id.Contains("manga", StringComparison.Ordinal))) weak.Add("Manga frequency and replacement slots");
        if (profile.Pools.Any(p => p.Id.Contains("god-pack", StringComparison.Ordinal))) weak.Add("god-pack odds");
        if (profile.Pools.Any(p => p.Id == "jolly-roger")) weak.Add("Leader/Jolly Roger allocation");
        if (profile.Pools.Any(p => p.Id is "gold-chase" or "silver-chase" or "signature" or "red-super" or "super-alt" or "super-leader")) weak.Add("ultra-chase frequencies");
        if (profile.Pools.Any(p => p.Id == "demon-elders") || profile.Code is "EB-03" or "OP-17" or "PRB-02") weak.Add("special-pack roster and displacement");
        if (profile.Pools.Any(p => p.Id == "pirate-foil")) weak.Add("bulk/Pirate Foil and SR weighting");
        weak.Add("equal likelihood within pools");
        return "Pull rates are community estimates, not guarantees. Weaker assumptions; " + string.Join(", ", weak) + ".";
    }

    public static MarkdownAttachment Create(SetProfile profile, DateTimeOffset sourceTimestamp)
    {
        Calculation.Validate(profile);
        string Number(decimal value) => value.ToString("0.############", CultureInfo.InvariantCulture);
        string Cell(string value) => value.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
        var b = new StringBuilder();
        b.AppendLine($"# {profile.Name} ({profile.Code}): assumed expected cards per box");
        b.AppendLine();
        b.AppendLine($"Price snapshot: {sourceTimestamp:yyyy-MM-dd HH:mm:ss} UTC  ");
        b.AppendLine($"Profile version: {profile.Version}  ");
        b.AppendLine($"Profile hash: {Calculation.ModelHash(profile)}");
        b.AppendLine();
        b.AppendLine(Warning(profile));
        b.AppendLine();
        b.AppendLine($"Physical contents: {profile.PacksPerBox} packs × {profile.CardsPerPack} cards + {profile.BonusCardsPerBox} packaged bonus cards = {profile.PacksPerBox * profile.CardsPerPack + profile.BonusCardsPerBox} cards. Case size: {profile.BoxesPerCase} boxes.");
        b.AppendLine();
        b.AppendLine("These are long-run averages for a randomly selected box from an unsearched case, not guaranteed contents of an individual box. Counts below are the actual model inputs AFTER replacement and special-pack adjustments. Do not apply those adjustments again.");
        b.AppendLine();
        b.AppendLine("| Pool | Report category | Distinct variants | Expected cards per box |");
        b.AppendLine("| --- | --- | ---: | ---: |");
        foreach (var pool in profile.Pools)
        {
            var cards = profile.Variants.Where(v => v.Pool == pool.Id).ToArray();
            b.AppendLine($"| {Cell(Label(pool.Id))} | {Cell(string.Join(", ", cards.Select(c => c.Category).Distinct()))} | {cards.Length} | {Number(pool.ExpectedCopiesPerBox)} |");
        }
        b.AppendLine($"| **Total** | | **{profile.Variants.Length}** | **{Number(profile.Pools.Sum(p => p.ExpectedCopiesPerBox))}** |");
        b.AppendLine();
        b.AppendLine("Counts may differ from the physical total by tiny decimal rounding amounts. Pool identifiers refer to distinct treatments; AA Leaders and ordinary Leaders are separate. Jolly Roger and other treatment pools can span multiple report categories.");
        b.AppendLine();
        b.AppendLine("## Calculation and weighting");
        b.AppendLine();
        b.AppendLine("Expected copies of a variant = pool expected cards × variant weight / total pool weight. Box EV = sum of expected copies × selected card price. Master set = one copy of each eligible variant. A one-per-36-box chase contributes its average value divided by 36, without dividing again by cards per box.");
        b.AppendLine();
        b.AppendLine(profile.Variants.All(v => v.Weight == 1) ? "Every variant currently has weight 1 within its pool: equal likelihood is assumed." : "This profile uses nonuniform weights. The exceptions to weight 1 are listed below:");
        foreach (var variant in profile.Variants.Where(v => v.Weight != 1)) b.AppendLine($"- {variant.Name} ({variant.Key}, {variant.Pool}): weight {Number(variant.Weight)}");
        b.AppendLine();
        b.AppendLine("Card prices prefer TCGplayer Market, falling back to Mid (median listings) when unavailable. Fallbacks are disclosed in the report; neither price available leaves an incomplete valuation. These pricing rules do not change pull probabilities. Graded prices are not used.");
        b.AppendLine();
        b.AppendLine("## Adopted assumptions and replacements");
        b.AppendLine();
        foreach (var assumption in profile.Assumptions) b.AppendLine("- " + assumption);
        foreach (var pool in profile.Pools.Where(p => !string.IsNullOrWhiteSpace(p.Note))) b.AppendLine($"- {Label(pool.Id)}: {pool.Note}");
        b.AppendLine();
        b.AppendLine("## Sources");
        b.AppendLine();
        foreach (var source in profile.Sources) b.AppendLine($"- <{source}>");
        b.AppendLine();
        b.AppendLine("Sources provide product details and community observations; their inclusion does not mean every adopted rate is measured or officially confirmed.");
        var safeCode = new string(profile.Code.Where(c => char.IsAsciiLetterOrDigit(c) || c == '-').ToArray());
        return new($"{safeCode}-pull-rate-assumptions.md", b.ToString());
    }

    private static string Label(string id) => id switch
    {
        "base-c" => "Common", "base-uc" => "Uncommon", "base-r" => "Rare", "base-sr" => "Super Rare",
        "base-sec" => "Secret Rare", "base-l" => "Ordinary Leader", "base-pr" => "Ordinary Promo",
        "aa" => "Ordinary Alternate Art", "aa-leader" => "Alternate Art Leader", "sp" => "Special Rare",
        "tr" => "Treasure Rare", "manga" => "Manga", "manga-nami" => "Manga Nami (standalone + god pack)",
        "manga-reprint-god-pack" => "Nine Manga reprints (god pack only)",
        "don-normal" => "DON!! Normal", "don-foil" => "DON!! Foil", "don-gold" => "DON!! Gold",
        "full-art" => "Full Art", "textured-event-stage" => "Textured Event/Stage", "jolly-roger" => "Jolly Roger",
        "box-topper" => "Packaged box topper", "signature" => "Gold-stamped signature", "wanted" => "Wanted Poster",
        "gold-chase" => "Gold chase", "silver-chase" => "Silver chase", "red-super" => "Red Super Alternate Art",
        "super-alt" => "Super Alternate Art", "super-leader" => "Super Leader Alternate Art", "sp-leader" => "Special Rare Leader",
        "demon-elders" => "Five Elder parallels (demon pack)", "imu-aa-leader" => "Imu AA Leader (ordinary + demon pack)",
        "event-sp" => "Special event art", "pirate-foil" => "Pirate Foil", "pandaman" => "Pandaman art", "don-special" => "DON!! Special Foil",
        _ => id
    };
}
