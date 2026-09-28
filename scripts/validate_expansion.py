"""Verify expanded rosters against publisher checklist counts and emit a coverage audit."""
import collections, hashlib, json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
read=lambda path:json.loads(path.read_text(encoding='utf-8-sig'))
profile_file=read(ROOT/'config/pullrates.json')
profiles=profile_file['sets']
assert len(profiles)==22 and len(profile_file['plannedSets'])==2
assert profiles==sorted(profiles,key=lambda p:(p['releaseDate'],p['code']))
reports={r['code']:r for r in read(ROOT/'out/latest.json')['sets']}
expected_codes={'OP-'+str(i).zfill(2) for i in range(1,18) if i not in [14,15]}|{'OP14-EB04','OP15-EB04','EB-01','EB-02','EB-03','PRB-01','PRB-02'}
assert {p['code'] for p in profiles}==expected_codes
rows=[]
for p in profiles:
    code=p['code']
    if code in ['OP-08','EB-01','PRB-01']:
        checklist=ROOT/'research'/f'{code.lower()}-official-checklist.json'
    else:
        slug='op14' if code=='OP14-EB04' else 'op15' if code=='OP15-EB04' else code.lower().replace('-','')
        checklist=ROOT/'research'/f'{slug}-coverage-checklist.json'
    official=read(checklist)
    actual=collections.Counter(v['number'] for v in p['variants'] if v['number'])
    expected=collections.Counter(v['number'] for v in official)
    assert actual==expected,(code,actual-expected,expected-actual)
    assert len({(v['productId'],v['subTypeName']) for v in p['variants']})==len(p['variants'])
    total=sum(pool['expectedCopiesPerBox'] for pool in p['pools'])
    assert abs(total-(p['packsPerBox']*p['cardsPerPack']+p['bonusCardsPerBox']))<1e-8
    report=reports[code]
    assert not report['issues'],(code,report['issues'])
    case_products=read(ROOT/'research/catalog-cache'/str(p['groupId'])/'products.json')['results']
    case=next(x for x in case_products if x['productId']==p['boosterCaseProductId'])
    assert "Box Case" in case['name']
    assert report['boxesPerCase']==p['boxesPerCase']==(10 if code in ['PRB-01','PRB-02'] else 12)
    missing=[dict(key=c['key'],name=c['name']) for c in report['cards'] if c['price'] is None]
    rows.append(dict(code=code,releaseDate=p['releaseDate'],groupId=p['groupId'],variants=len(p['variants']),
                     boosterCaseProductId=p['boosterCaseProductId'],caseName=case['name'],boxesPerCase=p['boxesPerCase'],caseMarketPrice=report['caseMarketPrice'],
                     expectedCards=round(total,6),numberedChecklistEntries=len(official),
                     checklistSha256=hashlib.sha256(checklist.read_bytes()).hexdigest(),
                     missingPrices=missing,midEstimates=sum(c['priceSource']=='Mid' for c in report['cards']),
                     exclusions=len(p['exclusions']),sources=p['sources']))
data=dict(sourceUpdatedAt=read(ROOT/'out/latest.json')['sourceUpdatedAt'],sets=rows,plannedSets=profile_file['plannedSets'])
(ROOT/'research/coverage-verification.json').write_text(json.dumps(data,indent=2)+'\n',encoding='utf-8')
lines=['# Expanded set coverage audit','',
       'All 22 released rosters reconcile by numbered-artwork multiplicity with their saved Bandai checklists. DON finishes are additional. This validates roster accounting, not measured odds or a visual image-by-image equivalence audit. Rates remain explicit community-based estimates and low-confidence assumptions.','',
       '| Set | English release | Variants | Expected physical cards | Missing prices | Mid fallbacks |',
       '| --- | --- | ---: | ---: | ---: | ---: |']
for r in rows: lines.append(f"| {r['code']} | {r['releaseDate']} | {r['variants']} | {r['expectedCards']:g} | {len(r['missingPrices'])} | {r['midEstimates']} |")
lines+=['','## Specific decisions','',
        '- OP01/02 include one packaged topper; OP01 sealed MP uses the later White box listing, avoiding the first-wave collectible box premium.',
        '- Cases use explicit Normal TCGCSV product IDs from the frozen 2026-09-26 catalog; OP01 uses Wave 2 White. Case contents EV scales box EV by 12 (OP/EB) or 10 (PRB01/02). Sealed case MP is Market-only; unavailable quotes do not affect contents completeness.',
        '- OP14-EB04 and OP15-EB04 remain combined English products, matching Bandai labels. TCGCSV calls the former OP14 and calls EB-03 EB-03-04; IDs are mapped explicitly.',
        '- Dash Packs are purchase campaigns, not assumed to be inside sealed boxes. Double Pack and tournament products are excluded.',
        '- Additional Normal finish quotes for ordinary R/SR/SEC are excluded from booster rosters. OP10 Normal alternate DON is unverified and excluded.',
        '- Publisher rarity overrides TCGCSV on EB04-029 (UC) and EB04-053 (R). Sentomaru has only a Normal quote; require Foil and retain unknown pricing.',
        '- PRB02 has 316 numbered artwork entries plus 90 DON finishes. Its four special Event artworks share the SP rate allocation, not ordinary AA.',
        '- OP13 Elder parallels are modeled as demon-pack exclusives. Imu AA Leader combines ordinary and special-pack expected counts without duplicate master-set entries.',
        '- OP09 Gold Roger, OP11/12/13 anniversary treatments, signature/red/super variants, Gold DON and Pandaman each use separate pools.',
        '- Scarce-event frequencies and replacement patterns are estimates. New gold (1/1200 boxes), silver (1/120), signature (1/240), red-super (1/1200), and some super-art rates are explicit placeholders, not measured rates. Attachment warnings flag them.',
        '- EB03 and OP17 special-pack rosters are modeled approximately; PRB02 assumes equal SR weighting despite reports of unequal new/reprint SR frequencies. These remain material limitations.',
        '- EB05 (2026-10-30) and OP18 (2026-11-20) are planned, inactive entries. Their catalogs are incomplete; reaching the date alone will not activate an unreviewed profile.','',
        '## Missing quotes','']
for r in rows:
    for c in r['missingPrices']: lines.append(f"- {r['code']}: {c['name']} ({c['key']}).")
lines+=['','## Verification','',
        'Run `python scripts/validate_expansion.py` after a full-set preview. It checks release ordering, requested coverage, unique identities, physical counts, official numbered-artwork multiplicities and runtime mapping issues. The companion JSON stores checklist hashes and per-set source URLs. C# tests additionally exercise release filtering, exact bonus counts, special pools, prices, report splitting, history and simulated webhook delivery. No bulk Discord posting was performed for this expansion.']
(ROOT/'research/COVERAGE-AUDIT.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
print(f'PASS: {len(rows)} released sets; {sum(r["variants"] for r in rows)} variants; publisher checklist counts match; {sum(len(r["missingPrices"]) for r in rows)} missing quotes; two future releases inactive.')
