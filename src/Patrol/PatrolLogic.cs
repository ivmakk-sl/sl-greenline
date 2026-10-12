using System;
using System.Collections.Generic;

namespace Greenline
{
    // The game-free decisions of Greenline's own Patrol: the chore of each pot, the Harvest chain with the
    // replant, and the cut at the Stamina budget. It follows the game's own Patrol (the decompiled
    // PlantChoreBatchManager.ResolveChoreAction and OnBatchRequest). No game or BepInEx type here.
    public static class PatrolLogic
    {
        // The plant actions of the furniture menu of a pot.
        public const int Plant = 1610, Pest = 1613, Weed = 1614, Water = 1615, CleanUp = 1616, Harvest = 1617, Till = 1618;

        // The bits of PlantComponent.AnomalyFlags that give a chore.
        private const int PestBit = 1, WeedBit = 2, DroughtBit = 4;

        // The one chore of a pot by its state, or 0: Remove Pests before Remove Weeds before Water for a
        // growing crop, Harvest for a mature crop, Clean Up for a withered crop, Till for a Poor pot. A chore
        // that the menu of the pot does not offer gives 0.
        public static int ChoreFor(GridLogic.PotState state, int flags, Func<int, bool> offered)
        {
            int action = 0;
            switch (state)
            {
                case GridLogic.PotState.Growing:
                    action = (flags & PestBit) != 0 ? Pest : (flags & WeedBit) != 0 ? Weed : (flags & DroughtBit) != 0 ? Water : 0;
                    break;
                case GridLogic.PotState.Mature: action = Harvest; break;
                case GridLogic.PotState.Withered: action = CleanUp; break;
                case GridLogic.PotState.Poor: action = Till; break;
            }
            return action != 0 && offered(action) ? action : 0;
        }

        // A pot with a chore: its place for the route, the walk to it (Config_Furniture.ActionId), its
        // chore, whether the pot is taken (the character has a chore on it in the queue, or a robot has claimed it), and
        // its own Auto-replant option.
        public sealed class PotChore
        {
            public long PotId;
            public int Floor;
            public float X, Y, Z;
            public int FurnitureActionId;
            public int ActionId;
            public bool Queued;
            public bool AutoReplant = true;
        }

        // One chore to queue, as a click in the menu of the pot sends it.
        public struct Send
        {
            public long PotId;
            public int FurnitureActionId;
            public int ActionId;
        }

        // The text that Patrol shows: no Stamina, no chore, each chore already queued, or the Stamina
        // covers only part of the chores. All: each chore fits, and no text shows.
        public enum Outcome { NoStamina, NoChore, AllQueued, Part, All }

        public sealed class PlanResult
        {
            public List<Send> Sends = new List<Send>();
            // The count of chores of the pots with no queued chore, with no budget cut.
            public int Total;
            public Outcome Outcome;
        }

        // The chores of one pot: the Harvest of a mature crop comes with its Till and its Plant chore while
        // Auto-replant is on, or with its Till only when the pot has its own Auto-replant off.
        private static IEnumerable<int> Chain(PotChore pot, bool replantOn)
        {
            yield return pot.ActionId;
            if (pot.ActionId != Harvest || !replantOn) yield break;
            yield return Till;
            if (pot.AutoReplant) yield return Plant;
        }

        // The chores to queue for the pots with a chore, in the given order, cut at the budget (the Stamina
        // over the Stamina cost of one chore). A cut can come inside a Harvest chain.
        public static PlanResult Plan(IList<PotChore> pots, bool replantOn, int budget)
        {
            var result = new PlanResult();
            if (budget < 1)
            {
                result.Outcome = Outcome.NoStamina;
                return result;
            }
            if (pots.Count == 0)
            {
                result.Outcome = Outcome.NoChore;
                return result;
            }
            result.Total = Pending(pots, replantOn);
            if (result.Total == 0)
            {
                result.Outcome = Outcome.AllQueued;
                return result;
            }
            // The chores of one pot go all together or wait: a Harvest with no Till and Plant would leave the pot Poor,
            // and the next Patrol gives a Poor pot its Till alone. A later pot whose chores fit still goes.
            foreach (var pot in pots)
            {
                if (pot.Queued) continue;
                var chain = new List<int>(Chain(pot, replantOn));
                if (result.Sends.Count + chain.Count > budget) continue;
                foreach (int action in chain)
                    result.Sends.Add(new Send { PotId = pot.PotId, FurnitureActionId = pot.FurnitureActionId, ActionId = action });
            }
            result.Outcome = result.Sends.Count == 0 ? Outcome.NoStamina
                : result.Sends.Count < result.Total ? Outcome.Part : Outcome.All;
            return result;
        }

        // The count of chores that wait: the chores of the pots with no queued chore, with no budget cut.
        public static int Pending(IList<PotChore> pots, bool replantOn)
        {
            int count = 0;
            foreach (var pot in pots)
                if (!pot.Queued)
                    foreach (int _ in Chain(pot, replantOn)) count++;
            return count;
        }
    }
}
