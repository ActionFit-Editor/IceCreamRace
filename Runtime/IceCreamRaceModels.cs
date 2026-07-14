using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ActionFit.Content;

namespace ActionFit.IceCreamRace
{
    public sealed class IceCreamRaceImportState
    {
        public IceCreamRaceImportState(
            int round,
            int collectedTokens,
            int prevDisplayedTokens,
            int prevDisplayedMultiplierStep,
            float prevDisplayedElapsedSeconds,
            long matchStartUtcTicks,
            IReadOnlyList<IceCreamRaceOpponent> opponents,
            IReadOnlyList<int> botFinishSeconds,
            IReadOnlyList<int> botCurveIndices,
            int pendingRank,
            int roadPoints,
            int claimedRoadPoints,
            bool eventStarted,
            bool pendingEnd,
            long eventEndUtcTicks,
            long completedUntilUtcTicks,
            bool firstPopupShown,
            bool tutorialShown,
            string raceId = null,
            string catalogVersion = null,
            string balanceRevision = null)
        {
            if (round < 1) throw new ArgumentOutOfRangeException(nameof(round));
            if (collectedTokens < 0) throw new ArgumentOutOfRangeException(nameof(collectedTokens));
            if (prevDisplayedTokens < 0) throw new ArgumentOutOfRangeException(nameof(prevDisplayedTokens));
            if (prevDisplayedMultiplierStep < 0) throw new ArgumentOutOfRangeException(nameof(prevDisplayedMultiplierStep));
            if (prevDisplayedElapsedSeconds < 0f) throw new ArgumentOutOfRangeException(nameof(prevDisplayedElapsedSeconds));
            if (matchStartUtcTicks < 0) throw new ArgumentOutOfRangeException(nameof(matchStartUtcTicks));
            if (pendingRank < 0 || pendingRank > 5) throw new ArgumentOutOfRangeException(nameof(pendingRank));
            if (roadPoints < 0) throw new ArgumentOutOfRangeException(nameof(roadPoints));
            if (claimedRoadPoints < 0 || claimedRoadPoints > roadPoints) throw new ArgumentOutOfRangeException(nameof(claimedRoadPoints));
            if (eventEndUtcTicks < 0) throw new ArgumentOutOfRangeException(nameof(eventEndUtcTicks));
            if (completedUntilUtcTicks < 0) throw new ArgumentOutOfRangeException(nameof(completedUntilUtcTicks));

            Opponents = CopyOpponents(opponents);
            BotFinishSeconds = CopyIntegers(botFinishSeconds, nameof(botFinishSeconds), false);
            BotCurveIndices = CopyIntegers(botCurveIndices, nameof(botCurveIndices), true);
            if (Opponents.Count > 4 || BotFinishSeconds.Count > 4 || BotCurveIndices.Count > 4)
            {
                throw new ArgumentException("Imported opponent collections cannot contain more than four entries.");
            }

            if (matchStartUtcTicks > 0
                && (Opponents.Count != 4 || BotFinishSeconds.Count != 4 || BotCurveIndices.Count != 4))
            {
                throw new ArgumentException("An active imported race requires four opponents, finish times, and curve indices.");
            }

            if (matchStartUtcTicks > 0 && (!eventStarted || eventEndUtcTicks <= 0))
            {
                throw new ArgumentException("An active imported race requires a started event with a positive end time.");
            }

            Round = round;
            CollectedTokens = collectedTokens;
            PrevDisplayedTokens = prevDisplayedTokens;
            PrevDisplayedMultiplierStep = prevDisplayedMultiplierStep;
            PrevDisplayedElapsedSeconds = prevDisplayedElapsedSeconds;
            MatchStartUtcTicks = matchStartUtcTicks;
            PendingRank = pendingRank;
            RoadPoints = roadPoints;
            ClaimedRoadPoints = claimedRoadPoints;
            EventStarted = eventStarted;
            PendingEnd = pendingEnd;
            EventEndUtcTicks = eventEndUtcTicks;
            CompletedUntilUtcTicks = completedUntilUtcTicks;
            FirstPopupShown = firstPopupShown;
            TutorialShown = tutorialShown;
            RaceId = raceId ?? string.Empty;
            CatalogVersion = catalogVersion ?? string.Empty;
            BalanceRevision = balanceRevision ?? string.Empty;
        }

