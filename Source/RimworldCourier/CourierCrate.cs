using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimworldCourier
{
    /// <summary>
    /// Sealed, refrigerated courier crate holding the real goods of one contract.
    ///
    /// Why a crate instead of loose goods: nothing in the game can consume it by accident. It is not
    /// food, not fuel (vanilla shuttle and Vehicle Framework refuel filters match by def), not
    /// tradeable, not a drug or medicine. The contents never tick, so they never rot.
    /// Breaking the seal releases the goods and fails the contract.
    ///
    /// The goods are kept in a plain deep-saved list, like quest reward items, and deliberately not
    /// in a ThingOwner: vanilla walks every IThingHolder on a map to compute colony wealth, so goods
    /// inside a holder would count as colony wealth and make raids bigger while you store a crate.
    /// </summary>
    public class CourierCrate : ThingWithComps
    {
        private List<Thing> contents = new List<Thing>();

        public int contractId = -1;

        /// <summary>Set right before the crate is intentionally removed (delivered, returned, opened).</summary>
        private bool resolved;

        public IReadOnlyList<Thing> Contents => contents;

        public CourierContract Contract => CourierWorldComponent.Instance?.GetContract(contractId);

        public float ContentsMass
        {
            get
            {
                float mass = 0f;
                for (int i = 0; i < contents.Count; i++)
                {
                    mass += contents[i].GetStatValue(StatDefOf.Mass) * contents[i].stackCount;
                }
                return mass;
            }
        }

        public float ContentsMarketValue
        {
            get
            {
                float value = 0f;
                for (int i = 0; i < contents.Count; i++)
                {
                    value += contents[i].MarketValue * contents[i].stackCount;
                }
                return value;
            }
        }

        public string ContentsSummary
        {
            get
            {
                if (contents.Count == 0)
                {
                    return "RC_CrateEmpty".Translate();
                }
                // Stacks of the same def are summed: "Chemfuel x150". Minified furniture is shown by its real name.
                return contents
                    .GroupBy(t => (t.GetInnerIfMinified() ?? t).def)
                    .Select(g => g.Key.LabelCap + " x" + g.Sum(t => t.stackCount))
                    .ToCommaList();
            }
        }

        public void AddContents(Thing thing)
        {
            if (thing == null || thing.Destroyed || thing.Spawned)
            {
                Log.Error("[RimworldCourier] Could not put " + thing + " into courier crate.");
                return;
            }
            thing.holdingOwner?.Remove(thing);
            contents.Add(thing);
        }

        public override string LabelNoCount
        {
            get
            {
                return "RC_CrateLabel".Translate(ContentsSummary);
            }
        }

        public override string DescriptionFlavor
        {
            get
            {
                StringBuilder sb = new StringBuilder(def.description);
                sb.AppendLine();
                sb.AppendLine();
                sb.Append("RC_CrateContents".Translate()).Append(": ").Append(ContentsSummary);
                CourierContract contract = Contract;
                if (contract != null)
                {
                    sb.AppendLine();
                    sb.Append(contract.GetStatusLine());
                }
                return sb.ToString();
            }
        }

        public override string GetInspectString()
        {
            StringBuilder sb = new StringBuilder();
            string baseString = base.GetInspectString();
            if (!baseString.NullOrEmpty())
            {
                sb.AppendLine(baseString);
            }
            sb.Append("RC_CrateContents".Translate()).Append(": ").Append(ContentsSummary);
            CourierContract contract = Contract;
            if (contract != null)
            {
                sb.AppendLine();
                sb.Append(contract.GetStatusLine());
            }
            return sb.ToString();
        }

        public override IEnumerable<StatDrawEntry> SpecialDisplayStats()
        {
            foreach (StatDrawEntry entry in base.SpecialDisplayStats())
            {
                yield return entry;
            }
            int priority = 5000;
            foreach (Thing thing in contents)
            {
                yield return new StatDrawEntry(
                    StatCategoryDefOf.BasicsImportant,
                    "RC_CrateContents".Translate(),
                    thing.LabelCap,
                    thing.DescriptionFlavor,
                    priority--);
            }
            CourierContract contract = Contract;
            if (contract != null)
            {
                yield return new StatDrawEntry(
                    StatCategoryDefOf.BasicsImportant,
                    "RC_CrateDestination".Translate(),
                    contract.DestinationLabel,
                    contract.GetStatusLine(),
                    priority--);
            }
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }
            if (Spawned)
            {
                yield return CourierCommands.BreakSealCommand(this, null);
            }
        }

        /// <summary>
        /// Releases the goods. On a map they drop next to the crate; inside a caravan they go into
        /// the caravan's inventory. Always notifies the contract.
        /// </summary>
        public void BreakSeal(Caravan caravan)
        {
            if (Destroyed)
            {
                return;
            }
            CourierWorldComponent.Instance?.Notify_SealBroken(this);
            if (caravan == null && !Spawned)
            {
                caravan = CourierUtility.CaravanOf(this);
            }
            List<Thing> released = contents.ToList();
            contents.Clear();
            Map map = MapHeld;
            IntVec3 cell = PositionHeld;
            for (int i = 0; i < released.Count; i++)
            {
                Thing thing = released[i];
                if (thing == null || thing.Destroyed)
                {
                    continue;
                }
                if (caravan != null && !Spawned)
                {
                    CaravanInventoryUtility.GiveThing(caravan, thing);
                }
                else if (map == null || !GenPlace.TryPlaceThing(thing, cell, map, ThingPlaceMode.Near))
                {
                    Log.Warning("[RimworldCourier] Nowhere to put " + thing + " from an opened courier crate.");
                    thing.Destroy();
                }
            }
            DestroyResolved();
        }

        /// <summary>
        /// Left behind: abandoned from a caravan's inventory, a caravan whose colonists all died, a
        /// map that was abandoned, or goods given away in a transport pod. The cargo is lost.
        /// </summary>
        public override void Notify_AbandonedAtTile(PlanetTile tile)
        {
            base.Notify_AbandonedAtTile(tile);
            if (!resolved)
            {
                CourierWorldComponent.Instance?.Notify_CrateLost(this);
            }
        }

        /// <summary>Destroys the crate without reporting it as lost (delivery, return, opening).</summary>
        public void DestroyResolved()
        {
            resolved = true;
            if (!Destroyed)
            {
                Destroy();
            }
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            if (!resolved)
            {
                CourierWorldComponent.Instance?.Notify_CrateLost(this);
            }
            base.Destroy(mode);
            for (int i = 0; i < contents.Count; i++)
            {
                if (contents[i] != null && !contents[i].Destroyed)
                {
                    contents[i].Destroy();
                }
            }
            contents.Clear();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref contents, "contents", LookMode.Deep);
            Scribe_Values.Look(ref contractId, "contractId", -1);
            Scribe_Values.Look(ref resolved, "resolved", false);

            // Saves made before 1.0 kept the goods in a ThingOwner ("innerContainer/innerList").
            // Deep lists only need the LoadingVars pass: the loaded things register themselves for
            // the later passes.
            if (Scribe.mode == LoadSaveMode.LoadingVars && contents == null && Scribe.EnterNode("innerContainer"))
            {
                try
                {
                    Scribe_Collections.Look(ref contents, "innerList", LookMode.Deep);
                }
                finally
                {
                    Scribe.ExitNode();
                }
            }
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (contents == null)
                {
                    contents = new List<Thing>();
                }
                // Goods whose def no longer exists (removed mod or DLC) load as null, or as an
                // empty minified thing: same cleanup as vanilla's ThingOwner.
                contents.RemoveAll(t => t == null || (t is MinifiedThing minified && minified.InnerThing == null));
            }
        }
    }
}
