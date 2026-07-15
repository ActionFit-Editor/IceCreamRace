using System;
using System.Collections.Generic;
using ActionFit.Content;
using NUnit.Framework;

namespace ActionFit.IceCreamRace.Tests
{
    public sealed class IceCreamRaceEngineTests
    {
        private static readonly DateTime MondayUtc =
            new DateTime(2026, 7, 13, 0, 0, 0, DateTimeKind.Utc);

        [Test]
        public void CatDetectiveParityCatalog_MatchesSourceTablesAndCurves()
        {
            IceCreamRaceCatalog catalog = IceCreamRaceCatalog.CreateCatDetectiveParity();

            Assert.That(catalog.CatalogVersion, Is.EqualTo("catdetective-676e6b96"));
            Assert.That(
                IceCreamRaceCatalog.CatDetectiveSourceCommit,
                Is.EqualTo("676e6b96dce415977f21121db2ace8c4aaee7fb1"));
            Assert.That(catalog.BalanceRevision, Is.EqualTo("pvp5-balance-1"));
            Assert.That(catalog.ActiveDays, Is.EquivalentTo(new[] { DayOfWeek.Monday, DayOfWeek.Tuesday }));
            Assert.That(catalog.Rounds.Count, Is.EqualTo(4));
            Assert.That(catalog.GetRound(1).RequiredTokens, Is.EqualTo(100));
            Assert.That(catalog.GetRound(4).RequiredTokens, Is.EqualTo(600));
            Assert.That(catalog.GetRound(99).Round, Is.EqualTo(4));
            Assert.That(catalog.GetOrderTokens(3), Is.EqualTo(4));
            Assert.That(catalog.GetOrderTokens(12), Is.EqualTo(13));
            Assert.That(catalog.MergeTuning.MinimumLevel, Is.EqualTo(4));
            Assert.That(catalog.MergeTuning.Chance, Is.EqualTo(0.15f));
            Assert.That(catalog.RoadMilestones.Count, Is.EqualTo(20));
            Assert.That(catalog.FinalRoadPoints, Is.EqualTo(500));
            Assert.That(catalog.RoadMilestones[19].Rewards.Count, Is.EqualTo(3));
            Assert.That(catalog.BotProgressCurves.Count, Is.EqualTo(4));
            Assert.That(catalog.BotProgressCurves[0].Evaluate(0.2f), Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(catalog.BotProgressCurves[1].Evaluate(0.8f), Is.EqualTo(0.45f).Within(0.0001f));
            Assert.That(catalog.BotProgressCurves[3].Evaluate(0.6f), Is.EqualTo(0.68f).Within(0.0001f));
            Assert.That(catalog.BotProgressCurves[0].keys[1].inTangent, Is.EqualTo(3f).Within(0.0001f));
        }

        [Test]
        public void WinningRounds_UsesFiveFourThreePlayersAndOneTwoFourMultipliers()
        {
            var context = CreateContext();
            IceCreamRaceEngine engine = context.Engine;

            Assert.That(engine.ParticipantCount, Is.EqualTo(5));
            Assert.That(engine.RankCutoff, Is.EqualTo(3));
            WinCurrentRound(engine);
            IceCreamRaceResultClaim first = engine.ClaimResult();
            Assert.That(first.RoadPointsAdded, Is.EqualTo(30));
            Assert.That(engine.Round, Is.EqualTo(2));

            Assert.That(engine.ParticipantCount, Is.EqualTo(4));
            Assert.That(engine.RankCutoff, Is.EqualTo(2));
            Assert.That(engine.CurrentMultiplier, Is.EqualTo(2));
            WinCurrentRound(engine);
            IceCreamRaceResultClaim second = engine.ClaimResult();
            Assert.That(second.RoadPointsAdded, Is.EqualTo(80));
            Assert.That(engine.Round, Is.EqualTo(3));

            Assert.That(engine.ParticipantCount, Is.EqualTo(3));
            Assert.That(engine.RankCutoff, Is.EqualTo(1));
            Assert.That(engine.CurrentMultiplier, Is.EqualTo(4));
            WinCurrentRound(engine);
            IceCreamRaceResultClaim third = engine.ClaimResult();
            Assert.That(third.RoadPointsAdded, Is.EqualTo(200));
            Assert.That(engine.Round, Is.EqualTo(4));
            Assert.That(engine.ParticipantCount, Is.EqualTo(3));
            Assert.That(engine.RankCutoff, Is.EqualTo(1));
            Assert.That(engine.CurrentMultiplier, Is.EqualTo(10));
            Assert.That(engine.RoadPoints, Is.EqualTo(310));
        }

        [Test]
        public void TryStartEvent_StartsEntryWindowWithoutStartingRace()
        {
            var context = CreateContext();

            Assert.That(context.Engine.TryStartEvent(), Is.True);
            Assert.That(context.Engine.IsEventStarted, Is.True);
            Assert.That(context.Engine.IsEventActive, Is.True);
            Assert.That(context.Engine.IsMatchmade, Is.False);
            Assert.That(context.Engine.IsRaceActive, Is.False);
            Assert.That(context.Engine.TryStartEvent(), Is.True);

            context.Engine.EndEvent();

            Assert.That(context.Engine.IsEventStarted, Is.False);
            Assert.That(context.Engine.IsEventCompleted, Is.True);
            Assert.That(context.Engine.TryStartEvent(), Is.False);
        }

        [Test]
        public void EvaluateTimeout_UsesCutoffBotDeadlineAndLiveRank()
        {
            var context = CreateContext();
            IceCreamRaceEngine engine = context.Engine;
            Assert.That(engine.Matchmake(), Is.True);
            Assert.That(engine.StartRace(), Is.True);
            Assert.That(engine.RaceId, Is.Not.Empty);
            Assert.That(engine.ActiveCatalogVersion, Is.EqualTo("catdetective-676e6b96"));
            Assert.That(engine.ActiveBalanceRevision, Is.EqualTo("pvp5-balance-1"));
            Assert.That(engine.RaceDeadlineSeconds, Is.EqualTo(300f));

            context.Clock.Advance(TimeSpan.FromSeconds(301));

            Assert.That(engine.CurrentRank, Is.EqualTo(5));
            Assert.That(engine.EvaluateTimeout(), Is.True);
            Assert.That(engine.PendingRank, Is.EqualTo(5));
            IceCreamRaceResultClaim result = engine.ClaimResult();
            Assert.That(result.Advanced, Is.False);
            Assert.That(engine.Round, Is.EqualTo(1));
        }

        [Test]
        public void ClaimRoadRewards_WhenFinalStateSaveFails_RestoreDoesNotGrantTwice()
        {
            var context = CreateContext();
            WinCurrentRound(context.Engine);
            context.Engine.ClaimResult();
            context.Store.FailOnSaveNumber = context.Store.SaveCount + 2;

            Assert.Throws<InvalidOperationException>(() => context.Engine.ClaimRoadRewards());
            Assert.That(context.Rewards.GrantCalls, Is.EqualTo(1));

            context.Store.FailOnSaveNumber = 0;
            IceCreamRaceEngine restored = context.CreateNewEngine();
            restored.Restore();

            Assert.That(restored.ClaimedRoadPoints, Is.EqualTo(30));
            Assert.That(restored.HasPendingRewardTransaction, Is.False);
            Assert.That(context.Rewards.GrantCalls, Is.EqualTo(1));
            Assert.That(restored.ClaimRoadRewards().Succeeded, Is.False);
        }

        [Test]
        public void ClaimRoadRewards_WhenGrantThrows_RestoreReplaysStoredSnapshotOnce()
        {
            var context = CreateContext();
            WinCurrentRound(context.Engine);
            context.Engine.ClaimResult();
            context.Rewards.ThrowOnGrant = true;

            Assert.Throws<InvalidOperationException>(() => context.Engine.ClaimRoadRewards());
            Assert.That(context.Store.Json, Does.Contain("_pendingRewardTransactionId"));

            context.Rewards.ThrowOnGrant = false;
            IceCreamRaceEngine restored = context.CreateNewEngine();
            restored.Restore();

            Assert.That(context.Rewards.GrantCalls, Is.EqualTo(1));
            Assert.That(context.Rewards.GetBalance("Energy"), Is.EqualTo(30));
            Assert.That(restored.ClaimedRoadPoints, Is.EqualTo(30));
        }

        [Test]
        public void ClaimRoadRewards_TransactionIdContainsEventInstanceTicks()
        {
            var context = CreateContext();
            WinCurrentRound(context.Engine);
            context.Engine.ClaimResult();
            long eventInstanceTicks = context.Engine.State.EventEndUtcTicks;

            IceCreamRaceRoadClaimResult result = context.Engine.ClaimRoadRewards();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(
                result.TransactionId,
                Does.Contain($"/event/{eventInstanceTicks}/road/"));
        }

        [Test]
        public void Serializer_NormalizesOlderSchemaAndRejectsFutureSchema()
        {
            var serializer = new IceCreamRaceStateSerializer();

            IceCreamRaceState legacy = serializer.Deserialize(
                "{\"_schemaVersion\":0,\"_round\":2,\"_roadPoints\":12," +
                "\"_claimedRoadPoints\":99,\"_catalogVersion\":\"cat\"," +
                "\"_balanceRevision\":\"balance\"}");

            Assert.That(legacy.SchemaVersion, Is.EqualTo(1));
            Assert.That(legacy.Round, Is.EqualTo(2));
            Assert.That(legacy.ClaimedRoadPoints, Is.EqualTo(12));
            Assert.Throws<FormatException>(() => serializer.Deserialize("{}"));
            Assert.Throws<NotSupportedException>(() =>
                serializer.Deserialize("{\"_schemaVersion\":99}"));
        }

        [Test]
        public void Restore_AfterDeadlineResolvesPendingResult()
        {
            var context = CreateContext();
            context.Engine.Matchmake();
            context.Engine.StartRace();
            context.Clock.Advance(TimeSpan.FromSeconds(301));

            IceCreamRaceEngine restored = context.CreateNewEngine();
            restored.Restore();

            Assert.That(restored.PendingRank, Is.EqualTo(5));
        }

        [Test]
        public void ImportStateIfEmpty_ImportsFlagsAndRefusesExistingStore()
        {
            var store = new MemoryStateStore();
            var rewards = new MemoryRewardService();
            var clock = new ManualClock(MondayUtc);
            var engine = new IceCreamRaceEngine(
                store,
                rewards,
                IceCreamRaceCatalog.CreateCatDetectiveParity(),
                clock,
                new MinimumRandom(),
                new FakeOpponentProvider());
            var import = new IceCreamRaceImportState(
                2,
                10,
                8,
                1,
                20f,
                0,
                Array.Empty<IceCreamRaceOpponent>(),
                Array.Empty<int>(),
                Array.Empty<int>(),
                0,
                60,
                30,
                true,
                false,
                MondayUtc.AddDays(2).Ticks,
                0,
                true,
                true);

            Assert.That(engine.ImportStateIfEmpty(import), Is.True);
            Assert.That(engine.Round, Is.EqualTo(2));
            Assert.That(engine.FirstPopupShown, Is.True);
            Assert.That(engine.TutorialShown, Is.True);
            Assert.That(engine.ImportStateIfEmpty(import), Is.False);

            IceCreamRaceState persisted = engine.Restore();
            Assert.That(persisted.RoadPoints, Is.EqualTo(60));
            Assert.That(persisted.ClaimedRoadPoints, Is.EqualTo(30));
        }

        [Test]
        public void EmptySchedulePolicy_DisablesCatDetectiveDefaultDays()
        {
            var store = new MemoryStateStore();
            var engine = new IceCreamRaceEngine(
                store,
                new MemoryRewardService(),
                IceCreamRaceCatalog.CreateCatDetectiveParity(),
                new ManualClock(MondayUtc),
                new MinimumRandom(),
                new FakeOpponentProvider(),
                schedulePolicy: new FixedIceCreamRaceSchedulePolicy(Array.Empty<DayOfWeek>()));

            engine.Restore();

            Assert.That(engine.TryStartEvent(), Is.False);
            Assert.That(engine.Matchmake(), Is.False);
            Assert.That(engine.IsEventActive, Is.False);
        }

        [Test]
        public void EmptySchedulePolicy_ClearsPersistedOrphanEventProgress()
        {
            var store = new MemoryStateStore();
            var rewards = new MemoryRewardService();
            var clock = new ManualClock(MondayUtc);
            var disabledSchedule = new FixedIceCreamRaceSchedulePolicy(Array.Empty<DayOfWeek>());
            var importingEngine = new IceCreamRaceEngine(
                store,
                rewards,
                IceCreamRaceCatalog.CreateCatDetectiveParity(),
                clock,
                new MinimumRandom(),
                new FakeOpponentProvider(),
                schedulePolicy: disabledSchedule);
            var import = new IceCreamRaceImportState(
                2,
                0,
                0,
                1,
                0f,
                0,
                Array.Empty<IceCreamRaceOpponent>(),
                Array.Empty<int>(),
                Array.Empty<int>(),
                0,
                60,
                30,
                false,
                false,
                0,
                0,
                true,
                true);

            Assert.That(importingEngine.ImportStateIfEmpty(import), Is.True);
            Assert.That(importingEngine.RoadPoints, Is.EqualTo(60));

            var restored = new IceCreamRaceEngine(
                store,
                rewards,
                IceCreamRaceCatalog.CreateCatDetectiveParity(),
                clock,
                new MinimumRandom(),
                new FakeOpponentProvider(),
                schedulePolicy: disabledSchedule);
            restored.Restore();

            Assert.That(restored.Round, Is.EqualTo(1));
            Assert.That(restored.RoadPoints, Is.Zero);
            Assert.That(restored.ClaimedRoadPoints, Is.Zero);
            Assert.That(restored.FirstPopupShown, Is.False);
            Assert.That(restored.ActiveCatalogVersion, Is.Empty);
        }

        [Test]
        public void ProjectSchedulePolicy_ReplacesCatalogDaysInsteadOfIntersectingThem()
        {
            var clock = new ManualClock(MondayUtc.AddDays(2));
            var engine = new IceCreamRaceEngine(
                new MemoryStateStore(),
                new MemoryRewardService(),
                IceCreamRaceCatalog.CreateCatDetectiveParity(),
                clock,
                new MinimumRandom(),
                new FakeOpponentProvider(),
                schedulePolicy: new FixedIceCreamRaceSchedulePolicy(
                    new[] { DayOfWeek.Wednesday, DayOfWeek.Thursday }));

            engine.Restore();

            Assert.That(engine.TryStartEvent(), Is.True);
            Assert.That(engine.Matchmake(), Is.True);
            Assert.That(engine.StartRace(), Is.True);
            Assert.That(engine.State.EventEndUtcTicks, Is.EqualTo(MondayUtc.AddDays(4).Ticks));
        }

        [Test]
        public void AddTokens_AfterEntryWindowEnd_DoesNotAccrue()
        {
            var context = CreateContext();
            context.Clock.Advance(
                TimeSpan.FromDays(1)
                + TimeSpan.FromHours(23)
                + TimeSpan.FromMinutes(59)
                + TimeSpan.FromSeconds(59));
            Assert.That(context.Engine.Matchmake(), Is.True);
            Assert.That(context.Engine.StartRace(), Is.True);

            context.Clock.Advance(TimeSpan.FromSeconds(2));

            Assert.That(context.Engine.IsEventActive, Is.False);
            Assert.That(context.Engine.AddTokens(10), Is.False);
            Assert.That(context.Engine.CollectedTokens, Is.Zero);
        }

        [Test]
        public void Restore_UnknownPinnedCatalogFailsInsteadOfChangingActiveRaceBalance()
        {
            var context = CreateContext();
            Assert.That(context.Engine.Matchmake(), Is.True);
            Assert.That(context.Engine.StartRace(), Is.True);
            context.Store.ReplaceJson(
                context.Store.Json.Replace(
                    "catdetective-676e6b96",
                    "catdetective-unknown"));

            IceCreamRaceEngine restored = context.CreateNewEngine();

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() => restored.Restore());
            Assert.That(exception.Message, Does.Contain("No Ice Cream Race catalog"));
        }

