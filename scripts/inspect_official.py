"""Extract official checklist identities from the cached Bandai HTML for research."""
import collections
import html
import re
from audit_catalog import CACHE, OUT, read_json, write_json


def official_rows(code):
    page = (CACHE / "official" / f"{code}.html").read_text(encoding="utf-8")
    rows = []
    for identity, body in re.findall(r'<dl class="modalCol" id="([^"]+)">(.*?)</dl>', page, re.S):
        info = re.search(r'<div class="infoCol">(.*?)</div>', body, re.S)
        values = re.findall(r'<span>(.*?)</span>', info[1])
        name = re.search(r'<div class="cardName">(.*?)</div>', body, re.S)[1]
        rows.append({"officialId": identity, "number": values[0], "rarity": values[1], "name": html.unescape(name)})
    if not rows:
        raise ValueError(f"No checklist rows parsed for {code}")
    return rows


if __name__ == "__main__":
    for code, local in [("op08", "op-08"), ("eb01", "eb-01"), ("prb01", "prb-01")]:
        rows = official_rows(code)
        write_json(OUT / f"{local}-official-checklist.json", rows)
        unique = {(r['number'], r['rarity']) for r in rows}
        print(code, "art entries", len(rows), "unique numbered rarities", collections.Counter(r for _, r in unique))
        if code == "prb01":
            cards = read_json(OUT / f"{local}-catalog.json")
            by_number = collections.defaultdict(list)
            local_by_number = collections.defaultdict(list)
            for row in rows:
                by_number[row['number']].append(row)
            for card in cards:
                if card['fields'].get('Number'):
                    local_by_number[card['fields']['Number']].append(card)
            gaps = []
            for number, entries in by_number.items():
                local_entries = local_by_number[number]
                if len(entries) != len(local_entries):
                    gaps.append({'number': number, 'officialIds': [r['officialId'] for r in entries],
                                 'rarity': entries[0]['rarity'], 'officialEntryCount': len(entries),
                                 'localProductCount': len(local_entries),
                                 'localNames': [p['name'] for p in local_entries]})
            write_json(OUT / 'prb-01-checklist-gaps.json', gaps)
            print('Numbered checklist gaps:', len(gaps))
