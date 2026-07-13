using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ActionFit.MergeContent;
using UnityEngine;

namespace ActionFit.IceCreamRace
{
    public sealed class IceCreamRaceRoundDefinition
    {
        public IceCreamRaceRoundDefinition(
            int round,
            int requiredTokens,
            int[] botMinimumSeconds,
            int[] botMaximumSeconds,
            int rewardRoadPoints)
        {
            if (round < 1) throw new ArgumentOutOfRangeException(nameof(round));
            if (requiredTokens <= 0) throw new ArgumentOutOfRangeException(nameof(requiredTokens));
            if (botMinimumSeconds == null) throw new ArgumentNullException(nameof(botMinimumSeconds));
            if (botMaximumSeconds == null) throw new ArgumentNullException(nameof(botMaximumSeconds));
            if (botMinimumSeconds.Length != 4 || botMaximumSeconds.Length != 4)
                throw new ArgumentException("Each round requires four bot time ranges.");
            if (rewardRoadPoints <= 0) throw new ArgumentOutOfRangeException(nameof(rewardRoadPoints));
            for (int index = 0; index < 4; index++)
            {
                if (botMinimumSeconds[index] <= 0 || botMaximumSeconds[index] < botMinimumSeconds[index])
                    throw new ArgumentException("Bot time ranges must be positive and ordered.");
            }

            Round = round;
            RequiredTokens = requiredTokens;
            BotMinimumSeconds = Array.AsReadOnly((int[])botMinimumSeconds.Clone());
            BotMaximumSeconds = Array.AsReadOnly((int[])botMaximumSeconds.Clone());
            RewardRoadPoints = rewardRoadPoints;
        }

        public int Round { get; }
        public int RequiredTokens { get; }
        public IReadOnlyList<int> BotMinimumSeconds { get; }
        public IReadOnlyList<int> BotMaximumSeconds { get; }
        public int RewardRoadPoints { get; }
    }

    public sealed class IceCreamRaceRoadMilestone
    {
        public IceCreamRaceRoadMilestone(
            int milestone,
            int requiredPoints,
            IReadOnlyList<ContentReward> rewards,
            bool isUltimate)
        {
            if (milestone < 1) throw new ArgumentOutOfRangeException(nameof(milestone));
            if (requiredPoints <= 0) throw new ArgumentOutOfRangeException(nameof(requiredPoints));
            if (rewards == null || rewards.Count == 0)
                throw new ArgumentException("A road milestone requires at least one reward.", nameof(rewards));
            var rewardCopy = new List<ContentReward>(rewards.Count);
            for (int index = 0; index < rewards.Count; index++)
            {
                ContentReward reward = rewards[index]
                    ?? throw new ArgumentException("Milestone rewards must not contain null.", nameof(rewards));
                rewardCopy.Add(new ContentReward(reward.RewardId, reward.Amount));
            }

            Milestone = milestone;
            RequiredPoints = requiredPoints;
            Rewards = new ReadOnlyCollection<ContentReward>(rewardCopy);
            IsUltimate = isUltimate;
        }

        public int Milestone { get; }
        public int RequiredPoints { get; }
        public IReadOnlyList<ContentReward> Rewards { get; }
        public bool IsUltimate { get; }
    }

    public sealed class IceCreamRaceMergeTuning
    {
        public IceCreamRaceMergeTuning(int minimumLevel, float chance, int tokenBonus)
        {
            if (minimumLevel < 1) throw new ArgumentOutOfRangeException(nameof(minimumLevel));
            if (chance < 0f || chance > 1f) throw new ArgumentOutOfRangeException(nameof(chance));
            if (tokenBonus < 0) throw new ArgumentOutOfRangeException(nameof(tokenBonus));
            MinimumLevel = minimumLevel;
            Chance = chance;
            TokenBonus = tokenBonus;
        }

        public int MinimumLevel { get; }
        public float Chance { get; }
        public int TokenBonus { get; }
    }