        [Test]
        public void Restore_UsesPinnedCatalogUntilEventEndsThenPinsNewDefault()
        {
            IceCreamRaceCatalog oldCatalog = CreateVariantCatalog("old-catalog", 111);
            IceCreamRaceCatalog newCatalog = CreateVariantCatalog("new-catalog", 999);
            var store = new MemoryStateStore();
            var rewards = new MemoryRewardService();
            var clock = new ManualClock(MondayUtc);
            var oldEngine = new IceCreamRaceEngine(
                store,
                rewards,
                oldCatalog,
                clock,
                new MinimumRandom(),
                new FakeOpponentProvider());
            oldEngine.Restore();
            Assert.That(oldEngine.Matchmake(), Is.True);
            Assert.That(oldEngine.StartRace(), Is.True);
            Assert.That(oldEngine.RequiredTokens, Is.EqualTo(111));

            var restored = new IceCreamRaceEngine(
                store,
                rewards,
                newCatalog,
                clock,
                new MinimumRandom(),
                new FakeOpponentProvider(),
                catalogResolver: new IceCreamRaceCatalogRegistry(oldCatalog, newCatalog));
            restored.Restore();

            Assert.That(restored.ActiveCatalogVersion, Is.EqualTo("old-catalog"));
            Assert.That(restored.RequiredTokens, Is.EqualTo(111));

            restored.EndEvent();
            clock.Advance(TimeSpan.FromDays(7));

            Assert.That(restored.TryStartEvent(), Is.True);
            Assert.That(restored.ActiveCatalogVersion, Is.EqualTo("new-catalog"));
            Assert.That(restored.RequiredTokens, Is.EqualTo(999));
        }

