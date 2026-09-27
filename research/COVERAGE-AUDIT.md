# Expanded set coverage audit

All 22 released rosters reconcile by numbered-artwork multiplicity with their saved Bandai checklists. DON finishes are additional. This validates roster accounting, not measured odds or a visual image-by-image equivalence audit. Rates remain explicit community-based estimates and low-confidence assumptions.

| Set | English release | Variants | Expected physical cards | Missing prices | Mid fallbacks |
| --- | --- | ---: | ---: | ---: | ---: |
| OP-01 | 2022-12-02 | 155 | 289 | 0 | 0 |
| OP-02 | 2023-03-10 | 155 | 289 | 0 | 0 |
| OP-03 | 2023-06-30 | 155 | 288 | 0 | 0 |
| OP-04 | 2023-09-22 | 150 | 288 | 0 | 0 |
| OP-05 | 2023-12-08 | 155 | 288 | 0 | 1 |
| OP-06 | 2024-03-15 | 152 | 288 | 0 | 0 |
| EB-01 | 2024-05-03 | 80 | 288 | 0 | 0 |
| OP-07 | 2024-06-28 | 152 | 288 | 0 | 0 |
| OP-08 | 2024-09-13 | 152 | 288 | 0 | 0 |
| PRB-01 | 2024-11-08 | 409 | 200 | 0 | 1 |
| OP-09 | 2024-12-13 | 160 | 288 | 1 | 1 |
| OP-10 | 2025-03-21 | 152 | 288 | 0 | 0 |
| EB-02 | 2025-05-09 | 105 | 288 | 5 | 1 |
| OP-11 | 2025-06-06 | 157 | 288 | 0 | 0 |
| OP-12 | 2025-08-22 | 156 | 288 | 0 | 0 |
| PRB-02 | 2025-10-03 | 406 | 200 | 0 | 0 |
| OP-13 | 2025-11-07 | 177 | 288 | 0 | 2 |
| OP14-EB04 | 2026-01-16 | 203 | 288 | 0 | 0 |
| EB-03 | 2026-02-20 | 102 | 288 | 0 | 0 |
| OP15-EB04 | 2026-04-03 | 198 | 288 | 1 | 0 |
| OP-16 | 2026-06-12 | 157 | 288 | 0 | 0 |
| OP-17 | 2026-08-28 | 177 | 288 | 2 | 0 |

## Specific decisions

- OP01/02 include one packaged topper; OP01 sealed MP uses the later White box listing, avoiding the first-wave collectible box premium.
- OP14-EB04 and OP15-EB04 remain combined English products, matching Bandai labels. TCGCSV calls the former OP14 and calls EB-03 EB-03-04; IDs are mapped explicitly.
- Dash Packs are purchase campaigns, not assumed to be inside sealed boxes. Double Pack and tournament products are excluded.
- Additional Normal finish quotes for ordinary R/SR/SEC are excluded from booster rosters. OP10 Normal alternate DON is unverified and excluded.
- Publisher rarity overrides TCGCSV on EB04-029 (UC) and EB04-053 (R). Sentomaru has only a Normal quote; require Foil and retain unknown pricing.
- PRB02 has 316 numbered artwork entries plus 90 DON finishes. Its four special Event artworks share the SP rate allocation, not ordinary AA.
- OP13 Elder parallels are modeled as demon-pack exclusives. Imu AA Leader combines ordinary and special-pack expected counts without duplicate master-set entries.
- OP09 Gold Roger, OP11/12/13 anniversary treatments, signature/red/super variants, Gold DON and Pandaman each use separate pools.
- Scarce-event frequencies and replacement patterns are estimates. New gold (1/1200 boxes), silver (1/120), signature (1/240), red-super (1/1200), and some super-art rates are explicit placeholders, not measured rates. Attachment warnings flag them.
- EB03 and OP17 special-pack rosters are modeled approximately; PRB02 assumes equal SR weighting despite reports of unequal new/reprint SR frequencies. These remain material limitations.
- EB05 (2026-10-30) and OP18 (2026-11-20) are planned, inactive entries. Their catalogs are incomplete; reaching the date alone will not activate an unreviewed profile.

## Missing quotes

- OP-09: Monkey.D.Luffy (119) (Manga) (597068:Foil).
- EB-02: Kouzuki Oden (SP) (629086:Foil).
- EB-02: Monkey.D.Luffy (061) (Manga) (629167:Foil).
- EB-02: Monkey.D.Luffy (SP) (629171:Foil).
- EB-02: Vegapunk (SP) (629184:Foil).
- EB-02: King (SP) (629188:Foil).
- OP15-EB04: Sentomaru (685324:Foil).
- OP-17: Monkey.D.Luffy (EB04-061) (SP) (710857:Foil).
- OP-17: Rocks.D.Xebec (118) (Super Alternate Art) (712117:Foil).

## Verification

Run `python scripts/validate_expansion.py` after a full-set preview. It checks release ordering, requested coverage, unique identities, physical counts, official numbered-artwork multiplicities and runtime mapping issues. The companion JSON stores checklist hashes and per-set source URLs. C# tests additionally exercise release filtering, exact bonus counts, special pools, prices, report splitting, history and simulated webhook delivery. No bulk Discord posting was performed for this expansion.
