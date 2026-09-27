# Milestone 1: Catalog and pull-rate audit

Research date: September 26, 2026 (America/Los_Angeles).

Price snapshot: **2026-09-26T20:05:16+0000** from TCGCSV.

## Outcome

The catalog audit is delivered, with explicit variant mappings and sourced draft profiles for **OP-08, EB-01, and PRB-01**. Catalog access works through Python's standard-library HTTPS client with certificate verification enabled. Earlier PowerShell/curl TLS failures were environment/client-specific; they did not establish a source outage.

The research utilities are not the production application. Milestone 2 remains a C# implementation.

**Implementation update (September 27 UTC):** representative profiles are now enabled in `config/pullrates.json` using the explicitly adopted assumptions documented in `README.md`. This closes the representative milestone by adoption, not by establishing manufacturer guarantees. Shared original listings remain price proxies with incomplete physical artwork verification; Brannew Normal is quarantined. The original drafts below preserve unresolved research observations and remain disabled. A working C# client has also passed a live TCGCSV dry run.

## Deliverables

| File | Purpose |
| --- | --- |
| [set-registry.json](set-registry.json) | Dispositions for all 87 source groups, including 24 booster candidates |
| [catalog-summary.json](catalog-summary.json) | Raw product/price counts and anomalies for the three representatives |
| [mapping-summary.json](mapping-summary.json) | Counts after treatment classification and membership decisions |
| `op-08`, `eb-01`, `prb-01` `*-catalog.json` | Representative card metadata and actual snapshot prices |
| `*-official-checklist.json` | Number, rarity, and artwork identifiers extracted from Bandai's game-card checklists |
| `*-variant-mapping.json` | Exact product/subtype keys, report categories, pull pools, and membership status |
| [prb-01-shared-reprints.json](prb-01-shared-reprints.json) | 54 proposed cross-group original-listing mappings |
| [prb-01-checklist-gaps.json](prb-01-checklist-gaps.json) | Reconciliation evidence for those 54 mappings |
| [pull-rate-profiles.draft.json](pull-rate-profiles.draft.json) | Physical configurations, observations, candidate rates, sources, exclusions, and unresolved rules |
| [source-manifest.json](source-manifest.json) | Source URLs, retrieval times, and SHA-256 hashes |

`catalog-matched` means a listing was matched/classified for the audit, not that its pull rate was verified. `proposed-shared-original-listing` explicitly requires equivalence review before production use.

## Discovery and set membership

The category is **68, One Piece Card Game**. The snapshot contains 87 groups. Twenty-four are booster candidates with a sealed box listing; 63 are excluded as standalone booster sets. Excluded groups can still supply an explicitly mapped reprint's price.

The candidates include OP01–OP18, EB-01, EB-02, combined EB-03-04, EB-05, PRB-01 and PRB-02. OP15 is cataloged as `OP15-EB04`. These labels must not be silently rewritten into independent products. OP18 and EB-05 have future catalog dates relative to this audit; release eligibility needs publisher verification before reporting. No candidate is automatically EV-enabled.

Discovery requires more than a name filter:

- Release-event, anniversary-tournament, and pre-release groups are not booster-box sets.
- EB-02 and EB-03-04 box listings say `Extra Booster: ... Box`, not `Booster Box`.
- OP-01 has separate blue/white box listings; selecting the first sealed listing would hide printing-wave differences.
- SP/TR cards can have old OP/ST card numbers while belonging to the current booster release.

