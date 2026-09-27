# One Piece Booster Box EV Reporter — Project Plan

Status: All 22 released sets in the requested list are configured and sorted by English release date; 3,865 variants reconcile to publisher numbered-artwork counts plus reviewed DON finishes. EB-05 and OP-18 are inactive planned releases pending full catalogs. Live ingestion and automated expansion checks passed; nine quotes remain unknown across four sets. Original three-set live Discord verification passed; the expansion was previewed without bulk posting. See [coverage audit](research/COVERAGE-AUDIT.md), [README](README.md) and [original audit](research/MILESTONE-1-AUDIT.md).

This is the living plan for the project. Update it as research resolves assumptions, implementation progresses, or requirements change. The eventual README must document the rules actually implemented; this plan tracks scope, decisions, and remaining work.

## Goal

Build a simple C# console application that runs once, downloads One Piece card pricing, calculates booster-box expected value (EV) and market changes, posts reports to configured Discord channels, and exits. An external scheduler handles daily execution.

## Confirmed decisions

- Use one Discord webhook URL per destination channel, supporting multiple channels and servers.
- Include every distinct artwork and finish obtainable from the sealed booster box in its master set, including variants from packaged bonuses.
- Show Manga and exceptional chase variants separately from ordinary alternate arts.
- Include box-topper and bonus packs packaged inside the sealed booster box in box EV.
- Count a distinct variant once in the master set, even if it has multiple pull sources. EV includes all eligible pull sources.
- Exclude sets without booster boxes, including starter decks and learn-together products.
- Use JSON configuration and clearly document all calculation rules in the README.

## Proposed implementation defaults

These are working defaults, adjustable as needed rather than independently confirmed user preferences.

- English cards and USD TCGplayer market prices sourced through TCGCSV.
- .NET 10 console application; this SDK is installed in the workspace environment.
- `appsettings.json` for application settings and destinations; companion `pullrates.json` for larger editable set profiles.
- Local dated JSON snapshots and delivery records rather than a database.
- Windows Task Scheduler for daily execution; no always-running Discord gateway connection.
- Gross card market value: do not subtract fees, shipping, taxes, or the cost of the box.

## Calculation model

For each distinct priced card variant:

```text
Card EV = expected copies per box × market price
Box EV = sum of Card EV for every included variant
Category EV = sum of Card EV within that reporting category
Category EV percentage = Category EV / Box EV × 100
Category collection total = sum of one price per distinct variant in the category
Master set value = sum of one price per distinct eligible variant
```

Use decimal arithmetic for prices and calculations, retaining precision until display rounding. Handle a zero or unavailable denominator explicitly.

### Case-level odds

Assume a box is selected randomly from an unfiltered case. Use 12 boxes per case as the starting assumption for standard OP/EB products. The PRB-01 English case has 10 boxes; always store and use case size per product. See the audit for evidence and limitations.

A hypothetical category appearing once per three 12-box cases has an expected count of `1 / 36` per box. If its average card price is $360, its contribution is $10 per box.

```text
Category EV = average price / 36
```

Do not divide this result by the number of cards in a box. `1 / (288 × 36)` is the fraction of card positions occupied by that category; converting that measure into box EV requires multiplying by 288 again.

If six equally likely variants share the category's one-per-36-box rate:

```text
Expected copies of each variant per box = 1 / (36 × 6)
```

Use explicit relative weights when variants are not equally likely. Equal likelihood is an assumption that must be documented, not inferred from prices. The one-per-36-box example is illustrative, not an adopted SP rate.

### Collation and replacement rules

- Model expected counts or weighted pack/box configurations for each product.
- Premium hits must replace the appropriate ordinary slots where applicable. Do not add chase rates on top of a complete box without accounting for replacement.
- Treat special packs as replacement configurations when they replace ordinary packs.
- Model packaged bonuses separately from ordinary pack contents, then include their contributions in total box EV.
- Validate that expected card counts reconcile with the modeled contents, including any separately allocated DON!! or bonus slots.
- Simulation is unnecessary for mean EV: correct expected counts suffice, even when pulls within a case are correlated.
- Community pull rates are estimates, not official guarantees. Record uncertainty and sources rather than presenting assumptions as guaranteed contents.

## Reporting categories and variant identity

Each distinct variant belongs to exactly one reporting category.

