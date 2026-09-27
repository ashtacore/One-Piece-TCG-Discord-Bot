"""Inspect release catalogs and cache publisher pages for the coverage expansion."""
import json, re, time, urllib.request
from pathlib import Path
ROOT = Path(__file__).resolve().parents[1]
CACHE = ROOT / 'research/catalog-cache'
WEB = ROOT / 'research/coverage-cache'
WEB.mkdir(exist_ok=True)

def get(url, name):
    path = WEB / name
    if not path.exists():
        time.sleep(.15)
        try:
            request = urllib.request.Request(url, headers={'User-Agent':'OnePieceEvResearch/0.2'})
            path.write_bytes(urllib.request.urlopen(request, timeout=40).read())
        except Exception as error:
            print('FETCH FAILED', url, type(error).__name__)
            return ''
    return path.read_text(encoding='utf-8')

if __name__ == '__main__':
    index = get('https://en.onepiece-cardgame.com/products/', 'index.html')
    links = sorted(set(re.findall(r'href=["\']([^"\']*(?:op\d|eb\d|prb\d)[^"\']*)', index)))
    print('\n'.join(links))
    for code in [f'op{i:02}' for i in range(1,19)] + ['eb02','eb03','eb05','prb02']:
        page = get(f'https://en.onepiece-cardgame.com/products/boosters/{code}.php', code+'.html')
        if not page:
            page = get(f'https://en.onepiece-cardgame.com/products/{code}.html', code+'.html')
        # Preserve the source HTML, print only product-content clues.
        plain = re.sub('<[^>]+>', ' ', page)
        plain = re.sub(r'\s+', ' ', plain)
        clues = [plain[max(0,m.start()-60):m.end()+180] for m in re.finditer(r'box topper|bonus|Dash Pack|Release Date|BOX|special pack|EB-04', plain, re.I)]
        print(code, '\n'.join(clues)[:3500], flush=True)
