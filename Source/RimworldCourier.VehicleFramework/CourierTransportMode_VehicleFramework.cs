using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Vehicles;
using Vehicles.World;
using Verse;

namespace RimworldCourier.VehicleFramework
{
    [StaticConstructorOnStartup]
    public static class VehicleFrameworkCompat
    {
        static VehicleFrameworkCompat()
        {
            CourierTransport.Register(new CourierTransportMode_VehicleFramework());
            Log.Message("[RimworldCourier] Vehicle Framework support enabled.");
        }
    }

    /// <summary>
    /// Caravans with Vehicle Framework vehicles. VF already patches vanilla mass capacity, so free
    /// capacity uses the vanilla API; reachability and travel time use VF's own world pathing,
    /// because vehicles have their own terrain rules (roads, water, mountains).
    ///
    /// Written against Vehicle Framework 1.6.2458 (source on GitHub).
    /// </summary>
    public class CourierTransportMode_VehicleFramework : CourierTransportMode
    {
        /// <summary>VF AerialVehicleInFlight.PctPerTick, used when a flight time cannot be derived from fuel use.</summary>
        private const float FallbackTransporterSpeed = 0.00025f;

        // Below the Odyssey shuttle (100): a caravan carrying a vanilla shuttle can only travel by launching it.
        public override int Priority => 50;

        public override bool Handles(Caravan caravan)
        {
            return caravan is VehicleCaravan vehicleCaravan && vehicleCaravan.VehiclesListForReading.Count > 0;
        }

        public override CourierTransportKind Kind(Caravan caravan)
        {
            return ((VehicleCaravan)caravan).AerialVehicle ? CourierTransportKind.Aircraft : CourierTransportKind.GroundVehicles;
        }

        public override bool OffersFuelRewards(Caravan caravan)
        {
            foreach (VehiclePawn vehicle in ((VehicleCaravan)caravan).VehiclesListForReading)
            {
                if (vehicle.CompFueledTravel != null && !vehicle.CompFueledTravel.Props.ElectricPowered)
                {
                    return true;
                }
            }
            return false;
        }

        public override bool FoodMatters(Caravan caravan) => !((VehicleCaravan)caravan).AerialVehicle;

        public override float FreeMass(Caravan caravan)
        {
            return Mathf.Max(0f, caravan.MassCapacity - caravan.MassUsage);
        }

        public override float MaxCandidateDistance(Caravan caravan)
        {
            // Vehicles cover far more ground than walkers; the path estimate does the real filtering.
            return Mathf.Max(40f, CourierMod.Settings.maxTravelDays * 15f);
        }

        public override float RunningCostSilver(Caravan caravan, PlanetTile from, PlanetTile to, int travelTicks)
        {
            VehicleCaravan vehicleCaravan = (VehicleCaravan)caravan;
            Vector3 origin = Find.WorldGrid.GetTileCenter(from);
            float silver = 0f;
            foreach (VehiclePawn vehicle in vehicleCaravan.VehiclesListForReading)
            {
                CompFueledTravel fuel = vehicle.CompFueledTravel;
                if (fuel == null || fuel.Props.ElectricPowered || fuel.Props.fuelType == null)
                {
                    continue;
                }
                float units = vehicleCaravan.AerialVehicle && vehicle.CompVehicleLauncher != null
                    ? vehicle.CompVehicleLauncher.FuelNeededToLaunchAtDist(origin, to)
                    : fuel.ConsumptionRatePerTickRaw * fuel.Props.fuelConsumptionWorldMultiplier * travelTicks;
                silver += units * fuel.Props.fuelType.BaseMarketValue;
            }
            return silver;
        }

        public override bool TryEstimateTravel(Caravan caravan, PlanetTile from, PlanetTile to, out int ticks)
        {
            VehicleCaravan vehicleCaravan = (VehicleCaravan)caravan;
            return vehicleCaravan.AerialVehicle
                ? TryEstimateFlight(vehicleCaravan, from, to, out ticks)
                : TryEstimateDrive(vehicleCaravan, from, to, out ticks);
        }

