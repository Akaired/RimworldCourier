using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimworldCourier
{
    /// <summary>
    /// Odyssey passenger shuttle carried by a caravan. The caravan cannot walk; it travels by
    /// launching, so feasibility is about the shuttle's hold (500 kg including passengers) and the
    /// range its fuel allows for a single hop.
    ///
    /// Formulas mirror RimWorld 1.6.4871: CaravanShuttleUtility (mass/fuel), CompLaunchable (range)
    /// and TravellingTransporters (flight speed).
    /// </summary>
    public class CourierTransportMode_OdysseyShuttle : CourierTransportMode
    {
        /// <summary>TravellingTransporters.TravelSpeed (private const in vanilla).</summary>
        private const float TransporterTravelSpeed = 0.00025f;

        public override int Priority => 100;

        public override bool Handles(Caravan caravan)
        {
            return ModsConfig.OdysseyActive && caravan.Shuttle != null;
        }

        public override CourierTransportKind Kind(Caravan caravan) => CourierTransportKind.Shuttle;

        public override bool OffersFuelRewards(Caravan caravan) => true;

        public override bool FoodMatters(Caravan caravan) => false;

        public override float FreeMass(Caravan caravan)
        {
            Building_PassengerShuttle shuttle = caravan.Shuttle;
            // Same computation as CaravanShuttleUtility.GetCaravanShuttleMass (private):
            // everything in the caravan, pawns included, minus the shuttle itself.
            float used = CollectionsMassCalculator.MassUsage(caravan.pawns, IgnorePawnsInventoryMode.IgnoreIfAssignedToUnload, includePawnsMass: true);
            used -= shuttle.GetStatValue(StatDefOf.Mass);
            return Mathf.Max(0f, shuttle.TransporterComp.MassCapacity - used);
        }

        public override float MaxCandidateDistance(Caravan caravan)
        {
            Building_PassengerShuttle shuttle = caravan.Shuttle;
            return shuttle.LaunchableComp.MaxLaunchDistanceAtFuelLevel(UsableFuel(caravan, shuttle));
        }

        public override bool TryEstimateTravel(Caravan caravan, PlanetTile from, PlanetTile to, out int ticks)
        {
            ticks = 0;
            Building_PassengerShuttle shuttle = caravan.Shuttle;
            if (from.Layer != to.Layer)
            {
                return false;
            }
            int distance = Find.WorldGrid.TraversalDistanceBetween(from, to, passImpassable: true, int.MaxValue, canTraverseLayers: true);
            if (distance > shuttle.LaunchableComp.MaxLaunchDistanceAtFuelLevel(UsableFuel(caravan, shuttle), to.Layer))
            {
                return false;
            }
            Vector3 start = Find.WorldGrid.GetTileCenter(from).normalized;
            Vector3 end = Find.WorldGrid.GetTileCenter(to).normalized;
            float arc = GenMath.SphericalDistance(start, end);
            ticks = Mathf.Max(1, Mathf.CeilToInt(arc / TransporterTravelSpeed));
            return true;
        }

        public override float RunningCostSilver(Caravan caravan, PlanetTile from, PlanetTile to, int travelTicks)
        {
            Building_PassengerShuttle shuttle = caravan.Shuttle;
            int distance = Find.WorldGrid.TraversalDistanceBetween(from, to, passImpassable: true, int.MaxValue, canTraverseLayers: true);
            float fuel = shuttle.LaunchableComp.FuelNeededToLaunchAtDist(distance, to.Layer);
            return fuel * ThingDefOf.Chemfuel.BaseMarketValue;
        }

        /// <summary>
        /// Fuel in the tank plus loose fuel in the cargo, capped by tank size. Fuel sealed inside
        /// courier crates is not counted: the refuel filter does not accept crates.
        /// </summary>
        private static float UsableFuel(Caravan caravan, Building_PassengerShuttle shuttle)
        {
            return Mathf.Min(shuttle.MaxFuelLevel, shuttle.FuelLevel + CaravanShuttleUtility.FuelInCaravan(caravan));
        }
    }
}
