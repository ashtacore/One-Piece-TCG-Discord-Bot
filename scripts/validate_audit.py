"""Validate frozen audit evidence and identity/replacement invariants offline."""
import collections
import hashlib
from fractions import Fraction
from audit_catalog import CACHE, OUT, read_json


def check(condition, message):
    if not condition:
        raise ValueError(message)


manifest = read_json(OUT / 'source-manifest.json')
check(manifest['consistentSourceTimestamp'], 'Mixed or unverified source snapshot')
if CACHE.exists():
    for relative, metadata in manifest['files'].items():
        check(hashlib.sha256((CACHE / relative).read_bytes()).hexdigest() == metadata['sha256'], f'Hash mismatch: {relative}')

registry = read_json(OUT / 'set-registry.json')['groups']
check(len(registry) == 87, 'Unexpected frozen group count; review a new snapshot deliberately')
candidates = [g for g in registry if g['disposition'] != 'excluded-as-booster-set']
check(len(candidates) == 24 and all(g['boosterBoxProductIds'] for g in candidates), 'Candidate box discovery failure')
check(not any(g['evEnabled'] for g in registry), 'Research registry must not enable unreviewed EV')

maps = {}
for code, count, official_count in [('op-08',152,151), ('eb-01',80,80), ('prb-01',409,319)]:
    rows = read_json(OUT / f'{code}-variant-mapping.json')
    maps[code.upper()] = rows
    keys = [(r['productId'],r['subTypeName']) for r in rows]
    check(len(keys) == len(set(keys)), f'Duplicate identity: {code}')
    included = [r for r in rows if r['membershipStatus'] not in {'excluded','review'}]
    check(len(included) == count, f'Unexpected mapped count: {code}')
    official = read_json(OUT / f'{code}-official-checklist.json')
    check(len(official) == official_count, f'Official checklist parse mismatch: {code}')
    for row in included:
        if '(Manga)' in row['name']:
            check(row['reportCategory'] == 'Manga / Other Chase', 'Manga counted in ordinary rarity')
        if '(SP)' in row['name']:
            check(row['reportCategory'] == 'SP', 'SP counted by printed rarity')
        if row['pool'] == 'aa-leader':
            check(row['reportCategory'] == 'AA', 'AA Leader double/category count')

op = {r['productId']:r for r in maps['OP-08']}
check(all(op[i]['membershipStatus']=='excluded' for i in [576484,576485]), 'DP05 included in OP08')
check(op[577568]['pool']=='don-foil', 'Wrong OP08 DON!!')
check(not any(r['pool'] in {'base-uc','sp','tr','don-foil'} for r in maps['EB-01']), 'Inapplicable EB01 pool')

prb = maps['PRB-01']
check(next(r for r in prb if r['productId']==594331 and r['subTypeName']=='Normal')['membershipStatus']=='review', 'Brannew quarantine lost')
check(next(r for r in prb if r['productId']==587709)['marketPrice'] is None, 'Missing Manga price was fabricated')
counts = collections.Counter(r['pool'] for r in prb if r['membershipStatus'] not in {'excluded','review'})
check(all(counts[p]==30 for p in ['don-normal','don-foil','don-gold']), 'DON!! finishes conflated')
check(counts['base-c']==23 and counts['base-uc']==25 and counts['base-sr']==25 and counts['base-r']==20 and counts['base-sec']==6, 'Headline checklist rarities used as ordinary pool sizes')
check(counts['full-art']==50 and counts['textured-event-stage']==22 and counts['aa']==60, 'AA treatments conflated')
shared = read_json(OUT / 'prb-01-shared-reprints.json')
check(len(shared)==54 and len({r['number'] for r in shared})==54, 'Shared mapping missing/duplicated')
check(all(r['marketPrice'] is not None for r in shared), 'Shared listing price missing')

draft = read_json(OUT / 'pull-rate-profiles.draft.json')
sources = draft['sources']
def verify_sources(value):
    if isinstance(value, dict):
        for key, child in value.items():
            if key == 'sourceIds':
                check(all(ref in sources for ref in child), f'Unknown source in {child}')
            verify_sources(child)
    elif isinstance(value, list):
        for child in value:
            verify_sources(child)
verify_sources(draft)
for profile in draft['profiles']:
    check(profile['enabledForEv'] is False and profile['expectedCopiesPerBox'] is None, 'Incomplete draft enabled')
    pool_ids = {r['pool'] for r in maps[profile['setCode']]}
    for observation in profile['observations']:
        check(observation['pool'] in pool_ids, 'Observation references absent pool')
        rate = observation['proposedExpectedCopiesPerBox']
        check(rate['numerator'] >= 0 and rate['denominator'] > 0, 'Invalid proposed rate')
    for constraint in profile.get('candidateConstraints', []):
        check(set(constraint['pools']) <= pool_ids, 'Constraint references absent pool')
recipe = draft['profiles'][0]['candidateCaseRecipe']['boxes']
check(sum(b['count'] for b in recipe)==12, 'Case recipe not 12 boxes')
totals = collections.Counter()
for box in recipe:
    for pool, count in box['hits'].items():
        totals[pool] += box['count'] * count
check(dict(totals)=={'aa':16,'base-sec':8,'sp':2,'aa-leader':4}, 'Case recipe arithmetic wrong')
check(sum(totals.values())==30, 'Premium hit count wrong')
check(draft['profiles'][2]['physical']['boxesPerCase']==10, 'PRB01 incorrectly uses 12-box case')
check(Fraction(2,10)==Fraction(1,5), 'Gold DON!! case conversion wrong')
check(len(draft['bonusReference']['eligibleProductIds'])==6 and draft['bonusReference']['expectedCardsPerBox']==1, 'OP01 topper reference wrong')

print(f'PASS: 87 groups; 24 booster candidates; 152/80/409 mapped variants; 54 shared mappings; {len(manifest["files"])} source hashes; draft references and case arithmetic.')
