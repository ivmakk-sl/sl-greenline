using SlShared.I18n;
using Xunit;

// The mod texts of the i18n files, with no game text: each one in English and in Chinese, and the text key
// of each one that the game names.
public class ModTextsTests
{
    private const int Chinese = 0, English = 1;

    private static I18nTexts Texts() => I18nTexts.Load(typeof(ModTextsTests).Assembly, "Greenline");

    // Name, text key (null: the game has no text), English, Chinese.
    public static TheoryData<string, string, string, string> Words => new TheoryData<string, string, string, string>
    {
        { "stateGrowing", "WebUI_PlantingDetails_3", "Growing", "生长中" },
        { "stateMature", "WebUI_PlantingDetails_4", "Mature", "成熟" },
        { "stateWithered", "WebUI_PlantingDetails_5", "Withered", "枯萎" },
        { "statePoor", "WebUI_PlantingDetails_6", "Poor", "贫瘠" },
        { "stateStalled", "WebUI_PlantingDetails_2", "Stalled", "停滞" },
        { "stateEmpty", "SR_Web_CoreUI1_59", "Empty", "空闲" },
        { "problemPest", "WebUI_PlantingDetails_9", "Pest", "虫害" },
        { "problemFrost", "WebUI_PlantingDetails_7", "Frost", "寒冷" },
        { "problemLightLow", "WebUI_PlantingDetails_8", "Low Light", "阳光不足" },
        { "problemLightHigh", "WebUI_PlantPanel_22", "Light Too Strong", "光照过强" },
        { "problemWeed", null, "Weed", "杂草" },
        { "problemDrought", null, "Drought", "缺水" },
        { "problemNeedFert", "SR_Web_DailyFert_StateFertStalled", "Needs Fertilizer", "缺肥停滞" },
        { "labelState", null, "State", "状态" },
        { "labelGrowth", null, "Growth", "生长" },
        { "labelHarvest", null, "Harvest", "收获" },
        { "labelFertilizer", "ItemType11", "Fertilizer", "肥料" },
        { "valueNone", null, "None", "无" },
        { "autoOn", null, "(auto: on)", "（自动：开）" },
        { "autoOff", null, "(auto: off)", "（自动：关）" },
        { "valueOn", null, "On", "开" },
        { "valueOff", null, "Off", "关" },
        // The label of the mod: the game gave its own Patrol the text of SR_Web_CoreUI1_117 (Tend Garden, 巡园) in 1.1.19611.
        { "tendAll", null, "Tend All", "一键巡田" },
        { "autoReplant", null, "Auto-replant", "自动补种" },
        { "autoFert", null, "Auto-fertilize", "自动施肥" },
        { "lastCrop", null, "Last crop:", "上次作物：" },
        { "replantStopped", null, "Replant stopped: {reason}", "自动补种中止：{reason}" },
        { "reasonSeeds", null, "seeds {have}/{need}", "种子 {have}/{need}" },
        { "reasonLight", "WebUI_PlantPanel_21", "not enough light", "光照不足" },
        { "reasonCold", "WebUI_PlantPanel_23", "too cold", "温度过低" },
        { "popNoFert", null, "{pot} replanted with no fertilizer: no fertilizer in storage", "{pot}已补种，未施肥：没有肥料" },
        { "patrolNoStamina", "HandMadeComponent_0", "Not enough Stamina.", "精力不足" },
        { "patrolNoChore", "PlantChoreBatch_0", "No plants need tending right now.", "家里的植物暂时不需要照料" },
        { "patrolPart", "PlantChoreBatch_1", "Stamina only covers {0} of {1} chores.", "精力只够处理 {0}/{1} 项农务" },
        { "patrolQueued", "PlantChoreBatch_2", "These chores are already queued.", "这些农务已经排在队列里了" },
        { "sameAsLastCrop", null, "Same as last crop", "同上次作物" },
        { "autoReplantTip", null, "After a harvest, Tend All plants the same crop again, with no planting window.", "收获后，一键巡田会自动补种同一作物，不打开种植窗口。" },
    };

    [Theory]
    [MemberData(nameof(Words))]
    public void Each_mod_text_has_its_English_and_Chinese_text(string name, string key, string english, string chinese)
    {
        Assert.Equal(english, Texts().For(English, null, null)[name]);
        Assert.Equal(chinese, Texts().For(Chinese, null, null)[name]);
    }

    [Theory]
    [MemberData(nameof(Words))]
    public void A_mod_text_with_a_text_key_shows_the_game_text(string name, string key, string english, string chinese)
    {
        var set = Texts().For(English, null, k => "game:" + k);

        Assert.Equal(key == null ? english : "game:" + key, set[name]);
    }

    [Fact]
    public void The_files_have_no_other_mod_text()
    {
        Assert.Equal(Words.Count, Texts().For(English, null, null).ToDictionary().Count);
    }
}
