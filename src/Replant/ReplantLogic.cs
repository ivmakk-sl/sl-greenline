using System;
using System.Collections.Generic;
using SlShared.I18n;

namespace Greenline
{
    // The game-free decisions of the replant: the seed of the last crop, the checks of the planting
    // window, the last-crop line, and the fertilizer. No game or BepInEx type here.
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
        public enum Reason { Plant, NoLastCrop, SeedNotInList, TooFewSeeds, NotEnoughLight, TooCold }

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

        // The decision from the checks of the planting window (RecalcSelection) for the selected seed. The
        // last crop comes from the same pot, and a pot keeps its size, so the crop always fits.
        public static Decision Classify(bool hasEnoughSeed, bool isLightOk, bool isTempOk, int have, int need)
        {
            Reason reason = !hasEnoughSeed ? Reason.TooFewSeeds
                : !isLightOk ? Reason.NotEnoughLight
                : !isTempOk ? Reason.TooCold
                : Reason.Plant;
            return new Decision { Reason = reason, Have = have, Need = need };
        }

        // The last-crop line of the planting window: the grey label with the crop name, and the red line of
        // a stopped replant ("" for none).
        public sealed class CropLine
        {
            public string Label, Crop, Red;
        }

        // The last-crop line from the given words, or null when the mod knows no last crop of the pot.
        // `stopped` is the decision of a Patrol replant that did not plant, null for a planting window by
        // hand. The containers have none of a seed that is not in the window's list.
        public static CropLine LineText(string cropName, Decision? stopped, IReadOnlyDictionary<string, string> words)
        {
            if (string.IsNullOrEmpty(cropName) || stopped?.Reason == Reason.NoLastCrop) return null;
            string W(string key) => words.TryGetValue(key, out string w) ? w : "";
            string reason;
            switch (stopped?.Reason ?? Reason.Plant)
            {
                case Reason.SeedNotInList: reason = I18nTexts.Fill(W("reasonSeeds"), ("have", 0), ("need", stopped.Value.Need)); break;
                case Reason.TooFewSeeds: reason = I18nTexts.Fill(W("reasonSeeds"), ("have", stopped.Value.Have), ("need", stopped.Value.Need)); break;
                case Reason.NotEnoughLight: reason = W("reasonLight"); break;
                case Reason.TooCold: reason = W("reasonCold"); break;
                default: reason = ""; break;
            }
            return new CropLine
            {
                Label = W("lastCrop"),
                Crop = cropName,
                Red = reason == "" ? "" : I18nTexts.Fill(W("replantStopped"), ("reason", reason)),
            };
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
