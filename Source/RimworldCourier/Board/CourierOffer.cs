using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimworldCourier
{
    /// <summary>A job shown on the board. Not saved: offers are regenerated from the board seed.</summary>
    public class CourierOffer
    {
        public int index;
        public Settlement destination;

        public CourierCargoGroupDef cargoGroup;
        public CourierReasonDef reason;

        public ThingDef cargoDef;
        public ThingDef cargoStuff;
        public int cargoCount;
        public float cargoMass;
        public float cargoValue;

        public float distanceTiles;
        public int travelTicks;
        public int deadlineTicks;

        public int rewardSilver;
        public int rewardGoodwill;
        public int rewardChemfuel;
        public int rewardHonor;

        public CourierTransportKind transportKind;
        public bool foodWarning;

        public string CargoLabel => GenLabel.ThingLabel(cargoDef, cargoStuff, cargoCount).CapitalizeFirst();

        public string Title => reason != null ? reason.LabelCap.ToString() : CargoLabel;
    }
}
