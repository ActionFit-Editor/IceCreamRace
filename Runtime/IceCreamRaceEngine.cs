using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using ActionFit.Content;
using ActionFit.Time;
using UnityEngine;

namespace ActionFit.IceCreamRace
{
    public sealed class IceCreamRaceEngine
    {
        public const string DefaultContentId = "ice-cream-race";

        private const int OpponentCount = 4;

        private readonly IContentStateStore _stateStore;
        private readonly IContentRewardService _rewardService;
        private readonly IceCreamRaceCatalog _catalog;
        private readonly IIceCreamRaceCatalogResolver _catalogResolver;
        private readonly IIceCreamRaceSchedulePolicy _schedulePolicy;
        private readonly IClock _clock;
        private TimeZoneInfo _calendarTimeZone;
        private TimeSpan _calendarDayBoundaryOffset;
        private readonly TimeZoneInfo _legacyCalendarTimeZone;
        private readonly IIceCreamRaceRandom _random;
        private readonly IIceCreamRaceOpponentProvider _opponentProvider;
        private readonly IceCreamRaceStateSerializer _serializer;
        private readonly IIceCreamRaceAccessPolicy _accessPolicy;
        private readonly string _contentId;

        private IceCreamRaceState _state = new IceCreamRaceState();
        private IceCreamRaceCatalog _resolvedEventCatalog;

        public IceCreamRaceEngine(
            IContentStateStore stateStore,
            IContentRewardService rewardService,
            IceCreamRaceCatalog catalog = null,
            IClock clock = null,
            IIceCreamRaceRandom random = null,
            IIceCreamRaceOpponentProvider opponentProvider = null,
            string contentId = DefaultContentId,
            IIceCreamRaceAccessPolicy accessPolicy = null,
            IIceCreamRaceSchedulePolicy schedulePolicy = null,
            IIceCreamRaceCatalogResolver catalogResolver = null,
            TimeZoneInfo calendarTimeZone = null,
            TimeZoneInfo legacyCalendarTimeZone = null)
            : this(
                stateStore,
                rewardService,
                catalog,
                clock,
                random,
                opponentProvider,
                contentId,
                accessPolicy,
                schedulePolicy,
                catalogResolver,
                calendarTimeZone,
                legacyCalendarTimeZone,
                TimeSpan.Zero)
        {
        }

        public IceCreamRaceEngine(
            IContentStateStore stateStore,
            IContentRewardService rewardService,
            IceCreamRaceCatalog catalog,
            IClock clock,
            IIceCreamRaceRandom random,
            IIceCreamRaceOpponentProvider opponentProvider,
            string contentId,
            IIceCreamRaceAccessPolicy accessPolicy,
            IIceCreamRaceSchedulePolicy schedulePolicy,
            IIceCreamRaceCatalogResolver catalogResolver,
            TimeZoneInfo calendarTimeZone,
            TimeZoneInfo legacyCalendarTimeZone,
            TimeSpan calendarDayBoundaryOffset)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _rewardService = rewardService ?? throw new ArgumentNullException(nameof(rewardService));
            _catalog = catalog ?? IceCreamRaceCatalog.CatDetectiveParity;
            _catalogResolver = catalogResolver ?? new IceCreamRaceCatalogRegistry(_catalog);
            _schedulePolicy = schedulePolicy
                ?? new FixedIceCreamRaceSchedulePolicy(_catalog.ActiveDays);
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
#pragma warning disable CS0618
            bool usesLegacyClockAlias = clock is IIceCreamRaceClock;
#pragma warning restore CS0618
            _calendarTimeZone = calendarTimeZone
                ?? (usesLegacyClockAlias ? TimeZoneInfo.Utc : throw new ArgumentNullException(nameof(calendarTimeZone)));
            _calendarDayBoundaryOffset = ValidateCalendarDayBoundaryOffset(calendarDayBoundaryOffset);
            _legacyCalendarTimeZone = legacyCalendarTimeZone
                ?? (usesLegacyClockAlias ? _calendarTimeZone : throw new ArgumentNullException(nameof(legacyCalendarTimeZone)));
            _random = random ?? new SystemIceCreamRaceRandom();
            _opponentProvider = opponentProvider ?? DefaultIceCreamRaceOpponentProvider.Instance;
            _serializer = new IceCreamRaceStateSerializer();
            _accessPolicy = accessPolicy ?? AlwaysAllowedIceCreamRaceAccessPolicy.Instance;
            _contentId = string.IsNullOrWhiteSpace(contentId)
                ? throw new ArgumentException("Content ID must not be empty.", nameof(contentId))
                : contentId;
        }

        public event Action<IceCreamRaceState> StateChanged;

        /// <summary>
        /// Replaces the new-event calendar policy without changing active deadline ticks or basis.
        /// </summary>
        public void ConfigureCalendar(
            TimeZoneInfo calendarTimeZone,
            TimeSpan calendarDayBoundaryOffset)
        {
            TimeZoneInfo selectedTimeZone = calendarTimeZone
                                            ?? throw new ArgumentNullException(nameof(calendarTimeZone));
            TimeSpan selectedBoundary =
                ValidateCalendarDayBoundaryOffset(calendarDayBoundaryOffset);
            _calendarTimeZone = selectedTimeZone;
            _calendarDayBoundaryOffset = selectedBoundary;
        }

