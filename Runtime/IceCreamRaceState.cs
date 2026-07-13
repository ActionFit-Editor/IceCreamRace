using System;
using System.Collections.Generic;
using UnityEngine;

namespace ActionFit.IceCreamRace
{
    [Serializable]
    public sealed class IceCreamRaceState
    {
        [SerializeField] private int _schemaVersion = IceCreamRaceStateSerializer.CurrentSchemaVersion;
        [SerializeField] private int _round = 1;
        [SerializeField] private int _collectedTokens;
        [SerializeField] private int _prevDisplayedTokens;
        [SerializeField] private int _prevDisplayedMultiplierStep;
        [SerializeField] private float _prevDisplayedElapsedSeconds;
        [SerializeField] private long _matchStartUtcTicks;
        [SerializeField] private List<IceCreamRaceOpponentState> _opponents = new List<IceCreamRaceOpponentState>();
        [SerializeField] private List<int> _botFinishSeconds = new List<int>();
        [SerializeField] private List<int> _botCurveIndices = new List<int>();
        [SerializeField] private int _pendingRank;
        [SerializeField] private int _roadPoints;
        [SerializeField] private int _claimedRoadPoints;
        [SerializeField] private bool _eventStarted;
        [SerializeField] private bool _pendingEnd;
        [SerializeField] private long _eventEndUtcTicks;
        [SerializeField] private long _completedUntilUtcTicks;
        [SerializeField] private bool _firstPopupShown;
        [SerializeField] private bool _tutorialShown;
        [SerializeField] private string _raceId = string.Empty;
        [SerializeField] private string _catalogVersion = string.Empty;
        [SerializeField] private string _balanceRevision = string.Empty;
        [SerializeField] private string _pendingRewardTransactionId = string.Empty;
        [SerializeField] private int _pendingRewardClaimedRoadPoints;
        [SerializeField] private List<IceCreamRaceRewardState> _pendingRewards = new List<IceCreamRaceRewardState>();

        public int SchemaVersion => _schemaVersion;
        public int Round => _round;
        public int CollectedTokens => _collectedTokens;
        public int PrevDisplayedTokens => _prevDisplayedTokens;
        public int PrevDisplayedMultiplierStep => _prevDisplayedMultiplierStep;
        public float PrevDisplayedElapsedSeconds => _prevDisplayedElapsedSeconds;
        public long MatchStartUtcTicks => _matchStartUtcTicks;
        public IReadOnlyList<IceCreamRaceOpponentState> Opponents => _opponents;
        public IReadOnlyList<int> BotFinishSeconds => _botFinishSeconds;
        public IReadOnlyList<int> BotCurveIndices => _botCurveIndices;
        public int PendingRank => _pendingRank;
        public int RoadPoints => _roadPoints;
        public int ClaimedRoadPoints => _claimedRoadPoints;
        public bool EventStarted => _eventStarted;
        public bool PendingEnd => _pendingEnd;
        public long EventEndUtcTicks => _eventEndUtcTicks;
        public long CompletedUntilUtcTicks => _completedUntilUtcTicks;
        public bool FirstPopupShown => _firstPopupShown;
        public bool TutorialShown => _tutorialShown;
        public string RaceId => _raceId ?? string.Empty;
        public string CatalogVersion => _catalogVersion ?? string.Empty;
        public string BalanceRevision => _balanceRevision ?? string.Empty;
        public string PendingRewardTransactionId => _pendingRewardTransactionId ?? string.Empty;
        public int PendingRewardClaimedRoadPoints => _pendingRewardClaimedRoadPoints;
        public IReadOnlyList<IceCreamRaceRewardState> PendingRewards => _pendingRewards;
        public bool HasPendingRewardTransaction => !string.IsNullOrEmpty(PendingRewardTransactionId);

