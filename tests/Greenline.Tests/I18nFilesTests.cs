using SlShared.I18n;
using Xunit;

// Checks the i18n files of the mod (src/i18n/), embedded in the test assembly as in the mod DLL.
public class I18nFilesTests
{
    private static readonly System.Reflection.Assembly Assembly = typeof(I18nFilesTests).Assembly;

    [Fact]
    public void TheI18nFilesHaveNoProblem()
    {
        Assert.Empty(I18nCheck.ModProblems(Assembly, "Greenline", null));
    }

    [GameContextFact]
    public void EachTextKeyIsAConstantTextOfTheGame()
    {
        Assert.Empty(I18nCheck.ModProblems(Assembly, "Greenline", ConstantTextTsv.Keys()));
    }
}

internal static class ConstantTextTsv
{
    // game/config/views/constant_text.tsv, found by a walk up from the test folder, or null.
    public static readonly string Path = Find();

    // The key column of each row.
    public static HashSet<string> Keys() =>
        new HashSet<string>(File.ReadLines(Path).Skip(1).Select(line => line.Split('\t')[0]));

    private static string Find()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            string path = System.IO.Path.Combine(dir.FullName, "game", "config", "views", "constant_text.tsv");
            if (File.Exists(path)) return path;
        }
        return null;
    }
}

internal sealed class GameContextFactAttribute : FactAttribute
{
    public GameContextFactAttribute()
    {
        if (ConstantTextTsv.Path == null) Skip = "The game context (game/config/views/constant_text.tsv) is missing.";
    }
}
