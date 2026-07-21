using System;
using System.Collections.Generic;
using UnityEngine;

namespace ActionFit.IceCreamRace
{
    public sealed class IceCreamRaceStateSerializer
    {
        public const int CurrentSchemaVersion = 2;

        public string Serialize(IceCreamRaceState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            int sourceSchemaVersion = state.SchemaVersion;
            if (sourceSchemaVersion <= 1)
            {
                state.MutableTimeBasis = (int)IceCreamRaceTimeBasis.LegacyCalendarTicks;
            }

            Normalize(state);
            Validate(state);
            return JsonUtility.ToJson(state);
        }

        public IceCreamRaceState Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("State JSON must not be empty.", nameof(json));
            }

            if (json.IndexOf("\"_schemaVersion\"", StringComparison.Ordinal) < 0)
            {
                throw new FormatException("Ice Cream Race state JSON has no schema version.");
            }

            IceCreamRaceState state;
            try
            {
                state = JsonUtility.FromJson<IceCreamRaceState>(json);
            }
            catch (ArgumentException exception)
            {
                throw new FormatException("Ice Cream Race state JSON is invalid.", exception);
            }

            if (state == null)
            {
                throw new FormatException("Ice Cream Race state JSON is invalid.");
            }

            if (state.SchemaVersion > CurrentSchemaVersion)
            {
                throw new NotSupportedException($"Ice Cream Race state schema {state.SchemaVersion} is newer than supported schema {CurrentSchemaVersion}.");
            }

            int sourceSchemaVersion = state.SchemaVersion;
            if (sourceSchemaVersion <= 1)
            {
                state.MutableTimeBasis = (int)IceCreamRaceTimeBasis.LegacyCalendarTicks;
            }

