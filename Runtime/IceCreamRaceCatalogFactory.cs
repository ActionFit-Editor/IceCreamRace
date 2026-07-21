using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ActionFit.Content;
using UnityEngine;

namespace ActionFit.IceCreamRace
{
    /// <summary>Canonical CSV text required to build the standalone Ice Cream Race balance.</summary>
    public sealed class IceCreamRaceCatalogCsvData
    {
        public IceCreamRaceCatalogCsvData(
            string eventSettings,
            string orderRewards,
            string rewardRoad,
            string rounds,
            string tuning)
        {
            EventSettings = RequireText(eventSettings, nameof(eventSettings));
            OrderRewards = RequireText(orderRewards, nameof(orderRewards));
            RewardRoad = RequireText(rewardRoad, nameof(rewardRoad));
            Rounds = RequireText(rounds, nameof(rounds));
            Tuning = RequireText(tuning, nameof(tuning));
        }

        public string EventSettings { get; }
        public string OrderRewards { get; }
        public string RewardRoad { get; }
        public string Rounds { get; }
        public string Tuning { get; }

        private static string RequireText(string value, string parameterName)
        {
            return string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException("CSV text must not be empty.", parameterName)
                : value;
        }
    }

    public sealed class IceCreamRaceMergeRewardDefinition
    {
        public IceCreamRaceMergeRewardDefinition(int level, int progress, float chance)
        {
            if (level < 1) throw new ArgumentOutOfRangeException(nameof(level));
            if (progress <= 0) throw new ArgumentOutOfRangeException(nameof(progress));
            if (chance < 0f || chance > 1f) throw new ArgumentOutOfRangeException(nameof(chance));
            Level = level;
            Progress = progress;
            Chance = chance;
        }

        public int Level { get; }
        public int Progress { get; }
        public float Chance { get; }
    }

    /// <summary>Complete importer-independent balance used by standalone compositions.</summary>
    public sealed class IceCreamRaceStandaloneCatalog
    {
        private const int ChanceResolution = 1_000_000;
        private readonly IReadOnlyDictionary<int, IceCreamRaceMergeRewardDefinition> _mergeRewards;

        internal IceCreamRaceStandaloneCatalog(
            IceCreamRaceCatalog catalog,
            IDictionary<int, IceCreamRaceMergeRewardDefinition> mergeRewards)
        {
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _mergeRewards = new ReadOnlyDictionary<int, IceCreamRaceMergeRewardDefinition>(
                new Dictionary<int, IceCreamRaceMergeRewardDefinition>(mergeRewards));
        }

        public IceCreamRaceCatalog Catalog { get; }
        public IReadOnlyDictionary<int, IceCreamRaceMergeRewardDefinition> MergeRewards => _mergeRewards;

        public int RollMergeProgress(int level, IIceCreamRaceRandom random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (!_mergeRewards.TryGetValue(level, out IceCreamRaceMergeRewardDefinition reward)
                || reward.Chance <= 0f)
            {
                return 0;
            }
            if (reward.Chance >= 1f) return reward.Progress;

            int threshold = Mathf.RoundToInt(reward.Chance * ChanceResolution);
            return random.Range(0, ChanceResolution) < threshold ? reward.Progress : 0;
        }
    }

    /// <summary>Builds the package catalog directly from the canonical CSV text.</summary>
    public static class IceCreamRaceCatalogFactory
    {
        public const string DefaultCatalogVersion = "cat-merge-icecream-csv-v1";
        public const string DefaultBalanceRevision = "balance-v1-20260720";

        public static IceCreamRaceStandaloneCatalog Create(IceCreamRaceCatalogCsvData csv)
        {
            return Create(csv, DefaultCatalogVersion, DefaultBalanceRevision);
        }

        public static IceCreamRaceStandaloneCatalog Create(
            IceCreamRaceCatalogCsvData csv,
            string catalogVersion,
            string balanceRevision)
        {
            if (csv == null) throw new ArgumentNullException(nameof(csv));

            List<DayOfWeek> activeDays = ParseActiveDays(csv.EventSettings);
            Dictionary<int, int> orderTokens = ParseOrderRewards(csv.OrderRewards);
            List<IceCreamRaceRoadMilestone> milestones = ParseRoad(csv.RewardRoad);
            List<IceCreamRaceRoundDefinition> rounds = ParseRounds(csv.Rounds);
            Dictionary<int, IceCreamRaceMergeRewardDefinition> tuning = ParseTuning(csv.Tuning);

            IceCreamRaceCatalog catalog = IceCreamRaceCatalog.Create(
                catalogVersion,
                balanceRevision,
                activeDays,
                orderTokens,
                rounds,
                milestones,
                BuildCanonicalBotCurves(),
                new IceCreamRaceMergeTuning(1, 0f, 0));
            return new IceCreamRaceStandaloneCatalog(catalog, tuning);
        }

