using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimworldCourier
{
    public static class CourierCommands
    {
        public static Command HandOverCommand(CourierContract contract, Caravan caravan)
        {
            bool returning = contract.state == CourierContractState.Returning;
            return new Command_Action
            {
                defaultLabel = returning
                    ? "RC_CommandReturn".Translate(contract.CargoLabel)
                    : "RC_CommandDeliver".Translate(contract.CargoLabel),
                defaultDesc = returning
                    ? "RC_CommandReturnDesc".Translate(contract.issuer?.Name ?? "?")
                    : "RC_CommandDeliverDesc".Translate(contract.DestinationLabel, contract.RewardLabel),
                icon = returning ? CourierTextures.ReturnIcon : CourierTextures.DeliverIcon,
                action = delegate
                {
                    CourierWorldComponent.Instance?.HandOver(contract, caravan);
                }
            };
        }

        /// <summary>
        /// Emergency use of the cargo. Works for a spawned crate (caravan == null) and for crates
        /// carried by a caravan on the world map.
        /// </summary>
        public static Command BreakSealCommand(CourierCrate crate, Caravan caravan)
        {
            return new Command_Action
            {
                defaultLabel = caravan == null
                    ? "RC_CommandBreakSeal".Translate()
                    : "RC_CommandBreakSealOf".Translate(crate.ContentsSummary),
                defaultDesc = "RC_CommandBreakSealDesc".Translate(),
                icon = CourierTextures.BreakSealIcon,
                alsoClickIfOtherInGroupClicked = false,
                action = delegate
                {
                    ConfirmBreakSeal(crate, caravan);
                }
            };
        }

        public static void ConfirmBreakSeal(CourierCrate crate, Caravan caravan)
        {
            CourierContract contract = crate.Contract;
            TaggedString text;
            if (contract != null && contract.IsOpen)
            {
                int penalty = CourierWorldComponent.FailurePenalty(contract, CourierFailReason.SealBroken);
                text = "RC_ConfirmBreakSeal".Translate(crate.ContentsSummary, contract.issuer?.Name ?? "?", penalty);
            }
            else
            {
                text = "RC_ConfirmBreakSealNoContract".Translate(crate.ContentsSummary);
            }
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(text, delegate
            {
                crate.BreakSeal(caravan);
            }, destructive: true));
        }

        /// <summary>One command per crate would flood the bar: a single command with a menu.</summary>
        public static Command CaravanBreakSealMenuCommand(Caravan caravan, List<CourierCrate> crates)
        {
            if (crates.Count == 1)
            {
                return BreakSealCommand(crates[0], caravan);
            }
            return new Command_Action
            {
                defaultLabel = "RC_CommandBreakSeal".Translate(),
                defaultDesc = "RC_CommandBreakSealDesc".Translate(),
                icon = CourierTextures.BreakSealIcon,
                alsoClickIfOtherInGroupClicked = false,
                action = delegate
                {
                    List<FloatMenuOption> options = new List<FloatMenuOption>();
                    foreach (CourierCrate crate in crates)
                    {
                        CourierCrate local = crate;
                        options.Add(new FloatMenuOption(local.ContentsSummary, delegate
                        {
                            ConfirmBreakSeal(local, caravan);
                        }));
                    }
                    Find.WindowStack.Add(new FloatMenu(options));
                }
            };
        }
    }
}
