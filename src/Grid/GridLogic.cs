using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Greenline
{
    // The game-free logic of the pot grid: the pot states, the badges, the floor layout, and the pot
    // data that page.js reads. No game or BepInEx type here, so the tests can link this file.
    public static class GridLogic
    {
        public enum PotState { Empty, Growing, Mature, Withered, Poor }

        // The bits of the game's PlantAnomaly flags.
        public const int Pest = 1, Weed = 2, Drought = 4, LightLow = 8, LightHigh = 16, Frost = 32;

        // The problems that get a badge, the most urgent first: Pest and Frost kill the crop, the others
        // only stop its growth. LightHigh has no game icon, so it gets no badge.
        private static readonly (int Bit, string Badge)[] BadgeOrder =
        {
            (Pest, "pest"), (Frost, "frost"), (Drought, "drought"), (Weed, "weed"), (LightLow, "lightLow"),
        };

        // The badge of a cell: the withered badge for a withered crop, else the most urgent problem of
        // a crop, else none. An empty or Poor pot has no crop, so it has no badge.
        public static string Badge(int flags, PotState state)
        {
            if (state == PotState.Withered) return "withered";
            if (state == PotState.Empty || state == PotState.Poor) return "";
            foreach (var (bit, badge) in BadgeOrder)
                if ((flags & bit) != 0) return badge;
            return "";
        }

        // The time to mature of a growing crop. A problem stops the growth: the game's remaining time still
        // counts down, and the game gives the stalled time back when the problem is cleared (ClearAnomaly
        // moves the planting start by it). So the time to mature of a stalled crop adds the stalled time,
        // and stays still.
        public static int TimeToMature(int remainSeconds, int flags, int stallStartSeconds, int nowSeconds)
        {
            if (flags == 0 || stallStartSeconds <= 0) return remainSeconds;
            return remainSeconds + System.Math.Max(0, nowSeconds - stallStartSeconds);
        }

        // One placed pot, read out of the game by PotGrid.
        public sealed class Pot
        {
            public long PotId;
            // The id of the pot's row in the game's HUD plant list, 0 when the pot has no row.
            public long RowId;
            // The id that a click sends to the game to go to the pot: the ShowInstanceId of the pot.
            public long ClickId;
            public int Floor;
            public string PotName = "", PotIcon = "";
            public int PotSize;
            public PotState State;
            public string CropName = "", CropIcon = "";
            public int CropSize;
            public int Flags;
            public int GrowRemainSeconds;
            // The full growth time of the crop, for the growth bar of the hover card.
            public int GrowTotalSeconds;
            public string FertName = "", FertIcon = "";
            public bool AutoFert;
            public bool AutoReplant = true;
        }

        // One floor of the grid: the floor id, its name, and its pot ids in cell order.
        public sealed class FloorLine
        {
            public int Floor;
            public string Label = "";
            public List<long> Pots = new List<long>();
        }

        // The floors in the floor-button order of the game, then the floors that the buttons do not
        // name, by id. Inside a floor, the pots by id: the id is fixed while the pot stays placed, so a
        // pot keeps its cell when its state changes.
        public static List<FloorLine> LayoutFloors(IEnumerable<Pot> pots, IList<int> floorOrder)
        {
            return pots.GroupBy(p => p.Floor)
                .OrderBy(g => floorOrder.Contains(g.Key) ? floorOrder.IndexOf(g.Key) : int.MaxValue)
                .ThenBy(g => g.Key)
                .Select(g => new FloorLine { Floor = g.Key, Pots = g.Select(p => p.PotId).OrderBy(id => id).ToList() })
                .ToList();
        }

        // The problems that the hover card lists, in the badge order, then LightHigh.
        private static readonly (int Bit, string Name)[] ProblemOrder =
        {
            (Pest, "pest"), (Frost, "frost"), (Drought, "drought"), (Weed, "weed"), (LightLow, "lightLow"), (LightHigh, "lightHigh"),
        };

        private static string StateName(PotState state)
        {
            switch (state)
            {
                case PotState.Growing: return "growing";
                case PotState.Mature: return "mature";
                case PotState.Withered: return "withered";
                case PotState.Poor: return "poor";
                default: return "empty";
            }
        }

        // The pot data that page.js reads (the JSON contract of the design). A problem is listed only
        // for a live crop: a withered crop is dead, and an empty or Poor pot has no crop. There is no
        // JSON library that the plugin can use (netstandard2.1 has no System.Text.Json), so the text is
        // built by hand.
        // `patrol` is whether the game's Patrol is available, and `pendingChores` the count of chores
        // that wait, the same as the game's "Tend ×N" quick action.
        public static string ToJson(string lang, IEnumerable<KeyValuePair<string, string>> words, IEnumerable<FloorLine> floors, IEnumerable<Pot> pots,
            bool patrol, int pendingChores)
        {
            var sb = new StringBuilder();
            sb.Append("{\"lang\":").Append(PageJson.Str(lang))
                .Append(",\"patrol\":").Append(patrol ? "true" : "false")
                .Append(",\"pendingChores\":").Append(pendingChores)
                .Append(",\"words\":{");
            bool first = true;
            foreach (var w in words)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append(PageJson.Str(w.Key)).Append(':').Append(PageJson.Str(w.Value));
            }
            sb.Append("},\"floors\":[");
            first = true;
            foreach (var f in floors)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append("{\"id\":").Append(f.Floor).Append(",\"label\":").Append(PageJson.Str(f.Label))
                    .Append(",\"cells\":[").Append(string.Join(",", f.Pots)).Append("]}");
            }
            sb.Append("],\"pots\":{");
            first = true;
            foreach (var p in pots.OrderBy(p => p.PotId))
            {
                if (!first) sb.Append(',');
                first = false;
                bool live = p.State == PotState.Growing || p.State == PotState.Mature;
                var problems = live ? ProblemOrder.Where(x => (p.Flags & x.Bit) != 0).Select(x => PageJson.Str(x.Name)) : Enumerable.Empty<string>();
                sb.Append('"').Append(p.PotId).Append("\":{")
                    .Append("\"rowId\":").Append(p.RowId)
                    .Append(",\"clickId\":").Append(p.ClickId)
                    .Append(",\"potName\":").Append(PageJson.Str(p.PotName))
                    .Append(",\"potIcon\":").Append(PageJson.Str(p.PotIcon))
                    .Append(",\"potSize\":").Append(p.PotSize)
                    .Append(",\"state\":").Append(PageJson.Str(StateName(p.State)))
                    .Append(",\"cropName\":").Append(PageJson.Str(p.CropName))
                    .Append(",\"cropIcon\":").Append(PageJson.Str(p.CropIcon))
                    .Append(",\"cropSize\":").Append(p.CropSize)
                    .Append(",\"problems\":[").Append(string.Join(",", problems)).Append(']')
                    .Append(",\"badge\":").Append(PageJson.Str(Badge(p.Flags, p.State)))
                    .Append(",\"growRemainSeconds\":").Append(p.GrowRemainSeconds)
                    .Append(",\"growTotalSeconds\":").Append(p.GrowTotalSeconds)
                    .Append(",\"fertName\":").Append(PageJson.Str(p.FertName))
                    .Append(",\"fertIcon\":").Append(PageJson.Str(p.FertIcon))
                    .Append(",\"autoFert\":").Append(p.AutoFert ? "true" : "false")
                    .Append(",\"autoReplant\":").Append(p.AutoReplant ? "true" : "false")
                    .Append('}');
            }
            sb.Append("}}");
            return sb.ToString();
        }
    }
}
