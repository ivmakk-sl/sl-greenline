using System.Collections.Generic;
using System.Linq;
using System.Text;
using SlShared.FloorGrid;
using SlShared.Json;

namespace Greenline
{
    // The game-free logic of the pot grid: the pot states, the badges, the layout by floor and climate area, and the pot
    // data that page.js reads. No game or BepInEx type here, so the tests can link this file.
    public static class GridLogic
    {
        public enum PotState { Empty, Growing, Mature, Withered, Poor }

        // The most cells of one line of the grid (--gl-fg-max-cols of tokens.css).
        public const int CellsPerLine = 7;

        // The bits of the game's PlantAnomaly flags. NeedFert is a research crop that did not get its
        // fertilizer round.
        public const int Pest = 1, Weed = 2, Drought = 4, LightLow = 8, LightHigh = 16, Frost = 32, NeedFert = 256;

        // The problems that get a badge, the most urgent first: Pest and Frost kill the crop, the others
        // only stop its growth. LightHigh has no game icon, so it gets no badge.
        private static readonly (int Bit, string Badge)[] BadgeOrder =
        {
            (Pest, "pest"), (Frost, "frost"), (Drought, "drought"), (Weed, "weed"), (LightLow, "lightLow"), (NeedFert, "needFert"),
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

        // The dose of a part dose as "<count>/<pot size>", or "" for a full dose or no fertilizer. The
        // game keeps the count that the planting was short of (PlantFertMissing).
        public static string FertDose(int fertItem, int fertMissing, int potSize)
        {
            if (fertItem <= 0 || fertMissing <= 0 || fertMissing >= potSize) return "";
            return $"{potSize - fertMissing}/{potSize}";
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
            // The climate area of the pot (an EnvArea id of the game), 0 when the mod cannot read it.
            public int Area;
            public string PotName = "", PotIcon = "";
            public int PotSize;
            public PotState State;
            public string CropName = "", CropIcon = "";
            public int CropSize;
            // A research crop: Pest and Frost do not kill it, and it has no harvest window.
            public bool NeverWithers;
            public int Flags;
            public int GrowRemainSeconds;
            // The full growth time of the crop, for the growth bar of the hover card.
            public int GrowTotalSeconds;
            public string FertName = "", FertIcon = "";
            // The dose of a part dose ("1/4"), or "" (FertDose).
            public string FertDose = "";
            public bool AutoFert;
            public bool AutoReplant = true;
        }

        // A floor line (IsFloor) or a zone line of the grid: its key for the page, its grid row, its floor, its climate
        // area (for a floor line, the main area of the floor), and its name, which PotGrid sets.
        public sealed class Line
        {
            public string Key = "";
            public bool IsFloor;
            public int Floor;
            public int Area;
            public int Row;
            public string Name = "";
        }

        // The values of one climate area for the lines of the grid: its name and the texts of AreaFormat.
        public sealed class AreaValues
        {
            public string Name = "";
            public string Light = "";
            public string Temp = "";
        }

        // The grid place of one pot: the row and the column, each from 1.
        public sealed class Cell
        {
            public long PotId;
            public int Row;
            public int Column;
        }

        // The lines in row order, the cells in row and column order, the row count, and the column count.
        public sealed class Grid
        {
            public List<Line> Lines = new List<Line>();
            public List<Cell> Cells = new List<Cell>();
            public int Rows;
            public int Columns = 1;
        }

        // The layout of the pots by floor and by climate area. The floors follow the floor-button order of the game,
        // then the floors that the buttons do not name, by id. The main area of a floor (the lowest area id of its pots:
        // Interior first) has no zone line, and each other area of the floor gets one. Inside an area, the pots by id:
        // the id is fixed while the pot stays placed, so a pot keeps its cell when its state changes.
        public static Grid Layout(IEnumerable<Pot> pots, IReadOnlyList<int> floorOrder)
        {
            var sorted = pots.OrderBy(p => p.PotId).ToList();
            var cells = new List<GridCell>();
            var byKey = new Dictionary<string, long>();
            for (int i = 0; i < sorted.Count; i++)
            {
                string key = sorted[i].PotId.ToString();
                cells.Add(new GridCell { Key = key, Order = i, Floor = sorted[i].Floor, Zone = sorted[i].Area });
                byKey[key] = sorted[i].PotId;
            }
            var main = FloorGridLayout.MainZones(cells);
            var layout = FloorGridLayout.Layout(floorOrder, cells, (floor, zone) => main[floor] == zone, CellsPerLine);

            var grid = new Grid { Rows = layout.Rows, Columns = layout.Columns };
            foreach (var l in layout.Lines)
                grid.Lines.Add(new Line
                {
                    Key = l.IsFloor ? "f" + l.Floor : "z" + l.Floor + ":" + l.Zone,
                    IsFloor = l.IsFloor,
                    Floor = l.Floor,
                    Area = l.IsFloor ? main[l.Floor] : l.Zone,
                    Row = l.Row,
                });
            foreach (var kv in layout.Cells)
                grid.Cells.Add(new Cell { PotId = byKey[kv.Key], Row = kv.Value.Row, Column = kv.Value.Column });
            grid.Cells.Sort((a, b) => a.Row != b.Row ? a.Row.CompareTo(b.Row) : a.Column.CompareTo(b.Column));
            return grid;
        }

        // The problems that the hover card lists: the badge order with LightHigh after LightLow.
        private static readonly (int Bit, string Name)[] ProblemOrder =
        {
            (Pest, "pest"), (Frost, "frost"), (Drought, "drought"), (Weed, "weed"), (LightLow, "lightLow"), (LightHigh, "lightHigh"),
            (NeedFert, "needFert"),
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
        // that wait, the same as the game's "Tend ×N" quick action. `areas` holds the values of each climate
        // area with a pot, by area id (none when the mod cannot read them). `pin` is the pin of the plant list.
        public static string ToJson(string lang, IEnumerable<KeyValuePair<string, string>> words, Grid grid, IEnumerable<Pot> pots,
            bool patrol, int pendingChores, bool replantOn, IReadOnlyDictionary<int, AreaValues> areas = null,
            bool pin = false)
        {
            var sb = new StringBuilder();
            sb.Append("{\"lang\":").Append(JsonText.Str(lang))
                .Append(",\"patrol\":").Append(patrol ? "true" : "false")
                .Append(",\"pendingChores\":").Append(pendingChores)
                .Append(",\"replantOn\":").Append(replantOn ? "true" : "false")
                .Append(",\"pin\":").Append(pin ? "true" : "false")
                .Append(",\"words\":{");
            bool first = true;
            foreach (var w in words)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append(JsonText.Str(w.Key)).Append(':').Append(JsonText.Str(w.Value));
            }
            sb.Append("},\"grid\":{\"cols\":").Append(grid.Columns).Append(",\"rows\":").Append(grid.Rows).Append(",\"lines\":[");
            first = true;
            foreach (var l in grid.Lines)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append("{\"key\":").Append(JsonText.Str(l.Key))
                    .Append(",\"kind\":").Append(l.IsFloor ? "\"floor\"" : "\"zone\"")
                    .Append(",\"row\":").Append(l.Row)
                    .Append(",\"name\":").Append(JsonText.Str(l.Name))
                    .Append(",\"area\":").Append(l.Area).Append('}');
            }
            sb.Append("],\"cells\":[");
            first = true;
            foreach (var c in grid.Cells)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append("{\"pot\":").Append(c.PotId).Append(",\"row\":").Append(c.Row).Append(",\"col\":").Append(c.Column).Append('}');
            }
            sb.Append("]},\"areas\":{");
            first = true;
            if (areas != null)
                foreach (var kv in areas.OrderBy(kv => kv.Key))
                {
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append('"').Append(kv.Key).Append("\":{\"name\":").Append(JsonText.Str(kv.Value.Name))
                        .Append(",\"light\":").Append(JsonText.Str(kv.Value.Light))
                        .Append(",\"temp\":").Append(JsonText.Str(kv.Value.Temp)).Append('}');
                }
            sb.Append("},\"pots\":{");
            first = true;
            foreach (var p in pots.OrderBy(p => p.PotId))
            {
                if (!first) sb.Append(',');
                first = false;
                bool live = p.State == PotState.Growing || p.State == PotState.Mature;
                var problems = live ? ProblemOrder.Where(x => (p.Flags & x.Bit) != 0).Select(x => JsonText.Str(x.Name)) : Enumerable.Empty<string>();
                sb.Append('"').Append(p.PotId).Append("\":{")
                    .Append("\"rowId\":").Append(p.RowId)
                    .Append(",\"clickId\":").Append(p.ClickId)
                    .Append(",\"potName\":").Append(JsonText.Str(p.PotName))
                    .Append(",\"potIcon\":").Append(JsonText.Str(p.PotIcon))
                    .Append(",\"potSize\":").Append(p.PotSize)
                    .Append(",\"state\":").Append(JsonText.Str(StateName(p.State)))
                    .Append(",\"cropName\":").Append(JsonText.Str(p.CropName))
                    .Append(",\"cropIcon\":").Append(JsonText.Str(p.CropIcon))
                    .Append(",\"cropSize\":").Append(p.CropSize)
                    .Append(",\"neverWithers\":").Append(p.NeverWithers ? "true" : "false")
                    .Append(",\"problems\":[").Append(string.Join(",", problems)).Append(']')
                    .Append(",\"badge\":").Append(JsonText.Str(Badge(p.Flags, p.State)))
                    // The card reads the time to mature of a crop with no problem from the game row, which
                    // the game counts down itself, so the data does not change while such a crop grows.
                    .Append(",\"growRemainSeconds\":").Append(problems.Any() ? p.GrowRemainSeconds : 0)
                    .Append(",\"growTotalSeconds\":").Append(p.GrowTotalSeconds)
                    .Append(",\"fertName\":").Append(JsonText.Str(p.FertName))
                    .Append(",\"fertIcon\":").Append(JsonText.Str(p.FertIcon))
                    .Append(",\"fertDose\":").Append(JsonText.Str(p.FertDose))
                    .Append(",\"autoFert\":").Append(p.AutoFert ? "true" : "false")
                    .Append(",\"autoReplant\":").Append(p.AutoReplant ? "true" : "false")
                    .Append('}');
            }
            sb.Append("}}");
            return sb.ToString();
        }
    }
}
