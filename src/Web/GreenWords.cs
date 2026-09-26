using System.Collections.Generic;
using System.Text;
using GameCore.HotUpdate;

namespace Greenline
{
    // The words of the mod in the current display language. Each word uses the game's own text of its
    // key when the game has one, else the mod's English or Chinese text. Read fresh on each push,
    // because the display language can change while the game runs.
    internal static class GreenWords
    {
        // Page key, game key (null: the game has no word), English, Chinese.
        private static readonly string[][] Table =
        {
            new[] { "stateGrowing", "WebUI_PlantingDetails_3", "Growing", "生长中" },
            new[] { "stateMature", "WebUI_PlantingDetails_4", "Mature", "成熟" },
            new[] { "stateWithered", "WebUI_PlantingDetails_5", "Withered", "枯萎" },
            new[] { "statePoor", "WebUI_PlantingDetails_6", "Poor", "贫瘠" },
            new[] { "stateStalled", "WebUI_PlantingDetails_2", "Stalled", "停滞" },
            new[] { "stateEmpty", "SR_Web_CoreUI1_59", "Empty", "空闲" },
            new[] { "problemPest", "WebUI_PlantingDetails_9", "Pest", "虫害" },
            new[] { "problemFrost", "WebUI_PlantingDetails_7", "Frost", "寒冷" },
            new[] { "problemLightLow", "WebUI_PlantingDetails_8", "Low Light", "阳光不足" },
            new[] { "problemLightHigh", "WebUI_PlantPanel_22", "Light Too Strong", "光照过强" },
            new[] { "problemWeed", null, "Weed", "杂草" },
            new[] { "problemDrought", null, "Drought", "缺水" },
            new[] { "labelState", null, "State", "状态" },
            new[] { "labelGrowth", null, "Growth", "生长" },
            new[] { "labelHarvest", null, "Harvest", "收获" },
            new[] { "labelFertilizer", "ItemType11", "Fertilizer", "肥料" },
            new[] { "valueNone", null, "None", "无" },
            new[] { "autoOn", null, "(auto: on)", "（自动：开）" },
            new[] { "autoOff", null, "(auto: off)", "（自动：关）" },
            new[] { "valueOn", null, "On", "开" },
            new[] { "valueOff", null, "Off", "关" },
            new[] { "tendAll", "SR_Web_CoreUI1_117", "Tend All", "一键巡田" },
            new[] { "autoReplant", null, "Auto-replant", "自动补种" },
            new[] { "autoFert", null, "Auto-fertilize", "自动施肥" },
            new[] { "reasonPrefix", null, "Auto-replant stopped: {0}", "自动补种中止：{0}" },
            new[] { "reasonNoLastCrop", null, "last crop unknown", "没有上次作物的记录" },
            new[] { "reasonSeeds", null, "{0} of {1} {2}", "{2} {0}/{1}" },
            new[] { "reasonFit", "WebUI_PlantPanel_32", "the crop does not fit this pot", "容器太小，无法容纳此植物" },
            new[] { "reasonLight", "WebUI_PlantPanel_21", "not enough light", "光照不足" },
            new[] { "reasonCold", "WebUI_PlantPanel_23", "too cold", "温度过低" },
            new[] { "popNoFert", null, "{0} replanted with no fertilizer: not enough fertilizer", "{0}已补种，未施肥：肥料数量不足" },
            new[] { "autoReplantTip", null, "After a harvest, Tend All plants the same crop again, with no planting window.", "收获后，一键巡田会自动补种同一作物，不打开种植窗口。" },
        };

        private static string lastLogged;

        internal static Dictionary<string, string> Dictionary()
        {
            var map = new Dictionary<string, string>();
            foreach (var w in Current()) map[w.Key] = w.Value;
            return map;
        }

        internal static List<KeyValuePair<string, string>> Current()
        {
            bool chinese = PotGrid.Lang() == "zh";
            var words = new List<KeyValuePair<string, string>>();
            var log = new StringBuilder();
            foreach (var row in Table)
            {
                string game = row[1] != null ? ConstantTextTools.ToConstantTextOrEmpty(row[1]) : "";
                string text = !string.IsNullOrEmpty(game) ? game : (chinese ? row[3] : row[2]);
                words.Add(new KeyValuePair<string, string>(row[0], text));
                if (row[1] != null) log.Append($"\n  {row[1]} = '{game}'");
            }
            if (Plugin.Verbose.Value && log.ToString() != lastLogged)
            {
                lastLogged = log.ToString();
                Plugin.Log.LogDebug("Greenline game words:" + lastLogged);
            }
            return words;
        }
    }
}