        public IceCreamRaceState State => _serializer.Clone(_state);
        public IceCreamRaceCatalog Catalog => EventCatalog;
        public int Round => _state.Round;
        public int RequiredTokens => CurrentRound.RequiredTokens;
        public int CollectedTokens => _state.CollectedTokens;
        public int PendingRank => _state.PendingRank;
        public int CurrentRank => _state.PendingRank != 0 ? _state.PendingRank : CalculateLiveRank();
        public int ParticipantCount => EventCatalog.GetParticipantCount(_state.Round);
        public int ActiveOpponentCount => ParticipantCount - 1;
        public int RankCutoff => EventCatalog.GetCutoff(_state.Round);
        public int CurrentMultiplier => EventCatalog.GetMultiplier(_state.Round);
        public int CurrentRoundRewardPoints => CurrentRound.RewardRoadPoints * CurrentMultiplier;
        public int RoadPoints => _state.RoadPoints;
        public int ClaimedRoadPoints => _state.ClaimedRoadPoints;
        public bool IsMatchmade => !IsRaceActive && _state.Opponents.Count == OpponentCount;
        public bool IsRaceActive => _state.MatchStartUtcTicks > 0;
        public bool HasPendingResult => _state.PendingRank != 0;
        public bool HasPendingRewardTransaction => _state.HasPendingRewardTransaction;
        public bool IsRewardServiceAvailable => _rewardService.IsAvailable;
        public bool IsEventStarted => _state.EventStarted;
        public bool PendingEnd => _state.PendingEnd;
        public bool IsAccessAllowed => _accessPolicy.IsAccessAllowed;
        public bool IsEventDay => IsActiveEventDay(IceCreamRaceTimeBasis.UtcTicks);
        public bool FirstPopupShown => _state.FirstPopupShown;
        public bool TutorialShown => _state.TutorialShown;
        public string RaceId => _state.RaceId;
        public string ActiveCatalogVersion => _state.CatalogVersion;
        public string ActiveBalanceRevision => _state.BalanceRevision;
        public bool IsEventCompleted => _state.CompletedUntilUtcTicks > NowTicks;
        public bool IsEventActive => _state.EventStarted
            && _schedulePolicy.IsEnabled
            && !_state.PendingEnd
            && !IsEventCompleted
            && _state.EventEndUtcTicks > NowTicks;
        public TimeSpan EventRemainingTime => RemainingUntil(_state.EventEndUtcTicks);
        public TimeSpan ExpectedRemainingTime
        {
            get
            {
                TimeSpan remaining = EventRemainingTime;
                if (remaining > TimeSpan.Zero) return remaining;
                return TryGetActiveWindowEndTicks(IceCreamRaceTimeBasis.UtcTicks, out long endTicks)
                    ? RemainingUntil(endTicks, IceCreamRaceTimeBasis.UtcTicks)
                    : TimeSpan.Zero;
            }
        }
        public float RaceElapsedSeconds => IsRaceActive
            ? Mathf.Max(0f, (float)((NowTicks - _state.MatchStartUtcTicks) / (double)TimeSpan.TicksPerSecond))
            : 0f;
        public float RaceDeadlineSeconds => CalculateDeadlineSeconds();

        public static IceCreamRaceEngine CreateDefault(
            IClock clock,
            TimeZoneInfo calendarTimeZone,
            string contentId = DefaultContentId)
        {
            return new IceCreamRaceEngine(
                new PlayerPrefsContentStateStore(),
                new PlayerPrefsContentRewardService(),
                clock: clock ?? throw new ArgumentNullException(nameof(clock)),
                contentId: contentId,
                calendarTimeZone: calendarTimeZone ?? throw new ArgumentNullException(nameof(calendarTimeZone)),
                legacyCalendarTimeZone: calendarTimeZone);
        }

        public IceCreamRaceState Restore()
        {
            _state = _stateStore.TryLoad(_contentId, out string json)
                ? _serializer.Deserialize(json)
                : new IceCreamRaceState();
            _resolvedEventCatalog = null;
            ResolvePinnedCatalog();

            if (_state.HasPendingRewardTransaction)
            {
                RecoverPendingRewardTransaction();
            }

            bool changed = ResolveRaceIfRequired();
            changed |= EvaluateTimeoutInternal();
            Persist(changed, changed);
            return State;
        }

