# One Piece booster-box EV reporter

A .NET 10 console application that runs once, reads English/USD TCGplayer market prices through TCGCSV, writes reports, and optionally delivers them to Discord webhooks. No external NuGet packages are required. Schedule the executable externally for daily execution.

**22 released sets are configured**, covering OP-01 through OP-17 (including combined OP14-EB04 and OP15-EB04), EB-01/02/03 and PRB-01/02. Profiles and reports are sorted by English release date. These are estimated profiles, not verified manufacturer pull odds. See the [coverage audit](research/COVERAGE-AUDIT.md) for dates, roster counts, exclusions and missing prices.

EB-05 (October 30, 2026) and OP-18 (November 20, 2026) are recorded in `plannedSets` in `config/pullrates.json`. They remain inactive because their catalogs are incomplete. A complete reviewed profile must be added before activation; the release date alone does not activate them. Active profiles are also filtered by both the current UTC date and price-snapshot date before reporting. Your supplied release dates agree with the catalog dates; month-only entries were resolved against publisher product pages.

Default and local settings now select all 22 released sets. Destination-specific filters remain supported. The next ordinary live run can therefore send the newly added set reports to enabled destinations; dry runs do not post.

## Run

From the repository directory with the .NET 10 SDK installed:

```powershell
dotnet run --project src/OnePiece.Ev -- --validate
dotnet run --project src/OnePiece.Ev -- --dry-run
dotnet run --project src/OnePiece.Ev -- --offline
```

`--dry-run` fetches prices but never posts or advances report history. `--offline` previews the saved catalog without network requests. Reports are written to `out/latest.md` and `out/latest.json`. With no enabled destinations, ordinary execution also produces a preview. `--catalog research/catalog-cache` previews the research download instead and always disables posting.

## Discord configuration

Copy `appsettings.json` to the ignored `appsettings.local.json`. Set the destination's `enabled` to `true`. Either set `webhookUrl` directly in that private file or use the configured environment variable:

```powershell
$env:ONEPIECE_DISCORD_WEBHOOK = 'https://discord.com/api/webhooks/YOUR_ID/YOUR_TOKEN'
dotnet run --project src/OnePiece.Ev -- --config appsettings.local.json
```

Use one destination object with a unique `id` per channel, including channels on different servers. `sets: []` selects every enabled supported set; otherwise list codes such as `OP-08`. An optional numeric `threadId` targets an existing Discord thread. Webhook URLs must use `https://discord.com/api/webhooks/...` without query parameters. When `webhookEnvironmentVariable` is provided, it takes precedence over `webhookUrl`.

The application sends outbound HTTPS requests to Discord's webhook URL. You do not need to host a server or configure an endpoint to receive webhook events.

All configured paths resolve relative to the configuration file. Set a stable working directory when scheduling, or pass an absolute config path. For deployment, `dotnet publish src/OnePiece.Ev -c Release -o out/publish` builds the executable; retain the configuration and `config/pullrates.json` and pass the config's absolute path. The process exits 0 on successful preview/delivery and 1 on a failure. The application does not install a scheduled task.

TCGCSV requests use a custom User-Agent and at least 100 ms spacing. A complete local catalog is reused for 24 hours; after that the provider timestamp is checked before downloading again. An update during download aborts the snapshot. Posting refuses data older than `maxSourceAgeHours` (48 by default). Offline previews may use older data and always show its timestamp.

## Calculation rules

For each pool, expected copies of card *i* equal `pool expected copies × card weight / sum of pool weights`. Box EV is the sum of `expected copies × selected price` across all eligible variants. Current weights are equal within each pool; this is a modeling assumption, not proof that collation is uniform. Prices are decimal USD values for the exact product ID and `Normal`/`Foil` subtype. Prefer TCGplayer `marketPrice`; if absent or negative, fall back to a nonnegative `midPrice`. Zero is a valid supplied price, not a missing value. No fees, shipping, box cost, or liquidity adjustment is subtracted.

