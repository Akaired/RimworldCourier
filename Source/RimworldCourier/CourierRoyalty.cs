using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimworldCourier
{
    /// <summary>
    /// Royalty rules: the Empire only hires couriers whose caravan includes a colonist holding at
    /// least the title of Yeoman. Imperial contracts pay a little more and sometimes grant honor
    /// to that colonist.
    /// </summary>
    public static class CourierRoyalty
    {
        public const float EmpirePayMultiplier = 1.15f;
        public const float HonorChance = 0.15f;

        private const string RequiredTitleDefName = "Yeoman";

        public static bool IsEmpire(Faction faction)
        {
            return ModsConfig.RoyaltyActive && faction != null && FactionDefOf.Empire != null && faction.def == FactionDefOf.Empire;
        }

        public static RoyalTitleDef RequiredTitle => DefDatabase<RoyalTitleDef>.GetNamedSilentFail(RequiredTitleDefName);

        /// <summary>The colonist of the caravan with the highest title in <paramref name="faction"/>, if any.</summary>
        public static Pawn BestTitledPawn(Caravan caravan, Faction faction)
        {
            Pawn best = null;
            int bestSeniority = int.MinValue;
            List<Pawn> pawns = caravan.PawnsListForReading;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn.royalty == null || !pawn.IsColonist)
                {
                    continue;
                }
                RoyalTitleDef title = pawn.royalty.GetCurrentTitle(faction);
                if (title != null && title.seniority > bestSeniority)
                {
                    best = pawn;
                    bestSeniority = title.seniority;
                }
            }
            return best;
        }

        /// <summary>Whether this caravan may work for <paramref name="faction"/> (always true for non-imperial factions).</summary>
        public static AcceptanceReport CanWorkFor(Caravan caravan, Faction faction)
        {
            if (!IsEmpire(faction))
            {
                return true;
            }
            RoyalTitleDef required = RequiredTitle;
            if (required == null)
            {
                return true;
            }
            Pawn best = BestTitledPawn(caravan, faction);
            RoyalTitleDef title = best?.royalty.GetCurrentTitle(faction);
            if (title != null && title.seniority >= required.seniority)
            {
                return true;
            }
            return "RC_EmpireRequiresTitle".Translate(required.GetLabelCapForBothGenders(), faction.Name);
        }

        /// <summary>Gives honor for a delivery: to the colonist named at acceptance, or the best-titled one present.</summary>
        public static Pawn GiveHonor(Faction faction, int amount, Pawn preferred, Caravan caravan)
        {
            if (amount <= 0 || !ModsConfig.RoyaltyActive || faction == null)
            {
                return null;
            }
            Pawn target = preferred;
            if (target == null || target.Dead || target.Destroyed || target.royalty == null || target.Faction != Faction.OfPlayer)
            {
                target = caravan != null ? BestTitledPawn(caravan, faction) : null;
            }
            if (target?.royalty == null)
            {
                return null;
            }
            target.royalty.GainFavor(faction, amount);
            return target;
        }
    }
}