        [Test]
        public void Serializer_RejectsIncompleteActiveAndPendingRewardSnapshots()
        {
            var serializer = new IceCreamRaceStateSerializer();

            Assert.Throws<FormatException>(() => serializer.Deserialize(
                "{\"_schemaVersion\":1,\"_eventStarted\":true," +
                "\"_catalogVersion\":\"cat\",\"_balanceRevision\":\"balance\"," +
                "\"_matchStartUtcTicks\":1,\"_raceId\":\"race\"}"));
            Assert.Throws<FormatException>(() => serializer.Deserialize(
                "{\"_schemaVersion\":1,\"_roadPoints\":30," +
                "\"_catalogVersion\":\"cat\",\"_balanceRevision\":\"balance\"," +
                "\"_pendingRewardTransactionId\":\"tx\"," +
                "\"_pendingRewardClaimedRoadPoints\":30,\"_pendingRewards\":[]}"));
            Assert.Throws<FormatException>(() => serializer.Deserialize(
                "{\"_schemaVersion\":1,\"_roadPoints\":30," +
                "\"_catalogVersion\":\"cat\",\"_balanceRevision\":\"balance\"," +
                "\"_pendingRewardTransactionId\":\"   \"," +
                "\"_pendingRewardClaimedRoadPoints\":30," +
                "\"_pendingRewards\":[{\"_rewardId\":\"coin\",\"_amount\":1}]}"));

            TestContext context = CreateContext();
            Assert.That(context.Engine.Matchmake(), Is.True);
            Assert.That(context.Engine.StartRace(), Is.True);
            string activeJson = context.Store.Json;
            long eventEndUtcTicks = context.Engine.State.EventEndUtcTicks;

            Assert.Throws<FormatException>(() => serializer.Deserialize(
                activeJson.Replace("\"_eventStarted\":true", "\"_eventStarted\":false")));
            Assert.Throws<FormatException>(() => serializer.Deserialize(
                activeJson.Replace(
                    $"\"_eventEndUtcTicks\":{eventEndUtcTicks}",
                    "\"_eventEndUtcTicks\":0")));

            IReadOnlyList<IceCreamRaceOpponent> opponents =
                new FakeOpponentProvider().CreateOpponents(4, new MinimumRandom());
            Assert.Throws<ArgumentException>(() => new IceCreamRaceImportState(
                1,
                0,
                0,
                0,
                0f,
                MondayUtc.Ticks,
                opponents,
                new[] { 300, 300, 300, 300 },
                new[] { 0, 1, 2, 3 },
                0,
                0,
                0,
                false,
                false,
                0,
                0,
                false,
                false));
        }