Market reflects completed sales; Mid is the median listing price and can exceed realizable sale value. Reports disclose how many listing-based estimates they include and list each affected card and price. Normalized cards and history persist `priceSource` as `Market`, `Mid`, or `Missing`. Historical reports without this field default to Market because the previous implementation used Market exclusively. This fallback applies to card valuation; the sealed-box `MP` line remains Market-only. No graded sales or secondary-source prices are mixed into card EV. See [TCGCSV field definitions](https://tcgcsv.com/docs).

The first online run after this upgrade refreshes old caches that omitted Mid, even if the provider timestamp has not changed. Offline mode asks for an online `--dry-run` when an old cache cannot supply those fields. Existing report history and delivery records are preserved; previously sent reports are not reposted by this upgrade.

A chase appearing once per 36 boxes contributes `average chase price / 36`. A $360 average contributes **$10 per box**. Do not divide by the number of cards in the box: that would convert a per-box frequency into a per-card probability without converting it back.

Expected pool counts must sum to `packsPerBox × cardsPerPack + bonusCardsPerBox`, within decimal rounding tolerance. Replacement hits reduce the displaced pool. Packaged bonuses are modeled as additional pools and physical bonus counts, with duplicate card identities consolidated and their expected counts combined. OP-01 and OP-02 include one packaged topper: 289 cards total. Other OP/EB profiles total 288; PRB profiles total 200. Purchase-campaign Dash Packs and Double Pack bonuses are excluded. Cases use 12 boxes for OP/EB and 10 for PRB.

Reports begin with `Booster Box:` and indented `EV` (expected contents value) and `MP` (sealed-box market price). MP uses the profile's exact `boosterBoxProductId` and its `Normal` market-price listing; an absent or null price displays `Unavailable` and does not affect card EV or master-set completeness. The sealed box is not included in the master-set total. A blank line separates the box section from the master-set price, which appears above the rarity breakdown. An extra blank line separates set reports; Discord's final message part includes an invisible spacing character to retain that line.

Each category is displayed with its full name and two indented lines: `AVG` is its expected value contribution per booster box (not the average individual card price), and `Total (X)` is the combined price of one copy of each of its X distinct variants. For example:

```text
Treasure Rare:
  AVG: $20.80 · 16.0%
  Total (1): $249.58
```

The master set sums **one copy of every distinct eligible artwork/finish**, regardless of its pull probability. Internal pool names remain separate even when combined for display. Categories are exclusive:

| Section | Membership |
| --- | --- |
| TR / SP | Treasure Rare / Special Rare treatments |
| AA | Alternate artworks including AA Leaders; PRB full-art and textured treatments retain separate internal odds |
| Manga / Other Chase | Manga and exceptional chase variants |
| Foils | Ordinary R, SR, SEC; eligible R Jolly Roger variants |
| Base | C and UC, including eligible Jolly Roger variants |
| Leaders | Ordinary L |
| DON!! | Each eligible DON artwork and finish |
| Other | Eligible P variants not covered above |

Treatment takes precedence over printed rarity. A Manga SEC is counted only as Manga, not again under Foils. DON Normal, Foil and Gold variants are distinct master-set entries. A printed number alone cannot identify an artwork or determine which booster contains a reprint.

If neither Market nor Mid is usable, the price is **unknown, not zero**. Known contributions may be shown as an explicitly INCOMPLETE subtotal, with missing variants named; no percentages or market deltas are shown for that set. A disclosed Mid fallback counts as priced, so it does not by itself trigger `postIncompleteReports: false`. Unknown catalog cards/finishes also flag a profile for review. No missing-card probability is redistributed to the remaining priced cards.

## Adopted initial estimates

The three original profiles below remain the baseline. Expansion-specific rules are in each profile's `assumptions` and generated attachment. Early OP sets use separate premium-hit recipes; OP-06 onward generally uses the accepted OP-style baseline, with explicit chase adjustments. OP14/OP15 include their EB04 cards in the same pools. Additional gold/silver/signature/red-super and special-pack frequencies include low-confidence modeling placeholders; the warning and attachment disclose these. Numbered artwork counts are checked against Bandai, but equal collation weights and these rare-event rates are not verified by that checklist. PRB-02 has its own 20-pack model and shared SP/event-art pool; it does not inherit PRB-01's full-art pool.

The exact, editable counts, allowlists, sources, assumptions and version are in [config/pullrates.json](config/pullrates.json). The research audit preserves the original unresolved observations. Adoption here means an explicit working assumption, not that research established a guarantee.

| Model | Adopted regular-box contents |
| --- | --- |
| OP-08 | C 166, UC 64, R 38.5, SR 7, L 8, DON 2; SEC 8/12, SP 2/12, AA Leader 4/12; AA starts at 16/12, reduced by TR 1/12 and Manga 1/36 |
| EB-01 | C 234, R 39 minus 1/36, SR 6, L 6, AA 2, SEC 8/12, AA Leader 4/12; Manga 1/36 displaces R |
| PRB-01 | Bulk C/UC/P 100, R 20, SR 18, SEC 2, L 1, DON 20, AA-level hits 2, full-art/textured hits 2, Jolly Roger 35; special replacements below |

For PRB-01, divide bulk uniformly across 52 eligible ordinary cards (23 C, 25 UC, 4 P), and full-art/textured slots across 72 candidates (50/22). DON counts are 18 Normal, 1.8 Foil and 0.2 Gold. Sanji AA Leader and standalone Manga Nami each assume 1/30 boxes and replace ordinary AA. Assume a ten-Manga god pack once per 150 boxes: scale **all regular counts by `1 - 1/3000`** to remove one average regular pack per god-pack event, then add 1/150 copies of each of the ten Manga cards. God packs are correlated events; linear EV only requires their marginal expected counts. This model does not estimate the distribution of returns or promise a minimum box value.

These chase frequencies, some ordinary slots, and displacement choices are weakly supported community estimates. In particular, OP-08 TR 1/case and PRB chase/Leader frequencies are assumptions. EB Manga displacement reports conflict. Change assumptions in the JSON profile and increment its version when better opening data becomes available.

PRB-01 includes **54 explicit original-listing price proxies** for ordinary reprints missing standalone listings. Number/rarity/finish and checklist correspondence were reviewed; all physical artwork equivalences have not been independently inspected. These are adopted shared-price assumptions. Unverified Brannew Normal is excluded; its Foil is included. Manga Ace's null Market price uses the disclosed $2,450 Mid fallback in the September 26 snapshot, contributing $16.33 to box EV and $2,450 to master-set value. Historical research artifacts retain the original missing-Market observation. OP-08 excludes both DP-05 Double Pack bonus DON cards.

Primary references: [Bandai OP-08](https://en.onepiece-cardgame.com/products/boosters/op08.php), [EB-01](https://en.onepiece-cardgame.com/products/boosters/eb01.php), [PRB-01](https://en.onepiece-cardgame.com/products/boosters/prb01.php), [DP-05](https://en.onepiece-cardgame.com/products/other/dp05.php), [OP-01 topper](https://en.onepiece-cardgame.com/products/boosters/op01.php). Community observations: [user's rarity guide](https://www.reddit.com/r/OnePieceTCGFinance/comments/1fp510n/understanding_one_piece_tcg_rarity_system_pull/), [opening-rate primer](https://www.reddit.com/r/OnePieceTCG/comments/1jgnfii/a_casual_players_primer_on_booster_box_drop_rates/), [EB-01 fourth-hit example](https://www.reddit.com/r/OnePieceTCG/comments/1cjbyiz/so_4hit_boxes_are_definitely_a_thing_with_eb01/), [English PRB opening discussion](https://www.reddit.com/r/OnePieceTCG/comments/1gjy045/english_prb01/). See the [audit](research/MILESTONE-1-AUDIT.md) for additional sources and their limitations.

## Changes, movers and delivery

Every set report ends with a concise warning about its weaker assumptions. The final Discord message part carries a downloadable `<set>-pull-rate-assumptions.md` file, generated directly from the same profile used for EV. It includes the expected-cards-per-box table AFTER all replacements and special-pack adjustments, physical contents, profile version/hash, weighting, assumptions and source links. Dry runs also write these files alongside `out/latest.md`. Markdown is uploaded as a file, not hosted as a webpage; Discord clients may offer a preview or download rather than render it as a formatted document.

Files use Discord's multipart webhook upload (`payload_json` plus `files[0]`). Their contents are frozen in the delivery ledger with the report, so retrying cannot substitute changed assumptions into an earlier report. Previously delivered batches are not resent merely to add attachments. See [Discord webhook upload documentation](https://docs.discord.com/developers/resources/webhook#execute-webhook).

Comparisons use the most recent earlier source snapshot retained after successful delivery, showing its actual timestamp (not necessarily yesterday). The first run has no deltas. A changed profile, card membership, or incomplete prices disables comparisons. Profile hashes include all profile fields. Price changes never include effects from a changed probability model.

A Market ↔ Mid switch excludes that card from movers and suppresses box EV, master-set and affected category deltas for that comparison, with an explicit pricing-source-change note. Unaffected categories and cards remain comparable. Consecutive Mid observations can produce changes, but Mid-based movers are labeled as estimates; these are listing movements, not evidence of completed sales. A missing-to-priced transition follows the existing incomplete-history rule and does not produce a spurious gain.

Movers must meet both configurable thresholds: absolute price change at least 1% of the previous price and absolute box-EV impact at least $0.05. Rank by absolute EV impact; show at most ten. Previous zero prices cannot produce percentage movers. Displayed cents are rounded; calculations retain decimal precision.

Discord messages are split below 2,000 UTF-16 code units and disable mentions. Each source timestamp/set/destination gets a frozen multipart outbox. Successfully sent parts are not resent on repeated runs. Known rate-limit rejections retry up to three times with Discord's delay. Timeout, connection failure or server error may occur after acceptance; these are marked uncertain and **never automatically resent**. No exactly-once guarantee is possible across a network failure.

For an uncertain delivery, inspect the configured channel and `data/deliveries.json`. With the app stopped, change the affected part to `sent` with its observed `messageId` if present, or to `pending` only after establishing it was not delivered. A crash may leave `inflight`, which requires the same check. Preserve the ledger and history between runs. A process lock prevents simultaneous runs using the same state directory. Changing a destination ID or URL creates a new destination identity and may send again.

## Verification and development

```powershell
python scripts/audit_catalog.py --fetch
python scripts/validate_audit.py
python scripts/fetch_expansion_prices.py
dotnet run --project src/OnePiece.Ev -- --catalog research/catalog-cache
python scripts/validate_expansion.py
dotnet run --project tests/OnePiece.Ev.Tests
```

The test executable uses the dated research cache and fake HTTP responses: card counts, one-in-36 EV, god-pack replacement, missing prices, model changes, mover cap/order, Unicode message limits, frozen outbox, 429 retries, and ambiguous delivery handling. It sends no real Discord messages. Research utilities use Python's standard library; the production app requires only .NET.

`scripts/build_profiles.py` rebuilds the original three profiles while retaining expanded profiles and planned releases. `scripts/build_expanded_profiles.py` rebuilds the 19 added profiles from cached catalogs and publisher checklists while retaining the original three. Both overwrite the profiles they own; run them intentionally after updating their assumptions/mappings. `scripts/inspect_expansion.py` caches publisher checklists. Frozen research downloads refuse a changed provider timestamp rather than mixing dates; preserve existing fixtures and start a separate snapshot when refreshing research. Production caching handles daily updates normally. Historical draft research profiles remain disabled to distinguish observations from adopted assumptions.
