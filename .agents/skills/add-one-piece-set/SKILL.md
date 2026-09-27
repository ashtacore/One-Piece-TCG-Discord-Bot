---
name: add-one-piece-set
description: Add or revise One Piece booster-set profiles in this repository, including catalog mappings, pull-rate assumptions, release eligibility, and verification. Use for new sets, activating planned releases, or correcting set coverage and collation; not ordinary report formatting.
---

# Add or revise a One Piece set

Deliver a reproducible, explicitly mapped English booster profile, its assumptions document, and verified local reports. Work from the repository root; paths below are root-relative.

## Establish the scope

Read `README.md` for implemented math, pricing, configuration, history and delivery behavior. Inspect `config/pullrates.json`, the relevant `research/` artifacts, and `src/OnePiece.Ev/Models.cs` before editing. Use a comparable profile as a starting point, not proof of another release's odds.

The user accepts community estimates because official detailed hit rates are unavailable. Proceed with documented, reasonable assumptions within that scope; do not require manufacturer guarantees. Distinguish publisher facts, opening observations, extrapolations and unmeasured placeholders. A warning does not turn an invented rate into evidence. Identify the most consequential uncertainties in the final report warning and attachment.

Preserve unrelated profiles, credentials, destination filters and delivery/history state. Adding coverage does not itself authorize a live webhook test. Honor any explicit live-test authorization already given for the current work. Never dump `appsettings.local.json` or credential-bearing values while inspecting settings.

## Verify the product and roster

1. Confirm the English name, release date, TCGCSV category/group ID and exact sealed-box product ID. Preserve combined English products such as OP14-EB04; catalog abbreviations and printed card-number prefixes can differ from product membership. Choose the intended box printing, not a collectible first-wave listing by accident.
2. Verify packs per box, cards per pack, case size and packaged bonus contents. Include bonuses inside the sealed box; exclude separate Double Pack products, store-purchase Dash Packs and tournament promos. Do not generalize either a 12-box case or bonus availability across products.
3. Retrieve products, prices and the publisher checklist. Reuse dated caches where suitable. Preserve source URLs, retrieval/snapshot dates and hashes. Fetch new prices into a separate snapshot if the provider date differs; never append new-day prices to a frozen fixture or relabel its timestamp. Check provider timestamps before and after collection.
4. Build an explicit allowlist keyed by product ID and price subtype, with number, name, category, pool and weight. Printed rarity alone does not identify AA, Manga, SP, special Event art, premium DON or reprints. Classification precedence matters: exceptional chase treatments before ordinary AA/printed rarity. Review each new treatment rather than automatically putting it in AA.
5. Reconcile numbered-artwork multiplicities against the publisher checklist; check DON finishes separately. Equal counts can conceal a wrong artwork mapping: inspect discrepancies and exceptional identities individually. Record explicit product/finish exclusions and reasons. Missing price rows must not remove otherwise eligible cards.

Repository examples worth consulting: OP01/02 packaged toppers; PRB01 shared original-listing proxies; PRB02 special Event art; OP13 Elder parallels and Imu; EB04-029/053 publisher/provider rarity conflicts. A number match does not establish artwork/finish equivalence. Use cross-group price proxies only as explicit, evidenced assumptions. Never substitute a Normal quote for an unresolved Foil variant silently.

## Build the model and configuration