        public bool ImportStateIfEmpty(IceCreamRaceImportState importState)
        {
            if (importState == null)
            {
                throw new ArgumentNullException(nameof(importState));
            }

            if (_stateStore.TryLoad(_contentId, out _))
            {
                return false;
            }

            var state = new IceCreamRaceState
            {
                MutableTimeBasis = (int)IceCreamRaceTimeBasis.LegacyCalendarTicks,
                MutableRound = importState.Round,
                MutableCollectedTokens = importState.CollectedTokens,
                MutablePrevDisplayedTokens = importState.PrevDisplayedTokens,
                MutablePrevDisplayedMultiplierStep = importState.PrevDisplayedMultiplierStep,
                MutablePrevDisplayedElapsedSeconds = importState.PrevDisplayedElapsedSeconds,
                MutableMatchStartUtcTicks = importState.MatchStartUtcTicks,
                MutablePendingRank = importState.PendingRank,
                MutableRoadPoints = importState.RoadPoints,
                MutableClaimedRoadPoints = importState.ClaimedRoadPoints,
                MutableEventStarted = importState.EventStarted,
                MutablePendingEnd = importState.PendingEnd,
                MutableEventEndUtcTicks = importState.EventEndUtcTicks,
                MutableCompletedUntilUtcTicks = importState.CompletedUntilUtcTicks,
                MutableFirstPopupShown = importState.FirstPopupShown,
                MutableTutorialShown = importState.TutorialShown,
                MutableRaceId = importState.MatchStartUtcTicks > 0
                    ? string.IsNullOrWhiteSpace(importState.RaceId) ? Guid.NewGuid().ToString("N") : importState.RaceId
                    : string.Empty,
                MutableCatalogVersion = importState.MatchStartUtcTicks > 0
                    || importState.EventStarted
                    || importState.RoadPoints > 0
                    ? string.IsNullOrWhiteSpace(importState.CatalogVersion) ? _catalog.CatalogVersion : importState.CatalogVersion
                    : string.Empty,
                MutableBalanceRevision = importState.MatchStartUtcTicks > 0
                    || importState.EventStarted
                    || importState.RoadPoints > 0
                    ? string.IsNullOrWhiteSpace(importState.BalanceRevision) ? _catalog.BalanceRevision : importState.BalanceRevision
                    : string.Empty,
                MutableBotFinishSeconds = new List<int>(importState.BotFinishSeconds),
                MutableBotCurveIndices = new List<int>(importState.BotCurveIndices)
            };

            var opponents = new List<IceCreamRaceOpponentState>(importState.Opponents.Count);
            for (int index = 0; index < importState.Opponents.Count; index++)
            {
                opponents.Add(IceCreamRaceOpponentState.FromModel(importState.Opponents[index]));
            }

            state.MutableOpponents = opponents;
            _state = state;
            _resolvedEventCatalog = null;
            ResolvePinnedCatalog();
            Persist(true, true);
            return true;
        }

        /// <summary>
        /// Starts the current catalog schedule window without starting a match or race.
        /// Project facades use this to show their entry UI before matchmaking begins.
        /// </summary>
        public bool TryStartEvent()
        {
            if (!CanEnterRace())
            {
                return false;
            }

            if (_state.EventStarted)
            {
                return _state.EventEndUtcTicks > NowTicks;
            }

            if (!TryGetActiveWindowEndTicks(IceCreamRaceTimeBasis.UtcTicks, out long endTicks))
            {
                return false;
            }

            PinCurrentCatalog();
            _state.MutableTimeBasis = (int)IceCreamRaceTimeBasis.UtcTicks;
            _state.MutableEventStarted = true;
            _state.MutableEventEndUtcTicks = endTicks;
            _state.MutablePendingEnd = false;
            _state.MutableFirstPopupShown = false;
            Persist(true, true);
            return true;
        }

