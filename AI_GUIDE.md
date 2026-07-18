# AI Guide - ActionFit Ice Cream Race

This guide is shipped with the package so an AI assistant can preserve the race and reward-safety contracts in consuming projects.

## Package Identity

- Package ID: `com.actionfit.icecream-race`
- Display name: ActionFit Ice Cream Race
- Repository: `https://github.com/ActionFit-Editor/IceCreamRace.git`
- Repository visibility: Public
- Current package version at generation time: `0.1.10`
- Unity version: `6000.2`
- Runtime dependency: `com.actionfit.content-core@0.2.2`

## Purpose

The package provides a project-neutral five-player elimination race based on AF_CatDetective snapshot `MCD-1000/5P_PVP@676e6b96dce415977f21121db2ace8c4aaee7fb1`. It owns deterministic race state transitions, live bot-curve ranking, CatDetective parity tuning, schema-versioned persistence, reward-road claiming, and idempotent restart recovery.

It does not own project event buses, UI, Addressables, Firebase, server matchmaking, game inventory mutation, analytics, localization, or migration of existing Cat Merge Cafe keys.

## Agent Skills

- `Skills~/manifest.json` registers schema v2 `icecream-race-help` and `icecream-race-audit` for Codex and Claude with read-only access.
- Help reads the generated `PACKAGE_SKILLS.md` first and explains engine ownership, integration boundaries, tests, and safety rules without executing gameplay.
- Audit inspects source and adapters for transition flushes, elimination and rank rules, catalog pins, displayed snapshot monotonicity, durable reward ordering, and restart recovery. It captures Git state before and after and never runs Unity, race commands, persistence, or rewards.
- Custom Package Manager owns installation and generated inventory. Do not author `PACKAGE_SKILLS.md` inside this package.

## Project Router Registration

This package should be listed in `Packages/com.actionfit.custompackagemanager/PACKAGE_AI_GUIDE_ROUTER.md`.

Requested router entry:

- `Packages/com.actionfit.icecream-race/AI_GUIDE.md` - ActionFit Ice Cream Race owns the reusable five-player elimination race, CatDetective parity catalog, durable state, and idempotent reward recovery.

## Runtime Architecture

- `IceCreamRaceEngine` is sealed so rank resolution, round transition, persistence ordering, and reward recovery cannot be overridden.
- `TryStartEvent` starts only the current schedule window; it does not create opponents or start the race timer. Project entry facades may use it before showing their matchmaking UI.
- `IceCreamRaceCatalog.CreateCatDetectiveParity()` owns the source commit's Monday/Tuesday schedule, four round rows, order and merge tuning, twenty reward-road milestones, and four monotonic progress curves.
- `IIceCreamRaceSchedulePolicy` replaces active days without copying or intersecting the balance catalog; an empty policy is the explicit kill switch.
- `IIceCreamRaceCatalogResolver` must resolve the recorded catalog version/revision for an in-progress event. Unknown snapshots fail explicitly instead of silently switching balance.
- `IIceCreamRaceClock`, `IIceCreamRaceRandom`, and `IIceCreamRaceOpponentProvider` are the supported replacement boundaries.
- `SystemIceCreamRaceClock`, `SystemIceCreamRaceRandom`, and `DefaultIceCreamRaceOpponentProvider` provide a runnable local default.
- `IceCreamRaceStateSerializer` serializes the schema-versioned Unity JSON state and rejects unknown future schema versions.
- `IContentStateStore` and `IContentRewardService` come from `com.actionfit.content-core`.
- Ordinary token progress may remain buffered, but event/race start, result resolution, timeout/end, result claim, and both reward transaction boundaries flush an `IFlushableContentStateStore` when available.
- `SaveDisplayedMultiplierStep()` acknowledges only the current round-derived `0..3` multiplier presentation step. It does not change token or elapsed-time display baselines and uses the ordinary buffered persistence path.
- `SaveDisplayedSnapshot(displayedTokens, displayedElapsedSeconds)` acknowledges only a fully completed presentation batch. Both values are monotonic, are clamped to the current authoritative race state, and leave the multiplier baseline unchanged. Interrupted or disabled presenters must not call it for their unfinished target.
- `DevForceWin()` and `DevForceLose()` are explicit developer commands on the sealed state owner. They accept only an active race with no pending result, persist and flush through the normal result path, and return `false` without writing for invalid or duplicate calls.

