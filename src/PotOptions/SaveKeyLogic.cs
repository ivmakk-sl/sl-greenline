using System.Collections.Generic;

namespace Greenline
{
    // The game-free logic of the mod's save keys. No game or BepInEx type here.
    public static class SaveKeyLogic
    {
        // The kinds of the mod's save keys: greenline.<kind>.<potId>.
        public static readonly string[] SaveKinds = { "crop", "fert", "replant" };

        private const string Prefix = "greenline.";

        // The save key of one value of a pot.
        public static string Key(string kind, long potId) => Prefix + kind + "." + potId;

        // Whether a key of the game's counter dictionary belongs to the mod.
        public static bool IsModKey(string key) => key != null && key.StartsWith(Prefix, System.StringComparison.Ordinal);

        // Moves the mod's save keys to the new pot ids after a load (old id to new id). A key of a pot with
        // no new id, and any key that is not a mod key, is dropped. The result is built fresh, so two pots
        // that swap ids cannot overwrite each other.
        public static Dictionary<string, int> Rekey(IDictionary<string, int> entries, IDictionary<long, long> oldToNew)
        {
            var result = new Dictionary<string, int>();
            foreach (var entry in entries)
            {
                string[] parts = entry.Key.Split('.');
                if (parts.Length != 3 || parts[0] != "greenline" || System.Array.IndexOf(SaveKinds, parts[1]) < 0) continue;
                if (!long.TryParse(parts[2], out long oldId) || !oldToNew.TryGetValue(oldId, out long newId)) continue;
                result[Key(parts[1], newId)] = entry.Value;
            }
            return result;
        }
    }
}