    /// <summary>Immutable active-day policy used by standalone package compositions.</summary>
    public sealed class FixedIceCreamRaceSchedulePolicy : IIceCreamRaceSchedulePolicy
    {
        private readonly HashSet<DayOfWeek> _activeDays;

        public FixedIceCreamRaceSchedulePolicy(IEnumerable<DayOfWeek> activeDays)
        {
            if (activeDays == null)
            {
                throw new ArgumentNullException(nameof(activeDays));
            }

            _activeDays = new HashSet<DayOfWeek>();
            foreach (DayOfWeek day in activeDays)
            {
                if (!Enum.IsDefined(typeof(DayOfWeek), day))
                {
                    throw new ArgumentOutOfRangeException(nameof(activeDays));
                }

                _activeDays.Add(day);
            }
        }

        public bool IsEnabled => _activeDays.Count > 0;

        public bool IsActiveDay(DayOfWeek dayOfWeek)
        {
            return _activeDays.Contains(dayOfWeek);
        }

        public DateTime GetActiveWindowEndUtc(DateTime utcNow)
        {
            if (utcNow.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException("Schedule time must have DateTimeKind.Utc.", nameof(utcNow));
            }

            if (!IsEnabled || !IsActiveDay(utcNow.DayOfWeek))
            {
                throw new InvalidOperationException("The Ice Cream Race schedule is not active.");
            }

            DateTime date = utcNow.Date;
            for (int dayOffset = 1; dayOffset <= 7; dayOffset++)
            {
                DateTime candidate = date.AddDays(dayOffset);
                if (!IsActiveDay(candidate.DayOfWeek))
                {
                    return DateTime.SpecifyKind(candidate, DateTimeKind.Utc);
                }
            }

            return DateTime.SpecifyKind(date.AddDays(7), DateTimeKind.Utc);
        }
    }

    /// <summary>Immutable lookup for catalogs that may own an in-progress event snapshot.</summary>
    public sealed class IceCreamRaceCatalogRegistry : IIceCreamRaceCatalogResolver
    {
        private readonly Dictionary<string, IceCreamRaceCatalog> _catalogs;

        public IceCreamRaceCatalogRegistry(params IceCreamRaceCatalog[] catalogs)
        {
            if (catalogs == null || catalogs.Length == 0)
            {
                throw new ArgumentException("At least one catalog is required.", nameof(catalogs));
            }

            _catalogs = new Dictionary<string, IceCreamRaceCatalog>(StringComparer.Ordinal);
            for (int index = 0; index < catalogs.Length; index++)
            {
                IceCreamRaceCatalog catalog = catalogs[index]
                    ?? throw new ArgumentException("Catalogs must not contain null.", nameof(catalogs));
                if (!_catalogs.TryAdd(Key(catalog.CatalogVersion, catalog.BalanceRevision), catalog))
                {
                    throw new ArgumentException("Catalog version and balance revision pairs must be unique.", nameof(catalogs));
                }
            }
        }

        public bool TryResolve(
            string catalogVersion,
            string balanceRevision,
            out IceCreamRaceCatalog catalog)
        {
            if (string.IsNullOrWhiteSpace(catalogVersion)
                || string.IsNullOrWhiteSpace(balanceRevision))
            {
                catalog = null;
                return false;
            }

            return _catalogs.TryGetValue(Key(catalogVersion, balanceRevision), out catalog);
        }

        private static string Key(string catalogVersion, string balanceRevision)
        {
            return catalogVersion + "\n" + balanceRevision;
        }
    }

    public sealed class IceCreamRaceCatalog
    {
        public const string CatDetectiveSourceCommit =
            "676e6b96dce415977f21121db2ace8c4aaee7fb1";
        public const string CatDetectiveCatalogVersion = "catdetective-676e6b96";

        private static readonly int[] Multipliers = { 1, 2, 4, 10 };

        private readonly HashSet<DayOfWeek> _activeDays;
        private readonly IReadOnlyCollection<DayOfWeek> _activeDaySnapshot;
        private readonly Dictionary<int, int> _orderTokens;
        private readonly IReadOnlyList<IceCreamRaceRoundDefinition> _rounds;
        private readonly IReadOnlyList<IceCreamRaceRoadMilestone> _roadMilestones;
        private readonly IReadOnlyList<AnimationCurve> _botProgressCurves;