        private static List<DayOfWeek> ParseActiveDays(string csv)
        {
            CanonicalCsvTable table = CanonicalCsvTable.Parse(csv, "EventSettings");
            if (table.Rows.Count != 1)
                throw new FormatException("IceCreamRace EventSettings must contain exactly one row.");
            return CanonicalCsvValue.ParseDays(table.Value(table.Rows[0], "ActiveDays"));
        }

        private static Dictionary<int, int> ParseOrderRewards(string csv)
        {
            CanonicalCsvTable table = CanonicalCsvTable.Parse(csv, "OrderReward");
            var result = new Dictionary<int, int>();
            for (int index = 0; index < table.Rows.Count; index++)
            {
                IReadOnlyList<string> row = table.Rows[index];
                int level = CanonicalCsvValue.ParseInt(table.Value(row, "Level"), "OrderReward.Level");
                int progress = CanonicalCsvValue.ParseInt(table.Value(row, "Progress"), "OrderReward.Progress");
                if (level < 0 || progress <= 0)
                    throw new FormatException("IceCreamRace OrderReward values are invalid.");
                if (!result.TryAdd(level, progress))
                    throw new FormatException($"IceCreamRace OrderReward contains duplicate level {level}.");
            }
            if (result.Count == 0)
                throw new FormatException("IceCreamRace OrderReward must contain at least one row.");
            return result;
        }

        private static List<IceCreamRaceRoadMilestone> ParseRoad(string csv)
        {
            CanonicalCsvTable table = CanonicalCsvTable.Parse(csv, "RewardRoad");
            var rows = new List<(int Stage, int Required, IReadOnlyList<ContentReward> Rewards)>();
            for (int index = 0; index < table.Rows.Count; index++)
            {
                IReadOnlyList<string> row = table.Rows[index];
                rows.Add((
                    CanonicalCsvValue.ParseInt(table.Value(row, "Stage"), "RewardRoad.Stage"),
                    CanonicalCsvValue.ParseInt(table.Value(row, "RequiredProgress"), "RewardRoad.RequiredProgress"),
                    CanonicalCsvValue.ParseRewards(table.Value(row, "Rewards"), "RewardRoad.Rewards", requireAny: true)));
            }
            rows.Sort((left, right) => left.Stage.CompareTo(right.Stage));

            var result = new List<IceCreamRaceRoadMilestone>(rows.Count);
            for (int index = 0; index < rows.Count; index++)
            {
                int expectedStage = index + 1;
                if (rows[index].Stage != expectedStage)
                    throw new FormatException($"IceCreamRace RewardRoad expected stage {expectedStage}.");
                result.Add(new IceCreamRaceRoadMilestone(
                    rows[index].Stage,
                    rows[index].Required,
                    rows[index].Rewards,
                    index == rows.Count - 1));
            }
            return result;
        }

        private static List<IceCreamRaceRoundDefinition> ParseRounds(string csv)
        {
            CanonicalCsvTable table = CanonicalCsvTable.Parse(csv, "Round");
            var result = new List<IceCreamRaceRoundDefinition>(table.Rows.Count);
            for (int index = 0; index < table.Rows.Count; index++)
            {
                IReadOnlyList<string> row = table.Rows[index];
                int round = CanonicalCsvValue.ParseInt(table.Value(row, "Round"), "Round.Round");
                result.Add(new IceCreamRaceRoundDefinition(
                    round,
                    CanonicalCsvValue.ParseInt(table.Value(row, "RequireTokens"), "Round.RequireTokens"),
                    new[]
                    {
                        CanonicalCsvValue.ParseInt(table.Value(row, "Bot1Min"), "Round.Bot1Min"),
                        CanonicalCsvValue.ParseInt(table.Value(row, "Bot2Min"), "Round.Bot2Min"),
                        CanonicalCsvValue.ParseInt(table.Value(row, "Bot3Min"), "Round.Bot3Min"),
                        CanonicalCsvValue.ParseInt(table.Value(row, "Bot4Min"), "Round.Bot4Min")
                    },
                    new[]
                    {
                        CanonicalCsvValue.ParseInt(table.Value(row, "Bot1Max"), "Round.Bot1Max"),
                        CanonicalCsvValue.ParseInt(table.Value(row, "Bot2Max"), "Round.Bot2Max"),
                        CanonicalCsvValue.ParseInt(table.Value(row, "Bot3Max"), "Round.Bot3Max"),
                        CanonicalCsvValue.ParseInt(table.Value(row, "Bot4Max"), "Round.Bot4Max")
                    },
                    CanonicalCsvValue.ParseInt(table.Value(row, "RewardPoints"), "Round.RewardPoints")));
            }
            result.Sort((left, right) => left.Round.CompareTo(right.Round));
            for (int index = 0; index < result.Count; index++)
            {
                if (result[index].Round != index + 1)
                    throw new FormatException($"IceCreamRace Round expected row {index + 1}.");
            }
            return result;
        }

