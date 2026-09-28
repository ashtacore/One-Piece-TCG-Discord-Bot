"""Build reviewed release-specific assumptions from a frozen catalog and publisher checklists.

Outputs explicit allowlists, never a runtime name-based classifier.
"""
import collections, json, re
from pathlib import Path
from sealed_cases import CASE_PRODUCTS
ROOT=Path(__file__).resolve().parents[1]
CACHE=ROOT/'research/catalog-cache'
read=lambda path:json.loads(path.read_text(encoding='utf-8-sig'))
fields=lambda p:{f['name']:f['value'] for f in p.get('extendedData',[])}
registry=read(ROOT/'research/set-registry.json')['groups']
GROUPS={g['groupId']:g for g in registry}
SETS=[('OP-01',3188,557280),('OP-02',17698,455866),('OP-03',22890,477176),('OP-04',23024,485833),
      ('OP-05',23213,498734),('OP-06',23272,515080),('OP-07',23387,532106),('OP-09',23589,563834),
      ('OP-10',23766,586671),('EB-02',23834,594069),('OP-11',24241,620180),('OP-12',24302,628346),
      ('PRB-02',24305,628452),('OP-13',24303,628352),('OP14-EB04',24537,665598),('EB-03',24545,666891),
      ('OP15-EB04',24637,682057),('OP-16',24664,689336),('OP-17',24736,704752)]

def localcode(code):
    return 'op14' if code=='OP14-EB04' else 'op15' if code=='OP15-EB04' else code.lower().replace('-','')

def exclusion(p):
    name=p['name']
    if any(t in name for t in ['Dash Pack','Double Pack','Special DON!! Card Pack','Film RED Promo','CS26-27','Heroines Special Set']):
        return 'External purchase campaign, separate sealed product or tournament promo; not inside booster box.'
    if p['productId']==549342: return 'Generic DON listing not identified as OP-02 booster custom DON.'
    if p['productId'] in [675745,675746]: return 'Heroines purchase campaign card, absent from EB-03 booster checklist.'
    return None

def classify(p,finish,code):
    name=p['name']; rarity=fields(p)['Rarity']
    if p['productId']==671340: return 'Base','base-uc'
    if p['productId']==685324: return 'Foils','base-r'
    basecat='Base' if rarity in ['C','UC'] else 'Leaders' if rarity=='L' else 'Other' if rarity in ['P','PR'] else 'Foils'
    if p['productId'] in [653833,653834,653835,653836]: return 'SP','event-sp'
    if p['productId']==632503: return 'Manga / Other Chase','silver-chase'
    if p['productId']==597065: return 'Manga / Other Chase','gold-chase'
    if p['productId'] in [657348,657354,657357,657364,657368]: return 'Manga / Other Chase','demon-elders'
    if p['productId']==657345: return 'AA','imu-aa-leader'
    if rarity=='DON!!':
        return 'DON!!', 'don-special' if '(Special Foil)' in name else 'don-gold' if '(Gold)' in name else 'don-foil' if finish=='Foil' else 'don-normal'
    if '(Box Topper)' in name: return 'AA','box-topper'
    if '(Gold-Stamped Signature)' in name: return 'Manga / Other Chase','signature'
    if '(Red Super Alternate Art)' in name: return 'Manga / Other Chase','red-super'
    if '(Super Leader Alternate Art)' in name: return 'Manga / Other Chase','super-leader'
    if '(Super Alternate Art)' in name: return 'Manga / Other Chase','super-alt'
    if '(Gold)' in name: return 'Manga / Other Chase','gold-chase'
    if '(Silver)' in name: return 'Manga / Other Chase','silver-chase'
    if '(Manga)' in name: return 'Manga / Other Chase','manga'
    if rarity=='TR' or '(TR)' in name: return 'TR','tr'
    if '(Wanted Poster)' in name: return 'SP','wanted'
    if '(SP)' in name: return 'SP','sp-leader' if rarity=='L' else 'sp'
    if '(Pandaman Art)' in name: return 'AA','pandaman'
    if '(Pirate Foil)' in name: return basecat,'pirate-foil'
    if '(Alternate Art)' in name or '(Parallel)' in name or '(Full Art)' in name:
        return 'AA','aa-leader' if rarity=='L' else 'aa'
    return basecat,'base-'+('pr' if rarity=='P' else rarity.lower())