        private IceCreamRaceCatalog(
            string catalogVersion,
            string balanceRevision,
            IEnumerable<DayOfWeek> activeDays,
            IDictionary<int, int> orderTokens,
            IList<IceCreamRaceRoundDefinition> rounds,
            IList<IceCreamRaceRoadMilestone> roadMilestones,
            IList<AnimationCurve> botProgressCurves,
            IceCreamRaceMergeTuning mergeTuning)
        {
            CatalogVersion = catalogVersion;
            BalanceRevision = balanceRevision;
            _activeDays = new HashSet<DayOfWeek>(activeDays);
            var orderedActiveDays = new List<DayOfWeek>(_activeDays);
            orderedActiveDays.Sort();
            _activeDaySnapshot = new ReadOnlyCollection<DayOfWeek>(orderedActiveDays);
            _orderTokens = new Dictionary<int, int>(orderTokens);
            _rounds = new ReadOnlyCollection<IceCreamRaceRoundDefinition>(rounds);
            _roadMilestones = new ReadOnlyCollection<IceCreamRaceRoadMilestone>(roadMilestones);
            _botProgressCurves = new ReadOnlyCollection<AnimationCurve>(botProgressCurves);
            MergeTuning = mergeTuning;
        }

        public static IceCreamRaceCatalog CatDetectiveParity { get; } = CreateCatDetectiveParity();

        public string CatalogVersion { get; }
        public string BalanceRevision { get; }
        public IReadOnlyCollection<DayOfWeek> ActiveDays => _activeDaySnapshot;
        public IReadOnlyList<IceCreamRaceRoundDefinition> Rounds => _rounds;
        public IReadOnlyList<IceCreamRaceRoadMilestone> RoadMilestones => _roadMilestones;
        public IReadOnlyList<AnimationCurve> BotProgressCurves => CloneCurves(_botProgressCurves);
        public IceCreamRaceMergeTuning MergeTuning { get; }
        public int FinalRoadPoints => _roadMilestones[_roadMilestones.Count - 1].RequiredPoints;

        public bool IsActiveDay(DayOfWeek dayOfWeek)
        {
            return _activeDays.Contains(dayOfWeek);
        }

        public IceCreamRaceRoundDefinition GetRound(int round)
        {
            round = Math.Max(1, round);
            for (int index = 0; index < _rounds.Count; index++)
            {
                if (_rounds[index].Round == round)
                {
                    return _rounds[index];
                }
            }

            return _rounds[_rounds.Count - 1];
        }

        public int GetOrderTokens(int itemValue)
        {
            return _orderTokens.TryGetValue(itemValue, out int tokens) ? tokens : 0;
        }

        public int GetMultiplier(int round)
        {
            int step = Math.Max(0, Math.Min(Multipliers.Length - 1, round - 1));
            return Multipliers[step];
        }

        public int GetParticipantCount(int round)
        {
            return Math.Max(3, 6 - Math.Max(1, round));
        }

        public int GetCutoff(int round)
        {
            return Math.Max(1, 4 - Math.Max(1, round));
        }

        public float EvaluateBotProgress(int curveIndex, int finishSeconds, float elapsedSeconds)
        {
            if (finishSeconds <= 0)
            {
                return 1f;
            }

            float normalizedTime = Mathf.Clamp01(elapsedSeconds / finishSeconds);
            curveIndex = Math.Max(0, Math.Min(_botProgressCurves.Count - 1, curveIndex));
            return Mathf.Clamp01(_botProgressCurves[curveIndex].Evaluate(normalizedTime));
        }