## Invariants

- Round 1 has five players and a top-three cutoff; round 2 has four players and a top-two cutoff; round 3 and later have three players and a first-place cutoff.
- Only active opponents contribute to live rank and the cutoff deadline.
- A finished opponent is always ahead. An unfinished opponent is ahead only when its curve-evaluated progress ratio is greater than the player's ratio.
- Result claim applies the current round's `1/2/4/10` multiplier before advancing the round. A failed cutoff resets to round 1.
- A forced win fills authoritative tokens to `RequiredTokens` and resolves rank 1. A forced loss preserves authoritative tokens and resolves `RankCutoff + 1`, clamped to `ParticipantCount`; neither command claims the result or grants road rewards.
- Claimed reward-road progress is monotonic within an event.
- An active race must belong to a started event with a positive end time and a pinned catalog pair. Reject malformed cross-field snapshots instead of restoring a permanently stalled race.
- An empty schedule policy clears all event-owned progress and catalog pins, including orphan imports whose `EventStarted` flag is already false.
- Previously displayed token and elapsed-time baselines never regress and never advance beyond authoritative progress. A presentation must acknowledge one common elapsed snapshot only after every participant lane in that batch has settled.

## Durable Reward Contract

Preserve this order:

1. Save the pending transaction ID, claimed target, and reward snapshot.
2. Check `HasGranted`; call `GrantOnce` only when required.
3. Verify `HasGranted` after the grant call.
4. Advance claimed progress and clear pending transaction data.
5. Save the finalized state.

Reward transaction IDs include the stable content ID and event-instance identity before the
per-claim suffix: `<contentId>/event/<eventEndUtcTicks>/road/<claimId>`. Do not remove the event
identity or replace it with a session-only counter.

`Restore` must recover a pending transaction before normal timeout/race restoration. Never rebuild pending rewards from a changed catalog; use the stored snapshot. Never clear pending transaction data during `EndEvent` before recovery succeeds.

Check `IsRewardServiceAvailable` before exposing a claim action. With no claimable milestone, `ClaimRoadRewards` returns `None`; with claimable rewards and an unavailable service, it fails before writing pending state.

## Integration Rules

- Route project order/merge events through a project adapter and call `AddTokens` with the calculated amount.
- Subscribe presentation code to `StateChanged`; do not add project UI types to the runtime assembly.
- Route developer controls through the engine commands and let the normal `StateChanged`, pending-result presentation, and `ClaimResult` flow continue. Do not edit serialized state JSON from a project DevTool.
- Replace local PlayerPrefs implementations with project adapters by constructor injection, without changing engine logic.
- Keep animation hooks, prefab selection, sound, localization, and analytics in a presentation or project package.
- Do not copy CatDetective image, audio, Spine, or other licensed assets into this public package without a separate redistribution review.

## Testing

Run `com.actionfit.icecream-race.Editor.Tests`. Keep deterministic fake clocks, random values, opponents, state stores, and reward services. Cover catalog parity, all elimination rounds, curve ranking, deadline resolution, developer force-result guards and durable restore, multiplier points, completed-presentation snapshot clamping and non-regression, claimed-road bounds, serializer compatibility, and crashes both before and after reward mutation.

## Package Tools Menu

- Unity menu root: `Tools/Package/ActionFit Ice Cream Race/`.
- `README` opens the installed package README.
- This package has no settings ScriptableObject and therefore exposes no `Setting SO` menu.

## Release Notes

- Publishing is manual through Custom Package Manager.
- Remote tags are immutable; check them before reusing a version.
- Update `package.json`, this guide, README, tests, and `Editor/PackageInfo/ActionFitPackageInfo_SO.asset` together when behavior changes.