def mapping(code,gid):
    products=read(CACHE/str(gid)/'products.json')['results']
    prices=read(CACHE/str(gid)/'prices.json')['results']
    variants=[]; excluded=[]
    for p in products:
        f=fields(p)
        if not f.get('Rarity'): continue
        reason=exclusion(p)
        if reason:
            excluded.append(dict(productId=p['productId'],subTypeName=None,reason=reason)); continue
        finishes=[r['subTypeName'] for r in prices if r['productId']==p['productId']]
        if p['productId']==685324:
            excluded.append(dict(productId=p['productId'],subTypeName='Normal',reason='Publisher lists EB04-053 as R, but provider labels it C/Normal. Do not substitute an unverified Normal quote for the Foil variant.'))
            finishes=['Foil']
        if not finishes:
            # Missing price rows must not remove a catalog card from the roster.
            special=any(t in p['name'] for t in ['Alternate Art','Parallel','Manga','(SP)','(Gold)','(Silver)','(TR)','Box Topper','Wanted Poster','Pirate Foil','Pandaman'])
            finishes=['Foil' if special or f['Rarity'] not in ['C','UC','L','PR','P'] else 'Normal']
        for finish in finishes:
            if finish=='Normal' and f['Rarity'] in ['R','SR','SEC']:
                excluded.append(dict(productId=p['productId'],subTypeName=finish,reason='Ordinary booster R/SR/SEC is Foil; shared Normal finish is not in the booster checklist.')); continue
            if code=='OP-10' and f['Rarity']=='DON!!' and finish=='Normal':
                excluded.append(dict(productId=p['productId'],subTypeName=finish,reason='Unverified Normal finish of the booster alternate DON; retain Foil.')); continue
            category,pool=classify(p,finish,code)
            variants.append(dict(groupId=gid,productId=p['productId'],subTypeName=finish,name=p['name'],number=f.get('Number'),category=category,pool=pool,weight=1))
            if p['productId'] in [671340,685324]:
                variants[-1]['pricingNote']='Publisher rarity overrides provider C metadata: EB04-029 is UC; EB04-053 is R. For Sentomaru require Foil price and keep unknown while only Normal is quoted.'
    return variants,excluded

GUIDE='https://www.reddit.com/r/OnePieceTCGFinance/comments/1fp510n/understanding_one_piece_tcg_rarity_system_pull/'
PRIMER='https://www.reddit.com/r/OnePieceTCG/comments/1jgnfii/a_casual_players_primer_on_booster_box_drop_rates/'

