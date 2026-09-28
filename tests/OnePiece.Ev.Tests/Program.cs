using OnePiece.Ev;
using System.Net;
using System.Text;

var checks=0;
void Check(bool ok,string name) {if(!ok) throw new Exception(name); checks++;}
var profileFile=JsonFiles.Read<ProfileFile>("config/pullrates.json");
var allProfiles=profileFile.Sets;
foreach(var p in allProfiles) Calculation.Validate(p);
var profiles=new[]{"OP-08","EB-01","PRB-01"}.Select(code=>allProfiles.Single(p=>p.Code==code)).ToArray();
Check(allProfiles.Length==22 && profileFile.PlannedSets.Length==2,"22 released profiles and two planned releases");
var releaseOrder=ReleaseCalendar.Select(allProfiles.Reverse(),[],new DateOnly(2026,9,27));
Check(releaseOrder.Select(p=>p.Code).SequenceEqual(allProfiles.Select(p=>p.Code)),"Reports sort by release date independent of input order");
Check(ReleaseCalendar.Select(allProfiles,[],new DateOnly(2024,5,2)).All(p=>p.ReleaseDate<new DateOnly(2024,5,3)),"Unreleased profiles excluded before fetching");
Check(ReleaseCalendar.Select(allProfiles,["EB-01","OP-01"],new DateOnly(2026,9,27)).Select(p=>p.Code).SequenceEqual(new[]{"OP-01","EB-01"}),"Set filters retain release order");
var allGroups=allProfiles.SelectMany(p=>p.Variants.Select(v=>v.GroupId).Append(p.GroupId)).Distinct().ToArray();
var expandedCatalog=Catalog.Import("research/catalog-cache",allGroups);
foreach(var profile in allProfiles)
{
    var report=Calculation.Evaluate(profile,expandedCatalog);
    Check(report.Issues.Length==0,$"{profile.Code}: every catalog card/finish mapped or explicitly excluded");
    Check(Math.Abs(report.Cards.Sum(c=>c.ExpectedCopies)-(profile.PacksPerBox*profile.CardsPerPack+profile.BonusCardsPerBox))<.00000001m,$"{profile.Code}: physical card count");
    Check(report.BoxesPerCase == (profile.Code is "PRB-01" or "PRB-02" ? 10 : 12) && report.KnownCaseEv == report.KnownEv * report.BoxesPerCase, $"{profile.Code}: case EV scales existing box model");
    Check(expandedCatalog.Data.Single(g=>g.GroupId==profile.GroupId).Products.Single(p=>p.ProductId==profile.BoosterCaseProductId).Name.Contains("Box Case"), $"{profile.Code}: exact case listing");
    Check(report.CaseMarketPrice == expandedCatalog.Data.Single(g=>g.GroupId==profile.GroupId).Prices.Single(p=>p.ProductId==profile.BoosterCaseProductId && p.SubTypeName=="Normal").MarketPrice,$"{profile.Code}: exact case market quote, including unavailable prices");
    Check(report.BoxMarketPrice is not null,$"{profile.Code}: sealed-box price maps to a real listing");
    Check(report.Cards.All(c=>c.ExpectedCopies>0),$"{profile.Code}: every eligible variant has nonzero modeled probability");
    Check(profile.Variants.All(v=>!v.Name.Contains("Dash Pack") && !v.Name.Contains("Double Pack")),$"{profile.Code}: external bonus products excluded");
}
Check(allProfiles.Where(p=>p.BonusCardsPerBox==1).Select(p=>p.Code).SequenceEqual(new[]{"OP-01","OP-02"}),"Only reviewed packaged toppers add physical bonus cards");
Check(allProfiles.Single(p=>p.Code=="OP-01").BoosterBoxProductId==557280,"OP01 uses White wave box price");
var op13=allProfiles.Single(p=>p.Code=="OP-13");
Check(op13.Variants.Count(v=>v.Pool=="demon-elders")==5 && Math.Abs(op13.Pools.Single(p=>p.Id=="demon-elders").ExpectedCopiesPerBox-5m/180)<.00000001m,"Demon pack exclusive Elder count");
var prb02=allProfiles.Single(p=>p.Code=="PRB-02");
Check(prb02.BoxesPerCase==10 && prb02.Variants.Count(v=>v.Pool=="event-sp")==4 && prb02.Variants.Count(v=>v.Category=="DON!!")==90,"PRB02 special events and distinct DON finishes");
Check(Calculation.Evaluate(allProfiles.Single(p=>p.Code=="OP-09"),expandedCatalog).Cards.Single(c=>c.Key=="597068:Foil").Price is null,"Catalog card with no price row stays in master roster");
var groups=profiles.SelectMany(p=>p.Variants.Select(v=>v.GroupId).Append(p.GroupId)).Distinct().ToArray();
var catalog=Catalog.Import("research/catalog-cache",groups);
var reports=profiles.Select(p=>Calculation.Evaluate(p,catalog)).ToArray();
Check(reports[0].Complete && reports[1].Complete,"OP/EB complete");
Check(reports[0].BoxMarketPrice == 246.47m, "Box MP uses configured sealed box Normal listing");
Check(reports[0].CaseMarketPrice == 2696.29m && reports[2].CaseMarketPrice == 9048.50m, "Case MP uses actual case quote, not multiplied box price");
foreach(decimal? quote in new decimal?[]{null,-1m,0m,123m})
{
    var caseCatalog=catalog with {Data=catalog.Data.Select(g=>g with {Prices=g.Prices.Select(p=>p.ProductId==profiles[0].BoosterCaseProductId ? p with {MarketPrice=quote,MidPrice=999m}:p).ToArray()}).ToArray()};
    var caseReport=Calculation.Evaluate(profiles[0],caseCatalog);
    Check(caseReport.CaseMarketPrice==(quote>=0 ? quote:null) && caseReport.Complete && caseReport.KnownEv==reports[0].KnownEv && caseReport.KnownMaster==reports[0].KnownMaster,"Case accepts zero market price; missing/negative quotes never use Mid or alter cards");
}
var noCaseCatalog=catalog with {Data=catalog.Data.Select(g=>g with {Products=g.Products.Where(p=>p.ProductId!=profiles[0].BoosterCaseProductId).ToArray()}).ToArray()};
var noCase=Calculation.Evaluate(profiles[0],noCaseCatalog);
Check(noCase.CaseMarketPrice is null && noCase.Complete,"Missing sealed case listing does not invalidate contents");
var noBoxPrice = catalog with { Data = catalog.Data.Select(g => g with { Prices = g.Prices.Where(p => p.ProductId != profiles[0].BoosterBoxProductId).ToArray() }).ToArray() };
var withoutBoxPrice = Calculation.Evaluate(profiles[0], noBoxPrice);
Check(withoutBoxPrice.BoxMarketPrice is null && withoutBoxPrice.Complete && withoutBoxPrice.KnownEv == reports[0].KnownEv && withoutBoxPrice.KnownMaster == reports[0].KnownMaster, "Missing box MP does not alter card EV or master set");
Check(Reporting.Render(withoutBoxPrice,null,catalog.SourceUpdatedAt,null,new()).Contains("MP: Unavailable"), "Unknown sealed price displayed explicitly");
Check(reports[2].Complete && reports[2].Cards.Single(c=>c.Key=="587709:Foil") is { Price:2450m, PriceSource:"Mid" },"Ace uses explicit Mid fallback");
Check(reports[0].Cards.All(c=>c.PriceSource=="Market"),"Market remains preferred when Mid is also available");
var missingCatalog = catalog with { Data = catalog.Data.Select(g => g with { Prices = g.Prices.Select(p => p.ProductId == 587709 ? p with {MidPrice=null} : p).ToArray() }).ToArray() };
var missingReport = Calculation.Evaluate(profiles[2], missingCatalog);
Check(!missingReport.Complete && missingReport.Cards.Single(c=>c.Key=="587709:Foil") is {Price:null,PriceSource:"Missing"},"Neither price available remains unknown");
Check(Math.Abs(reports[2].KnownEv-missingReport.KnownEv-2450m/150)<.00000001m && reports[2].KnownMaster-missingReport.KnownMaster==2450m,"Mid contributes correctly to EV and master value");
var legacy = System.Text.Json.JsonSerializer.Deserialize<CardValue>("{\"key\":\"1:Foil\",\"name\":\"Legacy\",\"category\":\"AA\",\"pool\":\"aa\",\"expectedCopies\":1,\"price\":2}",JsonFiles.Options)!;
Check(legacy.PriceSource=="Market","Old report history defaults to Market");
Check(reports.Select(r=>r.Cards.Length).SequenceEqual(new[]{152,80,409}),"Master counts");
for(int i=0;i<profiles.Length;i++) Check(Math.Abs(reports[i].Cards.Sum(c=>c.ExpectedCopies)-profiles[i].PacksPerBox*profiles[i].CardsPerPack)<.00000001m,"Physical count conservation");
var chase=reports[0].Cards.Single(c=>c.Pool=="manga");
Check(Math.Abs(chase.Ev!.Value-chase.Price!.Value/36)<.00000001m,"One in 36 EV");
Check(reports[2].Cards.Where(c=>c.Pool=="manga-reprint-god-pack").All(c=>Math.Abs(c.ExpectedCopies-1m/150)<.00000001m),"God pack marginal probability");
var before=reports[0];
var after=before with {Cards=before.Cards.Select(c=>c with {Price=c.Price*2}).ToArray()};
Check(Calculation.Movers(after,before,new()).Length==10,"Mover cap");
var legacyDestination=System.Text.Json.JsonSerializer.Deserialize<Destination>("""{"id":"legacy"}""",JsonFiles.Options)!;
Check(legacyDestination.IncludeBoosterBoxData && legacyDestination.IncludeCaseData && legacyDestination.IncludeMarketData && legacyDestination.IncludeCardList,"Existing destinations default to full reports");
foreach(var market in new[]{false,true})
foreach(var cards in new[]{false,true})
foreach(var boxes in new[]{false,true})
foreach(var cases in new[]{false,true})
{
    var destination=System.Text.Json.JsonSerializer.Deserialize<Destination>(
        $$"""{"id":"custom","sets":["OP-08"],"includeMarketData":{{market.ToString().ToLowerInvariant()}},"includeCardList":{{cards.ToString().ToLowerInvariant()}},"includeBoosterBoxData":{{boxes.ToString().ToLowerInvariant()}},"includeCaseData":{{cases.ToString().ToLowerInvariant()}}}""",JsonFiles.Options)!;
    var customized=Reporting.Render(after,before,catalog.SourceUpdatedAt.AddDays(1),catalog.SourceUpdatedAt,new(),destination.IncludeMarketData,destination.IncludeCardList,destination.IncludeBoosterBoxData,destination.IncludeCaseData);
    Check(destination.Sets.SequenceEqual(new[]{"OP-08"}),"Display switches preserve set selection");
    Check(customized.Contains("Market Data:")==market && customized.Contains("| Master set")==market && customized.Contains("Trend:")==market,"Market table toggle includes its total and legend");
    Check(customized.Contains(" bx) [")==cards,"Card list toggle independent of other sections");
    Check(customized.Contains("Booster Box:")==boxes && customized.Contains($"  MP: {Reporting.Money(after.BoxMarketPrice!.Value)}")==boxes,"Box switch controls heading and sealed price");
    Check(customized.Contains("Booster Case:")==cases && customized.Contains($"  MP: {Reporting.Money(after.CaseMarketPrice!.Value)}")==cases,"Case switch controls heading and sealed price");
    Check(customized.Split('\n').Count(l=>l.Split('|').Any(cell=>cell.Trim()=="Copies"))==(boxes?1:0)+(cases?1:0),"Only selected contents tables rendered");
    Check(customized.Contains(Reporting.Warning) && customized.Contains("accompanying Markdown file"),"Display switches retain warnings and assumptions");
}
var compactMissing=Reporting.Render(missingReport,null,catalog.SourceUpdatedAt,null,new(),false,false);
Check(compactMissing.Contains("INCOMPLETE") && compactMissing.Contains("Missing price:"),"Hidden optional sections retain missing-price disclosure");
var compactMid=Reporting.Render(reports[2],null,catalog.SourceUpdatedAt,null,new(),false,false);
Check(compactMid.Contains("Mid estimate:") && compactMid.Contains("listing-based estimate"),"Hidden optional sections retain Mid disclosure");
Check(Calculation.Movers(after,before,new()).Zip(Calculation.Movers(after,before,new()).Skip(1)).All(pair=>Math.Abs(pair.First.BoxImpact)>=Math.Abs(pair.Second.BoxImpact)),"Mover ordering");
Check(!Calculation.Comparable(after with {ModelHash="changed"},before),"Changed model resets comparison");
Check(!Calculation.Comparable(missingReport,missingReport),"Incomplete comparison suppressed");
Check(Reporting.Render(missingReport,null,catalog.SourceUpdatedAt,null,new()).Contains("INCOMPLETE"),"Subtotal labeled");
Check(Reporting.Render(reports[2],null,catalog.SourceUpdatedAt,null,new()).Contains("Includes 1 listing-based estimate"),"Fallback disclosed in report");
var switched = after with { Cards=after.Cards.Select((c,i)=>i==0 ? c with {PriceSource="Mid"} : c).ToArray() };
Check(!Calculation.Comparable(switched,before) && Calculation.SourceChanges(switched,before).Length==1,"Source switch invalidates aggregate comparison");
var switchMovers=Calculation.Movers(switched,before,new() {MinBoxImpact=0,MinPriceChangePercent=0});
Check(switchMovers.Length>0 && switchMovers.All(m=>m.Card.Key!=switched.Cards[0].Key),"Source switch excluded while unaffected movers retained");
Check(!Calculation.Comparable(before,switched) && Calculation.Movers(before,switched,new()).All(m=>m.Card.Key!=switched.Cards[0].Key),"Mid to Market switch also excluded");
var switchText=Reporting.Render(switched,before,catalog.SourceUpdatedAt.AddDays(1),catalog.SourceUpdatedAt,new());
Check(switchText.Contains("Pricing source changed:") && switchText.Contains("box/master-set and affected category deltas suppressed"),"Affected comparison warning");
var stableMid=reports[2] with {Cards=reports[2].Cards.Select(c=>c.PriceSource=="Mid" ? c with {Price=c.Price+100} : c).ToArray()};
Check(Calculation.Comparable(stableMid,reports[2]) && Calculation.Movers(stableMid,reports[2],new()).Single().Card.PriceSource=="Mid","Unchanged Mid source can produce labeled listing movements");
Check(Reporting.Split(new string('a',1899)+"🟢"+new string('b',3000)).All(s=>s.Length<=1900 && !char.IsHighSurrogate(s[^1])),"Discord UTF16 limits");
var temp=Path.Combine(Path.GetTempPath(),"onepiece-tests-"+Guid.NewGuid()); Directory.CreateDirectory(temp);
var cacheSettings = new AppSettings { StateDirectory = Path.Combine(temp,"catalog"), Source = new() { RequestDelayMilliseconds = 0 } };
var cachePath = Path.Combine(cacheSettings.StateDirectory,"catalog.json");
var cached = catalog with { RetrievedAt = DateTimeOffset.UtcNow };
JsonFiles.Write(cachePath,cached);
var sourceStamp = cached.SourceUpdatedAt;
var sourceRequests = new List<string>();
using var sourceHandler = new FakeHandler(request =>
{
    var path = request.RequestUri!.AbsolutePath;
    sourceRequests.Add(path);
    var content = path == "/last-updated.txt" ? sourceStamp.ToString("O") : "{\"success\":true,\"results\":[]}";
    return new(HttpStatusCode.OK) { Content = new StringContent(content) };
});
var reused = await Catalog.Load(cacheSettings,groups,false,sourceHandler);
Check(sourceRequests.SequenceEqual(new[]{"/last-updated.txt"}) && reused.Data.Sum(g=>g.Prices.Length)==cached.Data.Sum(g=>g.Prices.Length),"Fresh cache still checks timestamp and reuses unchanged prices");
sourceRequests.Clear();
sourceStamp = sourceStamp.AddDays(1);
var refreshed = await Catalog.Load(cacheSettings,groups,false,sourceHandler);
Check(refreshed.SourceUpdatedAt==sourceStamp && refreshed.Data.All(g=>g.Prices.Length==0) && sourceRequests.Count==3+2*groups.Length && sourceRequests.First()=="/last-updated.txt" && sourceRequests.Last()=="/last-updated.txt","Changed timestamp refreshes even a newly checked cache and verifies snapshot consistency");
Check(JsonFiles.Read<CatalogSnapshot>(cachePath).SourceUpdatedAt==sourceStamp,"Refreshed snapshot saved");
sourceRequests.Clear();
await Catalog.Load(cacheSettings,groups,true,sourceHandler);
Check(sourceRequests.Count==0,"Offline cache never checks provider");
var stampChecks = 0;
using var changingSource = new FakeHandler(request => new(HttpStatusCode.OK) { Content = new StringContent(
    request.RequestUri!.AbsolutePath=="/last-updated.txt" ? sourceStamp.AddDays(++stampChecks).ToString("O") : "{\"success\":true,\"results\":[]}") });
