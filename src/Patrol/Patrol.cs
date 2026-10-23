using System;
using System.Collections.Generic;
using System.Text;
using SlShared.I18n;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using SlShared.Route;
using static Greenline.PatrolLogic;

namespace Greenline
{
    // Greenline's own Patrol (Tend All): it finds the chore of each pot as the game's own Patrol does,
    // orders the chores on the Patrol route, cuts them at the Stamina, and queues each one as a click in the
    // menu of the pot (ChoreSender). While it is on, the game's own Patrol (Tend Garden) stays switched off
    // (GamePatrolOff).
    internal static class Patrol
    {
        // Whether Patrol can run: the config option, planting level 3, and a working send of a chore.
        internal static bool Available()
        {
            if (!Plugin.EnablePatrol.Value || ChoreSender.Broken) return false;
            var proficiency = BaseSingleton<BattleLogicWorld>.Instance?._ProficiencyManager;
            return proficiency != null && proficiency.GetLevel(ProficiencyType.Plant) >= GameKey.PlantChoreBatchUnlockLevel;
        }

        // The count of chores that Patrol finds now, with no Stamina cut: the count of the Tend All button. `home` is
        // the pot list of the grid push (PotGrid.HomePots), so one pass over the furniture serves both.
        internal static int PendingChores(List<(Furniture Furniture, Config_Furniture Config, PlantComponent Plant)> home)
        {
            if (!Available()) return 0;
            var world = BaseSingleton<BattleLogicWorld>.Instance;
            return Pending(Scan(world, world._AgentManager.GetLeadingRoleId(), home), PotOptions.GlobalReplant());
        }

        // A click on Tend All.
        internal static void Run()
        {
            if (!Available()) return;
            try
            {
                var world = BaseSingleton<BattleLogicWorld>.Instance;
                var role = world._AgentManager.GetLeadingRole();
                var place = role.GetAgentComponent<TransformComponent>().Position;
                int floor = role.GetAgentComponent<UserComponent>()?.MapConfigId ?? 0;
                var start = new RouteStart { Floor = floor, X = place.x, Y = place.y, Z = place.z };
                // The game gives no place of the stairs of a floor, so a later floor starts from the last pot
                // of the floor before. The walk lengths come from the navigation mesh, around the walls.
                var walk = new WalkLength(m => Plugin.WarnOnce("Greenline: " + m));
                var clock = System.Diagnostics.Stopwatch.StartNew();
                var route = InRouteOrder(start, Scan(world, role.InstanceId), FloorStairs.Count, walk: walk.Of);
                long routeMs = clock.ElapsedMilliseconds;
                if (Plugin.Verbose.Value) Plugin.Log.LogDebug($"{WalkLine(start, route, walk)}; the scan and the route took {routeMs} ms");

                float stamina = role.GetAgentComponent<AttributeComponent>().GetTotalValue_Float(AttrName.Stamina);
                int cost = GameKey.GlobalSetting_PlantActionCost;
                int budget = cost > 0 ? (int)Math.Floor(stamina / cost) : int.MaxValue;
                var plan = Plan(route, PotOptions.GlobalReplant(), budget);

                int sent = 0;
                foreach (var send in plan.Sends)
                {
                    if (!ChoreSender.Send(send.PotId, send.FurnitureActionId, send.ActionId)) break;
                    sent++;
                    if (send.ActionId == PatrolLogic.Plant) Replant.Add(send.PotId);
                }
                if (Plugin.Verbose.Value) Plugin.Log.LogDebug(PlanLine(route, plan, budget, sent));
                ShowText(plan, sent);
                PotGrid.MarkDirty();
            }
            catch (Exception e) { Plugin.WarnOnce($"Greenline: Patrol failed: {e.Message}"); }
        }

        // The pots with a chore, as the game's own Patrol finds them.
        private static List<PotChore> Scan(BattleLogicWorld world, long roleId,
            List<(Furniture Furniture, Config_Furniture Config, PlantComponent Plant)> home = null)
        {
            var config = ConfigManager.Instance;
            var actions = world._ActionManager;
            // The pots that a robot has claimed (PlantChoreManager: pot id to robot id). The robot skips a pot that
            // the character has queued, and Patrol skips a pot that a robot has claimed, so the two never work on one
            // pot.
            var claims = world._PlantChoreManager?._claims;
            var chores = new List<PotChore>();
            foreach (var (furniture, furnitureConfig, plant) in home ?? PotGrid.HomePots())
            {
                int action = ChoreFor(PotGrid.ToPotState(plant.State), (int)plant.AnomalyFlags, a => Offered(config, furniture, a));
                if (action == 0) continue;
                var place = furniture.GetAgentComponent<TransformComponent>()?.Position ?? UnityEngine.Vector3.zero;
                chores.Add(new PotChore
                {
                    PotId = furniture.InstanceId,
                    Floor = furniture.MapConfigId,
                    X = place.x,
                    Y = place.y,
                    Z = place.z,
                    FurnitureActionId = furnitureConfig.ActionId,
                    ActionId = action,
                    Queued = actions.HasActionWithTarget(roleId, furniture.InstanceId)
                        || (claims != null && claims.ContainsKey(furniture.InstanceId)),
                    AutoReplant = PotOptions.Replant(furniture.InstanceId),
                });
            }
            return chores;
        }