        private static bool TryEstimateDrive(VehicleCaravan caravan, PlanetTile from, PlanetTile to, out int ticks)
        {
            ticks = 0;
            WorldVehiclePathGrid grid = WorldVehiclePathGrid.Instance;
            if (grid == null || !grid.reachability.CanReach(caravan, to))
            {
                return false;
            }
            ticks = VehicleCaravanPathingHelper.EstimatedTicksToArrive(caravan, from, to);
            if (ticks <= 0)
            {
                return false;
            }
            return HasFuelForDrive(caravan, ticks);
        }

        /// <summary>
        /// Fuel needed for the drive, per fuel type, against what is in the tanks plus loose fuel in
        /// the caravan. Fuel sealed in courier crates is not counted: VF's refuel matches by def.
        /// </summary>
        private static bool HasFuelForDrive(VehicleCaravan caravan, int ticks)
        {
            Dictionary<ThingDef, float> needed = new Dictionary<ThingDef, float>();
            Dictionary<ThingDef, float> available = new Dictionary<ThingDef, float>();
            foreach (VehiclePawn vehicle in caravan.VehiclesListForReading)
            {
                CompFueledTravel fuel = vehicle.CompFueledTravel;
                if (fuel == null || fuel.Props.ElectricPowered || fuel.Props.fuelType == null)
                {
                    continue;
                }
                ThingDef fuelType = fuel.Props.fuelType;
                needed.TryGetValue(fuelType, out float alreadyNeeded);
                available.TryGetValue(fuelType, out float alreadyAvailable);
                needed[fuelType] = alreadyNeeded + fuel.ConsumptionRatePerTickRaw * fuel.Props.fuelConsumptionWorldMultiplier * ticks;
                available[fuelType] = alreadyAvailable + fuel.Fuel;
            }
            if (needed.Count == 0)
            {
                return true;
            }
            foreach (Thing thing in CaravanInventoryUtility.AllInventoryItems(caravan))
            {
                if (available.ContainsKey(thing.def))
                {
                    available[thing.def] += thing.stackCount;
                }
            }
            foreach (KeyValuePair<ThingDef, float> pair in needed)
            {
                if (available[pair.Key] < pair.Value)
                {
                    return false;
                }
            }
            return true;
        }

        private static bool TryEstimateFlight(VehicleCaravan caravan, PlanetTile from, PlanetTile to, out int ticks)
        {
            ticks = 0;
            if (from.Layer != to.Layer)
            {
                return false;
            }
            int distance = Find.WorldGrid.TraversalDistanceBetween(from, to, passImpassable: true, int.MaxValue);
            Vector3 origin = Find.WorldGrid.GetTileCenter(from);
            int longest = 0;
            foreach (VehiclePawn vehicle in caravan.VehiclesListForReading)
            {
                CompVehicleLauncher launcher = vehicle.CompVehicleLauncher;
                if (launcher == null)
                {
                    return false;
                }
                if (distance > launcher.MaxLaunchDistance)
                {
                    return false;
                }
                int flightTicks = 0;
                CompFueledTravel fuel = vehicle.CompFueledTravel;
                if (fuel != null && !fuel.Props.ElectricPowered)
                {
                    float fuelNeeded = launcher.FuelNeededToLaunchAtDist(origin, to);
                    if (fuelNeeded > fuel.Fuel)
                    {
                        return false;
                    }
                    float perTick = fuel.ConsumptionRatePerTickRaw * launcher.FuelConsumptionWorldMultiplier;
                    if (perTick > 0f)
                    {
                        flightTicks = Mathf.CeilToInt(fuelNeeded / perTick);
                    }
                }
                if (flightTicks <= 0)
                {
                    Vector3 a = Find.WorldGrid.GetTileCenter(from).normalized;
                    Vector3 b = Find.WorldGrid.GetTileCenter(to).normalized;
                    flightTicks = Mathf.CeilToInt(GenMath.SphericalDistance(a, b) / FallbackTransporterSpeed);
                }
                longest = Mathf.Max(longest, flightTicks);
            }
            ticks = Mathf.Max(1, longest);
            return true;
        }
    }
}
