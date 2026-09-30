using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimworldCourier
{
    /// <summary>Vanilla caravan: pawns and pack animals walking on the world map.</summary>
    public class CourierTransportMode_OnFoot : CourierTransportMode
    {
        public override int Priority => 0;

        public override bool Handles(Caravan caravan) => true;

        public override CourierTransportKind Kind(Caravan caravan) => CourierTransportKind.OnFoot;

        public override float FreeMass(Caravan caravan)
        {
            return Mathf.Max(0f, caravan.MassCapacity - caravan.MassUsage);
        }

        public override float MaxCandidateDistance(Caravan caravan)
        {
            // A healthy walking caravan covers roughly 2-4 tiles a day; this is a generous pre-filter,
            // the real check is the path-based estimate below.
            return Mathf.Max(20f, CourierMod.Settings.maxTravelDays * 5f);
        }

        public override bool TryEstimateTravel(Caravan caravan, PlanetTile from, PlanetTile to, out int ticks)
        {
            ticks = 0;
            if (!Find.WorldReachability.CanReach(from, to))
            {
                return false;
            }
            ticks = CaravanArrivalTimeEstimator.EstimatedTicksToArrive(from, to, caravan);
            return ticks > 0;
        }
    }
}