        [Test]
        public void CatalogCreate_NormalizesUnsafeTangentsToMonotonicSegments()
        {
            IceCreamRaceCatalog source = IceCreamRaceCatalog.CreateCatDetectiveParity();
            var curves = new List<UnityEngine.AnimationCurve>(source.BotProgressCurves);
            curves[0] = new UnityEngine.AnimationCurve(
                new UnityEngine.Keyframe(0f, 0f, 0f, 100f),
                new UnityEngine.Keyframe(1f, 1f, -100f, 0f));
            IceCreamRaceCatalog catalog = IceCreamRaceCatalog.Create(
                "custom",
                "custom-balance",
                new[] { DayOfWeek.Monday },
                new Dictionary<int, int> { { 3, 4 } },
                new List<IceCreamRaceRoundDefinition>(source.Rounds),
                new List<IceCreamRaceRoadMilestone>(source.RoadMilestones),
                curves,
                source.MergeTuning);

            UnityEngine.AnimationCurve normalized = catalog.BotProgressCurves[0];
            float previous = 0f;
            for (int step = 0; step <= 100; step++)
            {
                float current = normalized.Evaluate(step / 100f);
                Assert.That(current, Is.GreaterThanOrEqualTo(previous - 0.0001f));
                Assert.That(current, Is.InRange(0f, 1f));
                previous = current;
            }
        }

