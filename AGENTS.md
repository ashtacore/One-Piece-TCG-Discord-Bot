# One Piece EV reporter

This .NET 10 console app reads English/USD TCGCSV prices, calculates booster-box EV and master-set values from explicit set profiles, and posts reports with assumption attachments to Discord webhooks.

Read [README.md](README.md) for setup, calculation/pricing rules, configuration, and verification commands. Keep it consistent with implemented behavior; use `--dry-run` for routine testing.

Use [add-one-piece-set](.agents/skills/add-one-piece-set/SKILL.md) when adding or revising set profiles, activating planned releases, or changing catalog mappings or pull-rate assumptions. It covers research, reproducible configuration, tests, and coverage verification.
