using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimworldCourier
{
    public class WorldObjectCompProperties_CourierCaravan : WorldObjectCompProperties
    {
        public WorldObjectCompProperties_CourierCaravan()
        {
            compClass = typeof(WorldObjectComp_CourierCaravan);
        }
    }

    /// <summary>
    /// Added to the Caravan world object (and Vehicle Framework's VehicleCaravan through the compat
    /// patch). Gives the caravan the emergency "break seal" command for the crates it carries.
    /// No Harmony needed: WorldObject.GetGizmos already yields the gizmos of its comps.
    /// </summary>
    public class WorldObjectComp_CourierCaravan : WorldObjectComp
    {
        public override IEnumerable<Gizmo> GetGizmos()
        {
            if (!(parent is Caravan caravan) || !caravan.IsPlayerControlled)
            {
                yield break;
            }
            CourierWorldComponent contracts = CourierWorldComponent.Instance;
            if (contracts == null)
            {
                yield break;
            }
            List<CourierCrate> crates = contracts.CratesIn(caravan);
            if (crates.Count > 0)
            {
                yield return CourierCommands.CaravanBreakSealMenuCommand(caravan, crates);
            }
        }
    }
}