- Model expected copies per box, not a per-card probability multiplied by price without converting back to box scale. One card per N boxes contributes average eligible price / N.
- Separate pools when frequencies differ. Define which pool each chase replaces. For special packs, remove displaced ordinary contents and add the event's expected cards; record event frequency, roster and any unresolved case-level displacement. Do not apply replacement adjustments twice.
- Reconcile pool counts to packs times cards plus packaged bonuses. This is an accounting invariant, not evidence that the assumed distribution is correct. Preserve decimal precision and avoid negative counts.
- Keep one master-set entry per distinct artwork/finish, even when obtainable through several mechanisms. Combine its expected counts, using a separate pool when necessary rather than duplicating its identity. Document equal-weight assumptions or justified unequal weights.
- Retain Market-to-Mid fallback, source tracking and unknown-price behavior from the README. Pricing uncertainty and pull-rate uncertainty are separate. Do not add graded or secondary prices as part of set coverage unless requested.
- Add sources, specific assumptions, confidence and a new profile version. If introducing a pool, update its readable label and relevant warning in `AssumptionDocument.cs`; verify report category and calculation behavior.
- Sort profiles by English release date. Keep incomplete future catalogs in `plannedSets`; a date passing is insufficient for activation. When activating, remove the planned entry only after the profile is complete and verified. Release filtering checks both current and source dates.
- Update `enabledSets` where the task calls for activation, preserving destination filters. Empty enabled/destination set lists mean all eligible sets; explain any expanded next-run posting scope.

## Maintain reproducibility

Inspect generators before running them. `scripts/build_profiles.py` owns the original three profiles. `scripts/build_expanded_profiles.py` rebuilds a fixed set list and reconstructs planned entries: update its roster/date/source logic and retention behavior before adding a new set, or it can drop a manually added profile. Keep generator inputs and generated configuration consistent; inspect the diff for unrelated changes.

`research/set-registry.json` and original audit artifacts describe a historical snapshot; preserve that provenance when adding new evidence. The fetch/checklist helpers contain fixed group lists, series IDs and cache locations. Adapt them to the target release and snapshot instead of assuming they are universal discovery tools. Publisher URL shapes also vary.

The C# tests and `scripts/validate_expansion.py` include fixed coverage counts/code lists and dated-price assertions. Update those for intentional coverage changes while preserving meaningful roster and arithmetic checks. Do not weaken assertions just to make a new profile pass.

## Test and inspect

Use the committed non-secret config or a temporary preview config with suitable set selection and separate output/state paths when needed. Always run commands from the repository root. Choose the applicable checks:

```powershell
dotnet run --project src/OnePiece.Ev -- --validate --dry-run
dotnet run --project tests/OnePiece.Ev.Tests
dotnet run --project src/OnePiece.Ev -- --catalog research/catalog-cache
python scripts/validate_expansion.py
```

The catalog path is an example: use the correct dated fixture and align test loaders when it differs. The expansion validator consumes a full-set `out/latest.json`; generate the matching preview before running it. `python scripts/validate_audit.py` checks the original audit when shared research artifacts change. After offline checks pass, use `--dry-run` for production HTTPS verification; `--offline` only checks an existing compatible cache.

Verify meaningful behavior:

- Every eligible catalog identity is mapped or explicitly excluded; no duplicate master-set entries or zero-probability eligible variants.
- Publisher roster correspondence, exact box listing, physical totals, packaged bonuses and replacement arithmetic.
- Special-pack marginal counts and combined-source variants; newly introduced treatment precedence.
- Missing Market uses labeled Mid; neither price stays unknown; unverified finishes do not receive another finish's price.
- Release ordering, future-date exclusion and requested set filtering.
- Generated report and per-set Markdown attachment use the same profile/hash and adjusted counts. Inspect warnings, incomplete totals and multipart message boundaries. Run simulated webhook tests if delivery/attachments change.
- Existing representative profiles still pass; version/source changes cannot become false price movers.

Do not clear delivery records to force a repost. Preview mode must not advance history or send messages. If a live test is authorized, use the existing delivery/reconciliation rules and stop on an ambiguous delivery outcome rather than automatically retrying it.

## Finish

Update README coverage and any changed implemented rules, plus the coverage audit and verification JSON. Do not claim odds are verified merely because tests pass. Summarize added/changed sets, checks actually run, missing quotes and mapping limitations, inactive planned releases, and whether messages were sent. Link the local preview and assumptions document for review. Do not block completion solely on a missing price when the configured incomplete-report behavior handles it honestly.