        public bool Matchmake()
        {
            if (!CanEnterRace() || IsRaceActive || HasPendingResult)
            {
                return false;
            }

            if (IsMatchmade)
            {
                return true;
            }

            IReadOnlyList<IceCreamRaceOpponent> opponents =
                _opponentProvider.CreateOpponents(OpponentCount, _random);
            if (opponents == null || opponents.Count != OpponentCount)
            {
                throw new InvalidOperationException($"Opponent provider must return exactly {OpponentCount} opponents.");
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var storedOpponents = new List<IceCreamRaceOpponentState>(OpponentCount);
            var curveIndices = new List<int>(OpponentCount);
            for (int index = 0; index < opponents.Count; index++)
            {
                IceCreamRaceOpponent opponent = opponents[index]
                    ?? throw new InvalidOperationException("Opponent provider returned a null opponent.");
                if (!ids.Add(opponent.OpponentId))
                {
                    throw new InvalidOperationException("Opponent IDs must be unique within a match.");
                }

                storedOpponents.Add(IceCreamRaceOpponentState.FromModel(opponent));
                curveIndices.Add(_random.Range(0, EventCatalog.BotProgressCurves.Count));
            }

            _state.MutableOpponents = storedOpponents;
            _state.MutableBotCurveIndices = curveIndices;
            Persist(true);
            return true;
        }

        public bool CancelMatchmaking()
        {
            if (IsRaceActive || !IsMatchmade)
            {
                return false;
            }

            ClearMatchmaking();
            Persist(true);
            return true;
        }

        public bool StartRace()
        {
            if (!CanEnterRace() || _state.PendingEnd || HasPendingResult)
            {
                return false;
            }

            if (IsRaceActive)
            {
                return true;
            }

            if (!IsMatchmade)
            {
                return false;
            }

            bool startsNewEvent = !_state.EventStarted || _state.EventEndUtcTicks <= NowTicks;
            long newEventEndTicks = 0L;
            if (startsNewEvent
                && !TryGetActiveWindowEndTicks(IceCreamRaceTimeBasis.UtcTicks, out newEventEndTicks))
            {
                return false;
            }

            PinCurrentCatalogIfMissing();
            if (startsNewEvent)
            {
                _state.MutableTimeBasis = (int)IceCreamRaceTimeBasis.UtcTicks;
                _state.MutableEventStarted = true;
                _state.MutableEventEndUtcTicks = newEventEndTicks;
                _state.MutablePendingEnd = false;
            }

            IceCreamRaceRoundDefinition round = CurrentRound;
            var finishSeconds = new List<int>(OpponentCount);
            for (int index = 0; index < OpponentCount; index++)
            {
                finishSeconds.Add(_random.Range(
                    round.BotMinimumSeconds[index],
                    checked(round.BotMaximumSeconds[index] + 1)));
            }

            _state.MutableCollectedTokens = 0;
            _state.MutablePendingRank = 0;
            _state.MutablePrevDisplayedTokens = 0;
            _state.MutablePrevDisplayedElapsedSeconds = 0f;
            _state.MutableBotFinishSeconds = finishSeconds;
            _state.MutableMatchStartUtcTicks = NowTicks;
            _state.MutableRaceId = Guid.NewGuid().ToString("N");
            Persist(true, true);
            return true;
        }

        public bool AddTokens(int amount)
        {
            if (amount <= 0 || !CanAccrueTokens())
            {
                return false;
            }

            int next = Math.Min(RequiredTokens, checked(_state.CollectedTokens + amount));
            if (next == _state.CollectedTokens)
            {
                return false;
            }

            _state.MutableCollectedTokens = next;
            bool resolved = next >= RequiredTokens;
            if (resolved)
            {
                ResolveRace(CalculateLiveRank());
            }

            Persist(true, resolved);
            return true;
        }

        /// <summary>Resolves the active unresolved race as a deterministic first-place developer result.</summary>
        public bool DevForceWin()
        {
            if (!CanForceResult())
            {
                return false;
            }

            _state.MutableCollectedTokens = RequiredTokens;
            ResolveRace(1);
            Persist(true, true);
            return true;
        }

        /// <summary>Resolves the active unresolved race at the first non-qualifying developer rank.</summary>
        public bool DevForceLose()
        {
            if (!CanForceResult())
            {
                return false;
            }

            int losingRank = Math.Min(ParticipantCount, RankCutoff + 1);
            ResolveRace(losingRank);
            Persist(true, true);
            return true;
        }

        public IceCreamRaceResultClaim ClaimResult()
        {
            if (_state.PendingRank == 0)
            {
                return new IceCreamRaceResultClaim(false, 0, false, 0, _state.Round);
            }

            int rank = _state.PendingRank;
            bool advanced = rank <= RankCutoff;
            int pointsAdded = 0;
            if (advanced)
            {
                pointsAdded = CurrentRoundRewardPoints;
                _state.MutableRoadPoints = checked(_state.RoadPoints + pointsAdded);
                _state.MutableRound = checked(_state.Round + 1);
            }
            else
            {
                _state.MutableRound = 1;
            }

            ClearRace();
            Persist(true, true);
            return new IceCreamRaceResultClaim(true, rank, advanced, pointsAdded, _state.Round);
        }

        public IceCreamRaceRoadClaimResult ClaimRoadRewards()
        {
            if (_state.HasPendingRewardTransaction)
            {
                EnsureRewardServiceAvailable();
                RecoverPendingRewardTransaction();
            }

            List<ContentReward> rewards = CollectClaimableRoadRewards();
            if (rewards.Count == 0)
            {
                return IceCreamRaceRoadClaimResult.None(_state.ClaimedRoadPoints);
            }

            EnsureRewardServiceAvailable();

            int claimedTarget = _state.RoadPoints;
            string eventInstanceId = _state.EventEndUtcTicks.ToString(CultureInfo.InvariantCulture);
            string transactionId =
                $"{_contentId}/event/{eventInstanceId}/road/{Guid.NewGuid():N}";
            var rewardStates = new List<IceCreamRaceRewardState>(rewards.Count);
            for (int index = 0; index < rewards.Count; index++)
            {
                rewardStates.Add(new IceCreamRaceRewardState(rewards[index].RewardId, rewards[index].Amount));
            }

            _state.MutablePendingRewardTransactionId = transactionId;
            _state.MutablePendingRewardClaimedRoadPoints = claimedTarget;
            _state.MutablePendingRewards = rewardStates;
            Persist(false, true);

            bool completed = RecoverPendingRewardTransaction();
            return new IceCreamRaceRoadClaimResult(
                true,
                transactionId,
                new ReadOnlyCollection<ContentReward>(rewards),
                claimedTarget,
                completed);
        }

        public bool EvaluateTimeout()
        {
            bool changed = EvaluateTimeoutInternal();
            if (changed)
            {
                Persist(true, true);
            }

            return changed;
        }

        public void EndEvent()
        {
            if (_state.HasPendingRewardTransaction)
            {
                RecoverPendingRewardTransaction();
            }

            long eventEndUtcTicks = _state.EventEndUtcTicks;
            if (eventEndUtcTicks > NowTicks)
            {
                _state.MutableCompletedUntilUtcTicks = Math.Max(
                    _state.CompletedUntilUtcTicks,
                    eventEndUtcTicks);
            }

            ClearEventProgress();
            Persist(true, true);
        }

        public void SaveDisplayedSnapshot()
        {
            ApplyDisplayedSnapshot(_state.CollectedTokens, RaceElapsedSeconds);
            _state.MutablePrevDisplayedMultiplierStep = GetCurrentMultiplierStep();
            Persist(true);
        }

        /// <summary>
        /// Stores only the race progress that a presenter has completely displayed.
        /// Values are monotonic and cannot advance beyond the current authoritative state.
        /// </summary>
        public void SaveDisplayedSnapshot(int displayedTokens, float displayedElapsedSeconds)
        {
            if (!ApplyDisplayedSnapshot(displayedTokens, displayedElapsedSeconds))
            {
                return;
            }

            Persist(true);
        }

        /// <summary>Stores the current round-derived step as the previously displayed multiplier step.</summary>
        public void SaveDisplayedMultiplierStep()
        {
            _state.MutablePrevDisplayedMultiplierStep = GetCurrentMultiplierStep();
            Persist(true);
        }

        private bool ApplyDisplayedSnapshot(int displayedTokens, float displayedElapsedSeconds)
        {
            int previousTokens = _state.PrevDisplayedTokens;
            int currentTokens = _state.CollectedTokens;
            int nextTokens = currentTokens < previousTokens
                ? previousTokens
                : Math.Min(currentTokens, Math.Max(previousTokens, displayedTokens));

            float previousElapsedSeconds = _state.PrevDisplayedElapsedSeconds;
            float currentElapsedSeconds = RaceElapsedSeconds;
            float nextElapsedSeconds = previousElapsedSeconds;
            if (!float.IsNaN(displayedElapsedSeconds)
                && !float.IsInfinity(displayedElapsedSeconds)
                && currentElapsedSeconds >= previousElapsedSeconds)
            {
                nextElapsedSeconds = Math.Min(
                    currentElapsedSeconds,
                    Math.Max(previousElapsedSeconds, displayedElapsedSeconds));
            }

            if (nextTokens == previousTokens
                && Math.Abs(nextElapsedSeconds - previousElapsedSeconds) < 0.0001f)
            {
                return false;
            }

            _state.MutablePrevDisplayedTokens = nextTokens;
            _state.MutablePrevDisplayedElapsedSeconds = nextElapsedSeconds;
            return true;
        }

        public bool MarkFirstPopupShown()
        {
            if (_state.FirstPopupShown)
            {
                return false;
            }

            _state.MutableFirstPopupShown = true;
            Persist(true, true);
            return true;
        }

        public bool MarkTutorialShown()
        {
            if (_state.TutorialShown)
            {
                return false;
            }

            _state.MutableTutorialShown = true;
            Persist(true, true);
            return true;
        }

        public int GetOrderTokens(IEnumerable<int> itemValues)
        {
            if (itemValues == null)
            {
                throw new ArgumentNullException(nameof(itemValues));
            }

            int total = 0;
            foreach (int itemValue in itemValues)
            {
                total = checked(total + EventCatalog.GetOrderTokens(itemValue));
            }

            return total;
        }

        public int RollMergeTokens(int mergedLevel)
        {
            IceCreamRaceMergeTuning tuning = EventCatalog.MergeTuning;
            if (mergedLevel < tuning.MinimumLevel)
            {
                return 0;
            }

            const int precision = 1000000;
            int threshold = Mathf.RoundToInt(tuning.Chance * precision);
            return _random.Range(0, precision) < threshold ? mergedLevel + tuning.TokenBonus : 0;
        }

        public float GetOpponentProgress(int opponentIndex)
        {
            if (opponentIndex < 0 || opponentIndex >= ActiveOpponentCount)
            {
                throw new ArgumentOutOfRangeException(nameof(opponentIndex));
            }

            if (opponentIndex >= _state.BotFinishSeconds.Count)
            {
                return 0f;
            }

            int curveIndex = opponentIndex < _state.BotCurveIndices.Count
                ? _state.BotCurveIndices[opponentIndex]
                : 0;
            return EventCatalog.EvaluateBotProgress(
                curveIndex,
                _state.BotFinishSeconds[opponentIndex],
                Math.Min(RaceElapsedSeconds, RaceDeadlineSeconds));
        }

        private DateTime UtcNow
        {
            get
            {
                DateTime utcNow = _clock.UtcNow;
                if (utcNow.Kind != DateTimeKind.Utc)
                {
                    throw new InvalidOperationException("IClock.UtcNow must have DateTimeKind.Utc.");
                }

                return utcNow;
            }
        }

        private DateTime CalendarNow => GetCalendarNow(_state.TimeBasis);

        private long NowTicks => _state.TimeBasis == IceCreamRaceTimeBasis.LegacyCalendarTicks
            ? CalendarNow.Ticks
            : UtcNow.Ticks;

        private bool IsActiveEventDay(IceCreamRaceTimeBasis basis)
        {
            DateTime calendarNow = GetCalendarNow(basis);
            return _schedulePolicy.IsEnabled && _schedulePolicy.IsActiveDay(calendarNow.DayOfWeek);
        }

        private bool TryGetActiveWindowEndTicks(IceCreamRaceTimeBasis basis, out long endTicks)
        {
            endTicks = 0L;
            DateTime calendarNow = GetCalendarNow(basis);
            if (!_schedulePolicy.IsEnabled || !_schedulePolicy.IsActiveDay(calendarNow.DayOfWeek))
            {
                return false;
            }

            DateTime calendarEnd = _schedulePolicy.GetActiveWindowEnd(calendarNow);
            long nowTicks;
            if (basis == IceCreamRaceTimeBasis.LegacyCalendarTicks)
            {
                endTicks = calendarEnd.Ticks;
                nowTicks = calendarNow.Ticks;
            }
            else
            {
                DateTime actualBoundaryTime = calendarEnd.Add(_calendarDayBoundaryOffset);
                DateTime unspecifiedEnd = ResolveValidCalendarTime(actualBoundaryTime);
                endTicks = TimeZoneInfo.ConvertTimeToUtc(unspecifiedEnd, _calendarTimeZone).Ticks;
                nowTicks = UtcNow.Ticks;
            }

            return endTicks > nowTicks;
        }

        private DateTime GetCalendarNow(IceCreamRaceTimeBasis basis)
        {
            TimeZoneInfo timeZone = basis == IceCreamRaceTimeBasis.LegacyCalendarTicks
                ? _legacyCalendarTimeZone
                : _calendarTimeZone;
            DateTime calendarNow = _clock.GetCurrentTime(timeZone).DateTime;
            return basis == IceCreamRaceTimeBasis.LegacyCalendarTicks
                ? calendarNow
                : calendarNow.Subtract(_calendarDayBoundaryOffset);
        }

        private long GetCompletionFallbackTicks()
        {
            DateTime calendarEnd = CalendarNow.Date.AddDays(7);
            if (_state.TimeBasis == IceCreamRaceTimeBasis.LegacyCalendarTicks)
            {
                return calendarEnd.Ticks;
            }

            DateTime actualBoundaryTime = calendarEnd.Add(_calendarDayBoundaryOffset);
            DateTime unspecifiedEnd = ResolveValidCalendarTime(actualBoundaryTime);
            return TimeZoneInfo.ConvertTimeToUtc(unspecifiedEnd, _calendarTimeZone).Ticks;
        }

        private DateTime ResolveValidCalendarTime(DateTime calendarTime)
        {
            DateTime unspecified = DateTime.SpecifyKind(calendarTime, DateTimeKind.Unspecified);
            const int maxAdvanceMinutes = 48 * 60;
            for (int minute = 0; minute <= maxAdvanceMinutes; minute++)
            {
                if (!_calendarTimeZone.IsInvalidTime(unspecified)) return unspecified;
                unspecified = unspecified.AddMinutes(1);
            }

            throw new InvalidOperationException("Unable to resolve a valid calendar boundary within 48 hours.");
        }

        private static TimeSpan ValidateCalendarDayBoundaryOffset(TimeSpan offset)
        {
            if (offset <= -TimeSpan.FromDays(1) || offset >= TimeSpan.FromDays(1))
                throw new ArgumentOutOfRangeException(
                    nameof(offset),
                    offset,
                    "Calendar day boundary offset must be greater than -24 hours and less than 24 hours.");
            return offset;
        }

        private TimeSpan RemainingUntil(long endTicks)
        {
            return RemainingUntil(endTicks, _state.TimeBasis);
        }

        private TimeSpan RemainingUntil(long endTicks, IceCreamRaceTimeBasis basis)
        {
            long nowTicks = basis == IceCreamRaceTimeBasis.LegacyCalendarTicks
                ? GetCalendarNow(basis).Ticks
                : UtcNow.Ticks;
            long remainingTicks = endTicks - nowTicks;
            return remainingTicks > 0 ? TimeSpan.FromTicks(remainingTicks) : TimeSpan.Zero;
        }

        private IceCreamRaceCatalog EventCatalog
        {
            get
            {
                ResolvePinnedCatalog();
                return _resolvedEventCatalog ?? _catalog;
            }
        }

        private IceCreamRaceRoundDefinition CurrentRound => EventCatalog.GetRound(_state.Round);

        private int GetCurrentMultiplierStep()
        {
            return Math.Max(0, Math.Min(3, _state.Round - 1));
        }

        private bool CanEnterRace()
        {
            return _schedulePolicy.IsEnabled
                && IsEventDay
                && _accessPolicy.IsAccessAllowed
                && !IsEventCompleted
                && !_state.PendingEnd;
        }

        private bool CanAccrueTokens()
        {
            return IsRaceActive
                && _state.PendingRank == 0
                && !_state.PendingEnd
                && IsEventActive;
        }

        private int CalculateLiveRank()
        {
            if (!IsRaceActive || RequiredTokens <= 0)
            {
                return 1;
            }

            float playerRatio = Mathf.Clamp01((float)_state.CollectedTokens / RequiredTokens);
            float elapsed = RaceElapsedSeconds;
            int opponentsAhead = 0;
            for (int index = 0; index < ActiveOpponentCount; index++)
            {
                if (index >= _state.BotFinishSeconds.Count)
                {
                    continue;
                }

                int finishSeconds = _state.BotFinishSeconds[index];
                if (finishSeconds <= elapsed)
                {
                    opponentsAhead++;
                    continue;
                }

                int curveIndex = index < _state.BotCurveIndices.Count
                    ? _state.BotCurveIndices[index]
                    : 0;
                if (EventCatalog.EvaluateBotProgress(curveIndex, finishSeconds, elapsed) > playerRatio)
                {
                    opponentsAhead++;
                }
            }

            return Math.Max(1, Math.Min(ParticipantCount, 1 + opponentsAhead));
        }

        private float CalculateDeadlineSeconds()
        {
            var activeFinishTimes = new List<int>(ActiveOpponentCount);
            for (int index = 0; index < ActiveOpponentCount && index < _state.BotFinishSeconds.Count; index++)
            {
                if (_state.BotFinishSeconds[index] > 0)
                {
                    activeFinishTimes.Add(_state.BotFinishSeconds[index]);
                }
            }

            if (activeFinishTimes.Count < RankCutoff)
            {
                return float.PositiveInfinity;
            }

            activeFinishTimes.Sort();
            return activeFinishTimes[RankCutoff - 1];
        }

        private bool ResolveRaceIfRequired()
        {
            if (!IsRaceActive || _state.PendingRank != 0)
            {
                return false;
            }

            if (_state.CollectedTokens >= RequiredTokens || RaceElapsedSeconds >= RaceDeadlineSeconds)
            {
                ResolveRace(CalculateLiveRank());
                return true;
            }

            return false;
        }

        private bool CanForceResult()
        {
            return IsRaceActive && _state.PendingRank == 0;
        }

        private void ResolveRace(int rank)
        {
            if (_state.PendingRank != 0)
            {
                return;
            }

            _state.MutablePendingRank = Math.Max(1, Math.Min(ParticipantCount, rank));
        }

        private bool EvaluateTimeoutInternal()
        {
            bool changed = ResolveRaceIfRequired();
            if (!_schedulePolicy.IsEnabled)
            {
                if (HasEventPayload())
                {
                    ClearEventProgress();
                    _state.MutableEventEndUtcTicks = 0;
                    return true;
                }

                return changed;
            }

            if (!_state.EventStarted || _state.EventEndUtcTicks > NowTicks || _state.PendingEnd)
            {
                return changed;
            }

            if (IsRaceActive && _state.PendingRank == 0 && RaceElapsedSeconds < RaceDeadlineSeconds)
            {
                return changed;
            }

            _state.MutablePendingEnd = true;
            return true;
        }

        private bool HasEventPayload()
        {
            return _state.EventStarted
                || _state.EventEndUtcTicks != 0
                || _state.PendingEnd
                || _state.Round != 1
                || _state.CollectedTokens != 0
                || _state.PrevDisplayedTokens != 0
                || _state.PrevDisplayedMultiplierStep != 0
                || _state.PrevDisplayedElapsedSeconds > 0f
                || _state.MatchStartUtcTicks != 0
                || _state.PendingRank != 0
                || _state.RoadPoints != 0
                || _state.ClaimedRoadPoints != 0
                || _state.FirstPopupShown
                || _state.Opponents.Count != 0
                || _state.BotFinishSeconds.Count != 0
                || _state.BotCurveIndices.Count != 0
                || !string.IsNullOrEmpty(_state.RaceId)
                || !string.IsNullOrEmpty(_state.CatalogVersion)
                || !string.IsNullOrEmpty(_state.BalanceRevision);
        }

        private List<ContentReward> CollectClaimableRoadRewards()
        {
            var totals = new Dictionary<string, long>(StringComparer.Ordinal);
            for (int index = 0; index < EventCatalog.RoadMilestones.Count; index++)
            {
                IceCreamRaceRoadMilestone milestone = EventCatalog.RoadMilestones[index];
                if (milestone.RequiredPoints <= _state.ClaimedRoadPoints
                    || milestone.RequiredPoints > _state.RoadPoints)
                {
                    continue;
                }

                for (int rewardIndex = 0; rewardIndex < milestone.Rewards.Count; rewardIndex++)
                {
                    ContentReward reward = milestone.Rewards[rewardIndex];
                    totals.TryGetValue(reward.RewardId, out long current);
                    totals[reward.RewardId] = checked(current + reward.Amount);
                }
            }

            var rewardIds = new List<string>(totals.Keys);
            rewardIds.Sort(StringComparer.Ordinal);
            var rewards = new List<ContentReward>(rewardIds.Count);
            for (int index = 0; index < rewardIds.Count; index++)
            {
                rewards.Add(new ContentReward(rewardIds[index], totals[rewardIds[index]]));
            }

            return rewards;
        }

        private bool RecoverPendingRewardTransaction()
        {
            EnsureRewardServiceAvailable();

            string transactionId = _state.PendingRewardTransactionId;
            if (string.IsNullOrEmpty(transactionId))
            {
                return false;
            }

            var rewards = new List<ContentReward>(_state.PendingRewards.Count);
            for (int index = 0; index < _state.PendingRewards.Count; index++)
            {
                IceCreamRaceRewardState reward = _state.PendingRewards[index];
                rewards.Add(new ContentReward(reward.RewardId, reward.Amount));
            }

            if (!_rewardService.HasGranted(transactionId))
            {
                _rewardService.GrantOnce(transactionId, rewards);
            }

            if (!_rewardService.HasGranted(transactionId))
            {
                throw new InvalidOperationException("Reward service did not persist the pending Ice Cream Race transaction.");
            }

            int claimedTarget = Math.Max(_state.ClaimedRoadPoints, _state.PendingRewardClaimedRoadPoints);
            _state.MutableClaimedRoadPoints = Math.Min(_state.RoadPoints, claimedTarget);
            _state.MutablePendingRewardTransactionId = string.Empty;
            _state.MutablePendingRewardClaimedRoadPoints = 0;
            _state.MutablePendingRewards.Clear();

            bool completed = _state.ClaimedRoadPoints >= EventCatalog.FinalRoadPoints;
            if (completed)
            {
                long completedUntil = _state.EventEndUtcTicks > NowTicks
                    ? _state.EventEndUtcTicks
                    : GetCompletionFallbackTicks();
                ClearEventProgress();
                _state.MutableCompletedUntilUtcTicks = completedUntil;
            }

            Persist(true, true);
            return completed;
        }

        private void EnsureRewardServiceAvailable()
        {
            if (!_rewardService.IsAvailable)
            {
                throw new NotSupportedException(
                    "The configured reward service cannot safely grant or recover Ice Cream Race rewards.");
            }
        }

        private void ClearRace()
        {
            _state.MutableMatchStartUtcTicks = 0;
            _state.MutableCollectedTokens = 0;
            _state.MutablePendingRank = 0;
            _state.MutablePrevDisplayedTokens = 0;
            _state.MutablePrevDisplayedElapsedSeconds = 0f;
            _state.MutableBotFinishSeconds.Clear();
            _state.MutableRaceId = string.Empty;
            ClearMatchmaking();
        }

        private void ClearMatchmaking()
        {
            _state.MutableOpponents.Clear();
            _state.MutableBotCurveIndices.Clear();
        }

        private void ClearEventProgress()
        {
            ClearRace();
            _state.MutableRound = 1;
            _state.MutableRoadPoints = 0;
            _state.MutableClaimedRoadPoints = 0;
            _state.MutablePrevDisplayedMultiplierStep = 0;
            _state.MutableEventStarted = false;
            _state.MutablePendingEnd = false;
            _state.MutableFirstPopupShown = false;
            _state.MutableCatalogVersion = string.Empty;
            _state.MutableBalanceRevision = string.Empty;
            _resolvedEventCatalog = null;
        }

        private void PinCurrentCatalog()
        {
            _state.MutableCatalogVersion = _catalog.CatalogVersion;
            _state.MutableBalanceRevision = _catalog.BalanceRevision;
            _resolvedEventCatalog = _catalog;
        }

        private void PinCurrentCatalogIfMissing()
        {
            if (string.IsNullOrEmpty(_state.CatalogVersion)
                && string.IsNullOrEmpty(_state.BalanceRevision))
            {
                PinCurrentCatalog();
                return;
            }

            ResolvePinnedCatalog();
        }

        private void ResolvePinnedCatalog()
        {
            string catalogVersion = _state.CatalogVersion;
            string balanceRevision = _state.BalanceRevision;
            if (string.IsNullOrEmpty(catalogVersion) && string.IsNullOrEmpty(balanceRevision))
            {
                _resolvedEventCatalog = null;
                return;
            }

            if (_resolvedEventCatalog != null
                && string.Equals(_resolvedEventCatalog.CatalogVersion, catalogVersion, StringComparison.Ordinal)
                && string.Equals(_resolvedEventCatalog.BalanceRevision, balanceRevision, StringComparison.Ordinal))
            {
                return;
            }

            if (string.Equals(_catalog.CatalogVersion, catalogVersion, StringComparison.Ordinal)
                && string.Equals(_catalog.BalanceRevision, balanceRevision, StringComparison.Ordinal))
            {
                _resolvedEventCatalog = _catalog;
                return;
            }

            if (_catalogResolver.TryResolve(catalogVersion, balanceRevision, out IceCreamRaceCatalog resolved)
                && resolved != null
                && string.Equals(resolved.CatalogVersion, catalogVersion, StringComparison.Ordinal)
                && string.Equals(resolved.BalanceRevision, balanceRevision, StringComparison.Ordinal))
            {
                _resolvedEventCatalog = resolved;
                return;
            }

            throw new InvalidOperationException(
                $"No Ice Cream Race catalog is registered for {catalogVersion}/{balanceRevision}. " +
                "Register the recorded catalog or migrate the saved event explicitly.");
        }

        private void Persist(bool notify, bool flush = false)
        {
            _stateStore.Save(_contentId, _serializer.Serialize(_state));
            if (flush && _stateStore is IFlushableContentStateStore flushable)
            {
                flushable.Flush();
            }

            if (notify)
            {
                StateChanged?.Invoke(State);
            }
        }
    }
}
