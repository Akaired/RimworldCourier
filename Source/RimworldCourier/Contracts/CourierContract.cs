using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimworldCourier
{
    public enum CourierContractState
    {
        /// <summary>Cargo on its way to the destination.</summary>
        Active,
        /// <summary>Destination vanished or turned hostile (to you or to the sender): cargo must go back to any settlement of the issuer.</summary>
        Returning,
        Completed,
        Returned,
        Failed,
        /// <summary>Issuer defeated: nobody left to deliver for. No reward, no penalty.</summary>
        Voided
    }

    public enum CourierFailReason
    {
        Expired,
        SealBroken,
        CargoLost
    }

    /// <summary>
    /// One accepted courier job. Tracks the crate itself, never the caravan: shuttles and
    /// splitting/merging destroy and recreate caravans, while the crate survives intact.
    /// </summary>
    public class CourierContract : IExposable, ILoadReferenceable
    {
        public int id;

        public Faction issuer;
        public Settlement origin;
        public Settlement destination;
        public string originLabel;
        public string destinationLabel;
        public Faction destinationFaction;

        public CourierCargoGroupDef cargoGroup;
        public CourierReasonDef reason;

        public ThingDef cargoDef;
        public ThingDef cargoStuff;
        public int cargoCount;
        /// <summary>Label at acceptance, shown if the cargo def disappears (mod or DLC removed).</summary>
        public string cargoLabel;
        public float cargoMass;
        public float cargoValue;

        public int rewardSilver;
        public int rewardGoodwill;
        public int rewardChemfuel;
        public int rewardHonor;

        /// <summary>The titled colonist who took an imperial job; receives the honor if still around.</summary>
        public Pawn honorPawn;

        public int estimatedTravelTicks;
        public int acceptedTick;
        public int deadlineTick;

        /// <summary>The vanilla quest shown in the Quests tab for this contract.</summary>
        public Quest quest;

        public CourierContractState state = CourierContractState.Active;
        public CourierCrate crate;

        /// <summary>The player has been told that the sender turned hostile (reset when relations recover).</summary>
        public bool issuerHostileNotified;

        public bool IsOpen => state == CourierContractState.Active || state == CourierContractState.Returning;

        public string DestinationLabel => destination != null ? destination.LabelCap.ToString() : destinationLabel;

        public int TicksLeft => deadlineTick - Find.TickManager.TicksGame;

        public string CargoLabel => cargoDef != null
            ? GenLabel.ThingLabel(cargoDef, cargoStuff, cargoCount).CapitalizeFirst()
            : (cargoLabel ?? "?");

        /// <summary>Short job title: the reason ("famine relief"), or the cargo for old contracts.</summary>
        public string Title => reason != null ? reason.label : CargoLabel;

        public string Story => CourierOfferGenerator.Story(reason, issuer, CargoLabel, DestinationLabel, destinationFaction);

        public string RewardLabel => CourierOfferGenerator.RewardLabel(rewardSilver, rewardGoodwill, rewardChemfuel, issuer, rewardHonor, honorPawn);

        /// <summary>Can the cargo be handed over at this settlement right now?</summary>
        public bool CanHandOverAt(Settlement settlement)
        {
            if (settlement == null)
            {
                return false;
            }
            switch (state)
            {
                case CourierContractState.Active:
                    return settlement == destination;
                case CourierContractState.Returning:
                    return settlement.Faction == issuer;
                default:
                    return false;
            }
        }

        public string GetStatusLine()
        {
            switch (state)
            {
                case CourierContractState.Active:
                    return "RC_StatusActive".Translate(DestinationLabel, destinationFaction?.Name ?? "?", TicksLeft.ToStringTicksToPeriod());
                case CourierContractState.Returning:
                    return "RC_StatusReturning".Translate(issuer?.Name ?? "?", TicksLeft.ToStringTicksToPeriod());
                case CourierContractState.Completed:
                    return "RC_StatusCompleted".Translate();
                case CourierContractState.Returned:
                    return "RC_StatusReturned".Translate();
                case CourierContractState.Failed:
                    return "RC_StatusFailed".Translate();
                default:
                    return "RC_StatusVoided".Translate();
            }
        }

        public string GetUniqueLoadID()
        {
            return "RC_CourierContract_" + id;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_References.Look(ref issuer, "issuer");
            Scribe_References.Look(ref origin, "origin");
            Scribe_References.Look(ref destination, "destination");
            Scribe_References.Look(ref destinationFaction, "destinationFaction");
            Scribe_Values.Look(ref originLabel, "originLabel");
            Scribe_Values.Look(ref destinationLabel, "destinationLabel");
            Scribe_Defs.Look(ref cargoGroup, "cargoGroup");
            Scribe_Defs.Look(ref reason, "reason");
            Scribe_Defs.Look(ref cargoDef, "cargoDef");
            Scribe_Defs.Look(ref cargoStuff, "cargoStuff");
            Scribe_Values.Look(ref cargoCount, "cargoCount");
            Scribe_Values.Look(ref cargoLabel, "cargoLabel");
            Scribe_Values.Look(ref cargoMass, "cargoMass");
            Scribe_Values.Look(ref cargoValue, "cargoValue");
            Scribe_Values.Look(ref rewardSilver, "rewardSilver");
            Scribe_Values.Look(ref rewardGoodwill, "rewardGoodwill");
            Scribe_Values.Look(ref rewardChemfuel, "rewardChemfuel");
            Scribe_Values.Look(ref rewardHonor, "rewardHonor");
            Scribe_References.Look(ref honorPawn, "honorPawn");
            Scribe_Values.Look(ref estimatedTravelTicks, "estimatedTravelTicks");
            Scribe_Values.Look(ref acceptedTick, "acceptedTick");
            Scribe_Values.Look(ref deadlineTick, "deadlineTick");
            Scribe_Values.Look(ref state, "state", CourierContractState.Active);
            Scribe_References.Look(ref crate, "crate");
            Scribe_Values.Look(ref issuerHostileNotified, "issuerHostileNotified", false);
            Scribe_References.Look(ref quest, "quest");
        }
    }
}