        // Whether the menu of the pot offers the action now: a function of
        // Furniture.GetAvailableFurnitureFuncsForRecommend whose ActionIds hold it.
        private static bool Offered(ConfigManager config, Furniture furniture, int action)
        {
            var funcs = furniture.GetAvailableFurnitureFuncsForRecommend();
            if (funcs == null) return false;
            for (int i = 0; i < funcs.Count; i++)
            {
                var func = funcs[i] != null ? config.Get_Config_FurnitureFunc(funcs[i].FurnitureFuncId) : null;
                if (func?.ActionIds != null && func.ActionIds.Contains(action)) return true;
            }
            return false;
        }

        private static void ShowText(PlanResult plan, int sent)
        {
            var texts = ModTexts.Current();
            string text;
            switch (plan.Outcome)
            {
                case Outcome.NoStamina: text = texts["patrolNoStamina"]; break;
                case Outcome.NoChore: text = texts["patrolNoChore"]; break;
                case Outcome.AllQueued: text = texts["patrolQueued"]; break;
                default:
                    if (sent >= plan.Total) return;
                    text = I18nTexts.Fill(texts["patrolPart"], ("0", sent), ("1", plan.Total));
                    break;
            }
            EventDispatcherTools.NotifyGlobal<string>(EnumNotify_Show.PopText, text);
        }

        // The walk of the route on the navigation mesh, against the walk of the route that the straight distances
        // give (the route before the mesh), over the same pots; a floor change counts nothing.
        private static string WalkLine(RouteStart start, List<PotChore> route, WalkLength walk)
        {
            var straight = InRouteOrder(start, route, FloorStairs.Count);
            double walked = Walked(start, route, walk), straightWalked = Walked(start, straight, walk);
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Greenline Patrol: walk {0:F1} m, the straight-distance route {1:F1} m; paths {2}, straight fallbacks {3} (walkable areas {4}, all areas {5}, partial {6})",
                walked, straightWalked, walk.Paths + walk.AllAreaPaths + walk.PartialPaths, walk.Fallbacks, walk.Paths, walk.AllAreaPaths, walk.PartialPaths);
        }

        private static double Walked(RouteStart start, List<PotChore> route, WalkLength walk)
        {
            double sum = 0;
            var here = (start.X, start.Y, start.Z);
            int floor = start.Floor;
            foreach (var pot in route)
            {
                if (pot.Floor == floor) sum += walk.Of(here, (pot.X, pot.Y, pot.Z));
                (here, floor) = ((pot.X, pot.Y, pot.Z), pot.Floor);
            }
            return sum;
        }

        private static string PlanLine(List<PotChore> route, PlanResult plan, int budget, int sent)
        {
            var floors = new Dictionary<long, int>();
            foreach (var pot in route) floors[pot.PotId] = pot.Floor;
            var sb = new StringBuilder("Greenline Patrol: plan [");
            for (int i = 0; i < plan.Sends.Count; i++)
            {
                if (i > 0) sb.Append(' ');
                var s = plan.Sends[i];
                sb.Append(s.PotId).Append(':').Append(floors[s.PotId]).Append(':').Append(s.ActionId);
            }
            return sb.Append("] budget ").Append(budget).Append(" queued ").Append(sent).Append(" of ").Append(plan.Total).ToString();
        }
    }

    // Keeps the game's own Patrol (Tend Garden) off while Greenline's Patrol is on. Its one switch,
    // GameKey.PlantChoreBatchEnabled, gates its button, the hotkey G, the "Tend ×N" quick action, the count of the
    // button, and the guide tip (PlantChoreBatchManager.IsUnlocked). This reducer sets the header value from the switch
    // at the load and at each planting level up, so the switch goes off before the reducer reads it, and the reducer
    // then hides the button and the guide tip itself.
    [HarmonyLib.HarmonyPatch(typeof(GameCore.HotUpdate.ReduxUI.Reducer_Web_CoreUI1), "RA_PlantOverviewUnlock")]
    internal static class GamePatrolOff
    {
        private static void Prefix()
        {
            try
            {
                bool game = GameKey.PlantChoreBatchEnabled;
                bool want = GameSwitch(Plugin.EnablePatrol.Value, game);
                if (want == game) return;
                GameKey.PlantChoreBatchEnabled = want;
                if (Plugin.Verbose.Value) Plugin.Log.LogDebug("Greenline: the game's own Patrol (Tend Garden) is switched off.");
            }
            catch (Exception e) { Plugin.WarnOnce($"Greenline: the game's own Patrol could not be switched off: {e.Message}"); }
        }
    }
}