try { await Catalog.Load(cacheSettings,groups,false,changingSource); throw new Exception("Expected changing source failure"); } catch(InvalidDataException) {}
Check(JsonFiles.Read<CatalogSnapshot>(cachePath).SourceUpdatedAt==sourceStamp,"Update during download preserves previous cache");
var endpoint=new Uri("https://discord.com/api/webhooks/1/fake?wait=true");
var calls=0;
using var http=new HttpClient(new FakeHandler(_=>{calls++;return new(HttpStatusCode.OK){Content=new StringContent("{\"id\":\"123\"}")};}));
var ledger=Path.Combine(temp,"sent.json");
var firstDelivery=await DiscordDelivery.Send(http,ledger,"batch",endpoint,"hello");
var repeatDelivery=await DiscordDelivery.Send(http,ledger,"batch",endpoint,"changed");
Check(calls==1,"Repeated batch not reposted");
Check(firstDelivery is {Sent:1,AlreadyDelivered:0} && repeatDelivery is {Sent:0,AlreadyDelivered:1},"Delivery summary counts sent and skipped messages");
Check(repeatDelivery.Summary=="Discord: 0 messages sent, 1 already delivered.","Console delivery summary format");
Check(JsonFiles.Read<DeliveryLedger>(ledger).Parts.Values.Single().Content=="hello\n\n\u200b","Frozen outbox preserves report separator");
var forcedAttachment=new MarkdownAttachment("updated.md","Updated assumptions");
var forced=await DiscordDelivery.Send(http,ledger,"batch",endpoint,"Updated report",forcedAttachment,forcePost:true);
var forceLedger=JsonFiles.Read<DeliveryLedger>(ledger);
Check(calls==2 && forced is {Sent:1,AlreadyDelivered:0},"Force post sends a new message");
Check(forceLedger.Parts.Values.Single(p=>p.Key.StartsWith("batch:")).Content.StartsWith("Updated report") && forceLedger.Parts.Values.Single(p=>p.Key.StartsWith("batch:")).Attachment==forcedAttachment,"Force post uses latest content and attachment");
Check(forceLedger.Parts.Values.Single(p=>p.Key.StartsWith("archive:")).Content.StartsWith("hello"),"Force post preserves previous receipt");
var afterForce=await DiscordDelivery.Send(http,ledger,"batch",endpoint,"Should not send");
Check(calls==2 && afterForce is {Sent:0,AlreadyDelivered:1},"Normal run skips newly force-posted batch");
var secondForce=await DiscordDelivery.Send(http,ledger,"batch",endpoint,"Second intentional resend",forcePost:true);
Check(calls==3 && secondForce.Sent==1,"Each force-post invocation intentionally resends");
var rateCalls=0;
using var rate=new HttpClient(new FakeHandler(_=>++rateCalls==1 ? new((HttpStatusCode)429){Content=new StringContent("{\"retry_after\":0}")} : new(HttpStatusCode.OK){Content=new StringContent("{\"id\":\"456\"}")}));
await DiscordDelivery.Send(rate,Path.Combine(temp,"rate.json"),"batch",endpoint,"hello");
Check(rateCalls==2,"429 retry");
var timeoutCalls=0;
using var timeout=new HttpClient(new FakeHandler(_=>{timeoutCalls++;throw new HttpRequestException("simulated ambiguous failure");}));
var uncertain=Path.Combine(temp,"uncertain.json");
for(int i=0;i<2;i++) {try{await DiscordDelivery.Send(timeout,uncertain,"batch",endpoint,"hello");throw new Exception("Expected failure");}catch(InvalidDataException){}}
Check(timeoutCalls==1 && JsonFiles.Read<DeliveryLedger>(uncertain).Parts.Values.Single().Status=="uncertain","Ambiguous delivery never retried automatically");
try { await DiscordDelivery.Send(timeout,uncertain,"batch",endpoint,"hello",forcePost:true); throw new Exception("Expected uncertain force failure"); } catch(InvalidDataException) {}
Check(timeoutCalls==1,"Force post cannot bypass uncertain delivery reconciliation");
var document=AssumptionDocument.Create(profiles[2],catalog.SourceUpdatedAt);
Check(document.FileName=="PRB-01-pull-rate-assumptions.md" && document.Content.Contains("AFTER replacement") && document.Content.Contains("0.039988888889") && document.Content.Contains(Calculation.ModelHash(profiles[2])),"Attachment matches adjusted profile and version hash");
Check(Reporting.Render(reports[2],null,catalog.SourceUpdatedAt,null,new()).EndsWith("see the accompanying Markdown file."),"Report ends with attachment reference");
var attachmentCalls=0;
using var upload=new HttpClient(new FakeHandler(request=>
{
    attachmentCalls++;
    Check(request.Content is MultipartFormDataContent,"Attachment uses multipart upload");
    var fields=((MultipartFormDataContent)request.Content!).ToArray();
    Check(fields.Length==2 && fields[0].Headers.ContentDisposition!.Name!.Trim('"')=="payload_json" && fields[1].Headers.ContentDisposition!.Name!.Trim('"')=="files[0]","Discord upload field names");
    using var payload=System.Text.Json.JsonDocument.Parse(fields[0].ReadAsStringAsync().GetAwaiter().GetResult());
    Check(payload.RootElement.GetProperty("allowed_mentions").GetProperty("parse").GetArrayLength()==0,"Uploads disable mentions");
    Check(fields[1].ReadAsStringAsync().GetAwaiter().GetResult()==document.Content && fields[1].Headers.ContentDisposition!.FileName!.Trim('"')==document.FileName,"Uploaded file bytes and name match frozen attachment");
    return attachmentCalls==1 ? new((HttpStatusCode)429){Content=new StringContent("{\"retry_after\":0}")} : new(HttpStatusCode.OK){Content=new StringContent("{\"id\":\"789\"}")};
}));
var attachmentLedger=Path.Combine(temp,"attachment.json");
await DiscordDelivery.Send(upload,attachmentLedger,"batch",endpoint,"Assumptions",document);
await DiscordDelivery.Send(upload,attachmentLedger,"batch",endpoint,"Changed",document with {Content="Changed model"});
Check(attachmentCalls==2 && JsonFiles.Read<DeliveryLedger>(attachmentLedger).Parts.Values.Single().Attachment==document,"Retry preserves attachment and sent batch is not reposted");
var multiLedger=Path.Combine(temp,"multipart-report.json");
await DiscordDelivery.Send(http,multiLedger,"batch",endpoint,new string('a',3500),document);
var multiParts=JsonFiles.Read<DeliveryLedger>(multiLedger).Parts.Values.OrderBy(p=>p.Key).ToArray();
Check(multiParts.Length>1 && multiParts.Count(p=>p.Attachment is not null)==1 && multiParts[^1].Attachment==document && multiParts.All(p=>p.Content.Length<=2000),"Only final report part carries attachment within message limit");
var tableFixture = before with { Cards = [
    new CardValue("common", "Common", "Base", "base-c", 100, .10m),
    new CardValue("uncommon", "Uncommon", "Base", "base-uc", 10, 1m)] };
