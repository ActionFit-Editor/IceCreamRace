---
name: icecream-race-audit
description: Audit ActionFit Ice Cream Race source and project adapters for durable transition flushes, elimination and live-rank rules, catalog pinning, displayed snapshot monotonicity, reward transaction ordering, and restart recovery without running Unity or changing state. Use when reviewing race package changes or integrations.
---

# Audit ActionFit Ice Cream Race

Keep the audit read-only and source-only. Do not start Unity, enter Play Mode, execute race commands, read or mutate persisted state, call reward services, open or save scenes or prefabs, or change repository, project, package, or release state.

1. Read repository instructions so project routing and safety rules apply before inspection.
2. From the repository root, capture `git status --short --untracked-files=all` as the audit baseline and preserve every pre-existing change.
3. Resolve `Packages/com.actionfit.icecream-race`; otherwise use `Library/PackageCache/com.actionfit.icecream-race@*` without editing it. Read `package.json`, `README.md`, and `AI_GUIDE.md`.
4. Use `rg` and read-only inspection to trace `IceCreamRaceEngine`, `IceCreamRaceCatalog`, `IceCreamRaceState`, `IceCreamRaceStateSerializer`, `IIceCreamRaceCatalogResolver`, state and reward adapters, project facades when present, and deterministic tests.
5. Verify and report source evidence for these contracts:
   - Event start, race start, result resolution, timeout or end, result claim, and both reward transaction boundaries flush `IFlushableContentStateStore` when available; ordinary token and presentation acknowledgement saves may remain buffered.
   - Round one uses five racers and a top-three cutoff, round two uses four and top two, and later rounds use three and first place. Finished and unfinished opponents follow engine-evaluated live-rank ordering, and only active opponents affect rank and cutoff time.
   - An active event pins `CatalogVersion` and `BalanceRevision`; restore rejects unknown or cross-field-invalid snapshots instead of silently switching balance.
   - `SaveDisplayedMultiplierStep` changes only the current derived multiplier baseline, while `SaveDisplayedSnapshot` clamps token and elapsed values monotonically to authoritative state after a completed presentation batch.
   - Reward-road claim first stores the transaction ID, claimed target, and reward snapshot; then checks and calls `GrantOnce`, verifies the receipt, advances claimed progress, clears pending data, and saves finalized state.
   - Transaction IDs include stable content and event-instance identity, and `Restore` recovers stored pending rewards before normal timeout or race restoration without rebuilding from a changed catalog.
   - An unavailable reward adapter blocks claim entry before pending state is written, and an empty schedule policy performs the documented event-owned cleanup.
6. Inspect dependencies, asmdefs, and `com.actionfit.icecream-race.Editor.Tests` coverage for catalog parity, all elimination rounds, live curves, deadlines, serializer compatibility, flush boundaries, displayed snapshot non-regression, reward failures, and restart recovery. Report missing evidence without executing tests.
7. Capture the same Git status command again and compare it with the baseline. If state changed during the audit, report the paths and do not claim a no-change result.
8. Return findings grouped as passed contracts, risks, missing evidence, and recommended validation.