The registry is a snapshot inventory, not a claim that all 24 profiles are researched. Source: [TCGCSV groups](https://tcgcsv.com/tcgplayer/68/groups), plus the product endpoints recorded in the manifest.

## Representative catalog reconciliation

| Product | Group | Box product | Raw card products | Raw card price rows | Mapped eligible variants |
| --- | ---: | ---: | ---: | ---: | ---: |
| OP-08 Two Legends | 23462 | 542504 | 154 | 154 | 152 |
| EB-01 Memorial Collection | 23333 | 521161 | 80 | 80 | 80 |
| PRB-01 The Best | 23496 | 545399 | 325 | 356 | 409, including 54 proposed shared mappings |

Counts are independently computed from the frozen source payloads. A card product can have multiple price subtypes, so a product count is not a master-set variant count.

### OP-08

The mapped roster is:

- 45 C, 30 UC, 26 ordinary R, 10 ordinary SR, 2 ordinary SEC, and 6 ordinary Leaders.
- 18 ordinary AA and 6 AA Leaders.
- 6 SP, 1 TR, 1 Manga, and 1 booster DON!! treatment.

Bandai's game-card checklist has 151 entries; adding the booster DON!! gives 152. The two extra catalog cards are **576484:Foil** and **576485:Foil**, the DP-05 bonus designs. Both are excluded. The included booster DON!! is **577568:Foil**. Bandai separately describes DP-05 as two OP-08 packs plus one of two bonus DON!! designs. [OP-08 checklist](https://en.onepiece-cardgame.com/cardlist/?series=569108), [DP-05 contents](https://en.onepiece-cardgame.com/products/other/dp05.php).

All 152 mapped variants have a market price in this snapshot. The rarity field alone is inadequate: the six `(SP)` products retain C, R, or SR metadata, and `(Parallel)` identifies ordinary AA. The Manga and TR require precedence over generic AA/printed-rarity classification.

### EB-01

The source's 80 card products reconcile to Bandai's 80 game-card artwork entries:

- Ordinary pool: 28 C, 21 R, 8 SR, 1 SEC, and 3 Leaders.
- Special treatments: 15 ordinary AA, 3 AA Leaders, and 1 Manga Chopper.

There is **no UC pool**, and no SP, TR, or custom DON!! appears in this checklist. Do not apply a generic OP rarity template. All 80 variants have prices. Bandai explicitly confirms 24 packs of 12 cards. [EB-01 contents](https://en.onepiece-cardgame.com/products/boosters/eb01.php), [EB-01 checklist](https://en.onepiece-cardgame.com/cardlist/?series=569201).

### PRB-01

This product requires cross-group pricing and treatment-aware pools:

| Pool | Distinct variants |
| --- | ---: |
| Ordinary C / UC / R / SR / SEC / P / L | 23 / 25 / 20 / 25 / 6 / 4 / 1 |
| Ordinary AA / AA Leader | 60 / 1 |
| Full Art / Textured Foil | 50 / 22 |
| Jolly Roger Foil | 72 |
| Normal / Foil / Gold DON!! | 30 / 30 / 30 |
| Manga | 10 |
| **Total** | **409** |

The ordinary game-card pool has **104 identities**. Bandai's 111-number headline includes seven numbers represented only by Manga in this release. Treating its advertised rarity totals as ordinary pull pools would insert nonexistent ordinary cards. The official game-card checklist contains 319 artwork entries; adding 90 DON!! finishes yields 409 candidate variants. [Official PRB-01 checklist](https://en.onepiece-cardgame.com/cardlist/?series=569301).

The source group alone is short by 54 ordinary listings. Each gap was matched to an ordinary English original-set product by number, rarity, and finish. The mappings are explicit and retain the original source group; they are **not a general rule to choose any matching card number**. Exact artwork/artist-credit equivalence needs final review. An independent audit reports the same shared-listing issue; its price metric and no-chase simulation model are not adopted here. [Independent model author's notes](https://theexpectedvalue.com/one-piece/ev/prb-01).

Two additional issues are visible in the actual snapshot:

- **594331:Normal — Brannew (Reprint):** quarantined. Its Foil record remains included. The extra nonfoil row is not sufficient evidence that the finish comes from this booster. [Community finish discussion](https://www.reddit.com/r/OnePieceTCG/comments/1gotcp3/).
- **587709:Foil — Manga Portgas.D.Ace:** `marketPrice` is null. Keep it in the roster and mark valuation incomplete; do not zero it, omit it, or use the original Manga's price without an explicit policy.

The 30 ordinary DON!! product IDs each have Normal and Foil price rows. Gold DON!! uses separate product IDs. These are 90 master-set variants, not 90 physical pulls or three independent DON!! slots.

`(Textured Foil)` and `(Full Art)` remain separate internal pools from `(Alternate Art)`, even though all contribute to the AA report section. Jolly Roger C/UC still report under Base, and Jolly Roger P cards under Other, consistent with the chosen rarity-based grouping.

## Physical contents and bonuses

| Product | Packs × cards | Boxes per case | Evidence / limitation |
| --- | --- | --- | --- |
| OP-08 | 24 × 12 | 12 | Standard English configuration; case descriptions corroborate the user's case assumption |
| EB-01 | 24 × 12 | 12 | Pack/box counts published by Bandai; standard-case assumption retained |
| PRB-01 | 20 × 10 | **10** | English retail configuration; not the standard 12-box case |

PRB-01's 10-box configuration is described by an English retailer and is consistent with the community's two-SEC-per-box / twenty-per-case account. [Retail configuration](https://phantomich.com/products/premium-booster-box-prb-01-english), [English opening discussion](https://www.reddit.com/r/OnePieceTCG/comments/1gjy045/english_prb01/).

No separately packaged topper was identified for these three representatives. This is an audit finding, not proof about every printing wave. No bonus is added by analogy. A god pack is a replacement pack configuration, not ten extra cards on top of 200.

For a verified bonus example, Bandai states OP-01 has one random topper from six designs. The draft includes the six catalog product IDs as a reference for later implementation. Equal 1/6 selection remains an assumption. [Official OP-01 bonus](https://en.onepiece-cardgame.com/products/boosters/op01.php).

## Pull-rate evidence and draft model

The draft JSON deliberately separates **candidate observations** from **adopted expected counts**. All three profiles have `enabledForEv: false` and a null final count model. This prevents a research estimate from silently becoming production truth.

### OP-08 candidate counts

An English opening discussion reports roughly 7 ordinary SR, 8 Leaders, and 2 custom DON!! per box. A commenter with 15+ combined OP06/07/08 cases reports usually 4 AA Leaders and 2 SP per case, with exceptions. Another comment's 12-box recipe implies 16 ordinary AA and 8 SEC per case. These are usable candidate inputs, but not an isolated OP08 frequency study. [Opening reports and recipe](https://www.reddit.com/r/OnePieceTCG/comments/1jgnfii/a_casual_players_primer_on_booster_box_drop_rates/).

The candidate recipe totals 30 premium hits per case, or 2.5 per box. Its AA count is **before** any Manga/TR replacement adjustment. Outstanding: TR and Manga rates, the displaced pools, and ordinary C/UC/Leader/DON!! slot allocation.

### EB-01 candidate counts

The same opening discussion gives 6 SR and 6 ordinary Leaders for EB01. Release-week reports show two ordinary AA plus SEC, and Manga occurring alongside those three hits. They disagree over whether Manga replaces an SR; it cannot safely be assigned to the ordinary AA slot by default. A separate community comment claims eight ordinary SEC per case, without a complete ledger. [Selected EB-01 pulls](https://www.reddit.com/r/OnePieceTCG/comments/1cjbyiz/so_4hit_boxes_are_definitely_a_thing_with_eb01/), [SEC-rate claim](https://www.reddit.com/r/OnePieceTCG/comments/1kjni2b/).

Outstanding: the SEC/AA Leader mixture, C/R counts, and Manga frequency/replacement. Do not interpret three premium hits as three ordinary AAs.

### PRB-01 candidate counts

First-hand selected-box reports support two SEC and two Foil-or-Gold DON!! per box. A detailed community recipe claims 18 SR, two ordinary-AA-level hits, and two Full Art/Textured hits. It claims two Gold DON!! per ten-box case, with Gold replacing Foil. Sanji AA and standalone Manga Nami are estimated at one per three cases and said to replace AA; neither assertion is a measured rate. [English PRB-01 discussion](https://www.reddit.com/r/OnePieceTCG/comments/1gjy045/english_prb01/).

Outstanding: bulk rarity/promo allocation, ordinary Leader versus Jolly Roger weights, Full Art/Textured split, chase replacements, and the separate Manga special-pack branch. A ten-Manga pack needs a correlated roster model, not ten independent manga-category chances. The generic guide's broad special-pack estimates are research leads, not adopted PRB01 odds. [User-supplied guide](https://www.reddit.com/r/OnePieceTCGFinance/comments/1fp510n/understanding_one_piece_tcg_rarity_system_pull/).

## Why no complete EV is published yet

Completeness requires both an eligible variant roster and an expected count for each pool. A numerically balanced 288- or 200-card model can still be wrong if its remainder is assigned to the wrong rarity. Likewise, adding plausible-looking chase rates to an existing hit count would double-count replacement slots.

The next rate review must either find sufficient English opening evidence or adopt explicit, documented modeling assumptions for each remaining slot. Missing evidence is not an official zero rate. No market-value figure in this audit is offered as a complete box EV.

## Reproduction and verification

From the repository root:

```powershell
python -X utf8 scripts/audit_catalog.py --fetch
python -X utf8 scripts/inspect_official.py
python -X utf8 scripts/build_audit_mappings.py
python -X utf8 scripts/validate_audit.py
```

The initial fetch caches provider files; subsequent runs reuse them. Omit `--fetch` for offline auditing. The fetch verifies that the source timestamp did not roll over during collection, and the offline audit verifies cached hashes. This is a frozen research snapshot, not the future daily-sync implementation. Do not mix later downloads into it. To research a new date, preserve the old cache and use a separate snapshot directory before fetching.

Raw provider JSON and official HTML are in the ignored `research/catalog-cache/` directory. Normalized artifacts and the manifest remain in the project. Re-fetching on a later date produces a new market snapshot, not the historical prices above.

Validation checks representative roster counts, unique product/subtype keys, category precedence, DP-05 exclusions, the Brannew quarantine, 54 explicit shared mappings, DON!! finishes, the missing Manga price, official checklist counts, candidate case arithmetic, source references, and disabled incomplete profiles.

## Remaining work before closing Milestone 1

- [ ] Review the 54 shared-listing artwork/artist-credit equivalences and the Brannew Normal anomaly.
- [ ] Resolve or explicitly adopt assumptions for ordinary slot allocation and within-pool weighting.
- [ ] Resolve or explicitly adopt assumptions for TR, Manga, and special-pack frequencies and replacements.
- [ ] Turn the draft profiles into complete, count-conserving models with uncertainty labels and documented bonus policies.

The catalog mappings, source fixtures, and known anomalies are ready to support Milestone 2's ingestion/classification code while this rate review continues.