var tableText=Reporting.Render(tableFixture,null,catalog.SourceUpdatedAt,null,new());
var baseRows=tableText.Split('\n').Where(line=>line.StartsWith("| Base ")).ToArray();
var caseCells=baseRows[0].Split('|',StringSplitOptions.RemoveEmptyEntries).Select(c=>c.Trim()).ToArray();
Check(caseCells.SequenceEqual(new[]{"Base","1320","$0.18","$240.00"}),"Case scales copies and EV while retaining weighted average");
var boxCells=baseRows[1].Split('|',StringSplitOptions.RemoveEmptyEntries).Select(c=>c.Trim()).ToArray();
var marketCells=baseRows[2].Split('|',StringSplitOptions.RemoveEmptyEntries).Select(c=>c.Trim()).ToArray();
Check(boxCells.SequenceEqual(new[]{"Base","110","$0.18","$20.00"}),"Box table AVG uses expected-copy weighting, not unweighted card mean");
Check(marketCells.SequenceEqual(new[]{"Base","2","$0.55","$1.10","–"}),"Market AVG is unweighted distinct-card mean; first observation has no trend");
string[] MarketRow(SetReport now, SetReport? old, string label) => Reporting.Render(now,old,catalog.SourceUpdatedAt.AddDays(1),catalog.SourceUpdatedAt,new())
    .Split("Market Data:")[1].Split('\n').First(line=>line.StartsWith("| "+label+" ")).Split('|',StringSplitOptions.RemoveEmptyEntries).Select(c=>c.Trim()).ToArray();
