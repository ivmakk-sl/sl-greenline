using System.Collections.Generic;

namespace Greenline
{
    // The game-free part of the "Same as last crop" sowing preset of the GR-3 robot and of the robot chore
    // order. No game or BepInEx type here.
    public static class RobotLogic
    {
        // The seed id that the sowing preset of a pot size keeps for "Same as last crop": a positive id that
        // is no item, so the game keeps it at a load, and without the mod the robot finds no seed and skips
        // that pot size.
        public const int Placeholder = 990001;

        // The pot sizes of the sowing presets (RobotComponent._sowPresetSeedIds): tier 0 small, 1 medium, 2 large.
        public const int SowTiers = 3;

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

        // The robot's own Plant action (0x23aa in PlantChoreManager.TryResolveChore), not the character's
        // Plant chore PatrolLogic.Plant.
        public const int RobotPlant = 9130;

        // The rank of a Harvest candidate (PlantChoreManager.RankHarvest). The game sorts the candidates by
        // rank, then by PlantChore.DecayRemain, both ascending.
        public const int RankHarvest = 1;

        // The DecayRemain of a Till or a Plant that the robot finishes first: the game gives a Harvest a
        // DecayRemain of 0 or more, so -1 sorts it before each Harvest, also a crop at its last second.
        public const float FinishDecay = -1f;

        // The rank and DecayRemain of a robot chore candidate: a Till or a robot Plant goes after Remove Pests
        // and before each Harvest, so the robot finishes an emptied pot before its next harvest. Each other
        // chore keeps the values that the game gave it.
        public static (int Rank, float DecayRemain) FinishRank(int actionId, int rank, float decayRemain) =>
            actionId == PatrolLogic.Till || actionId == RobotPlant ? (RankHarvest, FinishDecay) : (rank, decayRemain);
    }
}
