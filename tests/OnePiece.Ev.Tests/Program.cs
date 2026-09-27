using OnePiece.Ev;
using System.Net;
using System.Text;

var checks=0;
void Check(bool ok,string name) {if(!ok) throw new Exception(name); checks++;}
var profiles=JsonFiles.Read<ProfileFile>("config/pullrates.json").Sets;
foreach(var p in profiles) Calculation.Validate(p);
var groups=profiles.SelectMany(p=>p.Variants.Select(v=>v.GroupId).Append(p.GroupId)).Distinct().ToArray();
var catalog=Catalog.Import("research/catalog-cache",groups);
var reports=profiles.Select(p=>Calculation.Evaluate(p,catalog)).ToArray();
Check(reports[0].Complete && reports[1].Complete,"OP/EB complete");
Check(reports[0].BoxMarketPrice == 246.47m, "Box MP uses configured sealed box Normal listing");
var noBoxPrice = catalog with { Data = catalog.Data.Select(g => g with { Prices = g.Prices.Where(p => p.ProductId != profiles[0].BoosterBoxProductId).ToArray() }).ToArray() };
var withoutBoxPrice = Calculation.Evaluate(profiles[0], noBoxPrice);
Check(withoutBoxPrice.BoxMarketPrice is null && withoutBoxPrice.Complete && withoutBoxPrice.KnownEv == reports[0].KnownEv && withoutBoxPrice.KnownMaster == reports[0].KnownMaster, "Missing box MP does not alter card EV or master set");
Check(Reporting.Render(withoutBoxPrice,null,catalog.SourceUpdatedAt,null,new()).Contains("MP: Unavailable"), "Unknown sealed price displayed explicitly");
Check(!reports[2].Complete && reports[2].Cards.Count(c=>c.Price is null)==1,"Missing price not zero");
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
Check(!Calculation.Comparable(reports[2],reports[2]),"Incomplete comparison suppressed");
Check(Reporting.Render(reports[2],null,catalog.SourceUpdatedAt,null,new()).Contains("INCOMPLETE"),"Subtotal labeled");
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
Console.WriteLine($"PASS: {checks} checks; no real Discord requests.");

sealed class FakeHandler(Func<HttpRequestMessage,HttpResponseMessage> respond):HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)=>Task.FromResult(respond(request));
}
