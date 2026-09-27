"""Extend the frozen research snapshot only while its provider timestamp still matches."""
import hashlib, json, time, urllib.request
from datetime import datetime, timezone
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
CACHE=ROOT/'research/catalog-cache'
def fetch(url):
    time.sleep(.15)
    return urllib.request.urlopen(urllib.request.Request(url,headers={'User-Agent':'OnePieceEvResearch/0.2'}),timeout=45).read()
stamp=(CACHE/'last-updated.txt').read_text().strip()
if fetch('https://tcgcsv.com/last-updated.txt').decode().strip()!=stamp: raise ValueError('Frozen snapshot date changed; use a new cache.')
registry=json.loads((ROOT/'research/set-registry.json').read_text())
manifest=json.loads((CACHE/'manifest.json').read_text())
for group in registry['groups']:
    if group['disposition'] not in ['booster-candidate','representative-booster']: continue
    gid=group['groupId']; relative=f'{gid}/prices.json'; target=CACHE/relative
    if target.exists(): continue
    url=f'https://tcgcsv.com/tcgplayer/68/{gid}/prices'
    raw=fetch(url); data=json.loads(raw)
    if not data.get('success') or data.get('errors'): raise ValueError(url)
    target.write_bytes(raw)
    manifest['files'][relative]={'url':url,'sha256':hashlib.sha256(raw).hexdigest(),'retrievedAtUtc':datetime.now(timezone.utc).isoformat()}
    (CACHE/'manifest.json').write_text(json.dumps(manifest,indent=2))
    print('Saved',relative,len(data['results']),flush=True)
if fetch('https://tcgcsv.com/last-updated.txt').decode().strip()!=stamp: raise ValueError('Provider changed during download; do not combine prices.')
