using System;
using System.Collections.Generic;

namespace ActionFit.IceCreamRace
{
    public interface IIceCreamRaceClock
    {
        DateTime UtcNow { get; }
    }

    public interface IIceCreamRaceRandom
    {
        int Range(int minInclusive, int maxExclusive);
    }

    public interface IIceCreamRaceOpponentProvider
    {
        IReadOnlyList<IceCreamRaceOpponent> CreateOpponents(int count, IIceCreamRaceRandom random);
    }

    public interface IIceCreamRaceAccessPolicy
    {
        bool IsAccessAllowed { get; }
    }

    /// <summary>
    /// Supplies the live entry schedule independently from the immutable race-balance catalog.
    /// A policy with no active days is an explicit content kill switch.
    /// </summary>
    public interface IIceCreamRaceSchedulePolicy
    {
        bool IsEnabled { get; }
        bool IsActiveDay(DayOfWeek dayOfWeek);
        DateTime GetActiveWindowEndUtc(DateTime utcNow);
    }

    /// <summary>Resolves an immutable catalog snapshot recorded by an in-progress event.</summary>
    public interface IIceCreamRaceCatalogResolver
    {
        bool TryResolve(
            string catalogVersion,
            string balanceRevision,
            out IceCreamRaceCatalog catalog);
    }

    public sealed class AlwaysAllowedIceCreamRaceAccessPolicy : IIceCreamRaceAccessPolicy
    {
        public static AlwaysAllowedIceCreamRaceAccessPolicy Instance { get; } =
            new AlwaysAllowedIceCreamRaceAccessPolicy();

        private AlwaysAllowedIceCreamRaceAccessPolicy()
        {
        }

        public bool IsAccessAllowed => true;
    }

    public sealed class SystemIceCreamRaceClock : IIceCreamRaceClock
    {
        public static SystemIceCreamRaceClock Instance { get; } = new SystemIceCreamRaceClock();

        private SystemIceCreamRaceClock()
        {
        }

        public DateTime UtcNow => DateTime.UtcNow;
    }

    public sealed class SystemIceCreamRaceRandom : IIceCreamRaceRandom
    {
        private readonly object _sync = new object();
        private readonly Random _random;

        public SystemIceCreamRaceRandom()
            : this(new Random())
        {
        }

        public SystemIceCreamRaceRandom(int seed)
            : this(new Random(seed))
        {
        }

        private SystemIceCreamRaceRandom(Random random)
        {
            _random = random;
        }

        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "Maximum must be greater than minimum.");
            }

            lock (_sync)
            {
                return _random.Next(minInclusive, maxExclusive);
            }
        }
    }

    public sealed class DefaultIceCreamRaceOpponentProvider : IIceCreamRaceOpponentProvider
    {
        private static readonly string[] Names =
        {
            "하늘",
            "은하수정원",
            "느긋한파도",
            "고요한호수",
            "빛나는여름",
            "용감한하늘",
            "LunarSpirit73",
            "NeonSpirit311"
        };

        public static DefaultIceCreamRaceOpponentProvider Instance { get; } =
            new DefaultIceCreamRaceOpponentProvider();

        private DefaultIceCreamRaceOpponentProvider()
        {
        }

        public IReadOnlyList<IceCreamRaceOpponent> CreateOpponents(int count, IIceCreamRaceRandom random)
        {
            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            var opponents = new List<IceCreamRaceOpponent>(count);
            for (int index = 0; index < count; index++)
            {
                int nameIndex = random.Range(0, Names.Length);
                opponents.Add(new IceCreamRaceOpponent(
                    $"local-bot-{index + 1}",
                    Names[nameIndex],
                    $"profile-{random.Range(1, 13)}",
                    "frame-basic"));
            }

            return opponents;
        }
    }
}
