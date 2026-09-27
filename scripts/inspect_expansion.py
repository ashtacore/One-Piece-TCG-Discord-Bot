import collections, html, json, re
from pathlib import Path
from research_expansion import ROOT, CACHE, WEB, get

def rows(code):
    page=(WEB/(code+'-cards.html')).read_text(encoding='utf-8')
    result=[]
    for identity,body in re.findall(r'<dl class="modalCol" id="([^"]+)">(.*?)</dl>',page,re.S):
        info=re.search(r'<div class="infoCol">(.*?)</div>',body,re.S)
        values=re.findall(r'<span>(.*?)</span>',info[1])
        name=re.search(r'<div class="cardName">(.*?)</div>',body,re.S)[1]
        result.append(dict(id=identity,number=values[0],rarity=values[1],name=html.unescape(name)))
    return result

if __name__=='__main__':
    for code,series in [(f'op{i:02}',569100+i) for i in range(1,18)]+[('eb02',569202),('eb03',569203),('prb02',569302)]:
        get(f'https://en.onepiece-cardgame.com/cardlist/?series={series}',code+'-cards.html')
        cards=rows(code)
        (ROOT/'research'/f'{code}-coverage-checklist.json').write_text(json.dumps(cards,indent=2),encoding='utf-8')
        print(code,len(cards),collections.Counter(r['rarity'] for r in cards),flush=True)
    for code in ['op14','op15','op17']:
        page=get(f'https://en.onepiece-cardgame.com/products/boosters/{code}/',code+'-product.html')
        text=re.sub(r'\s+',' ',re.sub('<[^>]+>',' ',page))
        print(code, text[ text.find('FEATURES'):text.find('RELATED PRODUCTS')][:7500],flush=True)