        private static Dictionary<int, IceCreamRaceMergeRewardDefinition> ParseTuning(string csv)
        {
            CanonicalCsvTable table = CanonicalCsvTable.Parse(csv, "Tuning");
            var result = new Dictionary<int, IceCreamRaceMergeRewardDefinition>();
            for (int index = 0; index < table.Rows.Count; index++)
            {
                IReadOnlyList<string> row = table.Rows[index];
                int level = CanonicalCsvValue.ParseInt(table.Value(row, "Level"), "Tuning.Level");
                int progress = CanonicalCsvValue.ParseInt(table.Value(row, "Progress"), "Tuning.Progress");
                float chance = CanonicalCsvValue.ParseFloat(
                    table.Value(row, "ProgressChance"),
                    "Tuning.ProgressChance");
                if (!result.TryAdd(level, new IceCreamRaceMergeRewardDefinition(level, progress, chance)))
                    throw new FormatException($"IceCreamRace Tuning contains duplicate level {level}.");
            }
            if (result.Count == 0)
                throw new FormatException("IceCreamRace Tuning must contain at least one row.");
            return result;
        }

        private static List<AnimationCurve> BuildCanonicalBotCurves()
        {
            return new List<AnimationCurve>
            {
                LinearCurve(new Vector2(0f, 0f), new Vector2(0.25f, 0.35f), new Vector2(0.6f, 0.7f), new Vector2(1f, 1f)),
                LinearCurve(new Vector2(0f, 0f), new Vector2(0.4f, 0.2f), new Vector2(0.75f, 0.55f), new Vector2(1f, 1f)),
                LinearCurve(new Vector2(0f, 0f), new Vector2(1f, 1f)),
                LinearCurve(new Vector2(0f, 0f), new Vector2(0.2f, 0.12f), new Vector2(0.45f, 0.55f), new Vector2(0.7f, 0.65f), new Vector2(1f, 1f))
            };
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
            return new AnimationCurve(keys)
            {
                preWrapMode = WrapMode.ClampForever,
                postWrapMode = WrapMode.ClampForever
            };
        }

        private static float Slope(Vector2 from, Vector2 to)
        {
            return (to.y - from.y) / (to.x - from.x);
        }
    }

    internal sealed class CanonicalCsvTable
    {
        private readonly Dictionary<string, int> _columns;

        private CanonicalCsvTable(string name, Dictionary<string, int> columns, List<IReadOnlyList<string>> rows)
        {
            Name = name;
            _columns = columns;
            Rows = rows;
        }

        public string Name { get; }
        public IReadOnlyList<IReadOnlyList<string>> Rows { get; }

        public string Value(IReadOnlyList<string> row, string column)
        {
            if (!_columns.TryGetValue(column, out int index))
                throw new FormatException($"{Name} is missing column '{column}'.");
            return index < row.Count ? row[index] : string.Empty;
        }

        public static CanonicalCsvTable Parse(string text, string name)
        {
            List<List<string>> records = ParseRecords(text);
            if (records.Count < 3)
                throw new FormatException($"{name} must contain the three canonical header rows.");
            var columns = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int index = 0; index < records[2].Count; index++)
            {
                string field = records[2][index].Trim().TrimStart('\uFEFF');
                int annotation = field.IndexOf('(');
                string column = (annotation >= 0 ? field.Substring(0, annotation) : field).Trim();
                if (column.Length == 0 || !columns.TryAdd(column, index))
                    throw new FormatException($"{name} contains an empty or duplicate column name.");
            }

