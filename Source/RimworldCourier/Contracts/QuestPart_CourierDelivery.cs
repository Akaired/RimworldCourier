using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimworldCourier
{
    /// <summary>
    /// Links a vanilla quest to a courier contract. The quest is only the player-facing view:
    /// the Quests tab lists it with a live countdown, the destination settlement gets a
    /// "View quest" button, letters open it, and an alert fires when the deadline is close.
    /// All the logic stays in <see cref="CourierWorldComponent"/>.
    /// </summary>
    public class QuestPart_CourierDelivery : QuestPartActivable
    {
        public int contractId = -1;

        public CourierContract Contract => CourierWorldComponent.Instance?.GetContract(contractId);

        public override string ExpiryInfoPart
        {
            get
            {
                CourierContract c = Contract;
                if (c == null || !c.IsOpen)
                {
                    return null;
                }
                string key = c.state == CourierContractState.Returning ? "RC_QuestExpiryReturn" : "RC_QuestExpiryDeliver";
                return key.Translate(c.TicksLeft.ToStringTicksToPeriod());
            }
        }

        public override string ExpiryInfoPartTip
        {
            get
            {
                CourierContract c = Contract;
                if (c == null || !c.IsOpen)
                {
                    return null;
                }
                PlanetTile tile = c.destination?.Tile ?? c.origin?.Tile ?? PlanetTile.Invalid;
                if (!tile.Valid)
                {
                    return null;
                }
                string date = GenDate.DateFullStringWithHourAt(Find.TickManager.TicksAbs + c.TicksLeft, Find.WorldGrid.LongLatOf(tile));
                return "RC_QuestDeadlineOn".Translate(date);
            }
        }

        public override IEnumerable<GlobalTargetInfo> QuestLookTargets
        {
            get
            {
                CourierContract c = Contract;
                if (c == null || !c.IsOpen)
                {
                    yield break;
                }
                if (c.state == CourierContractState.Active && c.destination != null && c.destination.Spawned)
                {
                    yield return c.destination;
                }
                else if (c.state == CourierContractState.Returning)
                {
                    foreach (Settlement settlement in CourierWorldComponent.NearestSettlementsOf(c.issuer, c.crate, 3))
                    {
                        yield return settlement;
                    }
                }
                Caravan caravan = CourierUtility.CaravanOf(c.crate);
                if (caravan != null)
                {
                    yield return caravan;
                }
            }
        }

        public override IEnumerable<Faction> InvolvedFactions
        {
            get
            {
                CourierContract c = Contract;
                if (c == null)
                {
                    yield break;
                }
                if (c.issuer != null)
                {
                    yield return c.issuer;
                }
                if (c.destinationFaction != null && c.destinationFaction != c.issuer)
                {
                    yield return c.destinationFaction;
                }
            }
        }

        public override AlertReport AlertReport
        {
            get
            {
                CourierContract c = Contract;
                if (c == null || !c.IsOpen || c.TicksLeft > GenDate.TicksPerDay)
                {
                    return AlertReport.Inactive;
                }
                Caravan caravan = CourierUtility.CaravanOf(c.crate);
                if (caravan != null)
                {
                    return AlertReport.CulpritIs(caravan);
                }
                return AlertReport.Active;
            }
        }

        /// <summary>The base class overwrites the alert's culprit with this: jump to the carrying caravan.</summary>
        public override GlobalTargetInfo? AlertCulpritTarget
        {
            get
            {
                Caravan caravan = CourierUtility.CaravanOf(Contract?.crate);
                return caravan != null ? new GlobalTargetInfo(caravan) : (GlobalTargetInfo?)null;
            }
        }

        public override string AlertLabel
        {
            get
            {
                CourierContract c = Contract;
                return c == null ? null : (string)"RC_AlertDeadlineLabel".Translate(c.TicksLeft.ToStringTicksToPeriod());
            }
        }

        public override string AlertExplanation
        {
            get
            {
                CourierContract c = Contract;
                return c == null ? null : (string)"RC_AlertDeadlineText".Translate(c.CargoLabel, c.GetStatusLine());
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref contractId, "contractId", -1);
        }
    }
}
