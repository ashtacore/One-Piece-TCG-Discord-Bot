using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace OnePiece.Ev;

public static class DiscordDelivery
{
    public static Uri Endpoint(Destination d)
    {
        var url = d.ResolveUrl();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || u.Scheme != "https" || u.Host != "discord.com" || !u.IsDefaultPort || u.Query.Length != 0 || u.Fragment.Length != 0 || u.UserInfo.Length != 0 || !Regex.IsMatch(u.AbsolutePath, @"^/api/webhooks/\d+/[A-Za-z0-9_-]+$"))
            throw new InvalidDataException($"Invalid Discord webhook for '{d.Id}'.");
        if (d.ThreadId is not null && !Regex.IsMatch(d.ThreadId, @"^\d+$")) throw new InvalidDataException("Invalid thread ID.");
        return new(url + "?wait=true" + (d.ThreadId is null ? "" : "&thread_id=" + d.ThreadId));
    }
    public static async Task Send(HttpClient http, string path, string batch, Uri endpoint, string content)
    {
        var ledger = File.Exists(path) ? JsonFiles.Read<DeliveryLedger>(path) : new(new());
        var parts = ledger.Parts.Values.Where(p => p.Key.StartsWith(batch + ":", StringComparison.Ordinal)).OrderBy(p => p.Key).ToArray();
        if (parts.Length == 0)
        {
            var title = content.Split('\n')[0].TrimEnd('\r');
            if (title.Length > 150) title = "One Piece EV report";
            var chunks = Reporting.Split(content, 1700);
            // An invisible character preserves a blank final line when Discord trims trailing whitespace.
            chunks[^1] += "\n\n\u200b";
            parts = chunks.Select((s,i) => new DeliveryPart($"{batch}:{i:D4}", i == 0 ? s : title + $" (continued {i+1}/{chunks.Length})\n" + s,"pending",null,DateTimeOffset.UtcNow)).ToArray();
            foreach (var p in parts) ledger.Parts.Add(p.Key,p);
            JsonFiles.Write(path,ledger);
        }
        foreach (var part in parts)
        {
            if (part.Status == "sent") continue;
            if (part.Status is "inflight" or "uncertain") throw new InvalidDataException("Uncertain Discord delivery: inspect channel and reconcile deliveries.json before retrying.");
            void Save(string status,string? id=null) { ledger.Parts[part.Key]=part with {Status=status,MessageId=id,UpdatedAt=DateTimeOffset.UtcNow}; JsonFiles.Write(path,ledger); }
            for (int attempt=0;;attempt++)
            {
                Save("inflight");
                try
                {
                    using var response=await http.PostAsJsonAsync(endpoint,new {content=part.Content,allowed_mentions=new {parse=Array.Empty<string>()}});
                    if ((int)response.StatusCode==429)
                    {
                        Save("pending");
                        using var body=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                        var seconds=body.RootElement.GetProperty("retry_after").GetDouble();
                        if (attempt>=3 || seconds<0 || seconds>60) throw new InvalidDataException("Discord rate limit; retry later.");
                        await Task.Delay(TimeSpan.FromSeconds(seconds+.1)); continue;
                    }
                    if (!response.IsSuccessStatusCode)
                    {
                        Save((int)response.StatusCode>=500 ? "uncertain":"pending");
                        throw new InvalidDataException($"Discord returned HTTP {(int)response.StatusCode}.");
                    }
                    using var result=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    Save("sent",result.RootElement.GetProperty("id").GetString() ?? throw new InvalidDataException("Missing message ID.")); break;
                }
                catch(Exception ex) when(ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException)
                { Save("uncertain"); throw new InvalidDataException("Discord outcome uncertain; inspect channel before retrying."); }
            }
        }
    }
}