        [Test]
        public void CriticalTransitionsFlushButIntermediateTokenProgressUsesBufferedStoreSave()
        {
            var context = CreateContext();
            Assert.That(context.Store.FlushCount, Is.Zero);
            context.Engine.Matchmake();
            Assert.That(context.Store.FlushCount, Is.Zero);
            context.Engine.StartRace();
            Assert.That(context.Store.FlushCount, Is.EqualTo(1));

            context.Engine.AddTokens(1);

            Assert.That(context.Store.FlushCount, Is.EqualTo(1));

            context.Engine.AddTokens(context.Engine.RequiredTokens);

            Assert.That(context.Engine.PendingRank, Is.GreaterThan(0));
            Assert.That(context.Store.FlushCount, Is.EqualTo(2));
        }

        [Test]
        public void UnavailableRewardService_FailsBeforeWritingPendingTransaction()
        {
            var store = new MemoryStateStore();
            var engine = new IceCreamRaceEngine(
                store,
                new UnavailableRewardService(),
                IceCreamRaceCatalog.CreateCatDetectiveParity(),
                new ManualClock(MondayUtc),
                new MinimumRandom(),
                new FakeOpponentProvider());
            engine.Restore();
            WinCurrentRound(engine);
            engine.ClaimResult();

            Assert.Throws<NotSupportedException>(() => engine.ClaimRoadRewards());
            Assert.That(engine.HasPendingRewardTransaction, Is.False);
            Assert.That(store.Json, Does.Contain("\"_pendingRewardTransactionId\":\"\""));
        }

