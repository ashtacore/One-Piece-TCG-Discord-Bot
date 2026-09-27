"""Build the initial editable application profiles from the audited allowlists."""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
read = lambda path: json.loads((ROOT / path).read_text(encoding="utf-8"))
guide = "https://www.reddit.com/r/OnePieceTCGFinance/comments/1fp510n/understanding_one_piece_tcg_rarity_system_pull/"
primer = "https://www.reddit.com/r/OnePieceTCG/comments/1jgnfii/a_casual_players_primer_on_booster_box_drop_rates/"
profiles = []
specs = [
    ("OP-08", "Two Legends", 23462, 542504, "2024-09-13", 24, 12, 12,
     {"base-c":166,"base-uc":64,"base-r":38.5,"base-sr":7,"base-l":8,"don-foil":2,
      "base-sec":8/12,"sp":2/12,"aa-leader":4/12,"aa":16/12-1/12-1/36,"tr":1/12,"manga":1/36},
     ["Case recipe estimates AA 16, SEC 8, SP 2 and AA Leader 4 per 12 boxes; ordinary SR 7, Leader 8 and DON 2 per box.",
      "TR 1 per case and Manga 1 per 3 cases are low-confidence modeling assumptions, not guarantees; each displaces an ordinary AA.",
      "Bulk assumption: 7 C and 3 UC per pack before DON replaces C and Leaders replace UC. Remaining rare slots are R.",
      "Exclude the two DP-05 bonus DON listings; they do not come from this booster box."]),
    ("EB-01", "Memorial Collection", 23333, 521161, "2024-05-03",24,12,12,
     {"base-c":234,"base-r":39-1/36,"base-sr":6,"base-l":6,"aa":2,"base-sec":8/12,"aa-leader":4/12,"manga":1/36},
     ["Model 2 ordinary AA plus one SEC or AA Leader per box; SEC 8 and AA Leader 4 per case are estimates.",
      "Ordinary SR 6 and Leader 6 per box; assume 10 C per pack before Leader replacement and fill remaining rare slots with R.",
      "Manga 1 per 3 cases is an estimate. Adopt R displacement for the reported fourth hit; exact displaced slot is unverified."]),
    ("PRB-01", "The Best",23496,545399,"2024-11-08",20,10,10,{},
     ["English configuration: 20 ten-card packs, 10 boxes per case. Cases are not assumed to contain 12 boxes.",
      "Regular box model: 100 bulk C/UC/P, 20 R, 18 SR, 2 SEC, 20 DON, 2 AA-level hits, 2 full-art/textured hits, 1 Leader, 35 Jolly Roger.",
      "Allocate bulk and full-art/textured combined slots uniformly across their eligible cards; this is an explicit unverified collation assumption.",
      "DON allocation: 18 Normal, 1.8 Foil, 0.2 Gold per regular box. Gold replaces Foil.",
      "Sanji AA Leader and standalone Manga Nami each assumed 1 per 30 boxes and replace ordinary AA.",
      "God pack assumed 1 per 150 boxes (midpoint of 10-20 ten-box cases). It replaces one regular pack with Nami plus nine Manga reprints. Multiply all regular counts by 1-1/3000, then add 10/150 Manga cards.",
      "54 ordinary reprints use explicitly matched original English number/rarity/finish listings as shared-price proxies. Physical artwork equivalence is not independently verified for every card; this is an adopted pricing assumption, not a completed visual audit.",
      "Exclude unverified Brannew Normal; include its Foil finish. Manga Ace has a missing market price and must remain unknown."])
]
for code,name,gid,box,date,packs,cards,case,rates,assumptions in specs:
    if code == "PRB-01":
        rates = {"base-c":100*23/52,"base-uc":100*25/52,"base-pr":100*4/52,"base-r":20,"base-sr":18,"base-sec":2,
                 "base-l":1,"don-normal":18,"don-foil":1.8,"don-gold":.2,"aa":2-2/30,"aa-leader":1/30,
                 "manga-nami":1/30,"full-art":2*50/72,"textured-event-stage":2*22/72,"jolly-roger":35}
        rates = {k:v*(1-1/3000) for k,v in rates.items()}
        rates["manga-nami"] += 1/150
        rates["manga-reprint-god-pack"] = 9/150
    mappings = read(f"research/{code.lower()}-variant-mapping.json")
    variants, exclusions = [], []
    for item in mappings:
        if item["pool"] not in rates:
            exclusions.append({"productId":item["productId"],"subTypeName":None if item["pool"] == "external-double-pack-don" else item["subTypeName"],"reason":item["pool"]})
            continue
        variant = {"groupId":item["sourceGroupId"],"productId":item["productId"],"subTypeName":item["subTypeName"],
                   "name":item["name"],"number":item["number"],"category":item["reportCategory"],"pool":item["pool"],"weight":1}
        if item["sourceGroupId"] != gid: variant["pricingNote"] = "Explicit shared original-listing price proxy; see profile assumptions."
        variants.append(variant)
    rates = {k:round(v,12) for k,v in rates.items()}
    profiles.append(dict(code=code,name=name,version="2026-09-27.1",confidence="Community estimates; chase frequencies and collation assumptions unverified",
                         enabled=True,groupId=gid,boosterBoxProductId=box,releaseDate=date,packsPerBox=packs,cardsPerPack=cards,boxesPerCase=case,
                         bonusCardsPerBox=0,sources=[f"https://en.onepiece-cardgame.com/products/boosters/{code.lower().replace('-','')}.php",guide,primer],
                         assumptions=assumptions,pools=[dict(id=k,expectedCopiesPerBox=v) for k,v in rates.items()],variants=variants,exclusions=exclusions))
(ROOT/"config").mkdir(exist_ok=True)
(ROOT/"config/pullrates.json").write_text(json.dumps(dict(schemaVersion=1,sets=profiles),indent=2)+"\n",encoding="utf-8")
print("Built profiles:", [(p['code'], len(p['variants']), sum(x['expectedCopiesPerBox'] for x in p['pools'])) for p in profiles])