            var rows = new List<IReadOnlyList<string>>();
            for (int index = 3; index < records.Count; index++)
            {
                bool hasValue = false;
                for (int fieldIndex = 0; fieldIndex < records[index].Count; fieldIndex++)
                {
                    if (!string.IsNullOrWhiteSpace(records[index][fieldIndex]))
                    {
                        hasValue = true;
                        break;
                    }
                }
                if (hasValue) rows.Add(records[index]);
            }
            return new CanonicalCsvTable(name, columns, rows);
        }

        private static List<List<string>> ParseRecords(string text)
        {
            var records = new List<List<string>>();
            var record = new List<string>();
            var field = new StringBuilder();
            bool quoted = false;
            for (int index = 0; index < text.Length; index++)
            {
                char character = text[index];
                if (quoted)
                {
                    if (character == '"')
                    {
                        if (index + 1 < text.Length && text[index + 1] == '"')
                        {
                            field.Append('"');
                            index++;
                        }
                        else quoted = false;
                    }
                    else field.Append(character);
                    continue;
                }
                if (character == '"' && field.Length == 0) quoted = true;
                else if (character == ',')
                {
                    record.Add(field.ToString());
                    field.Clear();
                }
                else if (character == '\r' || character == '\n')
                {
                    if (character == '\r' && index + 1 < text.Length && text[index + 1] == '\n') index++;
                    record.Add(field.ToString());
                    field.Clear();
                    records.Add(record);
                    record = new List<string>();
                }
                else field.Append(character);
            }
            if (quoted) throw new FormatException("CSV contains an unterminated quoted field.");
            if (field.Length > 0 || record.Count > 0)
            {
                record.Add(field.ToString());
                records.Add(record);
            }
            return records;
        }
    }

    internal static class CanonicalCsvValue
    {
        public static int ParseInt(string value, string field)
        {
            if (!int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
                throw new FormatException($"{field} must be an integer.");
            return result;
        }

        public static float ParseFloat(string value, string field)
        {
            if (!float.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
                throw new FormatException($"{field} must be a float.");
            return result;
        }

        public static List<DayOfWeek> ParseDays(string value)
        {
            var result = new List<DayOfWeek>();
            string[] values = value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int index = 0; index < values.Length; index++)
            {
                if (!Enum.TryParse(values[index].Trim(), false, out DayOfWeek day)
                    || !Enum.IsDefined(typeof(DayOfWeek), day))
                    throw new FormatException($"Unsupported active day '{values[index]}'.");
                if (!result.Contains(day)) result.Add(day);
            }
            return result;
        }

        public static IReadOnlyList<ContentReward> ParseRewards(string value, string field, bool requireAny)
        {
            value = value.Trim();
            var rewards = new List<ContentReward>();
            if (value.Length == 0)
            {
                if (requireAny) throw new FormatException($"{field} requires at least one reward.");
                return rewards;
            }
            if (value.Length < 2 || value[0] != '[' || value[value.Length - 1] != ']')
                throw new FormatException($"{field} must use a reward array.");
            int index = 1;
            while (index < value.Length - 1)
            {
                while (index < value.Length - 1 && (char.IsWhiteSpace(value[index]) || value[index] == ',')) index++;
                if (index >= value.Length - 1) break;
                if (value[index] != '(') throw new FormatException($"{field} contains an invalid reward tuple.");
                int end = value.IndexOf(')', index + 1);
                if (end < 0) throw new FormatException($"{field} contains an unterminated reward tuple.");
                string[] tuple = value.Substring(index + 1, end - index - 1).Split(',');
                if (tuple.Length != 3)
                    throw new FormatException($"{field} reward tuples require type, item ID, and amount.");
                string type = tuple[0].Trim();
                string itemId = tuple[1].Trim();
                int amount = ParseInt(tuple[2], field + ".Amount");
                rewards.Add(new ContentReward(UsesItemKey(type) ? type + "/" + itemId : type, amount));
                index = end + 1;
            }
            if (requireAny && rewards.Count == 0)
                throw new FormatException($"{field} requires at least one reward.");
            return rewards;
        }

        private static bool UsesItemKey(string itemType)
        {
            return itemType == "BoardItem" || itemType == "Pass" || itemType == "Profile" || itemType == "Frame";
        }
    }
}
