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
var endpoint=new Uri("https://discord.com/api/webhooks/1/fake?wait=true");
var calls=0;
using var http=new HttpClient(new FakeHandler(_=>{calls++;return new(HttpStatusCode.OK){Content=new StringContent("{\"id\":\"123\"}")};}));
var ledger=Path.Combine(temp,"sent.json");
await DiscordDelivery.Send(http,ledger,"batch",endpoint,"hello");
await DiscordDelivery.Send(http,ledger,"batch",endpoint,"changed");
Check(calls==1,"Repeated batch not reposted");
Check(JsonFiles.Read<DeliveryLedger>(ledger).Parts.Values.Single().Content=="hello\n\n\u200b","Frozen outbox preserves report separator");
var rateCalls=0;
using var rate=new HttpClient(new FakeHandler(_=>++rateCalls==1 ? new((HttpStatusCode)429){Content=new StringContent("{\"retry_after\":0}")} : new(HttpStatusCode.OK){Content=new StringContent("{\"id\":\"456\"}")}));
await DiscordDelivery.Send(rate,Path.Combine(temp,"rate.json"),"batch",endpoint,"hello");
Check(rateCalls==2,"429 retry");
var timeoutCalls=0;
using var timeout=new HttpClient(new FakeHandler(_=>{timeoutCalls++;throw new HttpRequestException("simulated ambiguous failure");}));
var uncertain=Path.Combine(temp,"uncertain.json");
for(int i=0;i<2;i++) {try{await DiscordDelivery.Send(timeout,uncertain,"batch",endpoint,"hello");throw new Exception("Expected failure");}catch(InvalidDataException){}}
Check(timeoutCalls==1 && JsonFiles.Read<DeliveryLedger>(uncertain).Parts.Values.Single().Status=="uncertain","Ambiguous delivery never retried automatically");
var document=AssumptionDocument.Create(profiles[2],catalog.SourceUpdatedAt);
Check(document.FileName=="PRB-01-pull-rate-assumptions.md" && document.Content.Contains("AFTER replacement") && document.Content.Contains("0.039988888889") && document.Content.Contains(Calculation.ModelHash(profiles[2])),"Attachment matches adjusted profile and version hash");
Check(Reporting.Render(reports[2],null,catalog.SourceUpdatedAt,null,new()).EndsWith("Expected cards per box and model notes: see the accompanying Markdown file."),"Report ends with assumptions warning and attachment reference");
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
Console.WriteLine($"PASS: {checks} checks; no real Discord requests.");

sealed class FakeHandler(Func<HttpRequestMessage,HttpResponseMessage> respond):HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)=>Task.FromResult(respond(request));
}
