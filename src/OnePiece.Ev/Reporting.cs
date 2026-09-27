using System.Globalization;
using System.Text;

namespace OnePiece.Ev;

public static class Reporting
{
    public static string Money(decimal value) => "$" + Math.Round(value, 2).ToString("0.00", CultureInfo.InvariantCulture);
    public static string Signed(decimal value) => (Math.Round(value, 2) >= 0 ? "+" : "-") + Money(Math.Abs(value));
    public static string Safe(string text) => text.Replace("@", "＠").Replace("`", "'").Replace("*", "").Replace("_", " ").Replace("\r", " ").Replace("\n", " ");
    public static string Render(SetReport report, SetReport? previous, DateTimeOffset stamp, DateTimeOffset? previousStamp, MoverSettings settings)
    {
        var compare = Calculation.Comparable(report, previous);
        var compatible = Calculation.Compatible(report, previous);
        var sourceChanges = Calculation.SourceChanges(report, previous);
        var estimates = report.Cards.Where(c => c.PriceSource == "Mid").ToArray();
        string Delta(decimal value, decimal before, bool? allowed = null) => (allowed ?? compare) ? $" ({Signed(value - before)}) {(Math.Round(value - before, 2) == 0 ? "⚪" : value > before ? "🟢" : "🔴")}" : "";
        var b = new StringBuilder();
        b.AppendLine($"**{Safe(report.Name)} ({report.Code})**");
        b.AppendLine($"Source: {stamp:yyyy-MM-dd HH:mm} UTC · estimated odds");
        b.AppendLine("Booster Box:");
        b.AppendLine(report.Complete ? $"  EV: **{Money(report.KnownEv)}**{Delta(report.KnownEv, previous?.KnownEv ?? 0)}" : $"  EV: **INCOMPLETE — priced subtotal: {Money(report.KnownEv)}**");
        b.AppendLine($"  MP: {(report.BoxMarketPrice is decimal mp ? Money(mp) : "Unavailable")}");
        if (estimates.Length > 0) b.AppendLine($"Includes {estimates.Length} listing-based estimate{(estimates.Length == 1 ? "" : "s")} (TCGplayer Mid; details below).");
        b.AppendLine();
        b.AppendLine($"Master set ({report.Cards.Length}): {Money(report.KnownMaster)}{(report.Complete ? Delta(report.KnownMaster, previous?.KnownMaster ?? 0) : " (subtotal)")}");
        foreach (var category in Calculation.Categories)
        {
            var cards = report.Cards.Where(c => c.Category == category).ToArray();
            if (cards.Length == 0) continue;
            var ev = cards.Sum(c => c.Ev ?? 0);
            var oldEv = previous?.Cards.Where(c => c.Category == category).Sum(c => c.Ev ?? 0) ?? 0;
            var share = report.Complete && report.KnownEv > 0 ? $" · {100 * ev / report.KnownEv:0.0}%" : "";
            var label = category switch
            {
                "TR" => "Treasure Rare",
                "AA" => "Alternate Art",
                "SP" => "Special Rare",
                _ => category
            };
            b.AppendLine($"{label}:");
            b.AppendLine($"  AVG: {Money(ev)}{Delta(ev, oldEv, compatible && !sourceChanges.Any(c => c.Category == category))}{share}");
            b.AppendLine($"  Total ({cards.Length}): {Money(cards.Sum(c => c.Price ?? 0))}{(cards.Any(c => c.Price is null) ? " (subtotal)" : "")}");
        }
        if (compatible) b.AppendLine($"Changes vs {previousStamp:yyyy-MM-dd HH:mm} UTC");
        else b.AppendLine("Changes unavailable: first observation, incomplete prices, or changed model/membership.");
        foreach (var card in sourceChanges)
            b.AppendLine($"Pricing source changed: {Safe(card.Name)}; box/master-set and affected category deltas suppressed; card excluded from movers.");
        foreach (var mover in Calculation.Movers(report, previous, settings))
            b.AppendLine($"{(mover.PriceChange > 0 ? "🟢" : "🔴")} {Safe(mover.Card.Name)}: {Signed(mover.PriceChange)} ({Signed(mover.BoxImpact)} bx) [{Money(mover.Card.Price!.Value)}]{(mover.Card.PriceSource == "Mid" ? " (Mid estimate)" : "")}");
        foreach (var card in estimates) b.AppendLine($"Mid estimate: {Safe(card.Name)} [{card.Key}]: {Money(card.Price!.Value)}");
        foreach (var card in report.Cards.Where(c => c.Price is null)) b.AppendLine($"Missing price: {Safe(card.Name)} [{card.Key}]");
        foreach (var issue in report.Issues) b.AppendLine("Review: " + Safe(issue));
        b.AppendLine();
        b.AppendLine("⚠️ " + Safe(report.AssumptionsWarning));
        b.AppendLine("Expected cards per box and model notes: see the accompanying Markdown file.");
        return b.ToString().TrimEnd();
    }

    public static string[] Split(string content, int limit = 1900)
    {
        var result = new List<string>();
        var buffer = new StringBuilder();
        foreach (var original in content.Split('\n'))
        {
            var line = original.TrimEnd('\r');
            while (line.Length > limit)
            {
                if (buffer.Length > 0) { result.Add(buffer.ToString().TrimEnd()); buffer.Clear(); }
                var length = char.IsHighSurrogate(line[limit - 1]) ? limit - 1 : limit;
                result.Add(line[..length]); line = line[length..];
            }
            if (buffer.Length + line.Length + 1 > limit) { result.Add(buffer.ToString().TrimEnd()); buffer.Clear(); }
            buffer.AppendLine(line);
        }
        if (buffer.Length > 0) result.Add(buffer.ToString().TrimEnd());
        return result.Where(s => s.Length > 0).ToArray();
    }
}