        public int Round { get; }
        public int CollectedTokens { get; }
        public int PrevDisplayedTokens { get; }
        public int PrevDisplayedMultiplierStep { get; }
        public float PrevDisplayedElapsedSeconds { get; }
        public long MatchStartUtcTicks { get; }
        public IReadOnlyList<IceCreamRaceOpponent> Opponents { get; }
        public IReadOnlyList<int> BotFinishSeconds { get; }
        public IReadOnlyList<int> BotCurveIndices { get; }
        public int PendingRank { get; }
        public int RoadPoints { get; }
        public int ClaimedRoadPoints { get; }
        public bool EventStarted { get; }
        public bool PendingEnd { get; }
        public long EventEndUtcTicks { get; }
        public long CompletedUntilUtcTicks { get; }
        public bool FirstPopupShown { get; }
        public bool TutorialShown { get; }
        public string RaceId { get; }
        public string CatalogVersion { get; }
        public string BalanceRevision { get; }

        private static IReadOnlyList<IceCreamRaceOpponent> CopyOpponents(IReadOnlyList<IceCreamRaceOpponent> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var copy = new List<IceCreamRaceOpponent>(source.Count);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < source.Count; index++)
            {
                IceCreamRaceOpponent opponent = source[index]
                    ?? throw new ArgumentException("Imported opponents must not contain null.", nameof(source));
                if (!ids.Add(opponent.OpponentId))
                    throw new ArgumentException("Imported opponent IDs must be unique.", nameof(source));
                copy.Add(new IceCreamRaceOpponent(
                    opponent.OpponentId,
                    opponent.DisplayName,
                    opponent.ProfileId,
                    opponent.FrameId));
            }

            return new ReadOnlyCollection<IceCreamRaceOpponent>(copy);
        }

        private static IReadOnlyList<int> CopyIntegers(IReadOnlyList<int> source, string parameterName, bool curveIndex)
        {
            if (source == null) throw new ArgumentNullException(parameterName);
            var copy = new List<int>(source.Count);
            for (int index = 0; index < source.Count; index++)
            {
                int value = source[index];
                if (value < 0 || curveIndex && value > 3)
                    throw new ArgumentOutOfRangeException(parameterName);
                copy.Add(value);
            }

            return new ReadOnlyCollection<int>(copy);
        }
    }

    public sealed class IceCreamRaceOpponent
    {
        public IceCreamRaceOpponent(string opponentId, string displayName, string profileId, string frameId)
        {
            if (string.IsNullOrWhiteSpace(opponentId))
            {
                throw new ArgumentException("Opponent ID must not be empty.", nameof(opponentId));
            }

            OpponentId = opponentId;
            DisplayName = displayName ?? string.Empty;
            ProfileId = profileId ?? string.Empty;
            FrameId = frameId ?? string.Empty;
        }

        public string OpponentId { get; }
        public string DisplayName { get; }
        public string ProfileId { get; }
        public string FrameId { get; }
    }

    public readonly struct IceCreamRaceResultClaim
    {
        public IceCreamRaceResultClaim(bool succeeded, int rank, bool advanced, int roadPointsAdded, int nextRound)
        {
            Succeeded = succeeded;
            Rank = rank;
            Advanced = advanced;
            RoadPointsAdded = roadPointsAdded;
            NextRound = nextRound;
        }

        public bool Succeeded { get; }
        public int Rank { get; }
        public bool Advanced { get; }
        public int RoadPointsAdded { get; }
        public int NextRound { get; }
    }

    public sealed class IceCreamRaceRoadClaimResult
    {
        internal IceCreamRaceRoadClaimResult(
            bool succeeded,
            string transactionId,
            IReadOnlyList<ContentReward> rewards,
            int claimedRoadPoints,
            bool completedEvent)
        {
            Succeeded = succeeded;
            TransactionId = transactionId ?? string.Empty;
            Rewards = rewards ?? Array.Empty<ContentReward>();
            ClaimedRoadPoints = claimedRoadPoints;
            CompletedEvent = completedEvent;
        }

        public bool Succeeded { get; }
        public string TransactionId { get; }
        public IReadOnlyList<ContentReward> Rewards { get; }
        public int ClaimedRoadPoints { get; }
        public bool CompletedEvent { get; }

        internal static IceCreamRaceRoadClaimResult None(int claimedRoadPoints)
        {
            return new IceCreamRaceRoadClaimResult(
                false,
                string.Empty,
                new ReadOnlyCollection<ContentReward>(new List<ContentReward>()),
                claimedRoadPoints,
                false);
        }
    }
}