        public static IceCreamRaceCatalog Create(
            string catalogVersion,
            string balanceRevision,
            IEnumerable<DayOfWeek> activeDays,
            IDictionary<int, int> orderTokens,
            IList<IceCreamRaceRoundDefinition> rounds,
            IList<IceCreamRaceRoadMilestone> roadMilestones,
            IList<AnimationCurve> botProgressCurves,
            IceCreamRaceMergeTuning mergeTuning)
        {
            if (string.IsNullOrWhiteSpace(catalogVersion))
                throw new ArgumentException("Catalog version must not be empty.", nameof(catalogVersion));
            if (string.IsNullOrWhiteSpace(balanceRevision))
                throw new ArgumentException("Balance revision must not be empty.", nameof(balanceRevision));
            if (activeDays == null) throw new ArgumentNullException(nameof(activeDays));
            if (orderTokens == null) throw new ArgumentNullException(nameof(orderTokens));
            if (rounds == null || rounds.Count == 0)
                throw new ArgumentException("At least one round is required.", nameof(rounds));
            if (roadMilestones == null || roadMilestones.Count == 0)
                throw new ArgumentException("At least one road milestone is required.", nameof(roadMilestones));
            if (botProgressCurves == null || botProgressCurves.Count != 4)
                throw new ArgumentException("Exactly four bot progress curves are required.", nameof(botProgressCurves));
            if (mergeTuning == null) throw new ArgumentNullException(nameof(mergeTuning));

            var dayCopy = new List<DayOfWeek>();
            foreach (DayOfWeek day in activeDays)
            {
                if (!Enum.IsDefined(typeof(DayOfWeek), day))
                    throw new ArgumentOutOfRangeException(nameof(activeDays));
                if (!dayCopy.Contains(day)) dayCopy.Add(day);
            }

            var orderCopy = new Dictionary<int, int>();
            foreach (KeyValuePair<int, int> pair in orderTokens)
            {
                if (pair.Key < 0 || pair.Value <= 0)
                    throw new ArgumentException("Order token entries require non-negative values and positive tokens.", nameof(orderTokens));
                orderCopy.Add(pair.Key, pair.Value);
            }

            var roundCopy = new List<IceCreamRaceRoundDefinition>(rounds.Count);
            int previousRound = 0;
            for (int index = 0; index < rounds.Count; index++)
            {
                IceCreamRaceRoundDefinition round = rounds[index]
                    ?? throw new ArgumentException("Rounds must not contain null.", nameof(rounds));
                if (round.Round <= previousRound)
                    throw new ArgumentException("Rounds must be strictly ordered by round number.", nameof(rounds));
                if (round.Round != previousRound + 1)
                    throw new ArgumentException("Rounds must start at 1 and remain contiguous.", nameof(rounds));
                previousRound = round.Round;
                roundCopy.Add(new IceCreamRaceRoundDefinition(
                    round.Round,
                    round.RequiredTokens,
                    CopyIntegers(round.BotMinimumSeconds),
                    CopyIntegers(round.BotMaximumSeconds),
                    round.RewardRoadPoints));
            }

            var milestoneCopy = new List<IceCreamRaceRoadMilestone>(roadMilestones.Count);
            int previousMilestone = 0;
            int previousPoints = 0;
            bool foundUltimate = false;
            for (int index = 0; index < roadMilestones.Count; index++)
            {
                IceCreamRaceRoadMilestone milestone = roadMilestones[index]
                    ?? throw new ArgumentException("Road milestones must not contain null.", nameof(roadMilestones));
                if (milestone.Milestone <= previousMilestone || milestone.RequiredPoints <= previousPoints)
                    throw new ArgumentException("Road milestones and required points must be strictly increasing.", nameof(roadMilestones));
                if (foundUltimate)
                    throw new ArgumentException("An ultimate milestone must be the final milestone.", nameof(roadMilestones));
                foundUltimate = milestone.IsUltimate;
                previousMilestone = milestone.Milestone;
                previousPoints = milestone.RequiredPoints;
                milestoneCopy.Add(new IceCreamRaceRoadMilestone(
                    milestone.Milestone,
                    milestone.RequiredPoints,
                    milestone.Rewards,
                    milestone.IsUltimate));
            }

            var curveCopy = new List<AnimationCurve>(4);
            for (int index = 0; index < botProgressCurves.Count; index++)
            {
                curveCopy.Add(ValidateAndCloneCurve(botProgressCurves[index], nameof(botProgressCurves)));
            }

            return new IceCreamRaceCatalog(
                catalogVersion,
                balanceRevision,
                dayCopy,
                orderCopy,
                roundCopy,
                milestoneCopy,
                curveCopy,
                new IceCreamRaceMergeTuning(mergeTuning.MinimumLevel, mergeTuning.Chance, mergeTuning.TokenBonus));
        }