        internal int MutableSchemaVersion { get => _schemaVersion; set => _schemaVersion = value; }
        internal int MutableRound { get => _round; set => _round = value; }
        internal int MutableCollectedTokens { get => _collectedTokens; set => _collectedTokens = value; }
        internal int MutablePrevDisplayedTokens { get => _prevDisplayedTokens; set => _prevDisplayedTokens = value; }
        internal int MutablePrevDisplayedMultiplierStep { get => _prevDisplayedMultiplierStep; set => _prevDisplayedMultiplierStep = value; }
        internal float MutablePrevDisplayedElapsedSeconds { get => _prevDisplayedElapsedSeconds; set => _prevDisplayedElapsedSeconds = value; }
        internal long MutableMatchStartUtcTicks { get => _matchStartUtcTicks; set => _matchStartUtcTicks = value; }
        internal List<IceCreamRaceOpponentState> MutableOpponents { get => _opponents; set => _opponents = value; }
        internal List<int> MutableBotFinishSeconds { get => _botFinishSeconds; set => _botFinishSeconds = value; }
        internal List<int> MutableBotCurveIndices { get => _botCurveIndices; set => _botCurveIndices = value; }
        internal int MutablePendingRank { get => _pendingRank; set => _pendingRank = value; }
        internal int MutableRoadPoints { get => _roadPoints; set => _roadPoints = value; }
        internal int MutableClaimedRoadPoints { get => _claimedRoadPoints; set => _claimedRoadPoints = value; }
        internal bool MutableEventStarted { get => _eventStarted; set => _eventStarted = value; }
        internal bool MutablePendingEnd { get => _pendingEnd; set => _pendingEnd = value; }
        internal long MutableEventEndUtcTicks { get => _eventEndUtcTicks; set => _eventEndUtcTicks = value; }
        internal long MutableCompletedUntilUtcTicks { get => _completedUntilUtcTicks; set => _completedUntilUtcTicks = value; }
        internal bool MutableFirstPopupShown { get => _firstPopupShown; set => _firstPopupShown = value; }
        internal bool MutableTutorialShown { get => _tutorialShown; set => _tutorialShown = value; }
        internal string MutableRaceId { get => _raceId; set => _raceId = value; }
        internal string MutableCatalogVersion { get => _catalogVersion; set => _catalogVersion = value; }
        internal string MutableBalanceRevision { get => _balanceRevision; set => _balanceRevision = value; }
        internal string MutablePendingRewardTransactionId { get => _pendingRewardTransactionId; set => _pendingRewardTransactionId = value; }
        internal int MutablePendingRewardClaimedRoadPoints { get => _pendingRewardClaimedRoadPoints; set => _pendingRewardClaimedRoadPoints = value; }
        internal List<IceCreamRaceRewardState> MutablePendingRewards { get => _pendingRewards; set => _pendingRewards = value; }
    }

    [Serializable]
    public sealed class IceCreamRaceOpponentState
    {
        [SerializeField] private string _opponentId = string.Empty;
        [SerializeField] private string _displayName = string.Empty;
        [SerializeField] private string _profileId = string.Empty;
        [SerializeField] private string _frameId = string.Empty;

        public string OpponentId => _opponentId ?? string.Empty;
        public string DisplayName => _displayName ?? string.Empty;
        public string ProfileId => _profileId ?? string.Empty;
        public string FrameId => _frameId ?? string.Empty;

        internal static IceCreamRaceOpponentState FromModel(IceCreamRaceOpponent opponent)
        {
            return new IceCreamRaceOpponentState
            {
                _opponentId = opponent.OpponentId,
                _displayName = opponent.DisplayName,
                _profileId = opponent.ProfileId,
                _frameId = opponent.FrameId
            };
        }
    }

    [Serializable]
    public sealed class IceCreamRaceRewardState
    {
        [SerializeField] private string _rewardId = string.Empty;
        [SerializeField] private long _amount;

        public string RewardId => _rewardId ?? string.Empty;
        public long Amount => _amount;

        internal IceCreamRaceRewardState(string rewardId, long amount)
        {
            _rewardId = rewardId;
            _amount = amount;
        }

        private IceCreamRaceRewardState()
        {
        }
    }
}
