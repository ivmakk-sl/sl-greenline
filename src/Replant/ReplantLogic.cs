using System;
using System.Collections.Generic;

namespace Greenline
{
    // The game-free decisions of the replant: the seed of the last crop, the checks of the planting
    // window, the reason line, and the fertilizer. No game or BepInEx type here.
    public static class ReplantLogic
    {
        // The seed item of a crop: the lowest seed item id whose Config_Item.Plant is the crop's
        // Config_Plant id, or 0 when no seed item plants the crop. The game forgets the crop at the
        // harvest, so the mod keeps this seed as the last crop of the pot.
        public static int SeedForCrop(IEnumerable<(int Item, int Plant)> seedItems, int plantConfigId)
        {
            int best = 0;
            foreach (var (item, plant) in seedItems)
                if (plant == plantConfigId && (best == 0 || item < best)) best = item;
            return best;
        }

        // The replant decision: plant, or the reason not to plant.
        public enum Reason { Plant, NoLastCrop, SeedNotInList, TooFewSeeds, DoesNotFit, NotEnoughLight, TooCold }

        public struct Decision
        {
            public Reason Reason;
            public int Have, Need;
        }

        // The index of the last crop's seed in the planting window's seed list (seed item ids in the
        // window order). The window lists only the seeds that the containers have.
        public static (int Index, Reason Reason) FindSeed(int lastSeedItemId, IList<int> seedList)
        {
            if (lastSeedItemId == 0) return (-1, Reason.NoLastCrop);
            int index = seedList.IndexOf(lastSeedItemId);
            return index < 0 ? (-1, Reason.SeedNotInList) : (index, Reason.Plant);
        }

        // The decision from the checks of the planting window (RecalcSelection) for the selected seed.
        // A crop that does not fit makes the other checks meaningless, so it comes first.
        public static Decision Classify(bool hasEnoughSeed, bool isLightOk, bool isTempOk, bool fits, int have, int need)
        {
            Reason reason = !fits ? Reason.DoesNotFit
                : !hasEnoughSeed ? Reason.TooFewSeeds
                : !isLightOk ? Reason.NotEnoughLight
                : !isTempOk ? Reason.TooCold
                : Reason.Plant;
            return new Decision { Reason = reason, Have = have, Need = need };
        }

        // The reason line of the planting window when the replant stops, from the given words; empty for
        // a planting. The containers have none of a seed that is not in the window's list.
        public static string ReasonText(Decision decision, string seedName, IDictionary<string, string> words)
        {
            string W(string key) => words.TryGetValue(key, out string w) ? w : "";
            string reason;
            switch (decision.Reason)
            {
                case Reason.Plant: return "";
                case Reason.NoLastCrop: reason = W("reasonNoLastCrop"); break;
                case Reason.SeedNotInList: reason = string.Format(W("reasonSeeds"), 0, decision.Need, seedName); break;
                case Reason.TooFewSeeds: reason = string.Format(W("reasonSeeds"), decision.Have, decision.Need, seedName); break;
                case Reason.DoesNotFit: reason = W("reasonFit"); break;
                case Reason.NotEnoughLight: reason = W("reasonLight"); break;
                default: reason = W("reasonCold"); break;
            }
            return string.Format(W("reasonPrefix"), reason);
        }

        // Two speed bonuses closer than this are a tie.
        private const float TieBonus = 0.0001f;

        // The fertilizer that gives the pot the largest speed bonus, or 0 when the pool has none. The game
        // plants with a part dose: it takes min(Count, potSize) and gives Speed times that over potSize. A
        // tie goes to the lower fertilizer (the lower Speed), which keeps the better one for another pot.
        public static int PickFertilizer(IEnumerable<(int Item, int Count, float Speed)> ferts, int potSize)
        {
            int best = 0;
            float bestBonus = 0, bestSpeed = 0;
            foreach (var (item, count, speed) in ferts)
            {
                if (count <= 0) continue;
                float bonus = speed * Math.Min(count, potSize) / potSize;
                bool tie = best != 0 && Math.Abs(bonus - bestBonus) < TieBonus;
                if (tie ? speed < bestSpeed : bonus > bestBonus) { best = item; bestBonus = bonus; bestSpeed = speed; }
            }
            return best;
        }
    }
}
