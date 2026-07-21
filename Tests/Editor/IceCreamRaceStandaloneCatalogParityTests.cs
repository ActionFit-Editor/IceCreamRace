using System;
using System.IO;
using NUnit.Framework;

namespace ActionFit.IceCreamRace.Tests
{
    public sealed class IceCreamRaceStandaloneCatalogParityTests
    {
        [Test]
        public void CanonicalCsvFactory_ConsumesEveryTableAndPreservesReleasedBalance()
        {
            IceCreamRaceStandaloneCatalog standalone = IceCreamRaceCatalogFactory.Create(ReadCsv());
            IceCreamRaceCatalog catalog = standalone.Catalog;

            Assert.That(catalog.CatalogVersion, Is.EqualTo(IceCreamRaceCatalogFactory.DefaultCatalogVersion));
            Assert.That(catalog.BalanceRevision, Is.EqualTo(IceCreamRaceCatalogFactory.DefaultBalanceRevision));
            Assert.That(catalog.ActiveDays, Is.EquivalentTo(new[] { DayOfWeek.Tuesday, DayOfWeek.Wednesday }));
            Assert.That(catalog.GetOrderTokens(3), Is.EqualTo(3));
            Assert.That(catalog.GetOrderTokens(12), Is.EqualTo(120));
            Assert.That(catalog.Rounds, Has.Count.EqualTo(10));
            Assert.That(catalog.GetRound(1).RequiredTokens, Is.EqualTo(120));
            Assert.That(catalog.GetRound(10).RequiredTokens, Is.EqualTo(390));
            Assert.That(catalog.RoadMilestones, Has.Count.EqualTo(20));
            Assert.That(catalog.RoadMilestones[0].Rewards[0].RewardId, Is.EqualTo("Energy"));
            Assert.That(catalog.RoadMilestones[1].Rewards[0].RewardId, Is.EqualTo("BoardItem/70003_4"));
            Assert.That(catalog.RoadMilestones[19].IsUltimate, Is.True);
            Assert.That(catalog.BotProgressCurves, Has.Count.EqualTo(4));
            Assert.That(catalog.BotProgressCurves[0].Evaluate(0.25f), Is.EqualTo(0.35f).Within(0.0001f));
            Assert.That(catalog.BotProgressCurves[3].Evaluate(0.45f), Is.EqualTo(0.55f).Within(0.0001f));

            Assert.That(standalone.MergeRewards, Has.Count.EqualTo(8));
            Assert.That(standalone.RollMergeProgress(4, new BoundaryRandom(false)), Is.EqualTo(5));
            Assert.That(standalone.RollMergeProgress(4, new BoundaryRandom(true)), Is.Zero);
            Assert.That(standalone.RollMergeProgress(3, new BoundaryRandom(false)), Is.Zero);
        }

        [Test]
        public void CanonicalCsvFactory_DuplicateRoundFailsClosed()
        {
            IceCreamRaceCatalogCsvData valid = ReadCsv();
            var malformed = new IceCreamRaceCatalogCsvData(
                valid.EventSettings,
                valid.OrderRewards,
                valid.RewardRoad,
                valid.Rounds.Replace(
                    "2,150,180,240,240,300,300,360,360,480,40",
                    "1,150,180,240,240,300,300,360,360,480,40"),
                valid.Tuning);

            Assert.Throws<FormatException>(() => IceCreamRaceCatalogFactory.Create(malformed));
        }

        private static IceCreamRaceCatalogCsvData ReadCsv()
        {
            return new IceCreamRaceCatalogCsvData(
                Read("IceCreamRace_EventSettings.csv"),
                Read("IceCreamRace_OrderReward.csv"),
                Read("IceCreamRace_RewardRoad.csv"),
                Read("IceCreamRace_Round.csv"),
                Read("IceCreamRace_Tuning.csv"));
        }

        private static string Read(string fileName)
        {
            return File.ReadAllText(Path.Combine(
                "Packages/com.actionfit.icecream-race/Data/CSV",
                fileName));
        }

        private sealed class BoundaryRandom : IIceCreamRaceRandom
        {
            private readonly bool _maximum;

            public BoundaryRandom(bool maximum)
            {
                _maximum = maximum;
            }

            public int Range(int minInclusive, int maxExclusive)
            {
                return _maximum ? maxExclusive - 1 : minInclusive;
            }
        }
    }
}
