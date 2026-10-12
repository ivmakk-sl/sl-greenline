using System.Collections.Generic;

namespace Greenline
{
    // The game-free part of the "Same as last crop" sowing preset of the GR-3 robot. No game or BepInEx
    // type here.
    public static class RobotLogic
    {
        // The seed id that the sowing preset of a pot size keeps for "Same as last crop": a positive id that
        // is no item, so the game keeps it at a load, and without the mod the robot finds no seed and skips
        // that pot size.
        public const int Placeholder = 990001;

        // The seed that the robot plants in a pot: for the placeholder, the seed of the pot's crop when it
        // is mature (a harvest and sow plans before the harvest), else the seed of its last crop, else 0 (the
        // robot skips the pot); 0 too for a pot with its own Auto-replant off. Any other preset stays.
        public static int SeedFor(int preset, int matureSeed, int lastSeed, bool autoReplant)
        {
            if (preset != Placeholder) return preset;
            if (!autoReplant) return 0;
            return matureSeed > 0 ? matureSeed : lastSeed > 0 ? lastSeed : 0;
        }

        // Whether the placeholder is free: no item of the game has its id.
        public static bool PlaceholderIsFree(ISet<int> itemIds) => !itemIds.Contains(Placeholder);
    }
}