def profile(code,gid,box):
    variants,excluded=mapping(code,gid)
    counts=collections.Counter(v['pool'] for v in variants)
    official=read(ROOT/'research'/f'{localcode(code)}-coverage-checklist.json')
    actual=collections.Counter(v['number'] for v in variants if v['number'])
    expected=collections.Counter(v['number'] for v in official)
    if actual!=expected: raise ValueError((code,'Numbered artwork counts do not reconcile',actual-expected,expected-actual))
    urlcode=code.lower().replace('-','') if code not in ['OP14-EB04','OP15-EB04'] else code.lower()
    producturl=f'https://en.onepiece-cardgame.com/products/boosters/{urlcode}.php'
    if code in ['OP-16','OP-17']: producturl=f'https://en.onepiece-cardgame.com/products/{urlcode}.html'
    sources=[producturl,f'https://en.onepiece-cardgame.com/cardlist/?series={569100+int(code[3:]) if code.startswith("OP-") else 569114 if code=="OP14-EB04" else 569115 if code=="OP15-EB04" else 569302 if code=="PRB-02" else 569200+int(code[3:])}',GUIDE,PRIMER]
    assumptions=[
        'English booster contents only. Exclude purchase-campaign Dash Packs, Double Pack DON, tournament promos and unrelated sealed products. Missing price rows remain unknown rather than excluding a card.',
        'Community rates are adopted working estimates, not guarantees. Every card within a pool has equal weight unless specifically separated. These probabilities are not a fitted distribution of box returns.',
        'Match numbered artwork counts to the official checklist; DON finishes are reviewed separately. This does not establish precise production frequencies or reprint-wave collation.',
    ]
    if code in ['OP14-EB04','OP15-EB04']:
        assumptions+=['Publisher rarity overrides two provider metadata errors: EB04-029 is UC, not C; EB04-053 Sentomaru is R, not C. Sentomaru requires a Foil quote; the provider-only Normal quote is quarantined until finish equivalence is verified.']
    rates={}; packs=24; cards=12; case=12; bonus=1 if code in ['OP-01','OP-02'] else 0
    def assign(pool,rate):
        if pool in counts: rates[pool]=rate
    if code=='PRB-02':
        packs,cards,case=20,10,10
        bulk=sum(counts[p] for p in ['base-c','base-uc','base-pr'])
        for p in ['base-c','base-uc','base-pr']: assign(p,100*counts[p]/bulk)
        rates.update({'base-r':20,'base-sr':18,'base-sec':2,'don-normal':18,'don-foil':1.8,'don-gold':.2,
                      'aa':2-1/30,'manga':1/30,'sp':.2*6/10,'event-sp':.2*4/10,'pirate-foil':37.8})
        # Ten gold DON replace one ten-card pack. Uniform marginal weights model unknown pack combinations.
        g=1/175
        rates={p:r*(1-g/20) for p,r in rates.items()}
        rates['don-gold']+=10*g
        assumptions += [
            '20 ten-card packs and 10 boxes per case. Regular box: 100 C/UC/P, 20 R, 18 SR, 2 SEC, 20 DON, 2 AA-level hits, 0.2 SP/event-SP, and residual 37.8 Pirate Foils. Bulk/Pirate allocation is an unmeasured model assumption.',
            'English opening reports describe 2 AA and 2 SEC per box, with 2 SP/event-art hits per ten-box case. Split the shared pool equally over 6 SP and 4 event-art cards (0.12 SP and 0.08 event-art per box).',
            'Regular DON counts: 18 Normal, 1.8 Foil, 0.2 Gold; Gold replaces Foil. Manga Sanji assumed once per 30 boxes, replacing AA; its exact rate/displacement is uncertain.',
            'Gold DON god pack assumed once per 175 boxes (17.5 ten-box cases), replacing a regular ten-card pack. Scale regular counts by 1-1/3500, then add 10/175 Gold DON per box, equally weighted across 30 Gold designs. This models marginal EV, not exact combinations.',
            'SR cards are equally weighted despite opening reports suggesting new SRs occur less often than reprint SRs; this is a material unresolved weighting assumption.'
        ]
        sources += ['https://www.reddit.com/r/OnePieceTCG/comments/1nse1rp/prb_02_hit_rates_and_thoughts/','https://www.reddit.com/r/OnePieceTCG/comments/1m8nek4/','https://zardocards.com/products/prb-02-the-best-vol-2-booster-pack']
    elif code.startswith('EB-'):
        rates.update({'base-l':6,'base-sr':6,'aa':2,'base-sec':2/3})
        if code=='EB-02':
            rates['base-sec']=1
            rates['aa-leader']=1/12
            rates['sp-leader']=1/12
            rates['manga']=1/36; rates['aa']-=1/36
            assumptions += ['EB-02: adopt 2 ordinary AA, 1 SEC, 6 SR and 6 ordinary Leaders per box. AA Leader and SP Leader each assumed once per 12-box case; Manga once per 36 boxes displaces AA. Residual premium-slot accounting comes from R. Leader odds are disputed across regions and remain low-confidence estimates.']
            sources+=['https://www.reddit.com/r/OnePieceTCGFinance/comments/1qi62if/finally_completed_the_eb02_sp_leaders/','https://www.reddit.com/r/OnePieceTCGFinance/comments/1vidunp/eb02/']
        else:
            rates.update({'aa-leader':1/12,'sp':2/12,'manga':1/36,'don-normal':1,'don-foil':1-1/12,'don-gold':1/12})
            rates['aa']-=1/36
            assumptions += ['EB-03: use EB-style 6 SR, 6 Leaders, 2 ordinary AA (before Manga replacement), 2/3 SEC; AA Leader 1/12, SP 2/12, Manga 1/36 per box. Adopt 2 DON: 1 Normal plus 1 premium finish, with Gold 1/12 replacing Foil. Exact DON finish collation is unverified.',
                            'Heroines special pack: assume one per 180 boxes, replacing six randomly distributed ordinary cards with six SP cards sampled uniformly from the nine eligible SPs. Exact English special-pack roster/remaining slots are unverified; this is an explicit marginal-count approximation.']
        don=sum(r for p,r in rates.items() if p.startswith('don-'))
        rates['base-c']=240-rates['base-l']-don
        rates['base-r']=288-sum(rates.values())
        if code=='EB-03':
            # Six replacement slots; the remainder of the 12-card pack is left at ordinary expectation.
            factor=1-6/(180*288)
            rates={p:r*factor for p,r in rates.items()}; rates['sp']+=6/180
    else:
        early=code in ['OP-01','OP-02']; third=code=='OP-03'
        don=1 if early else 2
        rates.update({'base-c':168-don,'base-uc':64,'base-l':8,'base-sr':7,'base-sec':8/12,'aa-leader':4/12,'aa':16/12})
        assign('sp',2/12); assign('wanted',1/12)
        if third: rates['aa']=20/12; rates['aa-leader']=4/12; rates['wanted']=2/12
        if code=='OP-04': rates['aa']=20/12; rates['sp']=2/12
        if code=='OP-05':
            rates['aa']=16/12; rates['base-sec']=1; rates['aa-leader']=2/12; rates['sp']=4/12
        assign('tr',1/12)
        assign('manga',1/36)
        assign('signature',1/240)
        assign('gold-chase',1/1200)
        assign('silver-chase',1/120)
        assign('red-super',1/1200)
        assign('super-alt',1/36 if code=='OP-13' else 1/120)
        assign('super-leader',1/120)
        assign('pandaman',7/12)
        # Manga occupies SEC in early products; later chase replacements use AA as a working assumption.
        chase=sum(r for p,r in rates.items() if p in ['tr','manga','signature','gold-chase','silver-chase','red-super','super-alt','super-leader'])
        if early or third:
            rates['base-sec']-=rates.get('manga',0); chase-=rates.get('manga',0)
        rates['aa']-=chase
        if code=='OP-09':
            rates['sp']=1/12; rates['wanted']=1/12
        don_rare=2/12 if 'don-gold' in counts or 'don-special' in counts else 0
        rare_variants=counts['don-gold']+counts['don-special']
        if rare_variants:
            assign('don-gold',don_rare*counts['don-gold']/rare_variants)
            assign('don-special',don_rare*counts['don-special']/rare_variants)
        assign('don-foil',don-don_rare)
        assign('don-normal',don-don_rare)
        assign('box-topper',1)
        if code=='OP-13':
            # Imu occurs as an ordinary AA Leader and as a guaranteed special-pack card.
            rates['imu-aa-leader']=rates['aa-leader']/6; rates['aa-leader']*=5/6
            rates['demon-elders']=0
        rates['base-r']=288+bonus-sum(rates.values())
        if code=='OP-13':
            g=1/180
            rates={p:r*(1-g/24) for p,r in rates.items()}
            rates['demon-elders']+=5*g; rates['imu-aa-leader']+=g; rates['aa']+=6*g
            assumptions += ['Demon pack assumed once per 180 boxes, replacing one 12-card pack: five special Elder parallels, one Imu AA Leader and six ordinary AA. Scale regular counts by 1-1/4320 then add those counts. Reports of reduced AA Leaders elsewhere in the same case are not modeled; exact case-level displacement and the six filler AAs are uncertain.']
            sources+=['https://www.reddit.com/r/OnePieceTCG/comments/1ohjo5c/op13_case_odds/','https://www.reddit.com/r/OnePieceTCGFinance/comments/1ojpcjq/pulled_a_god_pack/']
        if code=='OP-17':
            g=1/180
            rates={p:r*(1-g/24) for p,r in rates.items()}; rates['aa']+=12*g
            assumptions+=['OP-17 special AA pack: adopt one per 180 boxes replacing one ordinary 12-card pack with 12 ordinary AAs sampled uniformly. Its exact English roster and event frequency are unmeasured; this is a marginal EV approximation. Pandaman variants average 7 per case and replace R; ultra alternate/leader variants are modeled separately at 1/120 boxes per pool.']
            sources+=['https://www.reddit.com/r/OnePieceTCG/comments/1voyt5t/op17_god_pack_confirmed/','https://tcgtalk.com/guides/op17-pull-rates-case-opening']
        assumptions += [
            'Standard OP bulk model: 7 C and 3 UC per pack; 8 Leaders replace UC per box, booster DON replace C. SR 7; ordinary R fills remaining physical slots. Combined OP14/OP15-EB04 use this same slot model over their larger card pools.',
            'OP01/02: 1 DON, AA 16/12, SEC 8/12 and AA Leader 4/12 per box before Manga replaces SEC. OP03: 2 DON, AA 20/12, SEC 8/12, AA Leader 4/12, Wanted 2/12. OP04: AA 20/12 and SP 2/12. OP05: SEC 1, AA Leader 2/12, SP 4/12, AA 16/12. Other OP sets default to AA 16/12, SEC 8/12, AA Leader 4/12, SP 2/12 before chase adjustments.',
            'Where present: Manga pool 1/36 boxes; TR 1/12; signature 1/240; gold chase 1/1200; silver chase 1/120; red-super pool 1/1200. These especially scarce-card frequencies are explicit low-confidence placeholders, not measured release-specific rates. Ordinary Manga probability is shared across the set Manga roster, not assigned independently to each Manga.',
            'Later chase cards displace ordinary AA in this model; Pandaman displaces R. Gold/premium DON collectively 2/12 per box, displacing ordinary DON. Exact replacement slots can differ; OP09 splits two case-level SP hits into one SP and one Wanted. Other Wanted pools assume 1/12 per box and consume R accounting.',
        ]
        if bonus: assumptions+=['One packaged box-topper card, equally weighted over six listed variants, adds one card outside the 288 pack cards. OP01 uses the Wave 2 White sealed-box price, not the collectible first-wave Blue box price.']
        if early or third: sources+=['https://www.reddit.com/r/OnePieceTCG/comments/12lt321/op02_pull_rate/','https://www.reddit.com/r/OnePieceTCG/comments/14i9xw4/']
    if set(rates)!=set(counts): raise ValueError((code,'pool mismatch',set(counts)-set(rates),set(rates)-set(counts)))
    if min(rates.values())<0: raise ValueError((code,'negative rate'))
    rates={p:round(v,12) for p,v in rates.items()}
    if abs(sum(rates.values())-(packs*cards+bonus))>1e-8: raise ValueError((code,'physical count mismatch',sum(rates.values())))
    return dict(code=code,name=GROUPS[gid]['name'],version='2026-09-27.expansion.2',confidence='Community estimates; rare chase frequencies, replacements and within-pool weights unverified',
                enabled=True,groupId=gid,boosterBoxProductId=box,boosterCaseProductId=CASE_PRODUCTS[code],releaseDate=GROUPS[gid]['catalogPublishedOn'][:10],packsPerBox=packs,cardsPerPack=cards,boxesPerCase=case,bonusCardsPerBox=bonus,
                sources=list(dict.fromkeys(sources)),assumptions=assumptions,pools=[dict(id=p,expectedCopiesPerBox=r) for p,r in rates.items()],variants=variants,exclusions=excluded)