var higher=tableFixture with {Cards=tableFixture.Cards.Select(c=>c with {Price=c.Price+1}).ToArray()};
Check(MarketRow(higher,tableFixture,"Base")[4]=="\U0001F7E2" && MarketRow(tableFixture,higher,"Base")[4]=="\U0001F534","Market totals use requested green/red circle code points");
Check(MarketRow(tableFixture,tableFixture,"Base")[4]=="\U0001F7E1","Unchanged uses requested yellow circle");
Check(MarketRow(higher,tableFixture,"Master set")[2]=="$1.55" && MarketRow(higher,tableFixture,"Master set")[4]=="\U0001F7E2","Master-set AVG and trend");
Check(MarketRow(higher with {ModelHash="new model"},tableFixture,"Master set")[4]=="–","Changed model suppresses trend");
var changedSource=higher with {Cards=higher.Cards.Select((c,i)=>i==0 ? c with {PriceSource="Mid"}:c).ToArray()};
Check(MarketRow(changedSource,tableFixture,"Base")[4]=="–" && MarketRow(changedSource,tableFixture,"Master set")[4]=="–","Source switches suppress category and master-set trends");
var unknown=tableFixture with {Cards=tableFixture.Cards.Select((c,i)=>i==0 ? c with {Price=null}:c).ToArray()};
Check(MarketRow(unknown,tableFixture,"Base")[2]=="--" && MarketRow(unknown,tableFixture,"Base")[4]=="–","Missing price leaves AVG and trend unavailable");
Check(!tableText.Contains('%') && tableText.IndexOf("| Master set",StringComparison.Ordinal)>tableText.IndexOf("Market Data:",StringComparison.Ordinal),"Percentages removed; master set in market table");
var tableMissing=Reporting.Render(tableFixture with {Cards=tableFixture.Cards.Select((c,i)=>i==0 ? c with {Price=null}:c).ToArray()},null,catalog.SourceUpdatedAt,null,new());
var missingCells=tableMissing.Split("Booster Box:")[1].Split('\n').First(line=>line.StartsWith("| Base ")).Split('|',StringSplitOptions.RemoveEmptyEntries).Select(c=>c.Trim()).ToArray();
Check(missingCells[2]=="--" && missingCells[3]=="$10.00*","Missing card cannot become a misleading averaged price");
string[] CaseRow(SetReport now, SetReport? old) => Reporting.Render(now,old,catalog.SourceUpdatedAt.AddDays(1),catalog.SourceUpdatedAt,new())
    .Split('\n').First(line=>line.StartsWith("| Base ")).Split('|',StringSplitOptions.RemoveEmptyEntries).Select(c=>c.Trim()).ToArray();
