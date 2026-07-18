---
name: icecream-race-help
description: Explain ActionFit Ice Cream Race, its installed skills, sealed engine ownership, race state, catalog parity, durable persistence, reward recovery, tests, and integration boundaries. Use when a user asks how the race package works or which package skill applies.
---

# ActionFit Ice Cream Race Help

Answer in the user's language. Explain the package without running an audit, starting Unity, executing race commands, reading persisted state, granting rewards, or changing project or release state unless the user separately requests an authorized operation.

1. Read `PACKAGE_SKILLS.md` first. Treat its generated package identity, complete related-skill table, `$skill-name` invocations, descriptions, and access boundaries as authoritative.
2. Resolve `Packages/com.actionfit.icecream-race`; otherwise use `Library/PackageCache/com.actionfit.icecream-race@*` without editing it. Read `package.json`, `README.md`, and `AI_GUIDE.md` when present.
3. Explain sealed `IceCreamRaceEngine` ownership, event and race transitions, five-to-four-to-three elimination, live curve ranking, pinned catalog snapshots, buffered versus flushed persistence, displayed presentation acknowledgements, and durable reward recovery.
4. Keep project event buses, order and merge conversion, inventory mutation, UI, Addressables, analytics, localization, legacy-key migration, and licensed assets in consuming-project adapters or presentation packages.
5. Identify `com.actionfit.icecream-race.Editor.Tests` and the package `README` menu under `Tools > Package > ActionFit Ice Cream Race`.
6. State that help and audit do not run Unity, execute gameplay, inspect or mutate saved data, grant rewards, edit scenes or prefabs, publish, push, tag, or update the package catalog.