        public static IceCreamRaceCatalog CreateCatDetectiveParity()
        {
            var rounds = new List<IceCreamRaceRoundDefinition>
            {
                Round(1, 100, 240, 300, 270, 300, 330, 360, 300, 330, 30),
                Round(2, 180, 300, 360, 300, 420, 330, 420, 300, 390, 40),
                Round(3, 400, 480, 510, 480, 600, 510, 600, 480, 540, 50),
                Round(4, 600, 660, 720, 720, 780, 720, 780, 780, 840, 60)
            };

            var orderTokens = new Dictionary<int, int>
            {
                { 3, 4 }, { 4, 5 }, { 5, 6 }, { 6, 7 }, { 7, 8 },
                { 8, 9 }, { 9, 10 }, { 10, 11 }, { 11, 12 }, { 12, 13 }
            };

            var milestones = new List<IceCreamRaceRoadMilestone>
            {
                Milestone(1, 30, false, Reward("Energy", 30)),
                Milestone(2, 60, false, Reward("Dia", 30)),
                Milestone(3, 80, false, Reward("Energy", 50)),
                Milestone(4, 100, false, Reward("Dia", 50)),
                Milestone(5, 120, false, Reward("Energy", 70)),
                Milestone(6, 140, false, Reward("Dia", 70)),
                Milestone(7, 160, false, Reward("Energy", 90)),
                Milestone(8, 180, false, Reward("Dia", 90)),
                Milestone(9, 200, false, Reward("Energy", 120)),
                Milestone(10, 220, false, Reward("Dia", 120)),
                Milestone(11, 240, false, Reward("Energy", 150)),
                Milestone(12, 260, false, Reward("Dia", 150)),
                Milestone(13, 280, false, Reward("Energy", 300)),
                Milestone(14, 300, false, Reward("Dia", 300)),
                Milestone(15, 320, false, Reward("Energy", 500)),
                Milestone(16, 350, false, Reward("Dia", 500)),
                Milestone(17, 380, false, Reward("Energy", 3000)),
                Milestone(18, 400, false, Reward("Dia", 3000)),
                Milestone(19, 450, false, Reward("Energy", 5000)),
                Milestone(
                    20,
                    500,
                    true,
                    Reward("Dia", 5000),
                    Reward("Energy", 3000),
                    Reward("BoardItem/30_1", 5))
            };

            var curves = new List<AnimationCurve>
            {
                LinearCurve(new Vector2(0f, 0f), new Vector2(0.2f, 0.6f), new Vector2(0.5f, 0.85f), new Vector2(1f, 1f)),
                LinearCurve(new Vector2(0f, 0f), new Vector2(0.6f, 0.2f), new Vector2(0.8f, 0.45f), new Vector2(1f, 1f)),
                LinearCurve(new Vector2(0f, 0f), new Vector2(1f, 1f)),
                LinearCurve(
                    new Vector2(0f, 0f),
                    new Vector2(0.2f, 0.3f),
                    new Vector2(0.4f, 0.38f),
                    new Vector2(0.6f, 0.68f),
                    new Vector2(0.8f, 0.75f),
                    new Vector2(1f, 1f))
            };

            return Create(
                CatDetectiveCatalogVersion,
                "pvp5-balance-1",
                new[] { DayOfWeek.Monday, DayOfWeek.Tuesday },
                orderTokens,
                rounds,
                milestones,
                curves,
                new IceCreamRaceMergeTuning(4, 0.15f, 1));
        }

