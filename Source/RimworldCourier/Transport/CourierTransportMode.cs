using System.Collections.Generic;
using RimWorld.Planet;
using Verse;

namespace RimworldCourier
{
    public enum CourierTransportKind
    {
        OnFoot,
        Shuttle,
        GroundVehicles,
        Aircraft
    }

    /// <summary>
    /// How a caravan moves, seen from the courier board: how much cargo it can still take, whether
    /// it can reach a settlement and how long the trip takes. The job generator only talks to this
    /// class, so vanilla walking, the Odyssey shuttle and Vehicle Framework vehicles (registered by the
    /// optional compatibility assembly) all produce feasible jobs the same way.
    /// </summary>
    public abstract class CourierTransportMode
    {
        /// <summary>Higher priority modes are asked first.</summary>
        public virtual int Priority => 0;

        public abstract bool Handles(Caravan caravan);

        public abstract CourierTransportKind Kind(Caravan caravan);

        /// <summary>Mass (kg) the caravan can still carry without exceeding its limit.</summary>
        public abstract float FreeMass(Caravan caravan);

        /// <summary>
        /// Whether the caravan, as it is now, can travel from <paramref name="from"/> to <paramref name="to"/>.
        /// On success <paramref name="ticks"/> is the estimated travel time.
        /// </summary>
        public abstract bool TryEstimateTravel(Caravan caravan, PlanetTile from, PlanetTile to, out int ticks);

        /// <summary>Chemfuel is only offered as payment to caravans that can burn it.</summary>
        public virtual bool OffersFuelRewards(Caravan caravan) => false;

        /// <summary>Whether food autonomy matters for the trip (a shuttle hop takes hours, not days).</summary>
        public virtual bool FoodMatters(Caravan caravan) => true;

        /// <summary>
        /// Straight-line distance (in tiles) beyond which destinations are not even path-checked.
        /// Keeps the board cheap to generate.
        /// </summary>
        public virtual float MaxCandidateDistance(Caravan caravan) => 250f;

        /// <summary>Extra silver folded into the pay to cover running costs (fuel for the trip).</summary>
        public virtual float RunningCostSilver(Caravan caravan, PlanetTile from, PlanetTile to, int travelTicks) => 0f;
    }

    public static class CourierTransport
    {
        private static readonly List<CourierTransportMode> modes = new List<CourierTransportMode>
        {
            new CourierTransportMode_OdysseyShuttle(),
            new CourierTransportMode_OnFoot()
        };

        /// <summary>Called by compatibility assemblies (e.g. Vehicle Framework) at startup.</summary>
        public static void Register(CourierTransportMode mode)
        {
            if (mode == null || modes.Contains(mode))
            {
                return;
            }
            modes.Add(mode);
            // Stable sort: highest priority first, registration order breaks ties.
            List<CourierTransportMode> sorted = new List<CourierTransportMode>(modes);
            sorted.SortStable((a, b) => b.Priority.CompareTo(a.Priority));
            modes.Clear();
            modes.AddRange(sorted);
        }

        public static CourierTransportMode For(Caravan caravan)
        {
            for (int i = 0; i < modes.Count; i++)
            {
                if (modes[i].Handles(caravan))
                {
                    return modes[i];
                }
            }
            return modes[modes.Count - 1];
        }
    }
}
