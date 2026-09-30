using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimworldCourier
{
    /// <summary>
    /// A kind of cargo (food, weapons, medicine...). The board first picks a group by weight, then
    /// a thing inside it, so groups with hundreds of defs (raw meats) don't crowd out the rest.
    /// Defined in XML (Defs/CourierCargoGroupDefs), so other mods can add or tweak groups.
    /// </summary>
    public class CourierCargoGroupDef : Def
    {
        public float weight = 1f;
        public List<ThingCategoryDef> categories = new List<ThingCategoryDef>();
        public List<ThingCategoryDef> excludedCategories = new List<ThingCategoryDef>();

        /// <summary>Things added explicitly, whatever their category (use MayRequire for DLC items).</summary>
        public List<ThingDef> things = new List<ThingDef>();

        /// <summary>Things removed explicitly, e.g. DLC items that have a group of their own.</summary>
        public List<ThingDef> excludedThings = new List<ThingDef>();

        /// <summary>The group ships buildings (furniture): they travel minified inside the crate.</summary>
        public bool minified;

        /// <summary>Allow things made from stuff (weapons, apparel); a stuff is picked per job.</summary>
        public bool allowStuff;

        /// <summary>Upper limit on the number of items in one crate (0 = only capacity and value limit it).</summary>
        public int maxCount;

        /// <summary>
        /// Ideology precepts (by defName, so the XML works without the DLC) that stop a faction
        /// from sending / receiving this cargo. E.g. factions that abhor organ use don't ship organs.
        /// </summary>
        public List<string> senderForbiddingPrecepts = new List<string>();
        public List<string> recipientForbiddingPrecepts = new List<string>();

        [Unsaved]
        private List<PreceptDef> cachedSenderPrecepts;

        [Unsaved]
        private List<PreceptDef> cachedRecipientPrecepts;

        public bool AllowedBy(Faction faction, bool asSender)
        {
            if (faction == null || !ModsConfig.IdeologyActive)
            {
                return true;
            }
            Ideo ideo = faction.ideos?.PrimaryIdeo;
            if (ideo == null)
            {
                return true;
            }
            List<PreceptDef> precepts = asSender
                ? (cachedSenderPrecepts ?? (cachedSenderPrecepts = Resolve(senderForbiddingPrecepts)))
                : (cachedRecipientPrecepts ?? (cachedRecipientPrecepts = Resolve(recipientForbiddingPrecepts)));
            for (int i = 0; i < precepts.Count; i++)
            {
                if (ideo.HasPrecept(precepts[i]))
                {
                    return false;
                }
            }
            return true;
        }

        private static List<PreceptDef> Resolve(List<string> names)
        {
            return names
                .Select(n => DefDatabase<PreceptDef>.GetNamedSilentFail(n))
                .Where(p => p != null)
                .ToList();
        }

        [Unsaved]
        private List<ThingDef> cachedDefs;

        [Unsaved]
        private Dictionary<ThingDef, float> cachedWeights;

        public List<ThingDef> Things
        {
            get
            {
                if (cachedDefs == null)
                {
                    cachedDefs = DefDatabase<ThingDef>.AllDefsListForReading
                        .Where(Matches)
                        .OrderBy(d => d.defName)
                        .ToList();
                }
                return cachedDefs;
            }
        }

        public bool Matches(ThingDef def)
        {
            if (excludedThings.Contains(def) || !CourierCargo.IsBasicEligible(def, allowStuff, minified))
            {
                return false;
            }
            if (things.Contains(def))
            {
                return true;
            }
            bool included = false;
            for (int i = 0; i < categories.Count; i++)
            {
                if (def.IsWithinCategory(categories[i]))
                {
                    included = true;
                    break;
                }
            }
            if (!included)
            {
                return false;
            }
            for (int i = 0; i < excludedCategories.Count; i++)
            {
                if (def.IsWithinCategory(excludedCategories[i]))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Weight of a thing inside the group: cheap goods are more common, and every leaf
        /// category shares its weight among its members (all raw meats together weigh like one).
        /// </summary>
        public float WeightOf(ThingDef def)
        {
            if (cachedWeights == null)
            {
                cachedWeights = new Dictionary<ThingDef, float>();
                Dictionary<ThingCategoryDef, int> leafSizes = new Dictionary<ThingCategoryDef, int>();
                foreach (ThingDef thing in Things)
                {
                    ThingCategoryDef leaf = thing.FirstThingCategory;
                    if (leaf != null)
                    {
                        leafSizes.TryGetValue(leaf, out int n);
                        leafSizes[leaf] = n + 1;
                    }
                }
                foreach (ThingDef thing in Things)
                {
                    ThingCategoryDef leaf = thing.FirstThingCategory;
                    int size = 1;
                    if (leaf != null)
                    {
                        leafSizes.TryGetValue(leaf, out size);
                    }
                    cachedWeights[thing] = CourierCargo.ValueWeight(thing) / UnityEngine.Mathf.Max(1, size);
                }
            }
            return cachedWeights.TryGetValue(def, out float w) ? w : 0f;
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }
            if (categories.NullOrEmpty() && things.NullOrEmpty())
            {
                yield return "no categories and no things";
            }
        }
    }
}