        private static IceCreamRaceRoundDefinition Round(
            int round,
            int requiredTokens,
            int bot1Min,
            int bot1Max,
            int bot2Min,
            int bot2Max,
            int bot3Min,
            int bot3Max,
            int bot4Min,
            int bot4Max,
            int rewardRoadPoints)
        {
            return new IceCreamRaceRoundDefinition(
                round,
                requiredTokens,
                new[] { bot1Min, bot2Min, bot3Min, bot4Min },
                new[] { bot1Max, bot2Max, bot3Max, bot4Max },
                rewardRoadPoints);
        }

        private static ContentReward Reward(string rewardId, long amount)
        {
            return new ContentReward(rewardId, amount);
        }

        private static IceCreamRaceRoadMilestone Milestone(
            int milestone,
            int requiredPoints,
            bool isUltimate,
            params ContentReward[] rewards)
        {
            return new IceCreamRaceRoadMilestone(
                milestone,
                requiredPoints,
                new ReadOnlyCollection<ContentReward>(rewards),
                isUltimate);
        }

        private static AnimationCurve LinearCurve(params Vector2[] points)
        {
            var keys = new Keyframe[points.Length];
            for (int index = 0; index < points.Length; index++)
            {
                float incoming = index == 0 ? Slope(points[0], points[1]) : Slope(points[index - 1], points[index]);
                float outgoing = index == points.Length - 1
                    ? Slope(points[index - 1], points[index])
                    : Slope(points[index], points[index + 1]);
                keys[index] = new Keyframe(points[index].x, points[index].y, incoming, outgoing);
            }

            var curve = new AnimationCurve(keys)
            {
                preWrapMode = WrapMode.ClampForever,
                postWrapMode = WrapMode.ClampForever
            };
            return curve;
        }

        private static float Slope(Vector2 from, Vector2 to)
        {
            return (to.y - from.y) / (to.x - from.x);
        }

        private static int[] CopyIntegers(IReadOnlyList<int> values)
        {
            var copy = new int[values.Count];
            for (int index = 0; index < values.Count; index++) copy[index] = values[index];
            return copy;
        }

        private static IReadOnlyList<AnimationCurve> CloneCurves(IReadOnlyList<AnimationCurve> curves)
        {
            var copy = new List<AnimationCurve>(curves.Count);
            for (int index = 0; index < curves.Count; index++) copy.Add(CloneCurve(curves[index]));
            return new ReadOnlyCollection<AnimationCurve>(copy);
        }

        private static AnimationCurve ValidateAndCloneCurve(AnimationCurve curve, string parameterName)
        {
            if (curve == null || curve.length < 2)
                throw new ArgumentException("Each bot curve requires at least two keys.", parameterName);
            Keyframe[] keys = curve.keys;
            if (Math.Abs(keys[0].time) > 0.0001f || Math.Abs(keys[0].value) > 0.0001f
                || Math.Abs(keys[keys.Length - 1].time - 1f) > 0.0001f
                || Math.Abs(keys[keys.Length - 1].value - 1f) > 0.0001f)
                throw new ArgumentException("Bot curves must start at (0,0) and end at (1,1).", parameterName);
            for (int index = 1; index < keys.Length; index++)
            {
                if (keys[index].time <= keys[index - 1].time
                    || keys[index].value < keys[index - 1].value
                    || keys[index].value < 0f
                    || keys[index].value > 1f)
                    throw new ArgumentException("Bot curves must be time-ordered and monotonically increase within 0..1.", parameterName);
            }

            var points = new Vector2[keys.Length];
            for (int index = 0; index < keys.Length; index++)
            {
                points[index] = new Vector2(keys[index].time, keys[index].value);
            }

            // Public catalogs intentionally use piecewise-linear segments. This guarantees
            // monotonic progress even when a caller supplies unsafe weighted/custom tangents.
            return LinearCurve(points);
        }

        private static AnimationCurve CloneCurve(AnimationCurve curve)
        {
            return new AnimationCurve(curve.keys)
            {
                preWrapMode = curve.preWrapMode,
                postWrapMode = curve.postWrapMode
            };
        }
    }
}
