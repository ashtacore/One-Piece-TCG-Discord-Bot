"""Build reviewable Milestone 1 variant/pool mappings from the frozen catalog."""
import collections
import re
from audit_catalog import CACHE, OUT, REPRESENTATIVES, fields, read_json, write_json

ORIGINAL_GROUPS = {'OP01':3188, 'OP02':17698, 'OP03':22890, 'OP04':23024,
                   'OP05':23213, 'OP06':23272, 'ST01':3189, 'ST02':3191,
                   'ST03':3192, 'ST04':3190, 'ST06':17699, 'ST07':22930,
                   'ST09':22957, 'ST12':23348}


def classify(card, subtype, set_code):
    name, rarity = card['name'], card['fields']['Rarity']
    if card['productId'] in {576484, 576485}:
        return 'Excluded', 'external-double-pack-don'
    if card['productId'] == 594331 and subtype == 'Normal':
        return 'Review', 'unverified-brannew-normal'
    if rarity == 'DON!!':
        return 'DON!!', 'don-gold' if '(Gold)' in name else 'don-foil' if subtype == 'Foil' else 'don-normal'
    if '(Manga)' in name:
        return 'Manga / Other Chase', 'manga-nami' if card['productId'] == 587966 else 'manga-reprint-god-pack' if set_code == 'PRB-01' else 'manga'
    if rarity == 'TR' or '(TR)' in name:
        return 'TR', 'tr'
    if '(SP)' in name:
        return 'SP', 'sp'
    if '(Jolly Roger Foil)' in name:
        return ('Base' if rarity in {'C','UC'} else 'Other' if rarity == 'PR' else 'Foils'), 'jolly-roger'
    if '(Full Art)' in name:
        return 'AA', 'full-art'
    if '(Textured Foil)' in name:
        return 'AA', 'textured-event-stage'
    if '(Alternate Art)' in name or '(Parallel)' in name:
        if rarity == 'L':
            return 'AA', 'aa-leader'
        return 'AA', 'aa'
    return ('Base' if rarity in {'C','UC'} else 'Leaders' if rarity == 'L' else 'Other' if rarity == 'PR' else 'Foils'), f'base-{rarity.lower()}'


def shared_reprints():
    mappings = []
    for gap in read_json(OUT / 'prb-01-checklist-gaps.json'):
        number = gap['number']
        gid = ORIGINAL_GROUPS[number.split('-')[0]]
        products = read_json(CACHE / str(gid) / 'products.json')['results']
        prices = read_json(CACHE / str(gid) / 'prices.json')['results']
        candidates = [p for p in products if fields(p).get('Number') == number and not
                      re.search(r'Alternate Art|Parallel|Manga|Box Topper|Pre-Errata|Reprint|\(SP\)|Pre-Release', p['name'], re.I)]
        if len(candidates) != 1:
            raise ValueError(f'Ambiguous original listing for {number}: {[(p["productId"],p["name"]) for p in candidates]}')
        product = candidates[0]
        subtype = 'Normal' if gap['rarity'] in {'C','UC','P','L'} else 'Foil'
        matched = [p for p in prices if p['productId'] == product['productId'] and p['subTypeName'] == subtype]
        if len(matched) != 1:
            raise ValueError(f'Missing/ambiguous exact price for {number}: {subtype}')
        mappings.append({'number':number, 'rarity':gap['rarity'], 'sourceGroupId':gid,
                         'productId':product['productId'], 'subTypeName':subtype,
                         'name':product['name'], 'url':product['url'], 'marketPrice':matched[0]['marketPrice'],
                         'mappingStatus':'proposed-shared-original-listing',
                         'evidence':'Official PRB-01 checklist has one additional entry for this number; original English ordinary listing matched by number, rarity and finish. Exact image/artist-credit equivalence still requires review.'})
    write_json(OUT / 'prb-01-shared-reprints.json', mappings)
    return mappings


if __name__ == '__main__':
    shared = shared_reprints()
    summaries = []
    for gid, code in REPRESENTATIVES.items():
        cards = read_json(OUT / f'{code.lower()}-catalog.json')
        variants = []
        for card in cards:
            for price in card['prices']:
                category, pool = classify(card, price['subTypeName'], code)
                variants.append({'productId':card['productId'], 'subTypeName':price['subTypeName'],
                                 'sourceGroupId':gid, 'name':card['name'],
                                 'number':card['fields'].get('Number'), 'printedRarity':card['fields']['Rarity'],
                                 'reportCategory':category, 'pool':pool, 'marketPrice':price['marketPrice'],
                                 'membershipStatus':'excluded' if category == 'Excluded' else 'review' if category == 'Review' else 'catalog-matched'})
        if code == 'PRB-01':
            for item in shared:
                rarity = item['rarity']
                variants.append({'productId':item['productId'], 'subTypeName':item['subTypeName'],
                                 'sourceGroupId':item['sourceGroupId'], 'name':item['name'], 'number':item['number'],
                                 'printedRarity':rarity, 'reportCategory':'Base' if rarity in {'C','UC'} else 'Foils',
                                 'pool':f'base-{rarity.lower()}', 'marketPrice':item['marketPrice'],
                                 'membershipStatus':'proposed-shared-original-listing'})
        keys = [(p['productId'], p['subTypeName']) for p in variants]
        assert len(keys) == len(set(keys)), f'Duplicate variant keys in {code}'
        included = [p for p in variants if p['membershipStatus'] not in {'excluded','review'}]
        summary = {'setCode':code, 'mappedVariantCount':len(included),
                   'excludedOrReviewCount':len(variants)-len(included),
                   'poolCounts':dict(sorted(collections.Counter(p['pool'] for p in included).items())),
                   'reportCategoryCounts':dict(sorted(collections.Counter(p['reportCategory'] for p in included).items())),
                   'missingMarketPriceKeys':[f"{p['productId']}:{p['subTypeName']}" for p in included if p['marketPrice'] is None],
                   'proposedSharedMappingCount':sum(p['membershipStatus']=='proposed-shared-original-listing' for p in included)}
        summaries.append(summary)
        write_json(OUT / f'{code.lower()}-variant-mapping.json', variants)
        print(summary)
    write_json(OUT / 'mapping-summary.json', summaries)