| Section | Contents |
| --- | --- |
| TR | Treasure Rare variants |
| AA | Ordinary alternate artworks, including AA Leaders |
| SP | Special Rare variants |
| Manga / Other Chase | Manga and exceptional chase treatments |
| Foils | Standard SEC, SR, and R versions |
| Base | Standard C and UC versions |
| Leaders | Standard L versions |
| DON!! | Included DON!! treatments |
| Other | Eligible cards that do not fit another section, when needed |

An AA SEC contributes to AA, not also to Foils. Premium DON!! treatments remain in DON!!, with separate internal rate pools. Explicit variant mappings resolve ambiguous special treatments.

Reporting categories are not calculation pools. Calculate C and UC separately; likewise R, SR, SEC, ordinary AA, AA Leaders, and distinct DON!! treatments may require different frequencies before their contributions are combined for display.

“Foils” is the requested rarity grouping, not a claim that every physically foil card belongs there. Premium products may contain foil C/UC cards and booster-pullable promotional cards.

Use TCGplayer product ID and price subtype to identify priced records. Preserve artwork/finish distinctions and verify catalog duplicates rather than deduplicating by card number alone.

## Set discovery and pull-rate profiles

Maintain an explicit registry of supported booster products, mapped to source groups and reviewed pull-rate profiles.

- Discover candidate booster releases and exclude decks and unrelated products.
- Verify booster-box availability rather than relying solely on a name blacklist.
- Determine product membership from the release and catalog mapping, not individual card-number prefixes. Reprints can retain older OP or ST numbers.
- Treat 24 packs × 12 cards for standard OP/EB products and 20 × 10 for premium products as initial templates to verify, not universal rules.
- Do not invent a full EV for an unsupported release. Flag it as awaiting a reviewed profile.

Each profile records:

- Product identity, applicable language/region, and source group mappings.
- Packs per box, cards per pack, boxes per case, and separate bonus contents.
- Pull pools, eligible variants, relative weights, and expected counts or configuration probabilities.
- Replacement relationships and any special-pack configurations.
- Classification and membership overrides where source metadata is insufficient.
- Source links, date reviewed, confidence notes, assumptions, and profile version.

## Data ingestion and pricing

Use TCGCSV's JSON endpoints for categories, groups, products, and prices. Join product metadata to price records, preserving price subtypes.

- Prefer `marketPrice`; fall back to `midPrice` when Market is unavailable/invalid. Persist the selected source and explicitly label listing-based estimates. The sealed-box MP remains Market-only.
- Check the source update timestamp before downloading a new snapshot.
- Follow the provider's daily-download guidance, custom User-Agent requirement, and request throttling.
- Cache data so repeated local runs can reuse a source snapshot.
- Validate responses and preserve the last good snapshot after an ingestion failure.
- If neither Market nor Mid is usable, keep the card unknown and totals incomplete. Never replace missing prices with zero. Source switches exclude the card from movers and suppress box/master-set and affected category deltas; unaffected comparisons remain available.
- Log unknown classifications and missing rate mappings for correction.

TCGCSV does not provide condition-specific SKU data. Describe these values as source market prices rather than guaranteed near-mint valuations.

## History, changes, and movers

Save dated source snapshots, normalized variant prices, profile versions, and report/delivery metadata.

- Compare with the previous source-day snapshot.
- If a day was missed, label the actual comparison interval rather than calling it a one-day change.
- On the first run, show current values without invented deltas or movers.
- Compare variants only when both snapshots have usable prices.
- Identify changes to profile rules or set membership and reset affected comparisons so model changes do not masquerade as market movement.

```text
Price change = current price - previous price
Price change percentage = Price change / previous price × 100
Box-EV impact = expected copies per box × Price change
```

The box-impact formula assumes unchanged pull weights and variant membership. Handle zero previous prices without division by zero.

Proposed configurable mover defaults:

- Absolute card-price movement of at least 1%.
- Absolute box-EV impact of at least $0.05.
- Both thresholds must pass.
- Rank by absolute box-EV impact, with deterministic tie-breaking.
- Show no more than ten movers per set.

Example mover format, using illustrative values:

```text
🟢 Card Name (AA): +$4.20 (+$0.35 bx) [$52.10]
```

## Discord report

Keep the compact format of the supplied Lorcana examples:

1. Set name and code.
2. `Booster Box:` heading with indented `EV:` (including comparison delta and direction) and `MP:` (the configured sealed-box listing's market price, or Unavailable).
3. Blank line, then master-set value and delta above the rarity breakdown.
4. Full category heading (for example `Treasure Rare:`), then an indented `AVG:` line with category box EV, delta and share of total box EV.
5. Indented `Total (X):` line with the collection value and variant count for each category. AVG means expected contribution per box, not average card price.
6. Qualifying top-moving cards.
7. Compact source date, comparison interval, and incompleteness notes when relevant. Finish each set report with an extra blank line for visual separation.

Each report ends with a short warning naming weaker pull-rate assumptions. Attach a per-set Markdown document to its final Discord message part containing the actual adjusted expected-card counts, physical configuration, weighting, assumptions, source links and profile version/hash. Generate the file from the calculation profile and freeze it with the delivery batch. Save the same document locally during previews. Community rates remain accepted working estimates; no claim of official guarantees is made.

Use 🟢 for increases, 🔴 for decreases, and ⚪ for unchanged values. Normalize rounded negative zero. Omit inapplicable categories rather than cluttering every set with empty sections.

Support multiple independently configured webhook destinations and optional set filters per destination. Split oversized reports within Discord limits without losing set context. Disable unintended mentions from source text.

Handle rate limits and transient errors. Record successful delivery per destination and message part so retries can resume after partial failures and ordinary repeated runs do not repost successful reports. Do not claim exactly-once delivery: an ambiguous network failure after Discord accepts a message can require reconciliation.

## Configuration and operating modes

`appsettings.json` should cover:

- Data-source settings, cache/state paths, and request limits.
- Webhook destinations, enabled flags, display names, and optional set filters.
- Enabled/excluded sets and profile-file location.
- Pricing and incomplete-data policies.
- Mover thresholds, maximum mover count, and report formatting.
- Logging options.

Keep committed examples free of actual webhook credentials; allow environment-variable overrides. Never log webhook tokens.

Provide:

- Default run: ingest or reuse data, calculate, post eligible undelivered reports, and exit.
- `--dry-run`: calculate and preview reports without posting or marking delivery successful.
- Configuration/profile validation mode.
- Clear exit codes and logs suitable for scheduled execution.
- Protection against overlapping runs corrupting snapshots or duplicating delivery.

## Implementation milestones

- [x] **1. Catalog and profile audit.** Representative memberships mapped; unresolved shared-price equivalence and pull odds adopted explicitly as assumptions, not claimed verified facts.
- [x] **2. Pricing and calculation core.** C# ingestion, explicit mappings, reconciled profiles and representative local reports implemented; live HTTPS dry run passed.
- [x] **3. History and comparisons.** Durable snapshots, profile hashes, deltas, mover filtering and first-run handling implemented and checked.
- [x] **4. Discord delivery.** Formatting, multipart outbox, rate-limit retries and delivery state implemented; fake-HTTP tests pass. Live verification passed: three confirmed message IDs; rerun preserved the same deliveries.
- [x] **5. Configuration and operations.** Example settings, validation, previews, process lock and deployment/scheduling guidance available in README.
- [x] **6. Released-set coverage and documentation.** Added all 22 released sets, date ordering, packaged toppers, separate chase/special-pack pools, publisher checklist reconciliation, generated assumption files and live ingestion verification. Future EB-05/OP-18 remain explicitly planned rather than assigned incomplete rosters.

Expansion follow-ups: obtain missing quotes, improve uncertain rates/finish equivalence, and finish EB-05/OP-18 profiles when catalogs are complete. OP15 Sentomaru's provider rarity/finish mismatch remains deliberately unpriced. No expanded bulk Discord post has been made.

### Milestone 1 progress

- [x] Resolve source access and capture a dated, hashed snapshot.
- [x] Inventory 87 groups and identify 24 booster candidates with box listings.
- [x] Audit OP-08, EB-01, and PRB-01 metadata, price subtypes, and official checklist counts.
- [x] Produce explicit variant/pool mappings: 152 OP-08, 80 EB-01, and provisionally 409 PRB-01 variants.
- [x] Identify DP-05 exclusions, PRB-01's 54 shared-listing gaps, Brannew's unverified Normal finish, and Manga Ace's missing price.
- [x] Record sourced candidate rates and verified OP-01 bonus contents in draft profiles.
- [x] Validate mappings, source hashes, and candidate case arithmetic offline.
- [x] Explicitly adopt 54 shared original listings as pricing proxies with physical-equivalence uncertainty documented; quarantine Brannew Normal and include Foil.
- [x] Adopt documented estimates for remaining slots, chase frequencies and replacements; counts reconcile to 288/288/200.

The [audit report](research/MILESTONE-1-AUDIT.md) and disabled [draft profiles](research/pull-rate-profiles.draft.json) preserve research observations. Enabled application profiles live in [config/pullrates.json](config/pullrates.json); the README distinguishes adopted assumptions from verified facts. Improving weakly supported rates and expanding set coverage remain ongoing work.

## Verification and acceptance criteria

- Hand-calculated fixtures verify expected-count weighting and case-to-box conversion.
- Replacement and bonus fixtures verify no double counting and reconciled card counts.
- Classification fixtures verify AA SEC, AA Leaders, Manga, SP/TR, premium DON!!, reprints, and finish variants.
- Master-set fixtures verify one copy per distinct eligible variant across multiple pull sources.
- Missing-price and unsupported-profile cases cannot produce an apparently complete EV.
- History fixtures cover first runs, missing days, zero previous prices, membership changes, and profile changes.
- Mover fixtures verify thresholds, ordering, and the ten-card cap.
- Delivery tests cover size limits, rate limits, partial failures, and repeated runs.
- Dry-run output is reviewable before configuring live destinations.
- README formulas and examples match the implemented model and profiles.

## Outstanding research and limitations

- Source access is resolved through Python HTTPS with certificate verification enabled. Representative payloads and official checklists have been inspected and saved; the production C# HTTP client has now passed a live HTTPS dry run.
- The linked community guide provides useful context but cannot establish reliable rates for every release by itself.
- Verify each product's ordinary slots, hit replacements, AA Leader rates, SP/TR/Manga rates, DON!! treatments, special packs, and packaged bonuses.
- PRB-01 requires 54 explicit shared original-listing mappings; these are explicitly adopted shared-price proxies; physical artwork-equivalence review remains incomplete. OP-08 requires excluding two DP-05 DON!! designs that share its source group.
- Define explicit treatment mappings for exceptional chase cards and any booster-pullable P cards.
- Confirm each supported product's regional configuration and release eligibility; do not infer these solely from catalog group timestamps.
- Scope of initial supported releases depends on completed profile audits. Unknown odds remain visible gaps, not silently adopted global rates.

## References

- [TCGCSV](https://tcgcsv.com/)
- [TCGCSV API documentation and usage guidance](https://tcgcsv.com/docs)
- [TCGCSV FAQ and historical-price archive information](https://tcgcsv.com/faq)
- [Community rarity and pull-rate guide supplied by the user](https://www.reddit.com/r/OnePieceTCGFinance/comments/1fp510n/understanding_one_piece_tcg_rarity_system_pull/)
- [Official Bandai PRB-01 product contents](https://en.onepiece-cardgame.com/products/boosters/prb01.php)
- [Official Bandai PRB-02 product contents](https://en.onepiece-cardgame.com/products/boosters/prb02.php)
- [Official Bandai OP-08 product page](https://en.onepiece-cardgame.com/products/boosters/op08.php)
- [Official Bandai EB-01 product page](https://en.onepiece-cardgame.com/products/boosters/eb01.php)
- [Discord webhook documentation](https://docs.discord.com/developers/resources/webhook)

## Decision log

| Decision | Status |
| --- | --- |
| Webhooks per destination channel | Confirmed by user; restored after reconsidering the bot API. No inbound infrastructure required. |
| All distinct box-obtainable artworks and finishes in master set | Confirmed by user |
| Separate Manga / exceptional chase section | Confirmed by user |
| Include packaged box bonuses in EV | Confirmed by user |
| PRB-01 English uses a 10-box case | Research finding; sourced in Milestone 1 audit |
| OP-08 excludes DP-05 bonus DON!!; premium finishes retain separate internal pools | Applied in audit mappings |
| PRB-01 shared listings / Brannew | 54 explicit pricing proxies adopted with uncertainty; Brannew Normal quarantined |
| Per-set sourced pull-rate profiles and expected-count EV model | Implemented for three representative sets |
| .NET 10, JSON snapshots, companion pullrates.json | Implemented |
| 1% price movement AND $0.05 EV impact; maximum ten movers | Proposed configurable defaults |

When changing this plan, update the relevant section and decision log together. Record changes to implemented calculation rules in the README and version the affected profiles.