        [Test]
        public void UnavailableRewardService_ReturnsNoneWhenNothingIsClaimable()
        {
            var engine = new IceCreamRaceEngine(
                new MemoryStateStore(),
                new UnavailableRewardService(),
                IceCreamRaceCatalog.CreateCatDetectiveParity(),
                new ManualClock(MondayUtc),
                new MinimumRandom(),
                new FakeOpponentProvider());
            engine.Restore();

            IceCreamRaceRoadClaimResult result = engine.ClaimRoadRewards();

            Assert.That(result.Succeeded, Is.False);
        }

        private static void WinCurrentRound(IceCreamRaceEngine engine)
        {
            Assert.That(engine.Matchmake(), Is.True);
            Assert.That(engine.StartRace(), Is.True);
            Assert.That(engine.AddTokens(engine.RequiredTokens), Is.True);
            Assert.That(engine.PendingRank, Is.EqualTo(1));
        }

        private static IceCreamRaceCatalog CreateVariantCatalog(string version, int firstRoundTokens)
        {
            IceCreamRaceCatalog source = IceCreamRaceCatalog.CreateCatDetectiveParity();
            var rounds = new List<IceCreamRaceRoundDefinition>(source.Rounds.Count);
            for (int index = 0; index < source.Rounds.Count; index++)
            {
                IceCreamRaceRoundDefinition round = source.Rounds[index];
                rounds.Add(new IceCreamRaceRoundDefinition(
                    round.Round,
                    round.Round == 1 ? firstRoundTokens : round.RequiredTokens,
                    CopyIntegers(round.BotMinimumSeconds),
                    CopyIntegers(round.BotMaximumSeconds),
                    round.RewardRoadPoints));
            }

            return IceCreamRaceCatalog.Create(
                version,
                version + "-balance",
                source.ActiveDays,
                new Dictionary<int, int> { { 3, 4 } },
                rounds,
                new List<IceCreamRaceRoadMilestone>(source.RoadMilestones),
                new List<UnityEngine.AnimationCurve>(source.BotProgressCurves),
                source.MergeTuning);
        }

        private static int[] CopyIntegers(IReadOnlyList<int> source)
        {
            var copy = new int[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                copy[index] = source[index];
            }

            return copy;
        }

