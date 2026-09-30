using RimWorld;
using Verse;

namespace RimworldCourier
{
    /// <summary>
    /// Adds the mass of the goods to the crate's own mass. Same pattern as vanilla's
    /// StatPart_UnfinishedThingIngredientsMass; attached to the Mass stat by a patch.
    /// </summary>
    public class StatPart_CourierCrateContentsMass : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            if (req.Thing is CourierCrate crate)
            {
                val += crate.ContentsMass;
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (req.Thing is CourierCrate crate)
            {
                return "RC_StatContentsMass".Translate() + ": " + crate.ContentsMass.ToStringMassOffset();
            }
            return null;
        }
    }
}
