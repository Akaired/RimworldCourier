using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimworldCourier
{
    /// <summary>Which goods can be shipped, how they are picked, and how to create them.</summary>
    public static class CourierCargo
    {
        /// <summary>Rules every cargo must pass, whatever its group.</summary>
        public static bool IsBasicEligible(ThingDef d, bool allowStuff, bool allowMinified = false)
        {
            if (d.category == ThingCategory.Building)
            {
                // Furniture and the like: shipped minified.
                return allowMinified && d.Minifiable && d.building != null && d.tradeability != Tradeability.None
                    && d.BaseMarketValue > 0f && (allowStuff || !d.MadeFromStuff) && d.BaseMass > 0.001f;
            }
            if (d.category != ThingCategory.Item || d.thingClass == null)
            {
                return false;
            }
            if (!d.EverHaulable || d.tradeability == Tradeability.None || d.BaseMarketValue <= 0f)
            {
                return false;
            }
            if ((d.MadeFromStuff && !allowStuff) || d.IsCorpse || d.isUnfinishedThing || d.destroyOnDrop)
            {
                return false;
            }
            if (d == ThingDefOf.Silver || d.thingClass == typeof(CourierCrate))
            {
                return false;
            }
            if (typeof(MinifiedThing).IsAssignableFrom(d.thingClass) || typeof(Pawn).IsAssignableFrom(d.thingClass))
            {
                return false;
            }
            // Persona weapons are bound to one owner: no sense as freight.
            if (d.HasComp(typeof(CompBladelinkWeapon)))
            {
                return false;
            }
            return d.BaseMass > 0.001f;
        }

        /// <summary>A tribal village does not ship advanced components.</summary>
        public static bool AllowedFor(ThingDef def, Faction issuer)
        {
            if (issuer == null || def.techLevel == TechLevel.Undefined)
            {
                return true;
            }
            return def.techLevel <= issuer.def.techLevel;
        }

        /// <summary>Common, cheap goods are shipped more often than luxury ones.</summary>
        public static float ValueWeight(ThingDef def)
        {
            float value = def.BaseMarketValue;
            if (value < 5f)
            {
                return 1f;
            }
            if (value < 25f)
            {
                return 0.8f;
            }
            if (value < 100f)
            {
                return 0.5f;
            }
            if (value < 500f)
            {
                return 0.3f;
            }
            return 0.15f;
        }

        /// <summary>Groups that have at least one thing this faction would ship.</summary>
        public static List<CourierCargoGroupDef> GroupsFor(Faction issuer)
        {
            return DefDatabase<CourierCargoGroupDef>.AllDefsListForReading
                .Where(g => g.weight > 0f && g.AllowedBy(issuer, asSender: true) && g.Things.Any(d => AllowedFor(d, issuer)))
                .ToList();
        }

        /// <summary>Picks group, thing and stuff with the current Rand state.</summary>
        public static bool TryPick(List<CourierCargoGroupDef> groups, Faction issuer, out CourierCargoGroupDef group, out ThingDef def, out ThingDef stuff)
        {
            def = null;
            stuff = null;
            group = groups.RandomElementByWeightWithFallback(g => g.weight);
            if (group == null)
            {
                return false;
            }
            CourierCargoGroupDef chosen = group;
            List<ThingDef> things = chosen.Things.Where(d => AllowedFor(d, issuer)).ToList();
            if (!things.TryRandomElementByWeight(d => chosen.WeightOf(d), out def))
            {
                return false;
            }
            if (def.MadeFromStuff)
            {
                TechLevel maxTech = issuer?.def.techLevel ?? TechLevel.Undefined;
                if (!GenStuff.TryRandomStuffByCommonalityFor(def, out stuff, maxTech) || stuff == null)
                {
                    return false;
                }
            }
            return true;
        }

        public static float UnitMass(ThingDef def, ThingDef stuff)
        {
            return def.GetStatValueAbstract(StatDefOf.Mass, stuff);
        }

        public static float UnitValue(ThingDef def, ThingDef stuff)
        {
            return def.GetStatValueAbstract(StatDefOf.MarketValue, stuff);
        }

        public static IEnumerable<Thing> MakeCargo(ThingDef def, ThingDef stuff, int count)
        {
            while (count > 0)
            {
                Thing thing = ThingMaker.MakeThing(def, stuff);
                thing.stackCount = Mathf.Min(count, def.stackLimit);
                count -= thing.stackCount;
                // Quality also triggers art (sculptures, furniture) and book titles.
                thing.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Normal, ArtGenerationContext.Outsider);
                if (thing is HumanEmbryo embryo)
                {
                    // Anonymous donors: make sure the gene set exists.
                    embryo.TryPopulateGenes();
                }
                if (def.category == ThingCategory.Building)
                {
                    thing = thing.MakeMinified();
                }
                yield return thing;
            }
        }
    }
}
