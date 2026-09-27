using System.Text;

namespace OnePiece.Ev;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        try { return await Run(args); }
        catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException or IOException or UnauthorizedAccessException or ArgumentException or HttpRequestException or TaskCanceledException)
        {
            Console.Error.WriteLine(ex is HttpRequestException or TaskCanceledException ? "Network request failed or timed out." : ex.Message);
            return 1;
        }
    }

    private static async Task<int> Run(string[] args)
    {
        string config="appsettings.json";
        string? import=null;
        bool dry=false,offline=false,validate=false;
        for(int i=0;i<args.Length;i++)
        {
            string Value() => i+1<args.Length ? args[++i] : throw new InvalidDataException("Missing argument value.");
            switch(args[i])
            {
                case "--config": config=Value(); break;
                case "--catalog": import=Value(); dry=true; break;
                case "--dry-run": dry=true; break;
                case "--offline": offline=true; dry=true; break;
                case "--validate": validate=true; break;
                case "--help": Console.WriteLine("OnePiece.Ev [--config FILE] [--validate] [--dry-run] [--offline] [--catalog DIRECTORY]"); return 0;
                default: throw new InvalidDataException("Unknown argument: "+args[i]);
            }
        }
        var settings=JsonFiles.Read<AppSettings>(config);
        var root=Path.GetDirectoryName(Path.GetFullPath(config))!;
        settings=settings with {ProfilesPath=Path.GetFullPath(settings.ProfilesPath,root),StateDirectory=Path.GetFullPath(settings.StateDirectory,root),ReportDirectory=Path.GetFullPath(settings.ReportDirectory,root)};
        var file=JsonFiles.Read<ProfileFile>(settings.ProfilesPath);
        if(file.SchemaVersion!=1 || file.Sets.Select(p=>p.Code).Distinct().Count()!=file.Sets.Length) throw new InvalidDataException("Invalid profile schema or duplicate codes.");
        foreach(var p in file.Sets) Calculation.Validate(p);
        foreach(var code in settings.EnabledSets.Concat(settings.Destinations.SelectMany(d=>d.Sets)))
            if(!file.Sets.Any(p=>p.Code==code && p.Enabled)) throw new InvalidDataException("No reviewed enabled profile for "+code);
        if(settings.Source.CategoryId!=68 || settings.Source.RequestDelayMilliseconds<100 || settings.Source.RefreshHours<24 || settings.Source.TimeoutSeconds<=0 || settings.Source.MaxSourceAgeHours<=0 || settings.Movers.MaximumCards is <0 or >10 || settings.Movers.MinBoxImpact<0 || settings.Movers.MinPriceChangePercent<0) throw new InvalidDataException("Invalid source or mover settings.");
        if(settings.Destinations.Any(d=>string.IsNullOrWhiteSpace(d.Id)) || settings.Destinations.Select(d=>d.Id).Distinct().Count()!=settings.Destinations.Length) throw new InvalidDataException("Destination IDs must be unique and nonempty.");
        var destinations=settings.Destinations.Where(d=>d.Enabled).ToArray();
        if(!dry) foreach(var d in destinations) _=DiscordDelivery.Endpoint(d);
        if(validate) {Console.WriteLine($"Validated {file.Sets.Length} profiles, {file.Sets.Sum(p=>p.Variants.Length)} variants.");return 0;}
        var profiles=file.Sets.Where(p=>p.Enabled && (settings.EnabledSets.Length==0 || settings.EnabledSets.Contains(p.Code))).ToArray();
        if(profiles.Length==0) throw new InvalidDataException("No sets selected.");
        Directory.CreateDirectory(settings.StateDirectory);
        using var runLock=new FileStream(Path.Combine(settings.StateDirectory,"run.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        var groups=profiles.SelectMany(p=>p.Variants.Select(v=>v.GroupId).Append(p.GroupId)).Distinct().Order().ToArray();
        var catalog=import is null ? await Catalog.Load(settings,groups,offline) : Catalog.Import(import,groups);
        if(!dry && (DateTimeOffset.UtcNow-catalog.SourceUpdatedAt>TimeSpan.FromHours(settings.Source.MaxSourceAgeHours) || catalog.SourceUpdatedAt>DateTimeOffset.UtcNow.AddMinutes(5))) throw new InvalidDataException("Source is stale or future-dated; inspect with --dry-run.");
        profiles=profiles.Where(p=>p.ReleaseDate<=DateOnly.FromDateTime(catalog.SourceUpdatedAt.UtcDateTime)).ToArray();
        var current=new ReportSnapshot(catalog.SourceUpdatedAt,DateTimeOffset.UtcNow,profiles.Select(p=>Calculation.Evaluate(p,catalog)).ToArray());
        var history=Path.Combine(settings.StateDirectory,"history"); Directory.CreateDirectory(history);
        var previous=Directory.EnumerateFiles(history,"*.json").Select(JsonFiles.Read<ReportSnapshot>).Where(s=>s.SourceUpdatedAt<current.SourceUpdatedAt).OrderByDescending(s=>s.SourceUpdatedAt).FirstOrDefault();
        var texts=current.Sets.ToDictionary(s=>s.Code,s=>Reporting.Render(s,previous?.Sets.FirstOrDefault(p=>p.Code==s.Code),current.SourceUpdatedAt,previous?.SourceUpdatedAt,settings.Movers));
        Directory.CreateDirectory(settings.ReportDirectory);
        File.WriteAllText(Path.Combine(settings.ReportDirectory,"latest.md"),string.Join("\n\n\n",texts.Values) + "\n\n");
        JsonFiles.Write(Path.Combine(settings.ReportDirectory,"latest.json"),current);
        Console.WriteLine(string.Join("\n\n\n",texts.Values) + "\n");
        if(dry || destinations.Length==0) {Console.WriteLine("Preview saved; no messages sent and history not advanced.");return 0;}
        using var http=new HttpClient {Timeout=TimeSpan.FromSeconds(45)};
        foreach(var d in destinations)
        {
            var endpoint=DiscordDelivery.Endpoint(d);
            foreach(var report in current.Sets.Where(s=>d.Sets.Length==0 || d.Sets.Contains(s.Code)))
            {
                if(!report.Complete && !settings.PostIncompleteReports) continue;
                var key=$"{d.Id}:{Calculation.Hash(endpoint.ToString())[..16]}:{current.SourceUpdatedAt:yyyyMMddHHmmss}:{report.Code}";
                await DiscordDelivery.Send(http,Path.Combine(settings.StateDirectory,"deliveries.json"),key,endpoint,texts[report.Code]);
            }
        }
        var snapshotPath=Path.Combine(history,$"{current.SourceUpdatedAt:yyyyMMddHHmmss}.json");
        if(!File.Exists(snapshotPath)) JsonFiles.Write(snapshotPath,current);
        Console.WriteLine("Discord delivery completed."); return 0;
    }
}