if __name__=='__main__':
    originals=[p for p in read(ROOT/'config/pullrates.json')['sets'] if p['code'] in ['OP-08','EB-01','PRB-01']]
    expanded=[profile(*spec) for spec in SETS]
    combined=sorted(originals+expanded,key=lambda p:(p['releaseDate'],p['code']))
    pending=[dict(code=code,name=GROUPS[gid]['name'],groupId=gid,releaseDate=GROUPS[gid]['catalogPublishedOn'][:10],reason='Unreleased; catalog/checklist incomplete. Requires a complete reviewed profile before activation.') for code,gid in [('EB-05',24820),('OP-18',24833)]]
    (ROOT/'config/pullrates.json').write_text(json.dumps(dict(schemaVersion=1,sets=combined,plannedSets=pending),indent=2)+'\n',encoding='utf-8')
    print('Wrote',len(combined),'active profiles and',len(pending),'planned releases')
    for code,gid,box in SETS:
        variants,excluded=mapping(code,gid)
        official=read(ROOT/'research'/f'{localcode(code)}-coverage-checklist.json')
        a=collections.Counter(v['number'] for v in variants if v['number'])
        b=collections.Counter(v['number'] for v in official)
        gaps={n:(b[n],a[n]) for n in sorted(a.keys()|b.keys()) if a[n]!=b[n]}
        print(code,'variants',len(variants),'pools',dict(collections.Counter(v['pool'] for v in variants)))
        print('Number gaps (official, catalog):',gaps)