Check(CaseRow(tableFixture with {BoxesPerCase=10},null).SequenceEqual(new[]{"Base","1100","$0.18","$200.00"}),"Ten-box case table scales without changing average");
Check(CaseRow(higher,tableFixture)[3]=="$1560.00 (+$1320.00)","Comparable case delta scales full-precision box delta");
Check(CaseRow(changedSource,tableFixture)[3]=="$1560.00","Case delta suppressed on pricing source change");
Check(CaseRow(higher,tableFixture with {BoxesPerCase=0})[3]=="$1560.00","Legacy history without case size cannot produce false case delta");
Check(CaseRow(unknown,tableFixture)[2]=="--" && CaseRow(unknown,tableFixture)[3]=="$120.00*","Missing prices retain scaled case subtotal with unavailable average and no delta");
foreach(var profile in allProfiles)
{
    var rendered=Reporting.Render(Calculation.Evaluate(profile,expandedCatalog),null,catalog.SourceUpdatedAt,null,new());
    var pieces=Reporting.Split(rendered,1700);
    Check(pieces.All(p=>p.Length<=1700 && p.Split('\n').Count(l=>l=="```")%2==0),$"{profile.Code}: Discord parts have balanced fences and fit budget");
    foreach(var piece in pieces)
    {
        var pipeLines=piece.Split('\n').Where(l=>l.StartsWith("| ")).ToArray();
        // Each individual table has aligned pipe positions; separate tables can use different widths.
        var inside=false; int[]? positions=null;
        foreach(var line in piece.Split('\n'))
        {
            if(line=="```") {inside=!inside;positions=null;continue;}
            if(!inside || !line.StartsWith("| ")) continue;
            var currentPositions=line.Select((c,i)=>(c,i)).Where(x=>x.c=='|').Select(x=>x.i).ToArray();
            positions ??= currentPositions;
            if(!positions.SequenceEqual(currentPositions)) throw new Exception("Misaligned table: "+profile.Code);
        }
    }
}
var longCode="```\n"+string.Join('\n',Enumerable.Repeat(new string('x',65),80))+"\n```";
var codeParts=Reporting.Split(longCode,200);
Check(codeParts.Length>1 && codeParts.All(p=>p.Length<=200 && p.StartsWith("```\n") && p.EndsWith("\n```")),"Oversized code block reopens correctly across messages");
var summaryBefore = new ReportSnapshot(catalog.SourceUpdatedAt, catalog.SourceUpdatedAt, [before]);
var summaryUp = Reporting.RenderSummary([after with {BoxMarketPrice=before.BoxMarketPrice+1}], summaryBefore);
Check(summaryUp.StartsWith("**Booster Box Summary**"+Environment.NewLine+Environment.NewLine+before.Code+": "+before.Name+Environment.NewLine) && summaryUp.Contains($"  EV: {Reporting.Money(after.KnownEv)} 🟢") && summaryUp.Contains($"  MP: {Reporting.Money(before.BoxMarketPrice!.Value+1)} 🟢"),"Summary header, identifiers, names, indentation, EV and sealed MP trends");
var summaryDown = Reporting.RenderSummary([before], summaryBefore with {Sets=[after with {BoxMarketPrice=before.BoxMarketPrice+1}]});
Check(summaryDown.Split("🔴").Length==3,"Summary falling EV and MP trends");
Check(Reporting.RenderSummary([before],summaryBefore).Split("🟡").Length==3,"Summary unchanged EV and MP trends");
Check(Reporting.RenderSummary([before],null).Split(" –").Length==3,"First summary has no trends");
Check(Reporting.RenderSummary([switched],summaryBefore).Contains($"EV: {Reporting.Money(switched.KnownEv)} –"),"Summary suppresses source-switch EV trend");
Check(Reporting.RenderSummary([before with {ModelHash="changed"}],summaryBefore).Split(" –").Length==3,"Summary suppresses changed-model trends");
Check(Reporting.RenderSummary([withoutBoxPrice],summaryBefore).Contains("MP: Unavailable –"),"Summary missing sealed MP");
Check(Reporting.RenderSummary([missingReport],null).Contains("EV: INCOMPLETE — priced subtotal:"),"Summary labels incomplete EV");
var allSummary = Reporting.RenderSummary(allProfiles.Select(p=>Calculation.Evaluate(p,expandedCatalog)),null);
Check(allSummary.EndsWith(Reporting.Warning) && !allSummary.Contains("Markdown") && !allSummary.Contains("notes"),"Summary standard warning without notes reference");
Check(Reporting.Split(allSummary,1700).Length==1,"All 22 sets fit one final summary message");
var caseSummaryUp=Reporting.RenderSummary([after with {CaseMarketPrice=before.CaseMarketPrice+1}],summaryBefore,true);
Check(caseSummaryUp.StartsWith("**Booster Case Summary**") && caseSummaryUp.Contains($"  EV: {Reporting.Money(after.KnownCaseEv)} 🟢") && caseSummaryUp.Contains($"  MP: {Reporting.Money(before.CaseMarketPrice!.Value+1)} 🟢"),"Case summary uses case EV and actual case MP with trends");
Check(Reporting.RenderSummary([before],summaryBefore with {Sets=[after with {CaseMarketPrice=before.CaseMarketPrice+1}]},true).Split("🔴").Length==3,"Case summary falling trends");
Check(Reporting.RenderSummary([before],summaryBefore,true).Split("🟡").Length==3,"Case summary unchanged trends");
Check(Reporting.RenderSummary([before],null,true).Split(" –").Length==3,"First case summary has no trends");
Check(Reporting.RenderSummary([switched],summaryBefore,true).Contains($"EV: {Reporting.Money(switched.KnownCaseEv)} –"),"Case summary suppresses source-switch EV trend");
Check(Reporting.RenderSummary([before with {ModelHash="changed"}],summaryBefore,true).Split(" –").Length==3,"Case summary suppresses changed-model trends");
Check(Reporting.RenderSummary([noCase],summaryBefore,true).Contains("MP: Unavailable –"),"Case summary missing sealed MP");
Check(Reporting.RenderSummary([missingReport],null,true).Contains($"EV: INCOMPLETE — priced subtotal: {Reporting.Money(missingReport.KnownCaseEv)}"),"Case summary labels scaled incomplete EV");
Check(Reporting.RenderSummary([before],summaryBefore with {Sets=[before with {BoxesPerCase=0,CaseMarketPrice=null}]},true).Split(" –").Length==3,"Legacy case history cannot produce false trends");
foreach(var boxes in new[]{false,true})
foreach(var cases in new[]{false,true})
{
    var summaries=Reporting.RenderSummaries([before],summaryBefore,boxes,cases);
    Check(summaries.Select(s=>s.Key).SequenceEqual((cases?new[]{"case-summary"}:Array.Empty<string>()).Concat(boxes?new[]{"summary"}:Array.Empty<string>())),"Summary selection and order follow independent display switches");
    Check(summaries.All(s=>s.Content.Contains(before.Code+": "+before.Name) && !s.Content.Contains("PRB-01:")),"Summaries retain supplied set selection");
}
Check(Reporting.RenderSummaries([],null).Length==0,"No summaries when no eligible reports");
var allCaseSummary=Reporting.RenderSummary(allProfiles.Select(p=>Calculation.Evaluate(p,expandedCatalog)),null,true);
Check(allCaseSummary.EndsWith(Reporting.Warning) && !allCaseSummary.Contains("Markdown") && Reporting.Split(allCaseSummary).All(p=>p.Length<=1900),"Case summary warning and Discord limits");
var summaryCalls=0;
using var summaryHttp=new HttpClient(new FakeHandler(request=>
{
    summaryCalls++;
    Check(request.Content is not MultipartFormDataContent,"Summary sends no attachment");
    return new(HttpStatusCode.OK){Content=new StringContent("{\"id\":\"summary\"}")};
}));
var summaryLedger=Path.Combine(temp,"summary.json");
await DiscordDelivery.Send(summaryHttp,summaryLedger,"snapshot:summary",endpoint,allSummary);
await DiscordDelivery.Send(summaryHttp,summaryLedger,"snapshot:summary",endpoint,"Changed summary");
Check(summaryCalls==1 && JsonFiles.Read<DeliveryLedger>(summaryLedger).Parts.Values.Single().Attachment is null,"Summary delivery is frozen and deduplicated without notes");
await DiscordDelivery.Send(summaryHttp,summaryLedger,"snapshot:case-summary",endpoint,allCaseSummary);
await DiscordDelivery.Send(summaryHttp,summaryLedger,"snapshot:case-summary",endpoint,"Changed case summary");
Check(summaryCalls==2 && JsonFiles.Read<DeliveryLedger>(summaryLedger).Parts.Values.All(p=>p.Attachment is null),"Case and box summaries have independent frozen delivery keys and no attachments");
Console.WriteLine($"PASS: {checks} checks; no real Discord requests.");

sealed class FakeHandler(Func<HttpRequestMessage,HttpResponseMessage> respond):HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)=>Task.FromResult(respond(request));
}