        private static TestContext CreateContext()
        {
            var store = new MemoryStateStore();
            var rewards = new MemoryRewardService();
            var clock = new ManualClock(MondayUtc);
            var context = new TestContext(store, rewards, clock);
            context.Engine.Restore();
            return context;
        }

        private sealed class TestContext
        {
            public TestContext(MemoryStateStore store, MemoryRewardService rewards, ManualClock clock)
            {
                Store = store;
                Rewards = rewards;
                Clock = clock;
                Engine = CreateNewEngine();
            }

            public MemoryStateStore Store { get; }
            public MemoryRewardService Rewards { get; }
            public ManualClock Clock { get; }
            public IceCreamRaceEngine Engine { get; }

            public IceCreamRaceEngine CreateNewEngine()
            {
                return new IceCreamRaceEngine(
                    Store,
                    Rewards,
                    IceCreamRaceCatalog.CreateCatDetectiveParity(),
                    Clock,
                    new MinimumRandom(),
                    new FakeOpponentProvider());
            }
        }

        private sealed class ManualClock : IIceCreamRaceClock
        {
            public ManualClock(DateTime utcNow)
            {
                UtcNow = utcNow;
            }

            public DateTime UtcNow { get; private set; }

            public void Advance(TimeSpan duration)
            {
                UtcNow = UtcNow.Add(duration);
            }
        }

        private sealed class MinimumRandom : IIceCreamRaceRandom
        {
            public int Range(int minInclusive, int maxExclusive)
            {
                return minInclusive;
            }
        }

        private sealed class FakeOpponentProvider : IIceCreamRaceOpponentProvider
        {
            public IReadOnlyList<IceCreamRaceOpponent> CreateOpponents(int count, IIceCreamRaceRandom random)
            {
                var opponents = new List<IceCreamRaceOpponent>(count);
                for (int index = 0; index < count; index++)
                {
                    opponents.Add(new IceCreamRaceOpponent(
                        $"bot-{index}",
                        $"Bot {index}",
                        $"profile-{index}",
                        "basic"));
                }

                return opponents;
            }
        }

        private sealed class MemoryStateStore : IContentStateStore, IFlushableContentStateStore
        {
            public string Json { get; private set; }
            public int SaveCount { get; private set; }
            public int FlushCount { get; private set; }
            public int FailOnSaveNumber { get; set; }

            public bool TryLoad(string contentId, out string json)
            {
                json = Json;
                return json != null;
            }

            public void Save(string contentId, string json)
            {
                SaveCount++;
                if (FailOnSaveNumber > 0 && SaveCount == FailOnSaveNumber)
                {
                    throw new InvalidOperationException("Simulated state save failure.");
                }

                Json = json;
            }

            public void Delete(string contentId)
            {
                Json = null;
            }

            public void Flush()
            {
                FlushCount++;
            }

            public void ReplaceJson(string json)
            {
                Json = json;
            }
        }

        private sealed class MemoryRewardService : IContentRewardService
        {
            private readonly HashSet<string> _granted = new HashSet<string>(StringComparer.Ordinal);
            private readonly Dictionary<string, long> _balances = new Dictionary<string, long>(StringComparer.Ordinal);

            public bool ThrowOnGrant { get; set; }
            public int GrantCalls { get; private set; }
            public bool IsAvailable => true;

            public bool HasGranted(string transactionId)
            {
                return _granted.Contains(transactionId);
            }

            public bool GrantOnce(string transactionId, IReadOnlyList<ContentReward> rewards)
            {
                if (ThrowOnGrant)
                {
                    throw new InvalidOperationException("Simulated reward failure.");
                }

                if (!_granted.Add(transactionId))
                {
                    return false;
                }

                GrantCalls++;
                for (int index = 0; index < rewards.Count; index++)
                {
                    ContentReward reward = rewards[index];
                    _balances.TryGetValue(reward.RewardId, out long balance);
                    _balances[reward.RewardId] = balance + reward.Amount;
                }

                return true;
            }

            public long GetBalance(string rewardId)
            {
                return _balances.TryGetValue(rewardId, out long balance) ? balance : 0;
            }
        }

        private sealed class UnavailableRewardService : IContentRewardService
        {
            public bool IsAvailable => false;

            public bool HasGranted(string transactionId)
            {
                return false;
            }

            public bool GrantOnce(string transactionId, IReadOnlyList<ContentReward> rewards)
            {
                throw new NotSupportedException();
            }
        }
    }
}
