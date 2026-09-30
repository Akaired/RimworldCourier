using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimworldCourier
{
    public enum CourierRecipient
    {
        Any,
        /// <summary>The destination belongs to the sender's own faction.</summary>
        SameFaction,
        /// <summary>The destination belongs to another faction.</summary>
        OtherFaction
    }

    /// <summary>
    /// Why the goods are being shipped. The label is the short job title shown on the board and in
    /// the quest name ("famine relief"); the clause completes the story sentence
    /// ("... to help them through a poor harvest."). Translatable through DefInjected.
    /// </summary>
    public class CourierReasonDef : Def
    {
        [MustTranslate]
        public string clause;

        /// <summary>Cargo groups this reason fits. Empty = any cargo (generic reasons).</summary>
        public List<CourierCargoGroupDef> groups = new List<CourierCargoGroupDef>();

        public CourierRecipient recipient = CourierRecipient.Any;

        /// <summary>
        /// Only for jobs issued by factions of these FactionDefs (by defName, so DLC factions like
        /// the Empire can be named without requiring the DLC). Empty = any sender.
        /// </summary>
        public List<string> issuerFactionDefs = new List<string>();

        public float commonality = 1f;

        public bool Generic => groups.NullOrEmpty();

        public bool IssuerSpecific => !issuerFactionDefs.NullOrEmpty();

        public bool Applies(CourierCargoGroupDef group, bool sameFaction, Faction issuer)
        {
            if (!Generic && !groups.Contains(group))
            {
                return false;
            }
            if (IssuerSpecific && (issuer == null || !issuerFactionDefs.Contains(issuer.def.defName)))
            {
                return false;
            }
            switch (recipient)
            {
                case CourierRecipient.SameFaction:
                    return sameFaction;
                case CourierRecipient.OtherFaction:
                    return !sameFaction;
                default:
                    return true;
            }
        }

        /// <summary>
        /// Picks a reason with the current Rand state. Reasons written for the cargo are preferred
        /// over generic ones; reasons written for the sender's faction (e.g. the Empire) are favoured.
        /// </summary>
        public static CourierReasonDef Pick(CourierCargoGroupDef group, bool sameFaction, Faction issuer)
        {
            return DefDatabase<CourierReasonDef>.AllDefsListForReading
                .Where(r => r.Applies(group, sameFaction, issuer))
                .RandomElementByWeightWithFallback(Weight);
        }

        private static float Weight(CourierReasonDef r)
        {
            float w = r.commonality;
            if (r.IssuerSpecific)
            {
                w *= r.Generic ? 1.2f : 2f;
            }
            else if (r.Generic)
            {
                w *= 0.3f;
            }
            return w;
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }
            if (clause.NullOrEmpty())
            {
                yield return "no clause";
            }
        }
    }
}
