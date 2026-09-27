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
        var boxRows = new List<string[]>();
        var marketRows = new List<string[]>();
        string TableDelta(decimal value, decimal before, bool allowed) => allowed ? $" ({Signed(value-before)})" : "";
        string Trend(decimal value, decimal before, bool allowed) => !allowed ? "–" : Math.Round(value - before, 2) switch
        {
            > 0 => "🟢",
            < 0 => "🔴",
            _ => "🟡"
        };
        foreach (var category in Calculation.Categories)
        {
            var cards = report.Cards.Where(c => c.Category == category).ToArray();
            if (cards.Length == 0) continue;
            var ev = cards.Sum(c => c.Ev ?? 0);
            var oldEv = previous?.Cards.Where(c => c.Category == category).Sum(c => c.Ev ?? 0) ?? 0;
            var copies = cards.Sum(c => c.ExpectedCopies);
            var missing = cards.Any(c => c.Price is null);
            var label = category switch
            {
                "TR" => "Treasure Rare",
                "AA" => "Alternate Art",
                "SP" => "Special Rare",
                "Manga / Other Chase" => "Manga / Chase",
                _ => category
            };
            boxRows.Add([label, copies.ToString("0.######", CultureInfo.InvariantCulture),
                missing || copies == 0 ? "--" : Money(ev / copies),
                Money(ev) + (missing ? "*" : "") + TableDelta(ev, oldEv, compatible && !sourceChanges.Any(c => c.Category == category))]);
            var total = cards.Sum(c => c.Price ?? 0);
            var oldTotal = previous?.Cards.Where(c => c.Category == category).Sum(c => c.Price ?? 0) ?? 0;
            marketRows.Add([label, cards.Length.ToString(CultureInfo.InvariantCulture),
                missing ? "--" : Money(total / cards.Length), Money(total) + (missing ? "*" : ""),
                Trend(total, oldTotal, compatible && !sourceChanges.Any(c => c.Category == category))]);
        }
        b.AppendLine(Table(["Rarity", "Copies", "AVG", "Total"], boxRows));
        b.AppendLine();
        b.AppendLine("Market Data:");
        marketRows.Add(["Master set", report.Cards.Length.ToString(CultureInfo.InvariantCulture),
            report.Complete && report.Cards.Length > 0 ? Money(report.KnownMaster / report.Cards.Length) : "--",
            Money(report.KnownMaster) + (report.Complete ? TableDelta(report.KnownMaster, previous?.KnownMaster ?? 0, compare) : "*"),
            Trend(report.KnownMaster, previous?.KnownMaster ?? 0, compare)]);
        b.AppendLine(Table(["Rarity", "Count", "AVG", "Total", "Trend"], marketRows));
        b.AppendLine("Trend: 🟢 up · 🔴 down · 🟡 unchanged · – no comparable data");
        if (!report.Complete) b.AppendLine("* Priced subtotal; missing prices or unresolved coverage. AVG is -- when a category has missing prices.");
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
        b.AppendLine("⚠️ Pull rates are community estimates, not guarantees.");
        b.AppendLine("For assumptions and prediction model notes: see the accompanying Markdown file.");
        return b.ToString().TrimEnd();
    }

    private static string Table(string[] headers, List<string[]> rows)
    {
        // These three circles occupy two UTF-16 units and are padded as two display columns.
        var widths = headers.Select((h, i) => Math.Max(h.Length, rows.Max(r => r[i].Length))).ToArray();
        string Row(string[] cells) => "| " + string.Join(" | ", cells.Select((c, i) => headers[i] == "Trend"
            ? c.PadLeft(c.Length + (widths[i] - c.Length) / 2).PadRight(widths[i])
            : i == 0 ? c.PadRight(widths[i]) : c.PadLeft(widths[i]))) + " |";
        return "```\n" + Row(headers) + "\n| " + string.Join(" | ", widths.Select(w => new string('-', w))) + " |\n"
            + string.Join("\n", rows.Select(Row)) + "\n```";
    }

    public static string[] Split(string content, int limit = 1900)
    {
        if (limit < 16) throw new ArgumentOutOfRangeException(nameof(limit));
        var result = new List<string>();
        var buffer = "";
        void Add(string piece)
        {
            if (buffer.Length > 0 && buffer.Length + 1 + piece.Length > limit)
            {
                result.Add(buffer); buffer = "";
            }
            buffer = buffer.Length == 0 ? piece : buffer + "\n" + piece;
        }
        var lines = content.Replace("\r\n", "\n").Split('\n');
        for (int i = 0; i < lines.Length;)
        {
            if (lines[i] == "```")
            {
                var start = ++i;
                while (i < lines.Length && lines[i] != "```") i++;
                var body = string.Join("\n", lines[start..i]);
                if (i < lines.Length) i++;
                // Keep each table intact where possible; oversized blocks reopen fences per part.
                foreach (var part in SplitPlain(body, limit - 8)) Add("```\n" + part + "\n```");
            }
            else
            {
                var start = i++;
                while (i < lines.Length && lines[i] != "```") i++;
                foreach (var part in SplitPlain(string.Join("\n", lines[start..i]), limit)) Add(part);
            }
        }
        if (buffer.Length > 0) result.Add(buffer);
        return result.ToArray();
    }

    private static string[] SplitPlain(string content, int limit)
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
            buffer.Append(line).Append('\n');
        }
        if (buffer.Length > 0) result.Add(buffer.ToString().TrimEnd());
        return result.Where(s => s.Length > 0).ToArray();
    }
}