            Normalize(state);
            Validate(state);
            return state;
        }

        public IceCreamRaceState Clone(IceCreamRaceState state)
        {
            return Deserialize(Serialize(state));
        }

        internal static void Normalize(IceCreamRaceState state)
        {
            state.MutableSchemaVersion = CurrentSchemaVersion;
            if (!Enum.IsDefined(typeof(IceCreamRaceTimeBasis), state.MutableTimeBasis))
            {
                throw new FormatException("Ice Cream Race time basis is invalid.");
            }
            state.MutableRound = Math.Max(1, state.MutableRound);
            state.MutableCollectedTokens = Math.Max(0, state.MutableCollectedTokens);
            state.MutablePrevDisplayedTokens = Math.Max(0, state.MutablePrevDisplayedTokens);
            state.MutablePrevDisplayedMultiplierStep = Math.Max(0, state.MutablePrevDisplayedMultiplierStep);
            state.MutablePrevDisplayedElapsedSeconds = Math.Max(0f, state.MutablePrevDisplayedElapsedSeconds);
            state.MutableMatchStartUtcTicks = Math.Max(0, state.MutableMatchStartUtcTicks);
            state.MutablePendingRank = Math.Max(0, Math.Min(5, state.MutablePendingRank));
            state.MutableRoadPoints = Math.Max(0, state.MutableRoadPoints);
            state.MutableClaimedRoadPoints = Math.Max(0, Math.Min(state.MutableClaimedRoadPoints, state.MutableRoadPoints));
            state.MutableEventEndUtcTicks = Math.Max(0, state.MutableEventEndUtcTicks);
            state.MutableCompletedUntilUtcTicks = Math.Max(0, state.MutableCompletedUntilUtcTicks);
            state.MutablePendingRewardClaimedRoadPoints = Math.Max(0, state.MutablePendingRewardClaimedRoadPoints);
            state.MutablePendingRewardTransactionId = state.MutablePendingRewardTransactionId ?? string.Empty;
            state.MutableRaceId = state.MutableRaceId ?? string.Empty;
            state.MutableCatalogVersion = state.MutableCatalogVersion ?? string.Empty;
            state.MutableBalanceRevision = state.MutableBalanceRevision ?? string.Empty;
            state.MutableOpponents = state.MutableOpponents ?? new List<IceCreamRaceOpponentState>();
            state.MutableBotFinishSeconds = state.MutableBotFinishSeconds ?? new List<int>();
            state.MutableBotCurveIndices = state.MutableBotCurveIndices ?? new List<int>();
            state.MutablePendingRewards = state.MutablePendingRewards ?? new List<IceCreamRaceRewardState>();

            for (int index = 0; index < state.MutableBotFinishSeconds.Count; index++)
            {
                state.MutableBotFinishSeconds[index] = Math.Max(0, state.MutableBotFinishSeconds[index]);
            }

            for (int index = 0; index < state.MutableBotCurveIndices.Count; index++)
            {
                state.MutableBotCurveIndices[index] = Math.Max(0, Math.Min(3, state.MutableBotCurveIndices[index]));
            }

            if (string.IsNullOrEmpty(state.MutablePendingRewardTransactionId))
            {
                state.MutablePendingRewardClaimedRoadPoints = 0;
                state.MutablePendingRewards.Clear();
            }
        }

        private static void Validate(IceCreamRaceState state)
        {
            bool hasCatalogVersion = !string.IsNullOrWhiteSpace(state.CatalogVersion);
            bool hasBalanceRevision = !string.IsNullOrWhiteSpace(state.BalanceRevision);
            if (hasCatalogVersion != hasBalanceRevision)
            {
                throw new FormatException(
                    "Ice Cream Race state must record catalog version and balance revision together.");
            }

            if ((state.EventStarted || state.RoadPoints > 0 || state.MatchStartUtcTicks > 0)
                && !hasCatalogVersion)
            {
                throw new FormatException(
                    "An active Ice Cream Race event must record its catalog version.");
            }

            if (state.EventStarted && state.EventEndUtcTicks <= 0)
            {
                throw new FormatException(
                    "A started Ice Cream Race event must record a positive end time.");
            }

            bool hasMatchmaking = state.Opponents.Count > 0 || state.BotCurveIndices.Count > 0;
            if (hasMatchmaking
                && (state.Opponents.Count != 4 || state.BotCurveIndices.Count != 4))
            {
                throw new FormatException(
                    "Ice Cream Race matchmaking state requires four opponents and four curve indices.");
            }

            var opponentIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < state.Opponents.Count; index++)
            {
                IceCreamRaceOpponentState opponent = state.Opponents[index];
                if (opponent == null
                    || string.IsNullOrWhiteSpace(opponent.OpponentId)
                    || !opponentIds.Add(opponent.OpponentId))
                {
                    throw new FormatException(
                        "Ice Cream Race opponents must be non-null and have unique IDs.");
                }
            }

            if (state.MatchStartUtcTicks > 0)
            {
                if (!state.EventStarted
                    || state.Opponents.Count != 4
                    || state.BotFinishSeconds.Count != 4
                    || state.BotCurveIndices.Count != 4
                    || string.IsNullOrWhiteSpace(state.RaceId))
                {
                    throw new FormatException(
                        "An active Ice Cream Race requires a started event, a race ID, and four complete opponent snapshots.");
                }

                for (int index = 0; index < state.BotFinishSeconds.Count; index++)
                {
                    if (state.BotFinishSeconds[index] <= 0)
                    {
                        throw new FormatException(
                            "Active Ice Cream Race finish times must be positive.");
                    }
                }
            }
            else if (state.BotFinishSeconds.Count != 0 || state.PendingRank != 0)
            {
                throw new FormatException(
                    "Finish times and a pending rank require an active Ice Cream Race.");
            }

            if (state.HasPendingRewardTransaction)
            {
                if (string.IsNullOrWhiteSpace(state.PendingRewardTransactionId)
                    || state.PendingRewardClaimedRoadPoints <= 0
                    || state.PendingRewardClaimedRoadPoints > state.RoadPoints
                    || state.PendingRewards.Count == 0)
                {
                    throw new FormatException(
                        "A pending Ice Cream Race reward transaction requires a valid target and rewards.");
                }

                for (int index = 0; index < state.PendingRewards.Count; index++)
                {
                    IceCreamRaceRewardState reward = state.PendingRewards[index];
                    if (reward == null
                        || string.IsNullOrWhiteSpace(reward.RewardId)
                        || reward.Amount <= 0)
                    {
                        throw new FormatException(
                            "Pending Ice Cream Race rewards must be non-null and positive.");
                    }
                }
            }
        }
    }
}
