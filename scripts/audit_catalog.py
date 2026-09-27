"""Milestone 1 research utility, not the production C# application.

Fetch once with --fetch; subsequent runs inspect the saved snapshot offline.
Only Python's standard library is required. No Discord connections are made.
"""

import argparse
import collections
import hashlib
import json
import re
import time
import urllib.request
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CACHE = ROOT / "research" / "catalog-cache"
OUT = ROOT / "research"
BASE = "https://tcgcsv.com/"
REPRESENTATIVES = {23462: "OP-08", 23333: "EB-01", 23496: "PRB-01"}
REPRINT_GROUPS = {3188, 17698, 22890, 23024, 23213, 23272, 3189, 3191,
                  3192, 3190, 17699, 22930, 22957, 23348}
SUPPORT_GROUPS = {17675, 23304} | REPRINT_GROUPS


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8"))


def write_json(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def fields(product):
    return {x["name"]: x["value"] for x in product.get("extendedData", [])}


def candidate(group):
    return bool(re.fullmatch(r"(?:OP\d+(?:-EB\d+)?|EB-\d+(?:-\d+)?|PRB-\d+)", group["abbreviation"]))


def fetch():
    CACHE.mkdir(parents=True, exist_ok=True)
    manifest_path = CACHE / "manifest.json"
    manifest = read_json(manifest_path) if manifest_path.exists() else {"files": {}}
    request = urllib.request.Request(BASE + 'last-updated.txt', headers={'User-Agent': 'OnePieceEvResearch/0.1'})
    with urllib.request.urlopen(request, timeout=45) as response:
        start_timestamp = response.read().decode('utf-8').strip()
    if (CACHE / 'last-updated.txt').exists() and (CACHE / 'last-updated.txt').read_text(encoding='utf-8').strip() != start_timestamp:
        raise ValueError('Source date differs from the frozen cache. Preserve this snapshot and use a separate cache for new research.')
    manifest['consistentSourceTimestamp'] = False
    write_json(manifest_path, manifest)

    def download(endpoint, relative, is_json=True):
        target = CACHE / relative
        url = endpoint if endpoint.startswith("https://") else BASE + endpoint
        if target.exists():
            if relative not in manifest['files']:
                manifest['files'][relative] = {
                    'url': url, 'retrievedAtUtc': datetime.fromtimestamp(target.stat().st_mtime, timezone.utc).isoformat(),
                    'sha256': hashlib.sha256(target.read_bytes()).hexdigest(),
                    'note': 'Registered from an existing research cache file; time is its filesystem modification time.'}
                write_json(manifest_path, manifest)
            return read_json(target) if is_json else target.read_text(encoding="utf-8")
        time.sleep(0.15)
        request = urllib.request.Request(url, headers={"User-Agent": "OnePieceEvResearch/0.1"})
        with urllib.request.urlopen(request, timeout=45) as response:
            raw = response.read()
        value = json.loads(raw) if is_json else raw.decode("utf-8")
        if is_json and (not value.get("success") or value.get("errors")):
            raise ValueError(f"Unsuccessful response: {endpoint}")
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(raw)
        manifest["files"][relative] = {
            "url": url,
            "retrievedAtUtc": datetime.now(timezone.utc).isoformat(),
            "sha256": hashlib.sha256(raw).hexdigest(),
        }
        write_json(manifest_path, manifest)
        print(f"Saved {relative}")
        return value

    timestamp = download("last-updated.txt", "last-updated.txt", False).strip()
    manifest["sourceUpdatedAt"] = timestamp
    groups = download("tcgplayer/68/groups", "groups.json")["results"]
    selected = {g["groupId"] for g in groups if candidate(g)} | SUPPORT_GROUPS
    for gid in sorted(selected):
        download(f"tcgplayer/68/{gid}/products", f"{gid}/products.json")
        if gid in REPRESENTATIVES or gid in REPRINT_GROUPS:
            download(f"tcgplayer/68/{gid}/prices", f"{gid}/prices.json")
    for code, series in [('op08', '569108'), ('eb01', '569201'), ('prb01', '569301')]:
        download(f'https://en.onepiece-cardgame.com/cardlist/?series={series}', f'official/{code}.html', False)
    # Detect a daily-build rollover instead of silently combining two snapshots.
    time.sleep(0.15)
    request = urllib.request.Request(BASE + "last-updated.txt", headers={"User-Agent": "OnePieceEvResearch/0.1"})
    with urllib.request.urlopen(request, timeout=45) as response:
        end_timestamp = response.read().decode("utf-8").strip()
    manifest["endSourceUpdatedAt"] = end_timestamp
    manifest["consistentSourceTimestamp"] = start_timestamp == timestamp == end_timestamp
    write_json(manifest_path, manifest)
    if not manifest['consistentSourceTimestamp']:
        raise ValueError("Source changed during collection; do not use this cache as a single price snapshot.")


def audit():
    groups = read_json(CACHE / "groups.json")["results"]
    manifest = read_json(CACHE / "manifest.json")
    if not manifest.get('consistentSourceTimestamp'):
        raise ValueError('Research cache was not verified against a consistent source timestamp.')
    for relative, entry in manifest['files'].items():
        if hashlib.sha256((CACHE / relative).read_bytes()).hexdigest() != entry['sha256']:
            raise ValueError(f'Cached source was modified: {relative}')
    registry = []
    summaries = []
    for group in groups:
        gid = group["groupId"]
        path = CACHE / str(gid) / "products.json"
        products = read_json(path)["results"] if path.exists() else []
        boxes = [p for p in products if not fields(p) and "box" in p["name"].lower()
                 and "booster" in p["name"].lower() and "case" not in p["name"].lower()]
        registry.append({
            "groupId": gid, "name": group["name"], "abbreviation": group["abbreviation"],
            "catalogPublishedOn": group["publishedOn"],
            "disposition": "representative-audit" if gid in REPRESENTATIVES else "booster-candidate" if candidate(group) else "excluded-as-booster-set",
            "productCatalogInspected": path.exists(),
            "boosterBoxProductIds": [p["productId"] for p in boxes],
            "evEnabled": False,
        })
        if gid not in REPRESENTATIVES:
            continue
        prices = read_json(CACHE / str(gid) / "prices.json")["results"]
        by_id = collections.defaultdict(list)
        for price in prices:
            by_id[price["productId"]].append(price)
        cards = [p for p in products if fields(p).get("Rarity") or fields(p).get("Number")]
        card_ids = {p["productId"] for p in cards}
        card_prices = [p for p in prices if p["productId"] in card_ids]
        variants = [{"productId": p["productId"], "name": p["name"], "url": p["url"],
                     "fields": fields(p), "prices": by_id[p["productId"]]} for p in cards]
        write_json(OUT / f"{REPRESENTATIVES[gid].lower()}-catalog.json", variants)
        summaries.append({
            "setCode": REPRESENTATIVES[gid], "groupId": gid,
            "productCount": len(products), "cardProductCount": len(cards),
            "priceRecordCount": len(prices), "cardPriceRecordCount": len(card_prices),
            "rarities": dict(sorted(collections.Counter(fields(p).get("Rarity", "<missing>") for p in cards).items())),
            "cardPriceSubtypes": dict(sorted(collections.Counter(p["subTypeName"] for p in card_prices).items())),
            "cardsWithoutPriceRecord": [p["productId"] for p in cards if not by_id[p["productId"]]],
            "cardPriceRecordsMissingMarketPrice": [{"productId": p["productId"], "subTypeName": p["subTypeName"]} for p in card_prices if p["marketPrice"] is None],
            "multiPriceCards": [p["productId"] for p in cards if len(by_id[p["productId"]]) > 1],
            "sealedProducts": [{"productId": p["productId"], "name": p["name"], "fields": fields(p)} for p in products if p["productId"] not in card_ids],
        })
    write_json(OUT / "set-registry.json", {"sourceUpdatedAt": manifest["sourceUpdatedAt"], "categoryId": 68, "groups": registry})
    write_json(OUT / "catalog-summary.json", {"sourceUpdatedAt": manifest["sourceUpdatedAt"], "sets": summaries})
    write_json(OUT / "source-manifest.json", manifest)
    print(json.dumps(summaries, indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--fetch", action="store_true", help="Download missing cache files; existing files are reused.")
    args = parser.parse_args()
    if args.fetch:
        fetch()
    audit()
